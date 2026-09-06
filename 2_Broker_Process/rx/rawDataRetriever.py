import zmq
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parent / '..' / 'proto'))
import brokerRx_pb2

class RxPathPull:    
    def receive(self):
        frames = self.m_rxSocket.recv_multipart()
        if len(frames) != 1:
            raise ValueError(f"Invalid RX message frame count: {len(frames)}")

        message = brokerRx_pb2.RxPullMessage()
        message.ParseFromString(frames[0])
        return message

    @staticmethod
    def image_location(p_message: brokerRx_pb2.RxPullMessage) -> int:
        if not p_message.HasField("data_payload"):
            raise ValueError("RX message does not contain an image location")
        return p_message.data_payload.image_location
    
    def connect(self):
        try:
            self.m_rxContext = zmq.Context()
            self.m_rxSocket = self.m_rxContext.socket(zmq.PULL)
            
            self.m_rxDataPollIn = zmq.Poller()
            self.m_rxDataPollIn.register(self.m_rxSocket, zmq.POLLIN)
        
            self.m_rxSocket.connect(self.m_rxSocketIp)
            self.socket = self.m_rxSocket

        except zmq.Again as e:
            print(f"The operation have an exception:{e}")
        except ValueError as e:
            print(f"Sending have an exception:{e}")
    
    
    def __init__(self,
                 p_rxSocketIp: str):
        self.m_rxSocketIp = p_rxSocketIp
