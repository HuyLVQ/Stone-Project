using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Basler.Pylon;

namespace Stone_Application.CameraClass
{   
    public class BaslerCamera
    {
        private static BaslerCamera instance;
        private Camera camera;
        private readonly PixelDataConverter converter = new PixelDataConverter
        {
            OutputPixelFormat = PixelType.BGR8packed
        };


        private BaslerCamera() { ; }

        public static BaslerCamera getInstance()
        {

            if (instance == null)
            {
                instance = new BaslerCamera();
                instance.camera = new Camera();

                try
                {
                    instance.camera.Open();
                    instance.camera.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BGR8);
                    instance.camera.Parameters[PLCamera.ExposureTime].SetValue(Config.EXPOSURE_TIME);

                    instance.camera.Parameters[PLCamera.AcquisitionMode].SetValue(PLCamera.AcquisitionMode.Continuous);
                    instance.camera.StreamGrabber.Start(GrabStrategy.LatestImages, GrabLoop.ProvidedByUser);
                    instance.camera.Parameters[PLCamera.AcquisitionStart].Execute();

                    Console.WriteLine($"[INFO] [CAMERA] Camera initialized successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] [CAMERA] Error opening camera: " + ex.Message);
                }
            }
            return instance;
        }

        public void cameraCapture()
        {
            IGrabResult result;
            try
            {
                result = instance.camera.StreamGrabber.RetrieveResult(5000, TimeoutHandling.ThrowException);
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
                    this.converter.OutputPixelFormat = PixelType.BGR8packed;

                    Event.IImage imageData = new Event.IImage();
                    converter.Convert(imageData.recvImage, result);

                    Common.imageQueue.Add(imageData);

                    Console.WriteLine("[INFO] [CAMERA] Image is captured successfully");
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
                instance.camera.Parameters[PLCamera.AcquisitionStop].Execute();
                instance.camera.Close();
                instance.camera.Dispose();
                Console.WriteLine($"[INFO] [CAMERA] Camera is closed sucessully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] [CAMERA] Camera could not be Closed\nError: {ex.Message}");
            }
        }
    }
}
