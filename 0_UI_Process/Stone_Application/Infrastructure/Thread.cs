using System;
using System.Threading;
using System.Threading.Tasks;
using Stone_Application.Event;
using Stone_Application.IPC;
using Stone_Application.Observer;
using Stone.Broker;

namespace Stone_Application.Infrastructure
{
    public static class MultiThread
    {
        private static CancellationTokenSource s_cancellationSource;
        private static Task s_captureTask;
        private static Task s_pipelineTask;
        private static readonly object s_startStopLock = new object();

        public static void thread1Work()
        {
            lock (s_startStopLock)
            {
                if (s_cancellationSource == null)
                    s_cancellationSource = new CancellationTokenSource();
                if (s_captureTask != null && !s_captureTask.IsCompleted)
                    return;

                CancellationToken token = s_cancellationSource.Token;
                s_captureTask = Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            lock (Common.s_lockState)
                            {
                                if (Common.s_currentState != Common.currentState.STREAMING)
                                {
                                    Thread.Sleep(100);
                                    continue;
                                }
                            }
                            if (Common.s_stopWatchMain.ElapsedMilliseconds >= Config.TIME_INTERVAL)
                            {
                                Common.s_stopWatchMain.Restart();
                                Common.camera.cameraCapture(token);
                            }
                            Thread.Sleep(1);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { Console.WriteLine("[ERROR] [THREAD #1] " + ex.Message); }
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
                if (s_pipelineTask != null && !s_pipelineTask.IsCompleted)
                    return;

                IPCServices ipc = IPCServices.getInstance();
                var aiEvent = Event.AIProcessEvent.getInstance(ipc);
                var dbObserver = new DBObserver<IInformation>(Common.s_repositoryInstance);
                var imageObserver = new UIImageObserver<IImage>();
                var informationObserver = new UIInformationObserver<IInformation>();
                aiEvent.attachInformationObserver(dbObserver);
                aiEvent.attachImageObserver(imageObserver);
                aiEvent.attachInformationObserver(informationObserver);

                CancellationToken token = s_cancellationSource.Token;
                s_pipelineTask = Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            while (ipc.TryReceiveResult(out var result))
                            {
                                var info = new IInformation();
                                if (result.RockPercentages.Count > 0) info.countMiSang = (long)result.RockPercentages[0];
                                if (result.RockPercentages.Count > 1) info.count1x2 = (long)result.RockPercentages[1];
                                if (result.RockPercentages.Count > 2) info.count2x4 = (long)result.RockPercentages[2];
                                if (result.RockPercentages.Count > 3) info.count4x6 = (long)result.RockPercentages[3];
                                if (result.Weight.Count > 0) info.measuredWeight1 = result.Weight[0];
                                if (result.Weight.Count > 1) info.measuredWeight2 = result.Weight[1];
                                if (result.Weight.Count > 2) info.measuredWeight3 = result.Weight[2];
                                if (result.Weight.Count > 3) info.measuredWeight4 = result.Weight[3];

                                if (result.MessageType == WorkerMessageType.ResultAndSave && result.HasImageSaveLocation)
                                    aiEvent.notifyImage(ipc.ReadOutputImage(result.ImageSaveLocation));
                                aiEvent.notifyInformation(info);
                                ipc.ReleaseImageSlot(result.ImageLocation);
                            }

                            IImage image;
                            if (Common.s_imageQueue.TryTake(out image, 10, token))
                                ipc.TryWriteAndSend(image, out _); // false means the bounded pipeline drops this frame.
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { Console.WriteLine("[ERROR] [THREAD #2] " + ex.Message); }
                    }
                }, token);
            }
        }

        public static void StopAll()
        {
            lock (s_startStopLock)
            {
                if (s_cancellationSource == null) return;
                s_cancellationSource.Cancel();
                try
                {
                    Task[] tasks = new[] { s_captureTask, s_pipelineTask };
                    Task.WaitAll(Array.FindAll(tasks, task => task != null), TimeSpan.FromSeconds(5));
                }
                catch (Exception ex) { Console.WriteLine("[WARN] [THREAD] Stop failed: " + ex.Message); }
                finally
                {
                    s_cancellationSource.Dispose();
                    s_cancellationSource = null;
                    s_captureTask = null;
                    s_pipelineTask = null;
                }
            }
        }
    }
}
