from enum import Enum
from typing import Optional

class WorkerCommandType(Enum):
    PROCESS = 1
    PROCESS_AND_SAVE = 2


class WorkerCommand:
    def __init__(self,
                 p_workerCommandType: WorkerCommandType,
                 p_imageLocation: int):
        self.m_workerCommandType = p_workerCommandType
        self.m_imageLocation = p_imageLocation
        

##===##

class WorkerMessageType(Enum):
    READY = 1
    REQUEST = 2
    RESULT = 3
    RESULT_AND_SAVE = 4
        
class WorkerMessage:
    def __init__(self,
                 p_workerId: str,
                 p_workerMessageType: WorkerMessageType,
                 p_imageResultLocation: Optional[int] = None,
                 p_rockPercentages: Optional[list[float]] = None,
                 p_weight: Optional[float] = None,
                 p_imageSaveLocation: Optional[int] = None):
        
        self.m_workerId = p_workerId
        self.m_workerMessageType = p_workerMessageType
        self.m_imageResultLocation = p_imageResultLocation
        self.m_rockPercentages = p_rockPercentages
        self.m_weight = p_weight
        self.m_imageSaveLocation = p_imageSaveLocation