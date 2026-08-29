import sys
from pathlib import Path
import threading
import zmq

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
sys.path.append(str(
    Path(__file__).resolve().parent / 'proto'
))
from tx.processedDataTransfer import TxPathPush
from rx.rawDataRetriever import RxPathPull
from router.brokerRouter import BrokerRouter
from worker.worker import Worker
import brokerDealer_pb2
import brokerTx_pb2

class Broker: 
    def runWorker(self):
        self.m_workers = []
        for workerIndex in range(self.m_workerCount):
            worker = Worker(
                workerIndex,
                self.m_brokerWorkerRouterSocket
            )        
            
            worker.dealerConnect()
            self.m_workers.append(worker)
            threading.Thread(target=worker.run, daemon=True).start()

    def run(self, p_pollTimeout: int = 100):
        """Run the RX -> worker -> TX broker pipeline."""
        poller = zmq.Poller()
        poller.register(self.m_rxInst.m_rxSocket, zmq.POLLIN)
        poller.register(self.m_routerInst.m_routerSocket, zmq.POLLIN)

        while True:
            events = dict(poller.poll(p_pollTimeout))

            if self.m_rxInst.m_rxSocket in events:
                rx_message = self.m_rxInst.rxPullRecvMsg()
                self.m_routerInst.enqueueImage(
                    self.m_rxInst.imageLocation(rx_message)
                )

            if self.m_routerInst.m_routerSocket in events:
                worker_message = self.m_routerInst.workerRouterRecv()
                if worker_message.message_type in (
                        brokerDealer_pb2.RESULT,
                        brokerDealer_pb2.RESULT_AND_SAVE):
                    self.m_txInst.txPushSendMsg(
                        brokerTx_pb2.TxPushMessage(
                            message_type=worker_message.message_type,
                            image_result_location=worker_message.image_result_location,
                            rock_percentages=worker_message.rock_percentages,
                            weight=worker_message.weight,
                            image_save_location=worker_message.image_save_location,
                        )
                    )

            self.m_routerInst.distributeTasks()
    
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
        
        self.m_routerInst = BrokerRouter(p_brokerWorkerRouterSocket)
        self.m_routerInst.brokerRouterConnect()
        self.runWorker()
        
        self.m_txInst = TxPathPush(p_brokerUiPushSocket)
        self.m_txInst.txConnecting()

        self.m_rxInst = RxPathPull(p_uiBrokerPullSocket)
        self.m_rxInst.rxConnecting()
