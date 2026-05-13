import numpy as np
from concurrent.futures import ThreadPoolExecutor
import struct

import win32event
import win32con
import pywintypes

import mmap

import logging

from utils.config import  MMF_TAGNAME, MMF_TIMEOUT, UI_2_AI_EVENT_TAGNAME, AI_2_UI_EVENT_TAGNAME, \
                          IMAGE_WIDTH, IMAGE_HEIGHT, \
                          WRITE_OFFSET, READ_OFFSET, MEMORY_ALLOCATION

class IPCHelper():
    m_instance = None
    m_hmap = None
    m_UI2AIEvent = None
    m_AI2UIEvent = None
    
    def __init__(p_cls):
        if p_cls.m_instance is None:
            p_cls.m_instance = p_cls
            
            try: 
                p_cls.m_hmap = mmap.mmap(-1, MEMORY_ALLOCATION, tagname=MMF_TAGNAME, access=mmap.ACCESS_WRITE)
                logging.info(f"Open mmf{MMF_TAGNAME} successfully")
                print(f"[INFO]Open mmf{MMF_TAGNAME} successfully", flush=True)
                
                p_cls.m_UI2AIEvent = win32event.OpenEvent(
                                                    win32con.SYNCHRONIZE | win32con.EVENT_MODIFY_STATE,
                                                    True, UI_2_AI_EVENT_TAGNAME)
                
                p_cls.m_AI2UIEvent = win32event.OpenEvent(
                                                    win32con.SYNCHRONIZE | win32con.EVENT_MODIFY_STATE,
                                                    True, AI_2_UI_EVENT_TAGNAME)
                
                logging.info(f"Allocated 2 signal events successfully")
                print(f"[INFO]Allocated 2 signal events successfully", flush=True)
            
            except pywintypes.error as e:
                logging.warning(f"Failed to allocate signal events: {e}")
                print(f"[WARN]Failed to allocate signal events: {e}", flush=True)
            except Exception as e:
                logging.warning(f"Failed to open mmf{MMF_TAGNAME}: {e}")
                print(f"[WARN]Failed to open mmf{MMF_TAGNAME}: {e}", flush=True)
        
    
    def taskWrite(self) -> np.ndarray:
        try:
            win32event.WaitForSingleObject(self.m_UI2AIEvent, MMF_TIMEOUT)
            
            self.m_hmap.seek(WRITE_OFFSET)
            
            rawBytes = self.m_hmap.read(IMAGE_WIDTH * IMAGE_HEIGHT * 3)
            imageConvert = np.frombuffer(rawBytes,
                                         dtype=np.uint8
                                        ).reshape((IMAGE_HEIGHT, IMAGE_WIDTH, 3))
                                                    
            logging.info(f"Retrieve image bytes from {MMF_TAGNAME} successfully")
            print(f"[INFO]Retrieve image bytes from {MMF_TAGNAME} successfully", flush=True)
            return imageConvert
        except Exception as e:
            logging.warning(f"Failed to retrieve image bytes from {MMF_TAGNAME}: {e}")
            print(f"[WARN]Failed to retrieve image bytes from {MMF_TAGNAME}: {e}", flush=True)
            return None
        
        
    def taskRead(self,
                 p_imgBytes: bytes,
                 p_classificationCount: dict,
                 p_measuredWeight: float):
        try:
            if (p_classificationCount is not None):
                measuredMiSang = p_classificationCount.get("MiSang", 0)
                self.m_hmap.seek(READ_OFFSET)
                self.m_hmap.write(struct.pack('f', measuredMiSang))
                
                measured1x2 = p_classificationCount.get("1x2", 0)
                self.m_hmap.seek(READ_OFFSET + 4)
                self.m_hmap.write(struct.pack('f', measured1x2))
            
                measured2x4 = p_classificationCount.get("2x4", 0)
                self.m_hmap.seek(READ_OFFSET + 8)
                self.m_hmap.write(struct.pack('f', measured2x4))
                
                measured4x6 = p_classificationCount.get("4x6", 0)
                self.m_hmap.seek(READ_OFFSET + 12)
                self.m_hmap.write(struct.pack('f', measured4x6))
                print(f"[INFO] MiSang: {measuredMiSang}   ||   1x2: {measured1x2}   ||   2x4: {measured2x4}   ||   4x6: {measured4x6}", flush=True)
            
            if (p_measuredWeight is not None):
                self.m_hmap.seek(READ_OFFSET + 16)
                self.m_hmap.write(struct.pack('f', p_measuredWeight))
                print(f"[INFO] Measured Weight: {p_measuredWeight}", flush=True)
            else:
                self.m_hmap.seek(READ_OFFSET + 16)
                self.m_hmap.write(struct.pack('f', 0))
                print(f"[INFO] Could not measured weight", flush=True)


            if (p_imgBytes is not None):
                self.m_hmap.seek(READ_OFFSET + 100)
                self.m_hmap.write(p_imgBytes)

            
            
            if (p_imgBytes is not None or p_classificationCount is not None or p_measuredWeight is not None):
                logging.info(f"Sent image and parsed information to {MMF_TAGNAME} successfully")
                print(f"[INFO]Sent image and parsed information to {MMF_TAGNAME} successfully", flush=True)
                
                self.m_hmap.flush()
        except Exception as e:
            logging.warning(f"Failed to send image and parsed information to {MMF_TAGNAME}: {e}")
            print(f"[WARN]Failed to send image and parsed information to {MMF_TAGNAME}: {e}", flush=True)

        win32event.SetEvent(self.m_AI2UIEvent)