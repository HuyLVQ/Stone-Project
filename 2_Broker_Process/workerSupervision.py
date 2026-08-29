class WorkerSupervision:
    
    
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
        
    