"""Offline session-video inference using the same YOLO and weight pipeline as runtime inference."""
import os
import sys

import cv2

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(ROOT_DIR)

from module.YOLO.src.YOLOModel import YOLOImpl
from utils.config import IMAGE_WIDTH, IMAGE_HEIGHT, TYPE_COLORS


def result_line(frame_count, counts, weight_totals):
    averages = [value / frame_count if frame_count else 0.0 for value in weight_totals]
    ordered_counts = [counts.get(name, 0) for name in list(TYPE_COLORS.keys())[:4]]
    print(
        "VIDEO_RESULT|frames={}|MiSang={}|1x2={}|2x4={}|4x6={}|weight1={}|weight2={}|weight3={}|weight4={}".format(
            frame_count, *ordered_counts, *averages),
        flush=True,
    )


def main():
    if len(sys.argv) != 2:
        raise ValueError("Usage: video_inference.py <finalized-video-path>")

    capture = cv2.VideoCapture(sys.argv[1])
    if not capture.isOpened():
        raise RuntimeError("Could not open the finalized recording.")

    model = YOLOImpl()
    total_counts = {name: 0 for name in TYPE_COLORS.keys()}
    total_weights = [0.0, 0.0, 0.0, 0.0]
    frame_count = 0
    try:
        while True:
            success, frame = capture.read()
            if not success:
                break
            if frame.shape[1] != IMAGE_WIDTH or frame.shape[0] != IMAGE_HEIGHT:
                frame = cv2.resize(frame, (IMAGE_WIDTH, IMAGE_HEIGHT), interpolation=cv2.INTER_LINEAR)

            inference = model.inference(frame)
            _, counts, weights = model.processAndVisualizeWithMultipleWeight(inference, None, frame)
            for name, value in counts.items():
                total_counts[name] += value
            for index, value in enumerate(weights):
                total_weights[index] += float(value) if value is not None else 0.0
            frame_count += 1
    finally:
        capture.release()

    result_line(frame_count, total_counts, total_weights)


if __name__ == "__main__":
    main()
