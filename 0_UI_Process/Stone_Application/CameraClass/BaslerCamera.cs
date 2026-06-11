using System;
using Basler.Pylon;

namespace Stone_Application.CameraClass
{
    public class BaslerCamera
    {
        private static BaslerCamera s_instance;
        private Camera m_camera;
        private readonly PixelDataConverter m_converter = new PixelDataConverter
        {
            OutputPixelFormat = PixelType.BGR8packed
        };


        private BaslerCamera() { ; }

        public static BaslerCamera getInstance()
        {

            if (s_instance == null)
            {
                s_instance = new BaslerCamera();
                s_instance.m_camera = new Camera();

                try
                {
                    s_instance.m_camera.Open();
                    s_instance.m_camera.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BGR8);
                    s_instance.m_camera.Parameters[PLCamera.ExposureTime].SetValue(Config.EXPOSURE_TIME);

                    s_instance.m_camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    s_instance.m_camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
                    s_instance.m_camera.Parameters[PLCamera.AcquisitionStart].Execute();

                    Console.WriteLine($"[INFO] [CAMERA] Camera initialized successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] [CAMERA] Error opening camera: " + ex.Message);
                }
            }
            return s_instance;
        }

        public void cameraCapture()
        {
            IGrabResult result;
            try
            {
                result = s_instance.m_camera.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] [CAMERA] Could not capture " + ex.Message);
                return;
            }

            using (result)
            {
                if (result.GrabSucceeded)
                {
                    this.m_converter.OutputPixelFormat = PixelType.BGR8packed;

                    Event.IImage imageData = new Event.IImage();

                    try
                    {
                        // Ensure a buffer is allocated for the unmanaged Convert call.
                        int payloadSize = (int)result.PayloadSize;
                        imageData.recvImage = new byte[payloadSize];

                        m_converter.Convert(imageData.recvImage, result);

                        Common.s_imageQueue.Add(imageData);

                        Console.WriteLine("[INFO] [CAMERA] Image is captured successfully");
                    }
                    catch (Exception ex)
                    {
                        // Catch interop/native exceptions and log them — avoid unhandled SEHException crashing the process.
                        Console.WriteLine("[ERROR] [CAMERA] Convert failed: " + ex.Message);
                    }
                }
                else
                {
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
                Console.WriteLine($"[INFO] [CAMERA] Camera is closed sucessully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] [CAMERA] Camera could not be Closed\nError: {ex.Message}");
            }
        }
    }
}
