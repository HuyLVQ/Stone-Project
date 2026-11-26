"""Analyze finalized video segments, retaining explicit loss/completeness accounting."""
import argparse
import json
import math
import os
from pathlib import Path
import sys

import cv2

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(ROOT_DIR)
CLASSES = ("MiSang", "1x2", "2x4", "4x6")


def load_recording(path):
    path = Path(path).resolve()
    if path.suffix.lower() != ".json":
        return [(path, None)], 0, False
    manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    if manifest.get("Version") != 1:
        raise ValueError("Unsupported recording manifest version")
    if manifest.get("Error"):
        raise ValueError("Recording could not finalize: " + manifest["Error"])
    segments = []
    for segment in manifest["Segments"]:
        segment_path = Path(segment["Path"])
        if not segment_path.is_absolute():
            segment_path = path.parent / segment_path
        count = int(segment["Frames"])
        if count < 0:
            raise ValueError("Negative expected frame count")
        segments.append((segment_path, count))
    if not segments:
        raise ValueError("Recording contains no finalized segments")
    losses = sum(int(manifest.get(name, 0)) for name in
                 ("Dropped", "CaptureFailures", "FailedFrames"))
    return segments, losses, True


def analyze(segments, model, width, height, recovery_limit=30, capture_factory=cv2.VideoCapture,
            batch_size=8):
    """Skip failed positions with bounded seeks; manifest counts establish completeness."""
    if recovery_limit < 1:
        raise ValueError("Recovery limit must be positive")
    if batch_size < 1:
        raise ValueError("Batch size must be positive")
    totals = {name: 0 for name in CLASSES}
    weights = [0.0] * 4
    processed = expected_total = skipped = 0
    all_expected = True

    def process_batch(batch, path):
        """Infer a batch when supported; retry frames individually if that call fails."""
        nonlocal processed, skipped
        if not batch:
            return

        frames = [frame for _, frame in batch]
        batch_inference = getattr(model, "inference_batch", None)
        inferences = None
        if len(batch) > 1 and callable(batch_inference):
            try:
                inferences = batch_inference(frames)
                if len(inferences) != len(batch):
                    raise RuntimeError("Batch inference returned an unexpected number of results")
            except Exception as exc:
                print("[WARN] Batch inference failed; retrying {} frames individually in {}: {}".format(
                    len(batch), path, exc), file=sys.stderr)

        if inferences is None:
            inferences = []
            for frame_index, frame in batch:
                try:
                    inferences.append(model.inference(frame))
                except Exception as exc:
                    inferences.append(exc)

        for (frame_index, frame), inference in zip(batch, inferences):
            try:
                if isinstance(inference, Exception):
                    raise inference
                _, counts, outputs = model.processAndVisualizeWithMultipleWeight(
                    inference, None, frame, visualize=False)
                values = [float(value) if value is not None else 0.0 for value in outputs]
                if len(values) != 4 or not all(math.isfinite(v) for v in values):
                    raise ValueError("Invalid weight output")
                frame_counts = [int(counts.get(name, 0)) for name in CLASSES]
                for name, value in zip(CLASSES, frame_counts):
                    totals[name] += value
                for number, value in enumerate(values):
                    weights[number] += value
                processed += 1
            except Exception as exc:
                skipped += 1
                print("[WARN] Skipped inference frame {} in {}: {}".format(frame_index, path, exc), file=sys.stderr)

    for path, expected in segments:
        capture = capture_factory(str(path))
        if not capture.isOpened():
            capture.release()
            if expected is None:
                raise RuntimeError("Could not open recording: " + str(path))
            expected_total += expected
            skipped += expected
            continue
        try:
            if expected is None:
                all_expected = False
                reported = capture.get(cv2.CAP_PROP_FRAME_COUNT)
                expected = int(reported) if math.isfinite(reported) and reported > 0 else None
            if expected is not None:
                expected_total += expected
            index = consecutive_failures = 0
            pending = []
            while expected is None or index < expected:
                success, frame = capture.read()
                if not success:
                    if expected is None:
                        process_batch(pending, path)
                        pending = []
                        break  # EOF or damage: standalone status remains Unknown.
                    process_batch(pending, path)
                    pending = []
                    skipped += 1
                    index += 1
                    consecutive_failures += 1
                    if consecutive_failures >= recovery_limit:
                        skipped += expected - index
                        break
                    if not capture.set(cv2.CAP_PROP_POS_FRAMES, index):
                        skipped += expected - index
                        break
                    continue
                consecutive_failures = 0
                index += 1
                try:
                    if frame.shape[1] != width or frame.shape[0] != height:
                        frame = cv2.resize(frame, (width, height), interpolation=cv2.INTER_LINEAR)
                    pending.append((index - 1, frame))
                    if len(pending) >= batch_size:
                        process_batch(pending, path)
                        pending = []
                except Exception as exc:
                    skipped += 1
                    print("[WARN] Skipped inference frame {} in {}: {}".format(index - 1, path, exc), file=sys.stderr)
            process_batch(pending, path)
        finally:
            capture.release()
    if processed == 0:
        raise RuntimeError("No frames could be processed; no finalized aggregate produced")
    return {"frames": processed, "expected": expected_total, "skipped": skipped,
            "counts": totals, "weights": [value / processed for value in weights],
            "known": all_expected}


def result_line(result, dropped, trusted):
    complete = trusted and result["known"] and result["frames"] == result["expected"] and not result["skipped"] and not dropped
    fields = [("frames", result["frames"]), ("expected", result["expected"]),
              ("skipped", result["skipped"]), ("dropped", dropped),
              ("complete", int(complete)), ("known", int(trusted and result["known"]))]
    fields.extend((name, result["counts"][name]) for name in CLASSES)
    fields.extend(("weight{}".format(i + 1), value) for i, value in enumerate(result["weights"]))
    return "VIDEO_RESULT|" + "|".join("{}={}".format(key, value) for key, value in fields)


def run_recording(recording, model, recovery_limit=30, batch_size=8):
    segments, dropped, trusted = load_recording(recording)
    from utils.config import IMAGE_WIDTH, IMAGE_HEIGHT
    result = analyze(segments, model, IMAGE_WIDTH, IMAGE_HEIGHT,
                     recovery_limit, batch_size=batch_size)
    return result_line(result, dropped, trusted)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("recording", help="Finalized session JSON manifest or standalone video")
    parser.add_argument("--recovery-limit", type=int, default=30)
    parser.add_argument("--batch-size", type=int, default=8,
                        help="Frames per YOLO inference batch (default: 8)")
    args = parser.parse_args()
    if args.batch_size < 1:
        parser.error("--batch-size must be positive")
    from module.YOLO.src.YOLOModel import YOLOImpl
    print(run_recording(args.recording, YOLOImpl(), args.recovery_limit, args.batch_size), flush=True)


if __name__ == "__main__":
    main()
