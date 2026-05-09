using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using EasyModbus;
using Stone_Application;
using Stone_Application.Event;
using Stone_Application.IPC;
using Stone_Application.Observer;

namespace Stone_Application.Infrastructure
{
    public static class MultiThread
    {
        public static void thread1Work()
        { 
            Task.Run(() =>
            {
                int[] temporaryStorage = new int[2];
                int encoderFeedback = 0;
                while (true)
                {
                    if (Common.stopWatchMain.ElapsedMilliseconds >= Config.TIME_INTERVAL)
                    {
                        Common.stopWatchMain.Restart();

                        Console.WriteLine("[INFO] [THREAD #1] New Thread 1 cycle...");

                        lock (Common.lockModbus)
                        {
                            temporaryStorage = Common.modbusClient.ReadHoldingRegisters(Config.ENC_ADDR, 2);
                            encoderFeedback = ((temporaryStorage[0] & 0xFFFF) | (temporaryStorage[1] << 16));
                            Console.WriteLine("[INFO] [THREAD #1] [MODBUS] Modbus read...");
                            System.Threading.Monitor.PulseAll(Common.lockModbus);
                        }

                        Console.WriteLine("[INFO] [THREAD #1] Camera capture...");
                        Common.camera.cameraCapture();
                    }
                }
            });
        }

        public static void thread2Work()
        {
            IPCServices ipcServices = IPCServices.getInstance();
            AIProcessEvent aiProcessEvent = AIProcessEvent.getInstance(ipcServices);

            IEventObserver<IInformation> dbObserver = new DBObserver<IInformation>(Common.repositoryInstance);
            IEventObserver<Event.IImage> uiImageObserver = new UIImageObserver<Event.IImage>();
            IEventObserver<IInformation> uiInformationObserver = new UIInformationObserver<IInformation>();

            aiProcessEvent.attachImageObserver(uiImageObserver);
            aiProcessEvent.attachInformationObserver(uiInformationObserver);
            aiProcessEvent.attachInformationObserver(dbObserver);

            Task.Run(() => 
            { 
                while(true)
                {
                    Console.WriteLine("[INFO] [THREAD #2] New Thread 2 cycle...");


                    Event.IImage temporaryImage = Common.imageQueue.Take();
                    Console.WriteLine("[INFO] [THREAD #2] Image taken from queue...");
                    ipcServices.writeTask(temporaryImage);

                    Common.ui2aiEvent.Set();
                    Console.WriteLine("[INFO] [THREAD #2] UI to AI event set...");

                    Common.ai2uiEvent.WaitOne();
                    Console.WriteLine("[INFO] [THREAD #2] AI to UI event received...");

                    IInformation temporaryInformation;
                    (temporaryInformation, temporaryImage) = ipcServices.readTask();

                    Console.WriteLine("[INFO] [THREAD #2] AI results read from shared memory...");
                    aiProcessEvent.notifyImage(temporaryImage);
                    aiProcessEvent.notifyInformation(temporaryInformation);
                }
            });
        }
    }
}
