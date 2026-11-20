"""Run the Weight_v2/test.py pipeline on finalized session recordings."""
import json
import math
from pathlib import Path
import sys

import cv2
import numpy as np
import pandas as pd
from ultralytics import YOLO

from Weight_v2.utils import utils as pipeline


def _recording_segments(recording):
    path = Path(recording).resolve()
    if path.suffix.lower() != ".json":
        return [(path, None)], 0, False

    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    if manifest.get("Version") != 1:
        raise ValueError("Unsupported recording manifest version")
    if manifest.get("Error"):
        raise ValueError("Recording could not finalize: " + manifest["Error"])
    segments = []
    for segment in manifest.get("Segments", []):
        video = Path(segment["Path"])
        if not video.is_absolute():
            video = path.parent / video
        expected = int(segment["Frames"])
        if expected < 0:
            raise ValueError("Negative expected frame count")
        segments.append((video, expected))
    if not segments:
        raise ValueError("Recording contains no finalized segments")
    dropped = sum(int(manifest.get(key, 0)) for key in
                  ("Dropped", "CaptureFailures", "FailedFrames"))
    return segments, dropped, True


def _feature_row(counts, areas, perimeters):
    row = {}
    for rock in pipeline.ROCK_TYPES:
        row[f"count_{rock}"] = counts[rock]
    for rock in pipeline.ROCK_TYPES:
        row[f"area_cm2_{rock}"] = areas[rock]
    for rock in pipeline.ROCK_TYPES:
        row[f"perimeter_cm_{rock}"] = perimeters[rock]
    return pd.DataFrame([[row[column] for column in pipeline.FEATURE_COLUMNS]],
                        columns=pipeline.FEATURE_COLUMNS)


def _visualize_weight_inference(result, original):
    """Draw the weight-pipeline classifications using YOLOModel's overlay style."""
    drawn = original.copy()
    overlay = drawn.copy()
    colors = {
        "mi_sang": (255, 120, 0),
        "1x2": (0, 220, 0),
        "2x4": (0, 220, 255),
        "4x6": (0, 80, 255),
    }

    if result.masks is not None:
        for object_id, contour_xy in enumerate(result.masks.xy):
            contour = np.asarray(contour_xy, dtype=np.float32)
            dimensions = pipeline.get_contour_dimensions(
                contour, pipeline.PIXEL2CM_WEIGHT)
            if dimensions is None:
                continue
            rock = pipeline.classify_rock(*dimensions)
            if rock not in colors:
                continue

            contour_int = np.asarray(contour, dtype=np.int32)
            rect = cv2.minAreaRect(contour)
            box = np.int32(cv2.boxPoints(rect))
            cv2.fillPoly(overlay, [contour_int], colors[rock])
            cv2.polylines(drawn, [box], isClosed=True, color=(0, 255, 0), thickness=2)

            moments = cv2.moments(contour_int)
            if moments["m00"]:
                center_x = int(moments["m10"] / moments["m00"])
                center_y = int(moments["m01"] / moments["m00"])
            else:
                center_x, center_y = int(contour[0][0]), int(contour[0][1])
            cv2.putText(drawn, "ID:{} - {}".format(object_id, rock),
                        (center_x - 20, center_y), cv2.FONT_HERSHEY_SIMPLEX,
                        2.0, (255, 255, 255), 1)

    cv2.addWeighted(overlay, 0.28, drawn, 0.72, 0, drawn)
    return drawn


def _accumulate(result, count_scale, weight_scale, counts, areas, perimeters):
    if result.masks is None:
        return
    for contour_xy in result.masks.xy:
        contour = np.asarray(contour_xy, dtype=np.float32)
        if contour.ndim != 2 or len(contour) < 3 or not np.isfinite(contour).all():
            continue
        dimensions = pipeline.get_contour_dimensions(contour, count_scale)
        if dimensions is not None:
            rock = pipeline.classify_rock(*dimensions)
            if rock in counts:
                counts[rock] += 1
        dimensions = pipeline.get_contour_dimensions(contour, weight_scale)
        if dimensions is None:
            continue
        rock = pipeline.classify_rock(*dimensions)
        if rock not in counts:
            continue
        areas[rock] += cv2.contourArea(contour) / (weight_scale ** 2)
        perimeters[rock] += cv2.arcLength(contour, True) / weight_scale


def analyze_recording(recording, model=None, capture_factory=cv2.VideoCapture, batch_size=8):
    """Sample every test.py interval and return its aggregate count and XGBoost result."""
    if batch_size < 1:
        raise ValueError("Batch size must be positive")
    segments, dropped, trusted = _recording_segments(recording)
    recording_path = Path(recording).resolve()
    image_directory = recording_path.parent / "inference_images"
    image_directory.mkdir(parents=True, exist_ok=True)
    model = model or YOLO(str(pipeline.YOLO_MODEL_PATH))
    counts = {rock: 0 for rock in pipeline.ROCK_TYPES}
    areas = {rock: 0.0 for rock in pipeline.ROCK_TYPES}
    perimeters = {rock: 0.0 for rock in pipeline.ROCK_TYPES}
    sampled_expected = processed = skipped = 0
    known = True

    def process_batch(batch, path):
        nonlocal processed, skipped
        if not batch:
            return

        frames = [frame for _, frame in batch]
        try:
            results = list(model(frames, verbose=False))
            if len(results) != len(batch):
                raise RuntimeError("Batch inference returned an unexpected number of results")
        except Exception as exc:
            print("[WARN] Batch inference failed; retrying {} samples individually in {}: {}".format(
                len(batch), path, exc), file=sys.stderr)
            results = []
            for frame_index, frame in batch:
                try:
                    individual_results = list(model(frame, verbose=False))
                    if len(individual_results) != 1:
                        raise RuntimeError("Single-frame inference returned an unexpected number of results")
                    results.append(individual_results[0])
                except Exception as frame_exc:
                    results.append(frame_exc)

        for (frame_index, frame), result in zip(batch, results):
            try:
                if isinstance(result, Exception):
                    raise result
                rendered = _visualize_weight_inference(result, frame)
                image_path = image_directory / "{}_part{:04d}_frame{:08d}.jpg".format(
                    recording_path.stem, segment_number, frame_index)
                if not cv2.imwrite(str(image_path), rendered):
                    raise RuntimeError("Could not save inference image: {}".format(image_path))
                _accumulate(result, pipeline.PIXEL2CM_COUNT,
                            pipeline.PIXEL2CM_WEIGHT, counts, areas, perimeters)
                processed += 1
            except Exception as exc:
                skipped += 1
                print("[WARN] Skipped sampled frame {} in {}: {}".format(frame_index, path, exc),
                      file=sys.stderr)

    for segment_number, (path, physical_expected) in enumerate(segments, start=1):
        capture = capture_factory(str(path))
        if not capture.isOpened():
            capture.release()
            if physical_expected is None:
                raise RuntimeError("Could not open recording: " + str(path))
            # The manifest has frame count but not FPS, so the v2 sample count
            # cannot be derived safely when the segment itself will not open.
            known = False
            continue
        try:
            fps = capture.get(cv2.CAP_PROP_FPS)
            if not math.isfinite(fps) or fps <= 0:
                raise ValueError("FPS is invalid for recording: {}".format(path))
            interval = max(1, round(fps * pipeline.VIDEO_CUT_TIME))
            if physical_expected is None:
                known = False
                reported = capture.get(cv2.CAP_PROP_FRAME_COUNT)
                physical_expected = int(reported) if math.isfinite(reported) and reported > 0 else None
            if physical_expected is not None:
                sampled_expected += len(range(0, physical_expected, interval))
            frame_index = consecutive_failures = 0
            pending = []
            while physical_expected is None or frame_index < physical_expected:
                ok, frame = capture.read()
                if not ok:
                    if physical_expected is None:
                        break
                    if frame_index % interval == 0:
                        skipped += 1
                    frame_index += 1
                    consecutive_failures += 1
                    if consecutive_failures >= 30:
                        break
                    if not capture.set(cv2.CAP_PROP_POS_FRAMES, frame_index):
                        break
                    continue
                consecutive_failures = 0
                current = frame_index
                frame_index += 1
                if current % interval:
                    continue
                try:
                    # test.py persists sampled images as JPEG before inference.
                    encoded_ok, encoded = cv2.imencode(".jpg", frame)
                    if not encoded_ok:
                        raise RuntimeError("Could not encode sampled frame as JPEG")
                    sampled_frame = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
                    if sampled_frame is None:
                        raise RuntimeError("Could not decode sampled JPEG frame")
                    pending.append((current, sampled_frame))
                    if len(pending) >= batch_size:
                        process_batch(pending, path)
                        pending = []
                except Exception as exc:
                    skipped += 1
                    print("[WARN] Skipped sampled frame {} in {}: {}".format(current, path, exc),
                          file=sys.stderr)
            process_batch(pending, path)
        finally:
            capture.release()

    if processed == 0:
        raise RuntimeError("No sampled frames could be processed")
    if trusted and known:
        # Account for sample positions after a damaged tail or failed seek.
        skipped = sampled_expected - processed
    prediction = float(pipeline.predict_xgboost(_feature_row(counts, areas, perimeters)))
    if not math.isfinite(prediction):
        raise ValueError("XGBoost returned a non-finite weight")
    complete = trusted and known and processed == sampled_expected and not skipped and not dropped
    fields = [("frames", processed), ("expected", sampled_expected), ("skipped", skipped),
              ("dropped", dropped), ("complete", int(complete)), ("known", int(trusted and known))]
    fields.extend((name, counts[rock]) for name, rock in zip(("MiSang", "1x2", "2x4", "4x6"),
                                                               pipeline.ROCK_TYPES))
    # Legacy session storage has four weight columns. Only column 1 is populated by test.py's XGBoost.
    fields.extend((("weight1", prediction), ("weight2", 0), ("weight3", 0), ("weight4", 0)))
    return "VIDEO_RESULT|" + "|".join("{}={}".format(key, value) for key, value in fields)


def main():
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording")
    args = parser.parse_args()
    print(analyze_recording(args.recording), flush=True)


if __name__ == "__main__":
    main()
