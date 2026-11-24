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
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            bool streaming;
                            lock (Common.s_lockState) streaming = Common.s_currentState == Common.currentState.STREAMING;
                            if (!streaming) { if (token.WaitHandle.WaitOne(100)) break; continue; }

                            // Capture is deliberately independent of inference cadence.
                            Common.camera.cameraCapture(token);
                            //Console.WriteLine("[INFO] [THREAD #1] Exiting thread.");
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

                aiProcessEvent.attachInformationObserver(dbObserver);
                aiProcessEvent.attachImageObserver(uiImageObserver);
                aiProcessEvent.attachInformationObserver(uiInformationObserver);
                aiProcessEvent.attachImageObserver(IotImageObserver<Event.IImage>.Instance);

                CancellationToken token = s_cancellationSource.Token;

                s_thread2Task = Task.Run(() =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {   
                            if (Config.s_isDebugMode == true) {
                                Console.WriteLine("[INFO] [THREAD #2] New Thread 2 cycle...");
                            }

                            bool streaming;
                            lock (Common.s_lockState) streaming = Common.s_currentState == Common.currentState.STREAMING;
                            if (!streaming || !ipcServices.isSessionActive())
                            { if (token.WaitHandle.WaitOne(100)) break; continue; }

                            // Coalesce missed intervals: one in-flight AI request is supported by
                            // the existing event protocol, and each due request uses the newest frame.
                            long delay = Config.TIME_INTERVAL - Common.s_stopWatchMain.ElapsedMilliseconds;
                            if (delay > 0)
                            {
                                if (token.WaitHandle.WaitOne((int)delay)) break;
                            }
                            Common.s_stopWatchMain.Restart();

                            int inferenceSlot;
                            long sessionToken;
                            if (!ipcServices.TryDispatch(out inferenceSlot, out sessionToken)) continue;
                            while (!token.IsCancellationRequested && ipcServices.HasPendingRequest(sessionToken))
                            {
                                if (token.WaitHandle.WaitOne(50)) break;
                                IInformation information;
                                Event.IImage image;
                                try
                                {
                                    if (ipcServices.CompleteInference(inferenceSlot, sessionToken, out information, out image))
                                    {
                                        // The inference slot is already released; UI delivery is asynchronous.
                                        ipcServices.PublishIfCurrent(sessionToken, () =>
                                        {
                                            aiProcessEvent.notifyImage(image);
                                            aiProcessEvent.notifyInformation(information);
                                        });
                                    }
                                }
                                catch (Exception ex) { Console.WriteLine("[ERROR] [RESULT] " + ex.Message); }
                                if (!ipcServices.HasPendingRequest(sessionToken)) break;
                            }
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

                    if (Config.s_isDebugMode == true) {
                        Console.WriteLine("[INFO] [THREAD #2] Exiting thread.");
                    }
                    
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
