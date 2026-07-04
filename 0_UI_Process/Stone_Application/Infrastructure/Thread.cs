using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        private static CancellationTokenSource s_cancellationSource;
        private static Task s_thread1Task;
        private static Task s_thread2Task;
        private static readonly object s_startStopLock = new object();

        public static void thread1Work()
        {
            lock (s_startStopLock)
            {
                if (s_cancellationSource == null)
                    s_cancellationSource = new CancellationTokenSource();

                if (s_thread1Task != null && !s_thread1Task.IsCompleted)
                    return; // already running

                CancellationToken token = s_cancellationSource.Token;

                s_thread1Task = Task.Run(() =>
                {
                    int[] temporaryStorage = new int[2];
                    int encoderFeedback = 0;

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            lock (Common.s_lockState)
                            {
                                if (Common.s_currentState != Common.currentState.STREAMING)
                                {
                                    Console.WriteLine("[INFO] [THREAD #1] Not in streamin mode, go to sleep...");
                                    Thread.Sleep(100);
                                    continue;
                                }
                            }

                            if (Common.s_stopWatchMain.ElapsedMilliseconds >= Config.TIME_INTERVAL)
                            {
                                Common.s_stopWatchMain.Restart();

                                Console.WriteLine("[INFO] [THREAD #1] New Thread 1 cycle...");

                                //lock (Common.s_lockModbus)
                                //{
                                //    temporaryStorage = Common.modbusClient.ReadHoldingRegisters(Config.ENC_ADDR, 2);
                                //    encoderFeedback = ((temporaryStorage[0] & 0xFFFF) | (temporaryStorage[1] << 16));
                                //    Console.WriteLine("[INFO] [THREAD #1] [MODBUS] Modbus read...");
                                //    System.Threading.Monitor.PulseAll(Common.s_lockModbus);
                                //}

                                Console.WriteLine("[INFO] [THREAD #1] Camera capture...");
                                Common.camera.cameraCapture();
                            }
                            // small delay to avoid busy spin if interval is very small
                            Thread.Sleep(1);
                            Console.WriteLine("[INFO] [THREAD #1] Exiting thread.");
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] [THREAD #1] Exception: {ex.Message}");
                        }
                    }

                }, token);
            }
        }

        public static void thread2Work()
        {
            lock (s_startStopLock)
            {
                if (s_cancellationSource == null)
                    s_cancellationSource = new CancellationTokenSource();

                if (s_thread2Task != null && !s_thread2Task.IsCompleted)
                    return; // already running

                IPCServices ipcServices = IPCServices.getInstance();
                AIProcessEvent aiProcessEvent = AIProcessEvent.getInstance(ipcServices);

                IEventObserver<IInformation> dbObserver = new DBObserver<IInformation>(Common.s_repositoryInstance);
                IEventObserver<Event.IImage> uiImageObserver = new UIImageObserver<Event.IImage>();
                IEventObserver<IInformation> uiInformationObserver = new UIInformationObserver<IInformation>();

                aiProcessEvent.attachImageObserver(uiImageObserver);
                aiProcessEvent.attachInformationObserver(uiInformationObserver);
                aiProcessEvent.attachInformationObserver(dbObserver);

                CancellationToken token = s_cancellationSource.Token;

                s_thread2Task = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            Console.WriteLine("[INFO] [THREAD #2] New Thread 2 cycle...");

                            Event.IImage temporaryImage;
                            try
                            {
                                // BlockingCollection.Take supports cancellation token
                                temporaryImage = Common.s_imageQueue.Take(token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }

                            Console.WriteLine("[INFO] [THREAD #2] Image taken from queue...");
                            try
                            {
                                ipcServices.writeTask(temporaryImage);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[ERROR] [THREAD #2] writeTask failed: " + ex.Message);
                                continue;
                            }

                            Common.ui2aiEvent.Set();
                            Console.WriteLine("[INFO] [THREAD #2] UI to AI event set...");

                            // Wait for AI to respond, but allow periodic cancellation checks.
                            while (!token.IsCancellationRequested)
                            {
                                // WaitOne with timeout to be able to check cancellation token regularly.
                                if (Common.ai2uiEvent.WaitOne(200))
                                    break;
                            }

                            if (token.IsCancellationRequested)
                                break;

                            Console.WriteLine("[INFO] [THREAD #2] AI to UI event received...");

                            IInformation temporaryInformation;
                            try
                            {
                                (temporaryInformation, temporaryImage) = ipcServices.readTask();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("[ERROR] [THREAD #2] readTask failed: " + ex.Message);
                                continue;
                            }

                            Console.WriteLine("[INFO] [THREAD #2] AI results read from shared memory...");
                            aiProcessEvent.notifyImage(temporaryImage);
                            aiProcessEvent.notifyInformation(temporaryInformation);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // swallow, shutting down
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] [THREAD #2] Unexpected exception: " + ex.Message);
                    }

                    Console.WriteLine("[INFO] [THREAD #2] Exiting thread.");
                }, token);
            }
        }

        public static void StopAll()
        {
            lock (s_startStopLock)
            {
                if (s_cancellationSource == null)
                    return;

                Console.WriteLine("[INFO] [THREAD] Stopping threads...");

                try
                {
                    s_cancellationSource.Cancel();

                    Task[] tasksToWait = Array.FindAll(new[] { s_thread1Task, s_thread2Task }, p_t => p_t != null);
                    if (tasksToWait.Length > 0)
                    {
                        try
                        {
                            Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(5));
                        }
                        catch (AggregateException ae)
                        {
                            foreach (var e in ae.InnerExceptions)
                                Console.WriteLine("[WARN] [THREAD] Task exception while stopping: " + e.Message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[WARN] [THREAD] WaitAll exception: " + ex.Message);
                        }
                    }
                }
                finally
                {
                    s_cancellationSource.Dispose();
                    s_cancellationSource = null;
                    s_thread1Task = null;
                    s_thread2Task = null;
                }

                Console.WriteLine("[INFO] [THREAD] Threads stopped.");
            }
        }
    }
}
