using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using EasyModbus;
using Stone_Application.CameraClass;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Basler.Pylon;
using Stone_Application.Event;
using Stone_Application.Repository;
using System.Collections.Concurrent;

public static class Common
    {
        public enum currentState
        {
            UN_INIT = 0,
            READY = 1,
            STREAMING = 2,
        }

        public static currentState s_currentState = currentState.UN_INIT;
        public static readonly object s_lockState = new object();

        public static readonly object s_lockModbus = new object();

        public static Stopwatch s_stopWatchMain = new Stopwatch();


        public static ModbusClient modbusClient { get; set; }
        

        public static BaslerCamera camera { get; set; }


        public static Process pythonProcess { get; set; }
        public static bool isFirstInference { get; set; } = true;
        public static int s_nextImageSlot = -1;
        public static readonly ConcurrentQueue<int> s_availableImageSlots = CreateImageSlots();

        private static ConcurrentQueue<int> CreateImageSlots()
        {
            var slots = new ConcurrentQueue<int>();
            for (int i = 0; i < Config.IMAGE_SLOT_COUNT; i++)
                slots.Enqueue(i * Config.IMAGE_SLOT_STRIDE);
            return slots;
        }


        public static MemoryMappedFile mmf { get; set; }

        public static EventWaitHandle ui2aiEvent { get; set; }
        public static EventWaitHandle ai2uiEvent { get; set; }
        
    
        public static readonly BlockingCollection<Stone_Application.Event.IInformation> s_informationQueue = new BlockingCollection<IInformation>(new ConcurrentQueue<Stone_Application.Event.IInformation>(), Config.BUFFER_BOUND);
        public static readonly BlockingCollection<Stone_Application.Event.IImage> s_imageQueue = new BlockingCollection<Stone_Application.Event.IImage>(new ConcurrentQueue<Stone_Application.Event.IImage>(), Config.BUFFER_BOUND);


    //public static IRepository<IInformation, IResultInformation> s_repositoryInstance = SQLServerRepository<IInformation, IResultInformation>.getIntance();
    //public static IRepository<IInformation, IResultInformation> s_repositoryInstance = NoSQLRepository<IInformation, IResultInformation>.getIntance();
        public static IRepository<IInformation, IResultInformation> s_repositoryInstance;

        private static int s_currentSessionId;
        private static bool s_sessionEntryPending;
        private static readonly object s_lockSession = new object();

        public static void StartPostgreSqlSession()
        {
            PostgreSqlRepository repository = s_repositoryInstance as PostgreSqlRepository;
            if (repository == null)
                return;

            lock (s_lockSession)
            {
                s_currentSessionId = repository.BeginSession();
                s_sessionEntryPending = true;
            }
        }

        public static void PrepareInformationForPersistence(IInformation p_information)
        {
            if (!(s_repositoryInstance is PostgreSqlRepository))
                return;

            lock (s_lockSession)
            {
                p_information.sessionId = s_currentSessionId;
                p_information.isStartOfSession = s_sessionEntryPending;
                s_sessionEntryPending = false;
            }
        }

        public static void ResetRepository()
        {
            s_repositoryInstance.reset();
        }

}
