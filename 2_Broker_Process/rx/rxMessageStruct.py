from enum import Enum
from typing import Optional

class RxPullMessageType(Enum):
    STARTUP = 1
    RAWIMAGE = 2

class RxFrame:
    def __init__(self,
                 p_imageLocation: int):
        self.p_imageLocation = p_imageLocation

class RxPullMessage:
    def __init__(self,
                 p_messageType: RxPullMessageType,
                 p_messageLog: Optional[str] = None,
                 p_dataPayload: Optional[RxFrame] = None):
        
        self.m_messageType = p_messageType
        self.m_messageLog = p_messageLog
        self.m_dataPayload = p_dataPayload