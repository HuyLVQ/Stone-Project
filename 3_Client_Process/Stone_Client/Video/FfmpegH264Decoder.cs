using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace Stone_Client.Video
{
    /// <summary>
    /// Decodes the server's H.264-in-MPEG-TS stream with an ffmpeg.exe child
    /// process: transport bytes go to its stdin, fixed-size BGR24 frames come
    /// back on its stdout. One instance decodes one session.
    ///
    /// The output is always scaled to <see cref="Width"/> x <see cref="Height"/>
    /// so the frame size on the pipe is known in advance. After a gap in the
    /// stream the server resumes at a keyframe, so the decoder just continues.
    /// </summary>
    internal sealed class FfmpegH264Decoder : IDisposable
    {
        private const int MaxReportedFfmpegLines = 10;

        private readonly Process m_process;
        private readonly Stream m_input;
        private readonly Action<Bitmap> m_onFrame;
        private readonly Action<string> m_onMessage;
        private readonly Thread m_outputThread;
        private readonly Thread m_errorThread;
        private readonly object m_inputLock = new object();
        private bool m_disposed;
        private long m_framesDecoded;

        internal int Width { get; private set; }
        internal int Height { get; private set; }
        internal long FramesDecoded { get { return Interlocked.Read(ref m_framesDecoded); } }

        /// <param name="p_onFrame">Called on a decoder thread; receives ownership of each bitmap.</param>
        /// <param name="p_onMessage">Called on a decoder thread with FFmpeg diagnostics.</param>
        internal FfmpegH264Decoder(string p_ffmpegPath, int p_width, int p_height,
                                   Action<Bitmap> p_onFrame, Action<string> p_onMessage)
        {
            if (p_width <= 0 || p_height <= 0 || p_width % 2 != 0 || p_height % 2 != 0)
                throw new ArgumentException("The decoded frame size must be positive and even.");
            Width = p_width;
            Height = p_height;
            m_onFrame = p_onFrame;
            m_onMessage = p_onMessage;

            m_process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = p_ffmpegPath,
                    Arguments = BuildArguments(p_width, p_height),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            if (!m_process.Start()) throw new InvalidOperationException("FFmpeg did not start.");
            m_input = m_process.StandardInput.BaseStream;

            m_outputThread = new Thread(PumpFrames) { IsBackground = true, Name = "H.264 decoder output" };
            m_errorThread = new Thread(PumpErrors) { IsBackground = true, Name = "H.264 decoder errors" };
            m_outputThread.Start();
            m_errorThread.Start();
        }

        /// <summary>
        /// Low-latency demux and decode of MPEG-TS on stdin. Probing is kept short
        /// (about one keyframe) so the first picture appears quickly.
        ///
        /// "-fflags nobuffer" must not be used: it discards the packets read while
        /// probing, so the first keyframe is lost and nothing is shown until the
        /// next one (a replay measured 1.3 s to the first frame with it, 0.3 s without).
        ///
        /// "-fps_mode passthrough" emits each decoded frame exactly once. Raw video
        /// output otherwise defaults to constant frame rate, and when the server
        /// skips ahead to a later keyframe the timestamp jump would make FFmpeg
        /// emit a burst of duplicate frames to fill the gap. It needs FFmpeg 5.1
        /// or later; the older "-vsync" spelling was removed in newer builds.
        /// </summary>
        internal static string BuildArguments(int p_width, int p_height)
        {
            return "-hide_banner -loglevel error -nostdin " +
                   "-flags low_delay -probesize 65536 -analyzeduration 200000 " +
                   "-f mpegts -i pipe:0 -map 0:v:0 -an " +
                   "-vf scale=" + p_width + ":" + p_height + " -fps_mode passthrough " +
                   "-pix_fmt bgr24 -f rawvideo pipe:1";
        }

        /// <summary>Feeds transport bytes in order. Blocks while FFmpeg is busy.</summary>
        /// <exception cref="IOException">FFmpeg has exited or its input is closed.</exception>
        internal void Write(byte[] p_transport)
        {
            if (p_transport == null || p_transport.Length == 0) return;
            lock (m_inputLock)
            {
                if (m_disposed) throw new ObjectDisposedException("FfmpegH264Decoder");
                if (m_process.HasExited) throw new IOException("FFmpeg exited with code " + m_process.ExitCode);
                m_input.Write(p_transport, 0, p_transport.Length);
                m_input.Flush();
            }
        }

        private void PumpFrames()
        {
            int frameBytes = checked(Width * Height * 3);
            byte[] buffer = new byte[frameBytes];
            bool frameErrorReported = false;
            try
            {
                Stream output = m_process.StandardOutput.BaseStream;
                while (true)
                {
                    int filled = 0;
                    while (filled < frameBytes)
                    {
                        int read = output.Read(buffer, filled, frameBytes - filled);
                        if (read == 0) return;      // FFmpeg finished; a partial frame is discarded
                        filled += read;
                    }

                    Interlocked.Increment(ref m_framesDecoded);
                    // Skip a frame that cannot be wrapped (for example, out of memory)
                    // rather than let the exception end this thread and the process.
                    Bitmap frame;
                    try { frame = FrameDecoding.FromBgr24(buffer, Width, Height); }
                    catch (Exception ex)
                    {
                        if (!frameErrorReported)
                        {
                            frameErrorReported = true;
                            Report("A decoded frame could not be shown (" + ex.Message + "); further failures are not reported.");
                        }
                        continue;
                    }
                    try { m_onFrame(frame); }
                    catch (Exception ex) { Report("Frame handler failed: " + ex.Message); }
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void PumpErrors()
        {
            int reported = 0;
            try
            {
                string line;
                while ((line = m_process.StandardError.ReadLine()) != null)
                {
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    reported++;
                    if (reported <= MaxReportedFfmpegLines) Report("FFmpeg: " + line.Trim());
                    else if (reported == MaxReportedFfmpegLines + 1) Report("Further FFmpeg messages for this session are suppressed.");
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void Report(string p_message)
        {
            try { if (m_onMessage != null) m_onMessage(p_message); }
            catch (Exception) { }
        }

        /// <summary>
        /// Closes FFmpeg's input so it flushes the frames it still holds, then
        /// waits briefly for it to exit (killing it if it does not).
        /// </summary>
        public void Dispose()
        {
            // A Write blocked on a stalled FFmpeg holds the input lock; killing the
            // process fails that write and releases the lock.
            if (!Monitor.TryEnter(m_inputLock, 3000))
            {
                try { m_process.Kill(); } catch (Exception) { }
                Monitor.Enter(m_inputLock);
            }
            try
            {
                if (m_disposed) return;
                m_disposed = true;
                try { m_input.Close(); } catch (Exception) { }
            }
            finally
            {
                Monitor.Exit(m_inputLock);
            }

            try { if (!m_process.WaitForExit(3000)) m_process.Kill(); }
            catch (Exception) { }
            m_outputThread.Join(TimeSpan.FromSeconds(2));
            m_errorThread.Join(TimeSpan.FromSeconds(2));
            m_process.Dispose();
        }
    }
}
