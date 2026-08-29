import zmq
import struct
import uuid

from workerMessageStruct import WorkerCommand, \
                                WorkerCommandType, \
                                WorkerMessage, \
                                WorkerMessageType

class Worker:
    def dealerRecv(self):
        commandType, imageLocation = self.m_rxSocket.recv_multipart()
        
        commandType = struct.unpack("!I", commandType)[0]
        imageLocation = struct.unpack("!I", imageLocation)[0]
        
        if (commandType == WorkerCommandType.PROCESS.value):
            # Implement normal processing
            print("Implement normal processing")
        elif (commandType == WorkerCommandType.PROCESS_AND_SAVE.value):
            # Implement processing and save
            print("Implement processing and save")
        else:
            raise ValueError(f"Invalid command type:{commandType}")
        

    
    def dealerSend(self, 
                   p_message: WorkerMessage):
        
        message = [
            struct.pack("!I", p_message.m_workerId),
            struct.pack("!I", p_message.m_workerMessageType.value),
        ]
        
        if (p_message.m_imageResultLocation is not None):
            message.append(struct.pack("!I", p_message.m_imageResultLocation))
            
        if (p_message.m_rockPercentages is not None):
            for rockPercentage in p_message.m_rockPercentages:
                message.append(struct.pack("<q", rockPercentage))
        
        if (p_message.m_weight is not None):
            message.append(struct.pack("<q", p_message.m_weight))
        
        if (p_message.m_imageSaveLocation is not None):
            message.append(struct.pack("!I", p_message.m_imageSaveLocation))
            
        self.m_rxSocket.send_multipart(message)

        
    
    def dealerConnect(self):
        try:
            self.m_rxContext = zmq.Context()
            self.m_rxSocket = self.m_rxContext.socket(zmq.DEALER)
            self.m_rxSocket.setsockopt(zmq.IDENTITY, self.m_workerId.encode())
            
            self.m_rxDataPollIn = zmq.Poller()
            self.m_rxDataPollIn.register(self.m_rxSocket, zmq.POLLIN)
        
            self.m_rxSocket.connect(self.m_rxSocketIp)

            self.dealerSend(
                WorkerMessage(
                    self.m_workerId,
                    WorkerMessageType.READY,
                    None,
                    None,
                    None,
                    None
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