import logging
import sys
import os

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(ROOT_DIR)

from module.YOLO.src.YOLOModel import YOLOImpl
from utils.IPCHelpers import IPCHelper
from utils.config import AI_WARMUP_REQUESTS



def main():
    AIModel = YOLOImpl()
    IPCInst = IPCHelper()
    warmup_remaining = AI_WARMUP_REQUESTS

    print("[INFO] Begin interference", flush=True)
    
    while True:        
        retrievedImgBytes = IPCInst.taskWrite()
        if retrievedImgBytes is None:
            print("[WARN]No image bytes retrieved, skipping this cycle", flush=True)
            logging.warning("No image bytes retrieved, skipping this cycle")
            
            IPCInst.taskRead(None, None, None)
            if warmup_remaining > 0:
                warmup_remaining -= 1
            continue
        
        inferenceResult = AIModel.inference(retrievedImgBytes)
        if warmup_remaining > 0:
            # Warm-up requests only prime YOLO inference. They do not need
            # visualization, contour classification, or weight regressors.
            IPCInst.taskRead(None, None, None)
            warmup_remaining -= 1
            continue
        # _, imgResult, counts = AIModel.processAndVisualize(inferenceResult, None, retrievedImgBytes)
        # IPCInst.taskRead(imgResult, counts, None)
        
        # imgResult, counts, measuredWeight = AIModel.processAndVisualizeWithWeight(inferenceResult, None, retrievedImgBytes, predictXGBoost)
        # IPCInst.taskRead(imgResult, counts, measuredWeight)
        
        imgResult, counts, measuredWeights = AIModel.processAndVisualizeWithMultipleWeight(inferenceResult, None, retrievedImgBytes)
        IPCInst.taskReadMultiple(imgResult, counts, measuredWeights)
                
        

if __name__ == "__main__":
    main()
