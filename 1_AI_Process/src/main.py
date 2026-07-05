import logging
import sys
import os

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(ROOT_DIR)

from module.YOLO.src.YOLOModel import YOLOImpl
from module.weightRegression.linearRegression.linearRegression import predictLinear
from module.weightRegression.polynominalRegression.polynominalRegression import predictPoly
from module.weightRegression.randomForest.randomForest import predictRandomForest
from module.weightRegression.XGBoost.XGBoost import predictXGBoost

from utils.IPCHelpers import IPCHelper



def main():
    AIModel = YOLOImpl()
    IPCInst = IPCHelper()

    print("[INFO] Begin interference", flush=True)
    
    while True:        
        retrievedImgBytes = IPCInst.taskWrite()
        if retrievedImgBytes is None:
            print("[WARN]No image bytes retrieved, skipping this cycle", flush=True)
            logging.warning("No image bytes retrieved, skipping this cycle")
            
            IPCInst.taskRead(None, None, None)
            continue
        
        inferenceResult = AIModel.inference(retrievedImgBytes)
        # _, imgResult, counts = AIModel.processAndVisualize(inferenceResult, None, retrievedImgBytes)
        # IPCInst.taskRead(imgResult, counts, None)
        
        imgResult, counts, measuredWeight = AIModel.processAndVisualizeWithWeight(inferenceResult, None, retrievedImgBytes, predictPoly)
        IPCInst.taskRead(imgResult, counts, measuredWeight)
                
        

if __name__ == "__main__":
    main()