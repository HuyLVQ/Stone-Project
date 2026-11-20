
"""
test.py

Chương trình kiểm thử dự đoán khối lượng và đếm số lượng đá.

Pipeline A:
    Video -> Frames -> YOLO -> Đếm số lượng đá.

Pipeline B:
    Video -> Frames -> YOLO -> Đặc trưng -> XGBoost -> Khối lượng.

Cách chạy:
    python test.py video/video_thu_nghiem.avi
    python test.py video/video_thu_nghiem.avi --task count
    python test.py video/video_thu_nghiem.avi --task weight --weight 10.3
    python test.py video/video_thu_nghiem.avi --task both --weight 10.3
"""

import argparse
import sys
from pathlib import Path

from utils import (
    set_video_path,
    video_to_frame,
    count_rocks,
    create_csv,
    prepare_data,
    predict_xgboost,
)
from utils import utils as utils_module


def parse_args():
    """Đọc các tham số dòng lệnh."""

    parser = argparse.ArgumentParser(
        description=(
            "Đếm số lượng đá bằng YOLO và/hoặc "
            "dự đoán khối lượng bằng YOLO + XGBoost."
        )
    )

    parser.add_argument(
        "video",
        nargs="?",
        default=None,
        help=(
            "Đường dẫn video. Có thể là đường dẫn tương đối "
            "hoặc tuyệt đối."
        ),
    )

    parser.add_argument(
        "--task",
        choices=["count", "weight", "both"],
        default="both",
        help=(
            "count: chỉ đếm đá; "
            "weight: chỉ dự đoán khối lượng; "
            "both: chạy cả hai. Mặc định: both."
        ),
    )

    parser.add_argument(
        "--weight",
        type=float,
        default=None,
        help=(
            "Khối lượng thực (kg), dùng để đánh giá sai số. "
            "Không bắt buộc."
        ),
    )

    return parser.parse_args()


def select_video(video_argument):
    """
    Xác định video đầu vào.

    Nếu người dùng không truyền đường dẫn:
    - Tự động chọn khi thư mục video chỉ có một video.
    - Yêu cầu truyền đường dẫn nếu có nhiều video.
    """

    if video_argument:
        return set_video_path(video_argument)

    video_dir = utils_module.VIDEO_DIR

    if not video_dir.is_dir():
        raise FileNotFoundError(
            f"Không tìm thấy thư mục video: {video_dir}"
        )

    supported_formats = utils_module.SUPPORTED_VIDEO_FORMATS

    videos = sorted(
        path
        for path in video_dir.iterdir()
        if path.is_file()
        and path.suffix.lower() in supported_formats
    )

    if len(videos) == 0:
        raise FileNotFoundError(
            f"Không tìm thấy video trong {video_dir}.\n"
            "Hãy đặt video vào thư mục video hoặc truyền "
            "đường dẫn video khi chạy chương trình."
        )

    if len(videos) > 1:
        video_list = "\n".join(
            f"  {index}. {path.name}"
            for index, path in enumerate(videos, start=1)
        )

        raise ValueError(
            "Có nhiều video trong thư mục video. "
            "Hãy chỉ định video muốn xử lý:\n"
            f"{video_list}\n\n"
            "Ví dụ:\n"
            "python test.py video/video_thu_nghiem.avi"
        )

    return set_video_path(videos[0])


def run_count_task():
    """Chạy riêng pipeline đếm đá bằng YOLO."""

    print("\n" + "=" * 55)
    print("PIPELINE A: ĐẾM SỐ LƯỢNG ĐÁ BẰNG YOLO")
    print("=" * 55)

    print(
        "Tỉ lệ pixel/cm:",
        utils_module.PIXEL2CM_COUNT,
    )

    rock_counts = count_rocks()

    total_detections = sum(rock_counts.values())

    print("\nKẾT QUẢ ĐẾM ĐÁ")
    print("-" * 40)
    print(f"Đá mi sàng : {rock_counts['mi_sang']} lượt")
    print(f"Đá 1x2     : {rock_counts['1x2']} lượt")
    print(f"Đá 2x4     : {rock_counts['2x4']} lượt")
    print(f"Đá 4x6     : {rock_counts['4x6']} lượt")
    print("-" * 40)
    print(f"Tổng lượt phát hiện: {total_detections}")

    return rock_counts


def run_weight_task(actual_weight=None):
    """Chạy riêng pipeline dự đoán khối lượng bằng YOLO + XGBoost."""

    print("\n" + "=" * 55)
    print("PIPELINE B: DỰ ĐOÁN KHỐI LƯỢNG BẰNG XGBOOST")
    print("=" * 55)

    print(
        "Tỉ lệ pixel/cm:",
        utils_module.PIXEL2CM_WEIGHT,
    )

    # Cập nhật khối lượng thực trước khi ghi CSV.
    if actual_weight is not None:
        utils_module.actual_weight = actual_weight

    # YOLO trích xuất đặc trưng bằng PIXEL2CM_WEIGHT.
    csv_file = create_csv()

    # Đưa các đặc trưng vào XGBoost.
    X_test, y_test = prepare_data(csv_file)

    # Dự đoán khối lượng.
    predicted_weight = predict_xgboost(X_test)

    print("\nKẾT QUẢ DỰ ĐOÁN KHỐI LƯỢNG")
    print("-" * 40)
    print(f"Khối lượng dự đoán: {predicted_weight:.3f} kg")

    if actual_weight is not None:
        absolute_error = abs(predicted_weight - actual_weight)

        print(f"Khối lượng thực   : {actual_weight:.3f} kg")
        print(f"Sai số tuyệt đối  : {absolute_error:.3f} kg")

        if actual_weight != 0:
            percentage_error = (
                absolute_error / abs(actual_weight)
            ) * 100

            print(f"Sai số tương đối  : {percentage_error:.2f}%")
        else:
            print(
                "Không tính sai số tương đối vì khối lượng thực bằng 0."
            )

    print(f"File CSV đặc trưng: {csv_file}")

    return predicted_weight


def main():
    """Hàm điều phối chương trình."""

    args = parse_args()

    try:
        selected_video = select_video(args.video)

        print("\n" + "=" * 55)
        print("THÔNG TIN ĐẦU VÀO")
        print("=" * 55)
        print(f"Video: {selected_video}")
        print(f"Tác vụ: {args.task}")

        if args.weight is not None:
            if args.weight < 0:
                raise ValueError(
                    "Khối lượng thực không được âm."
                )

            print(f"Khối lượng thực: {args.weight:.3f} kg")

        # Trích xuất frame một lần, dùng chung cho cả hai pipeline.
        video_to_frame()

        rock_counts = None
        predicted_weight = None

        if args.task in ("count", "both"):
            rock_counts = run_count_task()

        if args.task in ("weight", "both"):
            predicted_weight = run_weight_task(args.weight)

        print("\n" + "=" * 55)
        print("HOÀN TẤT")
        print("=" * 55)

        if rock_counts is not None:
            print("\nSố lượt phát hiện đá:")

            for rock_type, count in rock_counts.items():
                print(f"  {rock_type}: {count}")

            print(
                "  Tổng:",
                sum(rock_counts.values()),
            )

        if predicted_weight is not None:
            print(
                f"\nKhối lượng dự đoán: "
                f"{predicted_weight:.3f} kg"
            )

        return 0

    except (FileNotFoundError, ValueError, RuntimeError) as exc:
        print(f"\nLỖI: {exc}", file=sys.stderr)
        return 1

    except KeyboardInterrupt:
        print("\nĐã dừng chương trình.")
        return 130


if __name__ == "__main__":
    sys.exit(main())
