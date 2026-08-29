import zmq
import struct

class TxPathPush:
    def txPushSendMsg(self,
                      p_message):
        
        self.m_txSocket.send_multipart(p_message)
    
    def txConnecting(self):
        try:
            self.m_txContext = zmq.Context()
            self.m_txSocket = self.m_txContext.socket(zmq.PUSH)
        
            self.m_txSocket.bind(self.m_txSocketIp)

        except zmq.Again as e:
            print(f"The operation have an exception:{e}")
        except ValueError as e:
            print(f"Sending have an exception:{e}")
            

    def __init__(self,
                 p_txSocketIp: str):
        self.m_txSocketIp = p_txSocketIp