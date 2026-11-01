using System;
using System.Runtime.InteropServices;
using System.Threading;
using Basler.Pylon;
using Stone_Application.IPC;

namespace Stone_Application.CameraClass
{
    public class BaslerCamera
    {
        private static BaslerCamera s_instance;
        private Camera m_camera;
        private long m_lastCaptureToken;
        private BaslerCamera() { ; }

        public static BaslerCamera getInstance()
        {
            if (s_instance != null && s_instance.m_camera != null && s_instance.m_camera.IsOpen)
                return s_instance;

            lock (typeof(BaslerCamera))
            {
                if (s_instance != null && s_instance.m_camera != null && s_instance.m_camera.IsOpen)
                    return s_instance;

                var instance = new BaslerCamera();
                try
                {
                    instance.m_camera = new Camera();
                    instance.m_camera.Open();
                    instance.m_camera.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BGR8);
                    instance.m_camera.Parameters[PLCamera.ExposureTime].SetValue(Config.EXPOSURE_TIME);

                    instance.m_camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    instance.m_camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
                    instance.m_camera.Parameters[PLCamera.AcquisitionStart].Execute();

                    s_instance = instance;

                    if (Config.s_isDebugMode) {
                        Console.WriteLine($"[INFO] [CAMERA] Camera initialized successfully");
                    }
                    return s_instance;
                }
                catch (Exception ex)
                {
                    if (instance.m_camera != null)
                    {
                        try { instance.m_camera.Dispose(); }
                        catch { }
                    }
                    s_instance = null;
                    throw new InvalidOperationException("Could not initialize the Basler camera. Check that the camera is connected, powered, and available to the Pylon driver.", ex);
                }
            }
        }

        public void cameraCapture(CancellationToken p_cancellationToken)
        {
            var ipc = IPCServices.getInstance();
            long sessionToken = ipc.SessionToken;
            if (sessionToken == 0 || p_cancellationToken.IsCancellationRequested) return;
            IGrabResult result;
            try
            {
                result = s_instance.m_camera.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
            }
            catch (Exception ex)
            {
                Common.videoRecorder.NoteCaptureFailure(sessionToken);
                Console.WriteLine("[ERROR] [CAMERA] Could not capture " + ex.Message);
                return;
            }

            using (result)
            {
                if (result.GrabSucceeded)
                {
                    if (m_lastCaptureToken == sessionToken)
                        Common.videoRecorder.NoteCaptureFailure(sessionToken, result.SkippedImageCount);
                    m_lastCaptureToken = sessionToken;
                    Event.IImage imageData = new Event.IImage();

                    try
                    {
                        // The camera is configured for BGR8packed. Copy its native pixels
                        // into an owned buffer before disposing the grab result; queued
                        // recorder/IPC consumers must not retain the camera-owned pointer.
                        if ((PixelType)result.PixelTypeValue != PixelType.BGR8packed ||
                            result.Width != Config.IMAGE_WIDTH || result.Height != Config.IMAGE_HEIGHT)
                            throw new InvalidOperationException("Camera grab format does not match the configured BGR8 image geometry.");

                        int rowBytes = checked(Config.IMAGE_WIDTH * 3);
                        int sourceStride = checked(rowBytes + result.PaddingX);
                        imageData.recvImage = new byte[checked(rowBytes * Config.IMAGE_HEIGHT)];
                        IntPtr source = result.PixelDataPointer;
                        if (source == IntPtr.Zero)
                            throw new InvalidOperationException("Camera grab returned a null pixel buffer.");
                        for (int row = 0; row < Config.IMAGE_HEIGHT; row++)
                            Marshal.Copy(IntPtr.Add(source, checked(row * sourceStride)), imageData.recvImage,
                                row * rowBytes, rowBytes);

                        // Recording acceptance is independent from shared-memory publication.
                        // The token was captured before RetrieveResult, so old grabs cannot enter a new session.
                        Common.videoRecorder.TryAppend(sessionToken, imageData.recvImage);
                        ipc.QueueCaptureFrame(imageData.recvImage, sessionToken);

                        if (Config.s_isDebugMode) {
                            Console.WriteLine("[INFO] [CAMERA] Image is captured successfully");
                        }
                    }
                    catch (Exception ex)
                    {
                        Common.videoRecorder.NoteCaptureFailure(sessionToken);
                        // Catch interop/native exceptions and log them — avoid unhandled SEHException crashing the process.
                        Console.WriteLine("[ERROR] [CAMERA] Convert failed: " + ex.Message);
                    }
                }
                else
                {
                    Common.videoRecorder.NoteCaptureFailure(sessionToken);
                    Console.WriteLine("[ERROR] [CAMERA] Capture failed: " + result.ErrorDescription);
                }
            }
        }

        public void cameraClose()
        {
            try
            {
                s_instance.m_camera.Parameters[PLCamera.AcquisitionStop].Execute();
                s_instance.m_camera.Close();
                s_instance.m_camera.Dispose();

                if (Config.s_isDebugMode) {
                    Console.WriteLine($"[INFO] [CAMERA] Camera is closed sucessully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] [CAMERA] Camera could not be Closed\nError: {ex.Message}");
            }
        }
    }
}
