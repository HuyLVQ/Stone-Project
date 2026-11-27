using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Stone_Client.Video
{
    internal static class FrameDecoding
    {
        /// <summary>
        /// Decodes a JPEG into a bitmap that does not depend on the source bytes
        /// (GDI+ requires the stream of Image.FromStream to outlive the image).
        /// </summary>
        /// <exception cref="InvalidDataException">The bytes are not a readable image.</exception>
        internal static Bitmap DecodeJpeg(byte[] p_jpeg)
        {
            if (p_jpeg == null || p_jpeg.Length == 0) throw new InvalidDataException("The frame has no image data.");
            try
            {
                using (var stream = new MemoryStream(p_jpeg, false))
                using (Image decoded = Image.FromStream(stream, false, true))
                {
                    return new Bitmap(decoded);
                }
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("The image data could not be decoded: " + ex.Message, ex);
            }
            catch (ExternalException ex)
            {
                throw new InvalidDataException("The image data could not be decoded: " + ex.Message, ex);
            }
            catch (OutOfMemoryException ex)
            {
                // GDI+ reports malformed images, and images too large to allocate,
                // as OutOfMemoryException.
                throw new InvalidDataException("The image data could not be decoded: " + ex.Message, ex);
            }
        }

        /// <summary>Wraps tightly packed BGR24 pixels (FFmpeg's bgr24) in a new bitmap.</summary>
        internal static Bitmap FromBgr24(byte[] p_pixels, int p_width, int p_height)
        {
            int rowBytes = checked(p_width * 3);
            if (p_pixels == null || p_pixels.Length < checked(rowBytes * p_height))
                throw new ArgumentException("The pixel buffer is smaller than one frame.", "p_pixels");

            var bitmap = new Bitmap(p_width, p_height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, p_width, p_height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                // Format24bppRgb stores pixels as B, G, R in memory, matching bgr24.
                if (data.Stride == rowBytes)
                {
                    Marshal.Copy(p_pixels, 0, data.Scan0, rowBytes * p_height);
                }
                else
                {
                    for (int row = 0; row < p_height; row++)
                        Marshal.Copy(p_pixels, row * rowBytes, IntPtr.Add(data.Scan0, row * data.Stride), rowBytes);
                }
            }
            catch
            {
                bitmap.UnlockBits(data);
                bitmap.Dispose();
                throw;
            }
            bitmap.UnlockBits(data);
            return bitmap;
        }
    }
}
