using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Basler.Pylon;
using Stone_Application.IPC;

namespace Stone_Application.CameraClass
{
    public class BaslerCamera
    {
        // Singleton instance - prevent multiple instance.
        private static BaslerCamera s_instance;
        private Camera m_camera;
        private long m_lastCaptureToken;

        // Lock to guard the access to camera resources
        private readonly object m_captureLock = new object();
        private int m_connectionLost;
        private int m_closing;
        public static event Action<BaslerCamera> ConnectionLost;

        public bool IsConnected
        {
            get
            {
                var camera = m_camera;
                return Volatile.Read(ref m_connectionLost) == 0 &&
                    Volatile.Read(ref m_closing) == 0 && camera != null && camera.IsConnected;
            }
        }

        // @brief: Handle the Pylon connection-lost notification
        // @param:
        //      * sender: The object that raised the notification
        //      * e: Event data supplied by Pylon
        private void OnConnectionLost(object sender, EventArgs e)
        {
            ReportConnectionLost();
        }

        // @brief: Notify subscribers once when the camera connection is lost
        private void ReportConnectionLost()
        {
            // Pylon can call this inside its camera lock. Never wait for the UI,
            // dispose the camera, or acquire application locks in this callback.
            if (Volatile.Read(ref m_closing) == 0 && Interlocked.Exchange(ref m_connectionLost, 1) == 0)
                ConnectionLost?.Invoke(this);
        }
        // @brief: Create a camera wrapper without opening the physical device
        private BaslerCamera() { ; }

        // @brief: Get the connected singleton camera, opening and configuring it when needed
        // @return: The initialized BaslerCamera instance
        public static BaslerCamera getInstance()
        {
            if (s_instance != null && s_instance.IsConnected)
                return s_instance;

            lock (typeof(BaslerCamera))
            {
                if (s_instance != null && s_instance.IsConnected)
                    return s_instance;

                s_instance?.cameraClose();
                var instance = new BaslerCamera();
                try
                {
                    instance.m_camera = new Camera();
                    instance.m_camera.ConnectionLost += instance.OnConnectionLost;
                    instance.m_camera.Open();
                    instance.m_camera.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BGR8);
                    instance.m_camera.Parameters[PLCamera.ExposureTime].SetValue(Config.EXPOSURE_TIME);

                    instance.m_camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    instance.m_camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
                    instance.m_camera.Parameters[PLCamera.AcquisitionStart].Execute();
                    if (!instance.IsConnected)
                        throw new InvalidOperationException("Camera connection was lost during initialization.");

                    s_instance = instance;

                    if (Config.s_isDebugMode) {
                        Console.WriteLine($"[INFO] [CAMERA] Camera initialized successfully");
                    }
                    return s_instance;
                }
                catch (Exception ex)
                {
                    instance.cameraClose();
                    s_instance = null;
                    throw new InvalidOperationException("Could not initialize the Basler camera. Check that the camera is connected, powered, and available to the Pylon driver.", ex);
                }
            }
        }

        // @brief: Read the camera's resulting acquisition rate for recording playback
        // @return: The readable camera frame rate, or the configured fallback rate
        public double GetRecordingPlaybackFrameRate()
        {
            lock (m_captureLock)
            {
                if (!IsConnected) return Config.VIDEO_FRAME_RATE;
                foreach (var name in new[] { PLCamera.BslResultingAcquisitionFrameRate,
                    PLCamera.ResultingFrameRate, PLCamera.ResultingFrameRateAbs })
                {
                    try
                    {
                        var parameter = m_camera.Parameters[name];
                        if (!parameter.IsReadable) continue;
                        double fps = parameter.GetValue();
                        if (fps > 0 && !Double.IsInfinity(fps)) return fps;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[WARN] [CAMERA] Could not read recording playback rate: " + ex.Message);
                    }
                }
                return Config.VIDEO_FRAME_RATE;
            }
        }

        // @brief: Retrieve and publish one camera frame while protecting camera resources from reconnect cleanup
        // @param:
        //      * p_cancellationToken: Token used to cancel capture before retrieving a frame
        public void cameraCapture(CancellationToken p_cancellationToken)
        {
            // A reconnect must not dispose a camera while a grab owns its buffer.
            lock (m_captureLock)
            {
                if (p_cancellationToken.IsCancellationRequested) return;
                if (!IsConnected)
                {
                    ReportConnectionLost();
                    return;
                }
                CaptureFrame(p_cancellationToken);
            }
        }

        // @brief: Retrieve one frame, record it, and queue it for live inference when due
        // @details:
        //      1. Read the active IPC session token and return if capture is canceled or no session is active.
        //      2. Retrieve the latest camera image and timestamp it; account for retrieval failures and report
        //         a connection loss unless application shutdown canceled the capture.
        //      3. Record camera-reported skipped frames and reject unsuccessful grabs.
        //      4. Rent a pooled frame buffer, copy the grab into tightly packed BGR8 pixels, and record pool
        //         or copy failures in the active session's loss counters.
        //      5. Offer the owned frame to the session recorder, then reserve the configured IPC cadence and
        //         queue the frame for shared-memory publication when a live inference frame is due.
        //      6. Dispose the grab and release the capture thread's buffer reference on every exit path.
        // @param:
        //      * p_cancellationToken: Token used to stop capture and suppress disconnect reporting during shutdown
        private void CaptureFrame(CancellationToken p_cancellationToken)
        {
            var ipc = IPCServices.getInstance();
            long sessionToken = ipc.SessionToken;
            if (sessionToken == 0 || p_cancellationToken.IsCancellationRequested) return;
            IGrabResult result;
            long captureTimestamp;
            try
            {
                result = m_camera.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
                captureTimestamp = Stopwatch.GetTimestamp();
            }
            catch (Exception ex)
            {
                Common.videoRecorder.NoteCaptureFailure(sessionToken);
                Console.WriteLine("[ERROR] [CAMERA] Could not capture " + ex.Message);
                if (!p_cancellationToken.IsCancellationRequested) ReportConnectionLost();
                return;
            }

            using (result)
            {
                if (m_lastCaptureToken == sessionToken)
                    Common.videoRecorder.NoteCameraSkipped(sessionToken, result.SkippedImageCount);
                m_lastCaptureToken = sessionToken;
                if (!result.GrabSucceeded)
                {
                    Common.videoRecorder.NoteCaptureFailure(sessionToken);
                    Console.WriteLine("[ERROR] [CAMERA] Capture failed: " + result.ErrorDescription);
                    return;
                }

                if (!PooledFrameBuffer.TryRent(out PooledFrameBuffer frame))
                {
                    Common.videoRecorder.NotePoolDrop(sessionToken);
                    return;
                }

                using (frame)
                {
                    try
                    {
                        CopyPixels(result, frame.Buffer);
                    }
                    catch (Exception ex)
                    {
                        Common.videoRecorder.NoteCaptureFailure(sessionToken);
                        Console.WriteLine("[ERROR] [CAMERA] Frame copy failed: " + ex.Message);
                        return;
                    }

                    Common.videoRecorder.TryAppend(sessionToken, frame, captureTimestamp);
                    try
                    {
                        if (ipc.TryReserveCaptureFrame(sessionToken))
                            ipc.QueueCaptureFrame(frame, sessionToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] [IPC] Could not queue live frame: " + ex.Message);
                    }
                }
            }
        }

        // @brief: Copy a correctly formatted BGR8 camera image into a tightly packed destination buffer
        // @param:
        //      * result: Successful camera grab containing the source pixels and image geometry
        //      * destination: Buffer that receives the packed image bytes
        private static void CopyPixels(IGrabResult result, byte[] destination)
        {
            if ((PixelType)result.PixelTypeValue != PixelType.BGR8packed ||
                result.Width != Config.IMAGE_WIDTH || result.Height != Config.IMAGE_HEIGHT)
                throw new InvalidOperationException("Camera grab format does not match the configured BGR8 image geometry.");

            IntPtr source = result.PixelDataPointer;
            if (source == IntPtr.Zero)
                throw new InvalidOperationException("Camera grab returned a null pixel buffer.");
            int padding = result.PaddingX;
            if (padding == 0)
            {
                Marshal.Copy(source, destination, 0, Config.IMAGE_BYTE_COUNT);
                return;
            }

            int rowBytes = Config.IMAGE_WIDTH * 3;
            int sourceStride = checked(rowBytes + padding);
            for (int row = 0; row < Config.IMAGE_HEIGHT; row++)
                Marshal.Copy(IntPtr.Add(source, checked(row * sourceStride)), destination, row * rowBytes, rowBytes);
        }

        // @brief: Stop camera activity, detach connection notifications, and release the Pylon camera
        public void cameraClose()
        {
            Interlocked.Exchange(ref m_closing, 1);
            lock (m_captureLock)
            {
                if (m_camera == null) return;
                m_camera.ConnectionLost -= OnConnectionLost;
                try { m_camera.Close(); }
                catch (Exception ex) { Console.WriteLine("[WARN] [CAMERA] Close failed: " + ex.Message); }
                finally
                {
                    try { m_camera.Dispose(); }
                    catch (Exception ex) { Console.WriteLine("[WARN] [CAMERA] Dispose failed: " + ex.Message); }
                    m_camera = null;
                }
            }
        }
    }
}
