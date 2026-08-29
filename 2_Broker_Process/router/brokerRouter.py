import zmq
import struct
import asyncio
from collections import deque

import sys
from pathlib import Path

sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'worker' / 'workerMessageStruct'
))
from worker.workerMessageStruct import WorkerCommandType, \
                                       WorkerCommand, \
                                       WorkerMessageType

class BrokerRouter:
    def markWorkerIdle(self,
                       p_workerId: int):
        if p_workerId not in self.m_idleSet:
            self.m_idleWorker.append(p_workerId)
            self.m_idleSet.add(p_workerId)
    
    async def workerRouterSend(self, 
                               p_workerId: str,
                               p_imageLocation: int,
                               p_commandType: WorkerCommandType = WorkerCommandType.PROCESS):
        
        message = [
            p_workerId.encode(),
            struct.pack("!I", p_commandType.value)[0],
            struct.pack("!I", p_imageLocation)[0]
        ]
        
        await self.m_routerSocket.send_multipart(message)
    
    async def workerRouterRecv(self):
        self.m_poller.poll()
        
        message = await self.m_routerSocket.recv_multipart()
        
        if (struct.unpack("!I", message[1])[0] == WorkerMessageType.READY.value):
            self.m_idleWorker.append(message[0].decode("utf-8"))
            self.m_idleSet.add(message[0].decode("utf-8"))
        
        return message
    
    def taskDistribute(self):
        while self.m_imageQueue and self.m_idleWorker:
            workerId = self.m_idleWorker.popleft()
            self.m_idleSet.remove(workerId)

            imageLocation = self.m_imageQueue.popleft()
            
            if self.m_currentCount == 8:
                self.m_currentCount = 0
                self.workerRouterSend(
                    p_workerId=workerId,
                    p_commandType=WorkerCommandType.PROCESS_AND_SAVE,
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