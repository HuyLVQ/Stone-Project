using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Stone_Application.Networking.Iot
{
    internal sealed class FfmpegH264Encoder : IDisposable
    {
        private readonly Process m_process;
        private readonly object m_inputLock = new object();
        private readonly Action<byte[]> m_chunkHandler;
        private readonly Task m_outputTask;
        private readonly Task m_errorTask;
        private bool m_disposed;

        private FfmpegH264Encoder(string executablePath, Action<byte[]> chunkHandler)
        {
            m_chunkHandler = chunkHandler;
            m_process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "-hide_banner -loglevel error -nostdin " +
                        "-f rawvideo -pixel_format bgr24 -video_size 1920x1200 -framerate " + Config.IOT_STREAM_FPS + " -i pipe:0 " +
                        "-an -c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p " +
                        "-b:v " + Config.IOT_STREAM_BITRATE_KBPS + "k -maxrate " + Config.IOT_STREAM_BITRATE_KBPS + "k " +
                        "-bufsize " + (2 * Config.IOT_STREAM_BITRATE_KBPS) + "k " +
                        // One keyframe per second, so a client that joins or resyncs waits at most 1 s.
                        "-g " + Config.IOT_STREAM_FPS + " -keyint_min " + Config.IOT_STREAM_FPS + " -sc_threshold 0 " +
                        "-x264-params aud=1:repeat-headers=1 -f mpegts -mpegts_flags +resend_headers " +
                        "-muxdelay 0 -muxpreload 0 -flush_packets 1 pipe:1",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            if (!m_process.Start()) throw new InvalidOperationException("FFmpeg did not start.");
            m_outputTask = Task.Run((Action)PumpOutput);
            m_errorTask = Task.Run((Action)PumpErrors);
        }

        internal static FfmpegH264Encoder Start(string executablePath, Action<byte[]> chunkHandler)
        {
            if (!HasLibX264(executablePath))
                throw new InvalidOperationException("The configured FFmpeg build does not include the libx264 encoder.");
            return new FfmpegH264Encoder(executablePath, chunkHandler);
        }

        internal static bool HasLibX264(string executablePath)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = "-hide_banner -encoders",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    if (!process.Start()) return false;
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        return false;
                    }
                    string encoderList = stdout.Result + stderr.Result;
                    return process.ExitCode == 0 && encoderList.IndexOf("libx264", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] [IOT] Could not inspect FFmpeg encoders: " + ex.Message);
                return false;
            }
        }

        internal void WriteFrame(byte[] bgrFrame)
        {
            if (bgrFrame == null || bgrFrame.Length != Config.IMAGE_BYTE_COUNT)
                throw new InvalidDataException("The H.264 input frame has an unexpected byte length.");

            lock (m_inputLock)
            {
                if (m_disposed || m_process.HasExited)
                    throw new InvalidOperationException("The FFmpeg H.264 encoder is no longer running.");
                m_process.StandardInput.BaseStream.Write(bgrFrame, 0, bgrFrame.Length);
                m_process.StandardInput.BaseStream.Flush();
            }
        }

        private void PumpOutput()
        {
            // Publish whole 188-byte MPEG-TS packets only, so a receiver that
            // skips chunks (see IotClientConnection) never loses packet alignment.
            // The buffer is a multiple of the packet size, so it can never fill
            // up without yielding at least one complete packet.
            byte[] buffer = new byte[IotProtocol.TsPacketBytes * 174];
            int filled = 0;
            try
            {
                Stream output = m_process.StandardOutput.BaseStream;
                int read;
                while ((read = output.Read(buffer, filled, buffer.Length - filled)) > 0)
                {
                    filled += read;
                    int aligned = filled - filled % IotProtocol.TsPacketBytes;
                    if (aligned == 0) continue;

                    byte[] chunk = new byte[aligned];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, aligned);
                    filled -= aligned;
                    if (filled > 0) Buffer.BlockCopy(buffer, aligned, buffer, 0, filled);
                    try { m_chunkHandler(chunk); }
                    catch (Exception ex) { Console.WriteLine("[WARN] [IOT] Could not publish H.264 transport data: " + ex.Message); }
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException ex) { if (!m_disposed) Console.WriteLine("[WARN] [IOT] FFmpeg output ended: " + ex.Message); }
        }

        private void PumpErrors()
        {
            try
            {
                string line;
                while ((line = m_process.StandardError.ReadLine()) != null)
                    Console.WriteLine("[WARN] [IOT] FFmpeg: " + line);
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        public void Dispose()
        {
            lock (m_inputLock)
            {
                if (m_disposed) return;
                m_disposed = true;
                try { m_process.StandardInput.Close(); } catch { }
            }

            try
            {
                if (!m_process.WaitForExit(2000)) m_process.Kill();
            }
            catch { }
            try { m_outputTask.Wait(1000); } catch { }
            try { m_errorTask.Wait(1000); } catch { }
            m_process.Dispose();
        }
    }
}
