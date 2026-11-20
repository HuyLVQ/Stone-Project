"""Utilities for rock video processing.

Pipeline A:
    Video -> sampled frames -> YOLO segmentation -> cumulative detection counts.

Pipeline B (frame-level mass estimation):
    Each sampled frame -> YOLO segmentation -> 12 frame-level features
    -> XGBoost predicts that frame's mass -> sum frame predictions.

Important:
    The XGBoost model used by Pipeline B must be trained on frame-level
    feature rows with matching frame-level mass labels. A model trained on
    one aggregated row per complete video is not compatible with this
    frame-level inference pipeline.
"""

from __future__ import annotations

import csv
import re
from functools import lru_cache
from pathlib import Path
from typing import Optional

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
VIDEO_DIR = ROOT_DIR / "video4"

YOLO_MODEL_PATH = MODEL_DIR / "YOLO" / "best260807.pt"

# IMPORTANT: this must be a model trained on one row per frame,
# not one aggregated row per entire video. The filename is kept as xgboost_v3.pkl
# to match the existing project path; verify its training granularity.
XGBOOST_MODEL_PATH = MODEL_DIR / "XGBoost" / "xgboost_v3.pkl"

VIDEO_CUT_TIME = 0.88  # seconds
PIXEL2CM_COUNT = 14.4
PIXEL2CM_WEIGHT = 15.4
YOLO_CONF_WEIGHT = 0.10

SUPPORTED_VIDEO_FORMATS = {".avi", ".mp4", ".mov", ".mkv"}
ROCK_TYPES = ("mi_sang", "1x2", "2x4", "4x6")

# The order must exactly match the feature order used to train XGBoost.
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

FRAME_CSV_COLUMNS = [
    "folder_name",
    "frame_index",
    "timestamp_sec",
    "actual_video_weight_kg",
    *FEATURE_COLUMNS,
]

# ============================================================
# 2. VIDEO STATE
# ============================================================

video_path: str = ""
base_name: str = ""
folder_name: str = ""
actual_weight: float = 0.0

data_folder = Path()
csv_path = Path()
frame_interval_seconds = VIDEO_CUT_TIME

# Backward-compatible names used by some older scripts.
video_cut_time = VIDEO_CUT_TIME
pixel2cm = PIXEL2CM_WEIGHT
model_path = YOLO_MODEL_PATH


# ============================================================
# 3. VIDEO PATH AND METADATA
# ============================================================


def resolve_video_path(new_path: str | Path) -> Path:
    path = Path(new_path).expanduser()
    if path.is_absolute():
        candidates = [path]
    else:
        candidates = [ROOT_DIR / path, VIDEO_DIR / path, Path.cwd() / path]

    for candidate in candidates:
        if candidate.is_file():
            resolved = candidate.resolve()
            if resolved.suffix.lower() not in SUPPORTED_VIDEO_FORMATS:
                raise ValueError(
                    f"Định dạng video không được hỗ trợ: {resolved.suffix}"
                )
            return resolved

    raise FileNotFoundError(f"Không tìm thấy video: {new_path}")


def extract_actual_weight(path: str | Path | None = None) -> float:
    """Extract video total mass from names such as '27.6kg.avi'."""
    if path is None:
        return 0.0
    match = re.search(
        r"(\d+(?:[.,]\d+)?)\s*kg",
        Path(path).stem,
        flags=re.IGNORECASE,
    )
    return float(match.group(1).replace(",", ".")) if match else 0.0


def set_video_path(new_path: str | Path) -> Path:
    """Set the active video and its output directory."""
    global video_path, base_name, folder_name, actual_weight
    global data_folder, csv_path

    resolved = resolve_video_path(new_path)
    video_path = str(resolved)
    base_name = resolved.name
    folder_name = resolved.stem.replace(",", ".")
    actual_weight = extract_actual_weight(resolved)

    data_folder = DATA_DIR / folder_name
    csv_path = data_folder / f"Data_Weights_{folder_name}.csv"
    return resolved


# ============================================================
# 4. VIDEO TO FRAMES
# ============================================================


def video_to_frame() -> Path:
    """Extract one frame every VIDEO_CUT_TIME seconds."""
    global frame_interval_seconds

    if not video_path:
        raise RuntimeError("Chưa thiết lập video. Hãy gọi set_video_path() trước.")
    if VIDEO_CUT_TIME <= 0:
        raise ValueError("VIDEO_CUT_TIME phải lớn hơn 0.")

    data_folder.mkdir(parents=True, exist_ok=True)
    for old_frame in data_folder.glob("frame_*.jpg"):
        old_frame.unlink()

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        cap.release()
        raise RuntimeError(f"Không thể mở video: {video_path}")

    fps = float(cap.get(cv2.CAP_PROP_FPS))
    if not np.isfinite(fps) or fps <= 0:
        cap.release()
        raise ValueError(f"FPS không hợp lệ: {fps}")

    interval_frames = max(1, round(fps * VIDEO_CUT_TIME))
    frame_interval_seconds = interval_frames / fps

    print(f"Video: {Path(video_path).name}")
    print(f"FPS: {fps:.2f}")
    print(f"Chu kỳ trích xuất thực tế: {frame_interval_seconds:.3f} giây")
    print("Đang trích xuất frame...")

    frame_count = 0
    saved_count = 0
    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                break

            if frame_count % interval_frames == 0:
                out_path = data_folder / f"frame_{saved_count:04d}.jpg"
                if not cv2.imwrite(str(out_path), frame):
                    raise RuntimeError(f"Không thể lưu frame: {out_path}")
                saved_count += 1
            frame_count += 1
    finally:
        cap.release()

    if saved_count == 0:
        raise RuntimeError("Không trích xuất được frame nào.")

    print(f"Đã lưu {saved_count} frame vào: {data_folder}")
    return data_folder


# ============================================================
# 5. ROCK SIZE CLASSIFICATION AND CONTOUR FEATURES
# ============================================================


def classify_rock(length_cm: float, width_cm: float) -> Optional[str]:
    """Classify a detected rock using its shorter physical side."""
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


def get_contour_dimensions(contour: np.ndarray, pixel_per_cm: float):
    """Return (length_cm, width_cm) from a segmentation contour."""
    if pixel_per_cm <= 0:
        raise ValueError("Tỉ lệ pixel/cm phải lớn hơn 0.")

    contour = np.asarray(contour, dtype=np.float32)
    if contour.ndim != 2 or len(contour) < 3 or not np.isfinite(contour).all():
        return None

    _, dimensions, _ = cv2.minAreaRect(contour)
    width_px, height_px = dimensions
    if width_px <= 0 or height_px <= 0:
        return None

    return max(width_px, height_px) / pixel_per_cm, min(width_px, height_px) / pixel_per_cm


def _empty_frame_features() -> dict[str, float]:
    """Initialize the 12 features for one individual frame."""
    features: dict[str, float] = {}
    for rock in ROCK_TYPES:
        features[f"count_{rock}"] = 0
    for rock in ROCK_TYPES:
        features[f"area_cm2_{rock}"] = 0.0
    for rock in ROCK_TYPES:
        features[f"perimeter_cm_{rock}"] = 0.0
    return features


def _get_frame_paths() -> list[Path]:
    if not video_path:
        raise RuntimeError("Chưa thiết lập video. Hãy gọi set_video_path() trước.")
    if not data_folder.is_dir():
        raise FileNotFoundError(
            f"Không tìm thấy thư mục frame: {data_folder}. Hãy gọi video_to_frame() trước."
        )

    paths = sorted(data_folder.glob("frame_*.jpg"))
    if not paths:
        raise FileNotFoundError(f"Không tìm thấy frame trong {data_folder}.")
    return paths


# ============================================================
# 6. MODEL LOADING
# ============================================================


@lru_cache(maxsize=1)
def _load_yolo_model():
    if not YOLO_MODEL_PATH.is_file():
        raise FileNotFoundError(f"Không tìm thấy YOLO model: {YOLO_MODEL_PATH}")
    return YOLO(str(YOLO_MODEL_PATH))


@lru_cache(maxsize=1)
def _load_xgboost_model():
    if not XGBOOST_MODEL_PATH.is_file():
        raise FileNotFoundError(
            "Không tìm thấy model XGBoost cấp frame: "
            f"{XGBOOST_MODEL_PATH}\n"
            "Hãy trỏ XGBOOST_MODEL_PATH tới model đã được huấn luyện bằng "
            "đặc trưng từng frame và nhãn khối lượng tương ứng từng frame."
        )
    return joblib.load(XGBOOST_MODEL_PATH)


# ============================================================
# 7. PIPELINE A: COUNT DETECTIONS ACROSS FRAMES
# ============================================================


def count_rocks() -> dict[str, int]:
    """Count detection occurrences across all sampled frames (not unique rocks)."""
    if PIXEL2CM_COUNT <= 0:
        raise ValueError("PIXEL2CM_COUNT phải lớn hơn 0.")

    frame_paths = _get_frame_paths()
    model = _load_yolo_model()
    counts = {rock: 0 for rock in ROCK_TYPES}

    print("\n========== PIPELINE A: ĐẾM ĐÁ ==========")
    print(f"Tỉ lệ đếm: {PIXEL2CM_COUNT} pixel/cm")

    for frame_idx, img_path in enumerate(frame_paths, start=1):
        results = model(str(img_path), verbose=False)
        for result in results:
            if result.masks is None:
                continue
            for contour in result.masks.xy:
                dims = get_contour_dimensions(contour, PIXEL2CM_COUNT)
                if dims is None:
                    continue
                rock_type = classify_rock(*dims)
                if rock_type in counts:
                    counts[rock_type] += 1

        print(
            f"\rĐang đếm đá: frame {frame_idx}/{len(frame_paths)}",
            end="",
            flush=True,
        )

    print("\n\n========== KẾT QUẢ ĐẾM ĐÁ ==========")
    for rock_type in ROCK_TYPES:
        print(f"Đá {rock_type}: {counts[rock_type]} lượt phát hiện")
    print(f"Tổng số lượt phát hiện: {sum(counts.values())}")
    return counts


# ============================================================
# 8. PIPELINE B: FRAME FEATURES -> ONE XGBOOST PREDICTION / FRAME
# ============================================================


def create_csv() -> Path:
    """Create one feature row per sampled frame (no object crops, no video sum)."""
    if PIXEL2CM_WEIGHT <= 0:
        raise ValueError("PIXEL2CM_WEIGHT phải lớn hơn 0.")

    frame_paths = _get_frame_paths()
    model = _load_yolo_model()
    rows: list[dict[str, object]] = []

    print("\n========== PIPELINE B: ĐẶC TRƯNG TỪNG FRAME ==========")
    print(f"Tỉ lệ khối lượng: {PIXEL2CM_WEIGHT} pixel/cm")
    print("Mỗi frame tạo một dòng CSV; không cộng feature giữa các frame.")

    for frame_idx, img_path in enumerate(frame_paths):
        frame_features = _empty_frame_features()
        results = model(str(img_path), conf=YOLO_CONF_WEIGHT, verbose=False)

        for result in results:
            if result.masks is None:
                continue

            for contour_xy in result.masks.xy:
                contour = np.asarray(contour_xy, dtype=np.float32)
                dims = get_contour_dimensions(contour, PIXEL2CM_WEIGHT)
                if dims is None:
                    continue

                rock_type = classify_rock(*dims)
                if rock_type not in ROCK_TYPES:
                    continue

                frame_features[f"count_{rock_type}"] += 1
                frame_features[f"area_cm2_{rock_type}"] += (
                    float(cv2.contourArea(contour)) / (PIXEL2CM_WEIGHT ** 2)
                )
                frame_features[f"perimeter_cm_{rock_type}"] += (
                    float(cv2.arcLength(contour, True)) / PIXEL2CM_WEIGHT
                )

        # A row contains only detections from this frame.
        row: dict[str, object] = {
            "folder_name": folder_name,
            "frame_index": frame_idx + 1,
            "timestamp_sec": round(frame_idx * frame_interval_seconds, 4),
            # This is video-level metadata, NOT a frame-level training label.
            "actual_video_weight_kg": actual_weight,
        }
        for col in FEATURE_COLUMNS:
            value = frame_features[col]
            row[col] = int(value) if col.startswith("count_") else round(float(value), 6)
        rows.append(row)

        print(
            f"\rĐang trích xuất đặc trưng: frame {frame_idx + 1}/{len(frame_paths)}",
            end="",
            flush=True,
        )

    print()
    data_folder.mkdir(parents=True, exist_ok=True)
    frame_csv_path = data_folder / f"Data_Weights_{folder_name}_PerFrame.csv"
    pd.DataFrame(rows, columns=FRAME_CSV_COLUMNS).to_csv(
        frame_csv_path, index=False, encoding="utf-8-sig"
    )

    print(f"Đã lưu CSV đặc trưng từng frame: {frame_csv_path}")
    print(f"Số dòng đặc trưng: {len(rows)} (tương ứng {len(frame_paths)} frame)")
    return frame_csv_path


# ============================================================
# 9. PREPARE FRAME FEATURES
# ============================================================


def prepare_data(csv_file: str | Path) -> tuple[np.ndarray, np.ndarray]:
    """Read frame feature rows.

    Returns X and y_metadata for backward compatibility. y_metadata contains
    the video-level actual mass repeated in the CSV; it is NOT a valid
    frame-level target and must not be used to train a frame-level model.
    """
    csv_file = Path(csv_file)
    if not csv_file.is_file():
        raise FileNotFoundError(f"Không tìm thấy file CSV: {csv_file}")

    data = pd.read_csv(csv_file)
    missing = [col for col in FEATURE_COLUMNS if col not in data.columns]
    if missing:
        raise ValueError("CSV thiếu các cột: " + ", ".join(missing))
    if data.empty:
        raise ValueError("CSV không có dữ liệu frame.")

    X = data[FEATURE_COLUMNS].to_numpy(dtype=np.float64)
    if not np.isfinite(X).all():
        raise ValueError("Đặc trưng X chứa NaN hoặc vô cực.")

    if "actual_video_weight_kg" in data.columns:
        y_metadata = data["actual_video_weight_kg"].to_numpy(dtype=np.float64)
    elif "actual_weight" in data.columns:
        y_metadata = data["actual_weight"].to_numpy(dtype=np.float64)
    else:
        y_metadata = np.empty((0,), dtype=np.float64)

    return X, y_metadata


# ============================================================
# 10. FRAME-LEVEL XGBOOST INFERENCE AND VIDEO SUM
# ============================================================


def predict_frame_weights(csv_file: str | Path) -> tuple[pd.DataFrame, float]:
    """Predict one mass value for every frame, then sum all frame predictions.

    Empty frames (all 12 features are zero) are assigned 0 kg without querying
    XGBoost, avoiding a non-zero tree-model baseline for an empty frame.
    """
    csv_file = Path(csv_file)
    data = pd.read_csv(csv_file)
    missing = [col for col in FEATURE_COLUMNS if col not in data.columns]
    if missing:
        raise ValueError("CSV thiếu các cột: " + ", ".join(missing))
    if data.empty:
        raise ValueError("CSV không có frame để dự đoán.")

    X = data[FEATURE_COLUMNS].to_numpy(dtype=np.float64)
    if not np.isfinite(X).all():
        raise ValueError("Đặc trưng X chứa NaN hoặc vô cực.")

    model = _load_xgboost_model()
    predictions = np.zeros(len(data), dtype=np.float64)
    nonempty_mask = np.any(X != 0, axis=1)

    nonempty_indices = np.flatnonzero(nonempty_mask)
    print("\n========== XGBOOST: DỰ ĐOÁN TỪNG FRAME ==========")
    for step, row_idx in enumerate(nonempty_indices, start=1):
        frame_X = X[row_idx].reshape(1, -1)
        pred = np.asarray(model.predict(frame_X), dtype=np.float64).reshape(-1)
        if pred.size != 1 or not np.isfinite(pred[0]):
            raise RuntimeError(f"Dự đoán không hợp lệ tại frame {row_idx + 1}.")
        predictions[row_idx] = float(pred[0])
        print(
            f"\rĐang dự đoán: {step}/{len(nonempty_indices)} frame có đá",
            end="",
            flush=True,
        )
    print()

    output = data.copy()
    output["predicted_frame_weight_kg"] = predictions
    output["cumulative_weight_kg"] = np.cumsum(predictions)

    total_weight = float(predictions.sum())
    output_path = csv_file.parent / f"Weight_Predictions_{folder_name or csv_file.parent.name}.csv"
    output.to_csv(output_path, index=False, encoding="utf-8-sig")

    print("\n========== KẾT QUẢ DỰ ĐOÁN ==========")
    print(f"Số frame: {len(output)}")
    print(f"Tổng khối lượng dự đoán: {total_weight:.3f} kg")
    print(f"CSV dự đoán từng frame: {output_path}")

    return output, total_weight


def predict_xgboost(X_new: np.ndarray) -> float:
    """Compatibility wrapper: predict each row independently and sum outputs.

    Each row must represent one frame, not a cumulative aggregation of the
    entire video. For per-frame results, call predict_frame_weights(csv_file).
    """
    X_new = np.asarray(X_new, dtype=np.float64)
    if X_new.ndim == 1:
        X_new = X_new.reshape(1, -1)
    if X_new.ndim != 2:
        raise ValueError("X_new phải là mảng 1 hoặc 2 chiều.")
    if X_new.shape[0] == 0:
        raise ValueError("Không có frame để dự đoán.")
    if X_new.shape[1] != len(FEATURE_COLUMNS):
        raise ValueError(
            f"XGBoost cần {len(FEATURE_COLUMNS)} đặc trưng, "
            f"nhưng nhận được {X_new.shape[1]}."
        )
    if not np.isfinite(X_new).all():
        raise ValueError("X_new chứa NaN hoặc vô cực.")

    model = _load_xgboost_model()
    predictions = np.zeros(X_new.shape[0], dtype=np.float64)
    nonempty_mask = np.any(X_new != 0, axis=1)

    for row_idx in np.flatnonzero(nonempty_mask):
        pred = np.asarray(model.predict(X_new[row_idx].reshape(1, -1))).reshape(-1)
        if pred.size != 1 or not np.isfinite(pred[0]):
            raise RuntimeError(f"Dự đoán không hợp lệ tại mẫu {row_idx + 1}.")
        predictions[row_idx] = float(pred[0])

    return float(predictions.sum())
