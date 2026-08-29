import zmq
from collections import deque

import sys
from pathlib import Path

sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'proto'
))
import brokerDealer_pb2

class BrokerRouter:
    def markWorkerIdle(self,
                       p_workerId: int):
        if p_workerId not in self.m_idleSet:
            self.m_idleWorker.append(p_workerId)
            self.m_idleSet.add(p_workerId)

    def enqueueImage(self, p_imageLocation: int):
        self.m_imageQueue.append(p_imageLocation)
    
    def workerRouterSend(self,
                               p_workerId: str,
                               p_imageLocation: int,
                               p_commandType: int = brokerDealer_pb2.PROCESS):
        command = brokerDealer_pb2.WorkerCommand(
            command_type=p_commandType,
            image_location=p_imageLocation,
        )
        message = [
            p_workerId.encode(),
            command.SerializeToString(),
        ]
        
        self.m_routerSocket.send_multipart(message)
    
    def workerRouterRecv(self, p_timeout: int = 0):
        if not self.m_poller.poll(p_timeout):
            return None
        
        message = self.m_routerSocket.recv_multipart()
        
        worker_message = brokerDealer_pb2.WorkerMessage()
        worker_message.ParseFromString(message[-1])

        if worker_message.message_type in (
                brokerDealer_pb2.READY,
                brokerDealer_pb2.REQUEST,
                brokerDealer_pb2.RESULT,
                brokerDealer_pb2.RESULT_AND_SAVE):
            self.markWorkerIdle(message[0].decode("utf-8"))
        
        return worker_message

    def distributeTasks(self):
        self.taskDistribute()
    
    def taskDistribute(self):
        while self.m_imageQueue and self.m_idleWorker:
            workerId = self.m_idleWorker.popleft()
            self.m_idleSet.remove(workerId)

            imageLocation = self.m_imageQueue.popleft()
            
            if self.m_currentCount == 8:
                self.m_currentCount = 0
                self.workerRouterSend(
                    p_workerId=workerId,
                    p_commandType=brokerDealer_pb2.PROCESS_AND_SAVE,
                    p_imageLocation=imageLocation
                )
            else:
                self.m_currentCount += 1
                self.workerRouterSend(
                    p_workerId=workerId,
                    p_imageLocation=imageLocation
                )
    
    def brokerRouterConnect(self):
        self.m_context = zmq.Context()
        self.m_routerSocket = self.m_context.socket(zmq.ROUTER)
        
        self.m_poller = zmq.Poller()
        self.m_poller.register(self.m_routerSocket, zmq.POLLIN)
        
        self.m_routerSocket.bind(self.m_routerSocketIp)
    
    
    def __init__(self,
                 p_routerSocketIp: str,
                 ):
        self.m_routerSocketIp = p_routerSocketIp
        
        self.m_imageQueue = deque()
        self.m_idleWorker = deque()
        self.m_idleSet = set()          # Prevent the same worker from being queued twice.
        
        self.m_currentCount = 0         # Each 8 pictures captured, the command is set to PROCESS_AND_SAVE
