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


        public static MemoryMappedFile mmf { get; set; }

        public static EventWaitHandle ui2aiEvent { get; set; }
        public static EventWaitHandle ai2uiEvent { get; set; }
        
    
        public static BlockingCollection<Stone_Application.Event.IInformation> s_informationQueue = new BlockingCollection<IInformation>(new ConcurrentQueue<Stone_Application.Event.IInformation>(), Config.BUFFER_BOUND);
        public static BlockingCollection<Stone_Application.Event.IImage> s_imageQueue = new BlockingCollection<Stone_Application.Event.IImage>(new ConcurrentQueue<Stone_Application.Event.IImage>(), Config.BUFFER_BOUND);


        //public static IRepository<IInformation> s_repositoryInstance = SQLServerRepository<IInformation>.getIntance();
        public static IRepository<IInformation> s_repositoryInstance = NoSQLRepository<IInformation>.getIntance();

}
