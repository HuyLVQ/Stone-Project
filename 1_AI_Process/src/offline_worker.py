"""Persistent offline inference worker controlled by one recording path per stdin line."""
import sys
import os

import numpy as np

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(ROOT_DIR)
sys.path.append(os.path.dirname(ROOT_DIR))

from ultralytics import YOLO
from Weight_v2.utils import utils as weight_v2
from Weight_v2.session_inference import analyze_recording


def main():
    model = YOLO(str(weight_v2.YOLO_MODEL_PATH))
    # Load both model assets before reporting readiness.
    model(np.zeros((1200, 1920, 3), dtype=np.uint8), verbose=False)
    weight_v2._load_xgboost_model()
    print("OFFLINE_READY", flush=True)

    for line in sys.stdin:
        command = line.rstrip("\r\n")
        if command == "SHUTDOWN":
            break
        if not command.startswith("offline_signal_inference|"):
            continue
        recording = command.split("|", 1)[1]
        if not recording:
            print("OFFLINE_ERROR|Missing finalized recording path.", flush=True)
            continue
        try:
            print(analyze_recording(recording, model), flush=True)
        except Exception as exc:
            print("OFFLINE_ERROR|{}".format(str(exc).replace("\n", " ")), flush=True)


if __name__ == "__main__":
    main()
