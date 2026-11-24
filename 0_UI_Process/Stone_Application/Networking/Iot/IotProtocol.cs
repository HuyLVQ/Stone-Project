using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Stone_Application.Event;

namespace Stone_Application.Networking.Iot
{
    internal static class IotProtocol
    {
        internal const int HandshakePort = 50001;
        internal const int ClientTcpPort = 50002;
        internal const int MaxHandshakeBytes = 4096;
        internal const int MaxEnvelopeBytes = 16 * 1024 * 1024;
        private const uint SchemaVersion = 2;
        internal const long JpegQuality = 85;

        internal static byte[] CompressAnnotatedImage(byte[] bgrImage)
        {
            if (bgrImage == null || bgrImage.Length != Config.IMAGE_BYTE_COUNT)
                throw new InvalidDataException("The annotated image has an unexpected byte length.");

            using (Bitmap bitmap = new Bitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT, PixelFormat.Format24bppRgb))
            {
                Rectangle bounds = new Rectangle(0, 0, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT);
                BitmapData bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int packedRowBytes = checked(Config.IMAGE_WIDTH * 3);
                    if (bitmapData.Stride == packedRowBytes)
                    {
                        Marshal.Copy(bgrImage, 0, bitmapData.Scan0, bgrImage.Length);
                    }
                    else
                    {
                        for (int row = 0; row < Config.IMAGE_HEIGHT; row++)
                        {
                            IntPtr rowAddress = IntPtr.Add(bitmapData.Scan0, checked(row * bitmapData.Stride));
                            Marshal.Copy(bgrImage, checked(row * packedRowBytes), rowAddress, packedRowBytes);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                ImageCodecInfo jpegCodec = null;
                foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                {
                    if (String.Equals(codec.MimeType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        jpegCodec = codec;
                        break;
                    }
                }
                if (jpegCodec == null) throw new InvalidOperationException("The JPEG image encoder is unavailable.");

                using (var output = new MemoryStream())
                using (var encoderParameters = new EncoderParameters(1))
                {
                    encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
                    bitmap.Save(output, jpegCodec, encoderParameters);
                    return output.ToArray();
                }
            }
        }

        internal static byte[] CreateImageEnvelope(IImage image, byte[] jpegImage, long sequence)
        {
            var frame = new ProtoWriter();
            frame.UInt32(1, SchemaVersion);
            frame.String(2, image.liveSessionToken.ToString(System.Globalization.CultureInfo.InvariantCulture));
            frame.UInt64(3, checked((ulong)sequence));
            // IImage carries no camera timestamp, so record the server-side
            // time when the completed AI image is packaged for delivery.
            frame.Int64(4, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            frame.UInt32(5, checked((uint)Config.IMAGE_WIDTH));
            frame.UInt32(6, checked((uint)Config.IMAGE_HEIGHT));
            frame.String(7, "JPEG");
            frame.Bytes(8, jpegImage);

            var envelope = new ProtoWriter();
            envelope.UInt32(1, SchemaVersion);
            envelope.Message(10, frame.ToArray());
            return envelope.ToArray();
        }

        internal static byte[] CreateResultEnvelope(AIHelper.VideoInferenceResult result, long sessionToken, long sequence)
        {
            double[] counts = { result.MiSang, result.OneByTwo, result.TwoByFour, result.FourBySix };
            string[] ids = { "MiSang", "1x2", "2x4", "4x6" };
            double total = counts[0] + counts[1] + counts[2] + counts[3];

            var resultMessage = new ProtoWriter();
            resultMessage.UInt32(1, SchemaVersion);
            resultMessage.String(2, sessionToken.ToString(System.Globalization.CultureInfo.InvariantCulture));
            resultMessage.UInt64(3, checked((ulong)sequence));
            resultMessage.Int64(4, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            for (int index = 0; index < counts.Length; index++)
            {
                var rock = new ProtoWriter();
                rock.String(1, ids[index]);
                double percentage = total > 0 ? Math.Round(counts[index] * 100.0 / total, 2, MidpointRounding.AwayFromZero) : 0.0;
                rock.Double(2, percentage);
                resultMessage.Message(5, rock.ToArray());
            }

            // Match the finalized result shown by the application (the first
            // model's session-average prediction, expressed in kilograms).
            double weight = result.AverageWeights != null && result.AverageWeights.Length > 0
                ? result.AverageWeights[0]
                : 0.0;
            resultMessage.Double(6, weight);
            resultMessage.String(7, "kg");

            var envelope = new ProtoWriter();
            envelope.UInt32(1, SchemaVersion);
            envelope.Message(11, resultMessage.ToArray());
            return envelope.ToArray();
        }

        internal static byte[] Frame(byte[] envelope)
        {
            if (envelope == null || envelope.Length == 0 || envelope.Length > MaxEnvelopeBytes)
                throw new InvalidDataException("The IoT protobuf envelope is outside the permitted size.");

            byte[] framed = new byte[4 + envelope.Length];
            uint size = checked((uint)envelope.Length);
            framed[0] = (byte)(size >> 24);
            framed[1] = (byte)(size >> 16);
            framed[2] = (byte)(size >> 8);
            framed[3] = (byte)size;
            Buffer.BlockCopy(envelope, 0, framed, 4, envelope.Length);
            return framed;
        }

        internal static bool TryParseHandshake(string message, IPAddress observedAddress, out string clientId)
        {
            clientId = null;
            const string prefix = "SEARCHING FOR SERVER - my current id is:";
            if (String.IsNullOrWhiteSpace(message) || !message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string tail = message.Substring(prefix.Length).Trim();
            string[] parts = tail.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            string id = parts[0].Trim();
            if (id.Length == 0 || id.Length > 128 || id.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
                return false;

            // If a client declares its address, require it to agree with the
            // datagram source. The observed source address is always registered.
            for (int index = 1; index < parts.Length; index++)
            {
                string declaration = parts[index].Trim();
                const string ipPrefix = "my IP address is:";
                if (declaration.StartsWith(ipPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    IPAddress declared;
                    if (!IPAddress.TryParse(declaration.Substring(ipPrefix.Length).Trim(), out declared) ||
                        !NormalizeAddress(declared).Equals(NormalizeAddress(observedAddress)))
                        return false;
                }
            }

            clientId = id;
            return true;
        }

        internal static IPAddress NormalizeAddress(IPAddress address)
        {
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }

        private sealed class ProtoWriter
        {
            private readonly MemoryStream m_stream = new MemoryStream();

            internal byte[] ToArray() { return m_stream.ToArray(); }

            internal void UInt32(int field, uint value)
            {
                if (value == 0) return;
                Tag(field, 0); Varint(value);
            }

            internal void UInt64(int field, ulong value)
            {
                if (value == 0) return;
                Tag(field, 0); Varint(value);
            }

            internal void Int64(int field, long value)
            {
                if (value == 0) return;
                Tag(field, 0); Varint(unchecked((ulong)value));
            }

            internal void String(int field, string value)
            {
                if (System.String.IsNullOrEmpty(value)) return;
                Bytes(field, Encoding.UTF8.GetBytes(value));
            }

            internal void Message(int field, byte[] value) { Bytes(field, value); }

            internal void Bytes(int field, byte[] value)
            {
                if (value == null) throw new ArgumentNullException("value");
                Tag(field, 2); Varint(checked((ulong)value.Length));
                m_stream.Write(value, 0, value.Length);
            }

            internal void Double(int field, double value)
            {
                if (value == 0.0) return;
                Tag(field, 1);
                byte[] bytes = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                m_stream.Write(bytes, 0, bytes.Length);
            }

            private void Tag(int field, int wireType) { Varint(checked((ulong)((field << 3) | wireType))); }

            private void Varint(ulong value)
            {
                while (value >= 0x80)
                {
                    m_stream.WriteByte((byte)(value | 0x80));
                    value >>= 7;
                }
                m_stream.WriteByte((byte)value);
            }
        }
    }
}
