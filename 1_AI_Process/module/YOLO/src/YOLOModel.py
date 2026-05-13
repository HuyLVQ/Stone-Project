import random
import numpy as np

from ultralytics import YOLO
import cv2
import torch
import torch.nn as nn
from torchvision import models, transforms
from PIL import Image

from utils.config import RATIO_SCALE, TYPE_COLORS, IMAGE_HEIGHT, IMAGE_WIDTH


class YOLOImpl():
    m_model = None
    m_weightsPath = "..\\weight\\best260424.pt"
    m_clsInst = None
    
    m_device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    
    m_RATIO_SCALE = RATIO_SCALE
    m_TYPE_COLORS = TYPE_COLORS
    
    def __init__(self) -> bytes:
        if (self.m_model is None):
            self.loadModel()
        return
    
    def loadModel(self):
        self.m_model = YOLO(self.m_weightsPath)
        self.m_model.to(self.m_device)
        
        print(f"[INFO]Loaded YOLO model from {self.m_weightsPath} successfully", flush=True)
        return
        
    
    def inference(self,
                  p_imageBytes: np.ndarray) -> np.ndarray:
        
        inferenceResult = self.m_model(p_imageBytes,
                                       verbose = False,
                                       device = self.m_device)
        return inferenceResult
    
    

    def processAndVisualize(self,
                            p_inferenceResult: any,
                            p_ratioScale: float,
                            p_imgOriginal: np.ndarray) -> {dict, bytes, dict}: 
        
        if (p_ratioScale is None):
            p_ratioScale = self.m_RATIO_SCALE
        
        imgDraw = p_imgOriginal.copy()
        overlay = imgDraw.copy()    
        
        stats = []
        areaScale = p_ratioScale ** 2
        
        classificationCounts = {key: 0 for key in self.m_TYPE_COLORS.keys()}
        
        for r in p_inferenceResult:
            if r.masks is not None:
                masksXY = r.masks.xy 

                for i, poly in enumerate(masksXY):                  
                    contour = np.array(poly, dtype=np.float32).reshape((-1, 1, 2))
                    
                    areaPixel = cv2.contourArea(contour)
                    rect = cv2.minAreaRect(contour)
                    (wPx, hPx) = rect[1]
                    
                    lengthPixel = max(wPx, hPx)
                    widthPixel = min(wPx, hPx)
                    
                    lengthInCM = round(lengthPixel * p_ratioScale, 2)
                    widthInCM = round(widthPixel * p_ratioScale, 2)  
                    areaInCM2 = round(areaPixel * areaScale, 2)   
                    
                    if widthInCM < 5 and lengthInCM < 10:
                        rockType = "MiSang"
                    elif 10 <= lengthInCM < 20:
                        rockType = "1x2"
                    elif 20 <= lengthInCM < 40:
                        rockType = "2x4"
                    elif 40 <= lengthInCM <= 60:
                        rockType = "4x6"
                    else:
                        rockType = "Khac"
                        
                    classificationCounts[rockType] += 1
                    color = self.m_TYPE_COLORS[rockType]
                    
                    contourInt = np.array(poly, dtype=np.int32)
                    boxOBB = np.int32(cv2.boxPoints(rect))
                    
                    cv2.fillPoly(overlay, [contourInt], color)
                
                    cv2.polylines(imgDraw, [boxOBB], isClosed=True, color=(0, 255, 0), thickness=2)
                    
                    M = cv2.moments(contourInt)
                    if M["m00"] != 0:
                        cX = int(M["m10"] / M["m00"])
                        cY = int(M["m01"] / M["m00"])
                    else:
                        cX, cY = int(poly[0][0]), int(poly[0][1])

                    text = f"ID:{i} - {rockType}"
                    cv2.putText(imgDraw, text, (cX - 20, cY), cv2.FONT_HERSHEY_SIMPLEX, 0.35, (255, 255, 255), 1)
                    
                    stats.append({
                        "object_id": i,
                        "rockType": rockType,
                        "lengthInCM": lengthInCM,
                        "widthInCM": widthInCM,
                        "areaInCM2": areaInCM2
                    })


        alpha = 0.5
        cv2.addWeighted(overlay, alpha, imgDraw, 1 - alpha, 0, imgDraw)
                    
        return stats, imgDraw.tobytes(), classificationCounts
        