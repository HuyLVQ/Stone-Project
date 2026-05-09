using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.ExtendedProperties;
using Stone_Application.Event;
using Stone_Application.Forms;

namespace Stone_Application.Observer
{
    public class UIImageObserver<T> : IEventObserver<T> where T : IImage
    {
        public UIImageObserver()
        {
            ;
        }
        public void Update(T image)
        {
            Bitmap bmp = new Bitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT, PixelFormat.Format24bppRgb);
            BitmapData data = bmp.LockBits(
            new Rectangle(0, 0, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT),
                          ImageLockMode.WriteOnly,
                          PixelFormat.Format24bppRgb);

            Marshal.Copy(image.recvImage, 0, data.Scan0, image.recvImage.Length);

            bmp.UnlockBits(data);

            FormHome.updatePictureBox(bmp);
        }
    }
}
