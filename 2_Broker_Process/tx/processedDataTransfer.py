import zmq
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parent / '..' / 'proto'))
import brokerTx_pb2

class TxPathPush:
    def send(self, p_message: brokerTx_pb2.TxPushMessage):
        self.m_txSocket.send(p_message.SerializeToString())
    
    def connect(self):
        try:
            self.m_txContext = zmq.Context()
            self.m_txSocket = self.m_txContext.socket(zmq.PUSH)
        
            self.m_txSocket.bind(self.m_txSocketIp)
            self.socket = self.m_txSocket

        except zmq.Again as e:
            print(f"The operation have an exception:{e}")
        except ValueError as e:
            print(f"Sending have an exception:{e}")
            

    def __init__(self,
                 p_txSocketIp: str):
        self.m_txSocketIp = p_txSocketIp
