import zmq
import uuid
import sys
from pathlib import Path

sys.path.append(str(Path(__file__).resolve().parent / '..' / 'proto'))
import brokerDealer_pb2

class Worker:
    def dealerRecv(self):
        frames = self.m_rxSocket.recv_multipart()
        if len(frames) != 1:
            raise ValueError(f"Invalid worker command frame count: {len(frames)}")

        command = brokerDealer_pb2.WorkerCommand()
        command.ParseFromString(frames[0])
        
        if command.command_type == brokerDealer_pb2.PROCESS:
            # Implement normal processing
            print("Implement normal processing")
        elif command.command_type == brokerDealer_pb2.PROCESS_AND_SAVE:
            # Implement processing and save
            print("Implement processing and save")
        else:
            raise ValueError(f"Invalid command type:{command.command_type}")
        

    
    def dealerSend(self, 
                   p_message: brokerDealer_pb2.WorkerMessage):
        self.m_rxSocket.send(p_message.SerializeToString())

        
    
    def dealerConnect(self):
        try:
            self.m_rxContext = zmq.Context()
            self.m_rxSocket = self.m_rxContext.socket(zmq.DEALER)
            self.m_rxSocket.setsockopt(zmq.IDENTITY, self.m_workerId.encode())
            
            self.m_rxDataPollIn = zmq.Poller()
            self.m_rxDataPollIn.register(self.m_rxSocket, zmq.POLLIN)
        
            self.m_rxSocket.connect(self.m_rxSocketIp)

            self.dealerSend(
                brokerDealer_pb2.WorkerMessage(
                    worker_id=self.m_workerId,
                    message_type=brokerDealer_pb2.READY,
                )
            )
            
        except zmq.Again as e:
            print(f"The operation have an exception:{e}")
        except ValueError as e:
            print(f"Sending have an exception:{e}")
    
    
    def run(self):
        print("Run")
    
    def __init__(self,
                 p_workerId: int,
                 p_rxSocketIp: str):
        self.m_workerId = f"worker-{p_workerId}-{uuid.uuid4()}"
        self.m_rxSocketIp = p_rxSocketIp
