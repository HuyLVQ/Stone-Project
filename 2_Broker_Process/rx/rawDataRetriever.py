import zmq
import struct
import os

from rxMessageStruct import RxPullMessageType, \
                            RxFrame, \
                            RxPullMessage

class RxPathPull:    
    def rxPullRecvMsg(self):
        frames = self.m_rxSocket.recv_multipart()
        
        if (len(frames) == 3):
            messageType, messageLog, messagePayload = frames
        else:
            raise ValueError(f"Invalid frame count:{len(frames)}")
        
        # Process message type
        # Process message log
        
        return struct.unpack("<q", messagePayload)
    
    def rxConnecting(self):
        try:
            self.m_rxContext = zmq.Context()
            self.m_rxSocket = self.m_rxContext.socket(zmq.PULL)
            
            self.m_rxDataPollIn = zmq.Poller()
            self.m_rxDataPollIn.register(self.m_rxSocket, zmq.POLLIN)
        
            self.m_rxSocket.connect(self.m_rxSocketIp)

        except zmq.Again as e:
            print(f"The operation have an exception:{e}")
        except ValueError as e:
            print(f"Sending have an exception:{e}")
    
    
    def __init__(self,
                 p_rxSocketIp: str):
        self.m_rxSocketIp = p_rxSocketIp