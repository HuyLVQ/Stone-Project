import sys
from pathlib import Path
import threading

sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'rx' / 'processedDataTransfer'
))
sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'tx' / 'rawDataRetriever'
))
sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'router' / 'workerRouter'
))
sys.path.append(str(
    Path(__file__).resolve().parent / '..' / 'worker' / 'worker'
))
from tx.processedDataTransfer import TxPathPush
from rx.rawDataRetriever import RxPathPull
from router.brokerRouter import BrokerRouter
from worker.worker import Worker

class Broker: 
    def runWorker(self):
        for workerIndex in range(self.m_workerCount):
            worker = Worker(
                workerIndex,
                self.m_brokerWorkerRouterSocket
            )        
            
            worker.dealerConnect()
    
    def __init__(self,
                p_uiBrokerPullSocket: str,
                p_brokerUiPushSocket: str,
                p_brokerWorkerRouterSocket: str,
                p_workerBrokerDealerSocket: str,
                p_workerCount: int = 5,
                ):

        self.m_uiBrokerPullSocket = p_uiBrokerPullSocket
        self.m_brokerUiPushSocket = p_brokerUiPushSocket

        self.m_brokerWorkerRouterSocket = p_brokerWorkerRouterSocket
        self.m_workerBrokerDealerSocket = p_workerBrokerDealerSocket
        
        self.m_workerCount = p_workerCount
        
        self.m_routerInst = BrokerRouter("")
        self.m_routerInst.brokerRouterConnect()
        self.runWorker()
        
        self.m_txInst = TxPathPush("")
        
        self.m_rxInst = RxPathPull("")
        self.m_rxInst.rxConnecting()
        