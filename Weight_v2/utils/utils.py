
"""
utils.py
Pipeline xử lý video đá:
    Pipeline A: Video -> Frames -> YOLO -> Đếm số lượng đá.
    Pipeline B: Video -> Frames -> YOLO -> Features CSV -> XGBoost -> Khối lượng.

Hai pipeline sử dụng hai tỉ lệ pixel/cm độc lập.
"""

import csv
import re
from functools import lru_cache
from pathlib import Path

import cv2
import joblib
import numpy as np
import pandas as pd
from ultralytics import YOLO


# ============================================================
# 1. CONFIGURATION
# ============================================================

ROOT_DIR = Path(__file__).resolve().parent.parent

MODEL_DIR = ROOT_DIR / "model"
DATA_DIR = ROOT_DIR / "data_csv"
VIDEO_DIR = ROOT_DIR / "video"

YOLO_MODEL_PATH = MODEL_DIR / "YOLO" / "best260807.pt"
XGBOOST_MODEL_PATH = MODEL_DIR / "XGBoost" / "xgboost.pkl"

# Chu kỳ trích xuất frame, tính bằng giây
VIDEO_CUT_TIME = 3.13

# TỈ LỆ RIÊNG CHO TÁC VỤ ĐẾM ĐÁ
# Đơn vị: pixel/cm
PIXEL2CM_COUNT = 22

# TỈ LỆ RIÊNG CHO TÁC VỤ DỰ ĐOÁN KHỐI LƯỢNG
# Đơn vị: pixel/cm
PIXEL2CM_WEIGHT = 18.82

SUPPORTED_VIDEO_FORMATS = {".avi", ".mp4", ".mov", ".mkv"}

ROCK_TYPES = ("mi_sang", "1x2", "2x4", "4x6")

# Thứ tự đặc trưng phải khớp với lúc huấn luyện XGBoost.
FEATURE_COLUMNS = [
    "count_mi_sang",
    "count_1x2",
    "count_2x4",
    "count_4x6",
    "area_cm2_mi_sang",
    "area_cm2_1x2",
    "area_cm2_2x4",
    "area_cm2_4x6",
    "perimeter_cm_mi_sang",
    "perimeter_cm_1x2",
    "perimeter_cm_2x4",
    "perimeter_cm_4x6",
]

CSV_COLUMNS = [
    "folder_name",
    "actual_weight",
    *FEATURE_COLUMNS,
]


# ============================================================
# 2. VIDEO STATE
# ============================================================

video_path = ""
base_name = ""
folder_name = ""
actual_weight = 0.0

data_folder = Path()
csv_path = Path()

# Tương thích với một số đoạn code cũ.
video_cut_time = VIDEO_CUT_TIME
pixel2cm = PIXEL2CM_WEIGHT
model_path = YOLO_MODEL_PATH


# ============================================================
# 3. VIDEO PATH
# ============================================================

def resolve_video_path(new_path) -> Path:

    path = Path(new_path).expanduser()

    if path.is_absolute():
        candidates = [path]
    else:
        candidates = [
            ROOT_DIR / path,
            VIDEO_DIR / path,
            Path.cwd() / path,
        ]

    for candidate in candidates:
        if candidate.is_file():
            resolved = candidate.resolve()

            if resolved.suffix.lower() not in SUPPORTED_VIDEO_FORMATS:
                raise ValueError(
                    f"Định dạng video không được hỗ trợ: "
                    f"{resolved.suffix}"
                )

            return resolved

    raise FileNotFoundError(
        f"Không tìm thấy video: {new_path}"
    )


def extract_actual_weight(path=None) -> float:
    """
    Ví dụ:
        10.3kg.avi -> 10.3
        video_test.avi -> 0.0
    Tên video không bắt buộc phải chứa khối lượng.
    """

    if path is None:
        return 0.0

    match = re.search(
        r"(\d+(?:[.,]\d+)?)\s*kg",
        Path(path).stem,
        flags=re.IGNORECASE,
    )

    if match is None:
        return 0.0

    return float(match.group(1).replace(",", "."))


def set_video_path(new_path) -> Path:
    """Thiết lập video và đường dẫn dữ liệu tương ứng."""

    global video_path, base_name, folder_name
    global actual_weight, data_folder, csv_path

    resolved_path = resolve_video_path(new_path)

    video_path = str(resolved_path)
    base_name = resolved_path.name
    folder_name = resolved_path.stem.replace(",", ".")

    actual_weight = extract_actual_weight(resolved_path)

    data_folder = DATA_DIR / folder_name
    csv_path = data_folder / f"Data_Weights_{folder_name}.csv"

    return resolved_path


# ============================================================
# 4. VIDEO TO FRAMES
# ============================================================

def video_to_frame() -> Path:
    """
    Trích xuất frame theo chu kỳ VIDEO_CUT_TIME.
    """

    if not video_path:
        raise RuntimeError(
            "Chưa thiết lập video. Hãy gọi set_video_path() trước."
        )

    if VIDEO_CUT_TIME <= 0:
        raise ValueError("VIDEO_CUT_TIME phải lớn hơn 0.")

    data_folder.mkdir(parents=True, exist_ok=True)

    # Xóa frame do chương trình tạo ở lần chạy trước.
    for old_frame in data_folder.glob("frame_*.jpg"):
        old_frame.unlink()

    cap = cv2.VideoCapture(video_path)

    if not cap.isOpened():
        cap.release()
        raise RuntimeError(f"Không thể mở video: {video_path}")

    fps = cap.get(cv2.CAP_PROP_FPS)

    if not np.isfinite(fps) or fps <= 0:
        cap.release()
        raise ValueError(f"FPS không hợp lệ: {fps}")

    interval_frames = max(1, round(fps * VIDEO_CUT_TIME))

    print(f"Video: {Path(video_path).name}")
    print(f"FPS: {fps:.2f}")
    print(f"Chu kỳ trích xuất: {VIDEO_CUT_TIME:.2f} giây")
    print("Đang trích xuất frame...")

    frame_count = 0
    saved_count = 0

    try:
        while True:
            ret, frame = cap.read()

            if not ret:
                break

            if frame_count % interval_frames == 0:
                output_path = (
                    data_folder / f"frame_{saved_count:04d}.jpg"
                )

                if not cv2.imwrite(str(output_path), frame):
                    raise RuntimeError(
                        f"Không thể lưu frame: {output_path}"
                    )

                saved_count += 1

            frame_count += 1

    finally:
        cap.release()

    if saved_count == 0:
        raise RuntimeError("Không trích xuất được frame nào.")

    print(f"Đã lưu {saved_count} frame vào: {data_folder}")

    return data_folder


# ============================================================
# 5. CLASSIFY ROCK
# ============================================================

def classify_rock(length_cm: float, width_cm: float):
    """
    length_cm và width_cm được tính từ tỉ lệ pixel/cm
    của pipeline đang thực hiện.
    """

    if (
        not np.isfinite(length_cm)
        or not np.isfinite(width_cm)
        or length_cm < 0
        or width_cm < 0
    ):
        return None

    shorter_side_mm = min(length_cm, width_cm) * 10.0

    if shorter_side_mm < 10:
        return "mi_sang"

    if shorter_side_mm < 20:
        return "1x2"

    if shorter_side_mm < 40:
        return "2x4"

    return "4x6"


# ============================================================
# 6. YOLO MODEL
# ============================================================

@lru_cache(maxsize=1)
def _load_yolo_model():
    if not YOLO_MODEL_PATH.is_file():
        raise FileNotFoundError(
            f"Không tìm thấy YOLO model: {YOLO_MODEL_PATH}"
        )

    return YOLO(str(YOLO_MODEL_PATH))


def _get_frame_paths():
    """Kiểm tra và lấy danh sách frame đã trích xuất."""

    if not video_path:
        raise RuntimeError(
            "Chưa thiết lập video. Hãy gọi set_video_path() trước."
        )

    if not data_folder.is_dir():
        raise FileNotFoundError(
            f"Không tìm thấy thư mục frame: {data_folder}. "
            "Hãy gọi video_to_frame() trước."
        )

    frame_paths = sorted(data_folder.glob("frame_*.jpg"))

    if not frame_paths:
        raise FileNotFoundError(
            f"Không tìm thấy frame trong {data_folder}."
        )

    return frame_paths


def _get_contour_dimensions(contour, pixel_per_cm):
    """
    pixel_per_cm: tỉ lệ pixel/cm
    """

    if pixel_per_cm <= 0:
        raise ValueError("Tỉ lệ pixel/cm phải lớn hơn 0.")

    contour = np.asarray(contour, dtype=np.float32)

    if contour.ndim != 2 or len(contour) < 3:
        return None

    if not np.isfinite(contour).all():
        return None

    _, dimensions, _ = cv2.minAreaRect(contour)
    width_px, height_px = dimensions

    if width_px <= 0 or height_px <= 0:
        return None

    length_cm = max(width_px, height_px) / pixel_per_cm
    width_cm = min(width_px, height_px) / pixel_per_cm

    return length_cm, width_cm


# ============================================================
# 7. PIPELINE A: COUNT ROCKS WITH YOLO ONLY
# ============================================================

def count_rocks():
    """
    Video -> Frames -> YOLO -> PIXEL2CM_COUNT -> Đếm đá.
    Trả về:
        {
            "mi_sang": số lượng,
            "1x2": số lượng,
            "2x4": số lượng,
            "4x6": số lượng
        }
    đây là tổng lượt phát hiện qua các frame,
    không đảm bảo là số viên đá duy nhất.
    """

    if PIXEL2CM_COUNT <= 0:
        raise ValueError("PIXEL2CM_COUNT phải lớn hơn 0.")

    frame_paths = _get_frame_paths()
    model = _load_yolo_model()

    rock_counts = {rock: 0 for rock in ROCK_TYPES}

    print("\n========== PIPELINE A: ĐẾM ĐÁ ==========")
    print(f"Tỉ lệ đếm: {PIXEL2CM_COUNT} pixel/cm")

    for index, img_path in enumerate(frame_paths, start=1):
        results = model(str(img_path), verbose=False)

        for result in results:
            if result.masks is None:
                continue

            for contour in result.masks.xy:
                dimensions = _get_contour_dimensions(
                    contour,
                    PIXEL2CM_COUNT,
                )

                if dimensions is None:
                    continue

                length_cm, width_cm = dimensions

                rock_type = classify_rock(length_cm, width_cm)

                if rock_type in rock_counts:
                    rock_counts[rock_type] += 1

        print(
            f"\rĐang đếm đá: frame {index}/{len(frame_paths)}",
            end="",
            flush=True,
        )

    print()
    print("\n========== KẾT QUẢ ĐẾM ĐÁ ==========")

    for rock_type in ROCK_TYPES:
        print(f"Đá {rock_type}: {rock_counts[rock_type]} lượt phát hiện")

    total_rocks = sum(rock_counts.values())
    print(f"Tổng số lượt phát hiện: {total_rocks}")

    return rock_counts


# ============================================================
# 8. PIPELINE B: YOLO -> FEATURES CSV
# ============================================================

def create_csv() -> Path:
    """
    Chỉ dùng PIXEL2CM_WEIGHT, không dùng PIXEL2CM_COUNT.
    Đặc trưng:
        - Số lượt phát hiện từng loại đá.
        - Tổng diện tích từng loại (cm²).
        - Tổng chu vi từng loại (cm).
    """

    if PIXEL2CM_WEIGHT <= 0:
        raise ValueError("PIXEL2CM_WEIGHT phải lớn hơn 0.")

    frame_paths = _get_frame_paths()
    model = _load_yolo_model()

    stone_counts = {rock: 0 for rock in ROCK_TYPES}
    area_cm2 = {rock: 0.0 for rock in ROCK_TYPES}
    perimeter_cm = {rock: 0.0 for rock in ROCK_TYPES}

    print("\n========== PIPELINE B: TRÍCH XUẤT ĐẶC TRƯNG ==========")
    print(f"Tỉ lệ khối lượng: {PIXEL2CM_WEIGHT} pixel/cm")

    for index, img_path in enumerate(frame_paths, start=1):
        results = model(str(img_path), verbose=False)

        for result in results:
            if result.masks is None:
                continue

            for contour_xy in result.masks.xy:
                contour = np.asarray(contour_xy, dtype=np.float32)

                if contour.ndim != 2 or len(contour) < 3:
                    continue

                if not np.isfinite(contour).all():
                    continue

                dimensions = _get_contour_dimensions(
                    contour,
                    PIXEL2CM_WEIGHT,
                )

                if dimensions is None:
                    continue

                length_cm, width_cm = dimensions

                rock_type = classify_rock(length_cm, width_cm)

                if rock_type not in stone_counts:
                    continue

                stone_counts[rock_type] += 1

                # Diện tích: pixel² -> cm²
                area_cm2[rock_type] += (
                    cv2.contourArea(contour)
                    / (PIXEL2CM_WEIGHT ** 2)
                )

                # Chu vi: pixel -> cm
                perimeter_cm[rock_type] += (
                    cv2.arcLength(contour, True)
                    / PIXEL2CM_WEIGHT
                )

        print(
            f"\rĐang xử lý đặc trưng: frame "
            f"{index}/{len(frame_paths)}",
            end="",
            flush=True,
        )

    print()

    row = {
        "folder_name": folder_name,
        "actual_weight": actual_weight,
    }

    for rock in ROCK_TYPES:
        row[f"count_{rock}"] = stone_counts[rock]

    for rock in ROCK_TYPES:
        row[f"area_cm2_{rock}"] = round(area_cm2[rock], 4)

    for rock in ROCK_TYPES:
        row[f"perimeter_cm_{rock}"] = round(
            perimeter_cm[rock], 4
        )

    data_folder.mkdir(parents=True, exist_ok=True)

    with csv_path.open(
        mode="w",
        newline="",
        encoding="utf-8",
    ) as file:
        writer = csv.DictWriter(
            file,
            fieldnames=CSV_COLUMNS,
        )
        writer.writeheader()
        writer.writerow(row)

    print(f"Đã lưu CSV đặc trưng: {csv_path}")

    return csv_path


# ============================================================
# 9. PREPARE XGBOOST INPUT
# ============================================================

def prepare_data(csv_file) -> tuple[np.ndarray, np.ndarray]:
    """
    Đọc CSV và trả về:
        X: 12 đặc trưng theo thứ tự FEATURE_COLUMNS.
        y: khối lượng thực.

    y được giữ lại để tương thích với test.py.
    """

    csv_file = Path(csv_file)

    if not csv_file.is_file():
        raise FileNotFoundError(
            f"Không tìm thấy file CSV: {csv_file}"
        )

    data = pd.read_csv(csv_file)

    required_columns = ["actual_weight", *FEATURE_COLUMNS]

    missing = [
        column
        for column in required_columns
        if column not in data.columns
    ]

    if missing:
        raise ValueError(
            "CSV thiếu các cột: " + ", ".join(missing)
        )

    if data.empty:
        raise ValueError("CSV không có dữ liệu.")

    X = data[FEATURE_COLUMNS].to_numpy(dtype=np.float64)
    y = data["actual_weight"].to_numpy(dtype=np.float64)

    if not np.isfinite(X).all():
        raise ValueError("Đặc trưng X chứa NaN hoặc vô cực.")

    if not np.isfinite(y).all():
        raise ValueError("Khối lượng thực chứa NaN hoặc vô cực.")

    return X, y


# ============================================================
# 10. XGBOOST MODEL
# ============================================================

@lru_cache(maxsize=1)
def _load_xgboost_model():
    if not XGBOOST_MODEL_PATH.is_file():
        raise FileNotFoundError(
            f"Không tìm thấy XGBoost model: {XGBOOST_MODEL_PATH}"
        )

    return joblib.load(XGBOOST_MODEL_PATH)


def predict_xgboost(X_new) -> float:
    """
    Dự đoán khối lượng bằng XGBoost.

    Đầu vào phải có đúng 12 đặc trưng, theo thứ tự FEATURE_COLUMNS.
    Trả về khối lượng dự đoán của mẫu đầu tiên, đơn vị kg.
    """

    X_new = np.asarray(X_new, dtype=np.float64)

    if X_new.ndim == 1:
        X_new = X_new.reshape(1, -1)

    if X_new.ndim != 2:
        raise ValueError("X_new phải là mảng 1 hoặc 2 chiều.")

    if X_new.shape[0] == 0:
        raise ValueError("Không có mẫu để dự đoán.")

    if X_new.shape[1] != len(FEATURE_COLUMNS):
        raise ValueError(
            f"XGBoost cần {len(FEATURE_COLUMNS)} đặc trưng, "
            f"nhưng nhận được {X_new.shape[1]}."
        )

    if not np.isfinite(X_new).all():
        raise ValueError("X_new chứa NaN hoặc vô cực.")

    # Giữ hành vi cũ của dự án.
    if np.all(X_new == 0):
        return 0.0

    model = _load_xgboost_model()
    predictions = np.asarray(model.predict(X_new)).reshape(-1)

    if predictions.size == 0:
        raise RuntimeError("XGBoost không trả về kết quả.")

    prediction = float(predictions[0])

    if not np.isfinite(prediction):
        raise RuntimeError(
            "XGBoost trả về khối lượng không hợp lệ."
        )

    return prediction
