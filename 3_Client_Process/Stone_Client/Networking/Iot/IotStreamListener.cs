using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Stone_Client.Event;
using Stone_Client.Video;

namespace Stone_Client.Networking.Iot
{
    /// <summary>
    /// Receives the server's TCP stream. The server is the TCP client here: it
    /// dials this port, closes the image connection at STOP, and may open a new
    /// connection for the final result. A newly accepted connection therefore
    /// always replaces the previous one.
    ///
    /// Each record is a 4-byte big-endian length followed by one IotEnvelope.
    /// </summary>
    internal sealed class IotStreamListener : IDisposable
    {
        private const string LOG_SOURCE = "Stream";

        private readonly int m_port;
        private readonly object m_connectionLock = new object();
        private readonly object m_sessionLock = new object();
        private TcpListener m_listener;
        private Thread m_acceptThread;
        private TcpClient m_activeClient;
        private Thread m_activeReader;
        private volatile bool m_stopping;

        private string m_currentSessionId;
        private long m_sessionMessages;

        // H.264 decoding: one FFmpeg process per session, guarded by m_decoderLock.
        private readonly string m_ffmpegPath;
        private readonly object m_decoderLock = new object();
        private FfmpegH264Decoder m_decoder;
        private string m_decoderSessionId;
        private int m_decoderStarts;
        private string m_noFfmpegReportedSession;
        private string m_jpegErrorReportedSession;

        /// <param name="p_ffmpegPath">ffmpeg.exe used to decode H.264; null when unavailable.</param>
        internal IotStreamListener(int p_port, string p_ffmpegPath)
        {
            m_port = p_port;
            m_ffmpegPath = p_ffmpegPath;
        }

        internal bool Start()
        {
            if (m_listener != null) return true;
            try
            {
                var listener = new TcpListener(IPAddress.Any, m_port);
                listener.Start();
                m_listener = listener;
            }
            catch (SocketException ex)
            {
                Log(LogLevel.Error, "Cannot listen on TCP port " + m_port + ": " + ex.Message);
                return false;
            }

            m_acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "IoT stream accept" };
            m_acceptThread.Start();
            Log(LogLevel.Information, "Listening for the server on TCP port " + m_port + ".");
            return true;
        }

        private void AcceptLoop()
        {
            while (!m_stopping)
            {
                TcpClient client;
                try { client = m_listener.AcceptTcpClient(); }
                catch (SocketException) { if (m_stopping) break; continue; }
                catch (ObjectDisposedException) { break; }
                catch (InvalidOperationException) { break; }

                if (m_stopping) { client.Close(); break; }
                client.NoDelay = true;
                string remote = Describe(client);

                TcpClient previous;
                Thread reader = new Thread(() => ReadLoop(client)) { IsBackground = true, Name = "IoT stream reader" };
                lock (m_connectionLock)
                {
                    previous = m_activeClient;
                    m_activeClient = client;
                    m_activeReader = reader;
                }

                if (previous != null)
                {
                    previous.Close();
                    Log(LogLevel.Information, "Server opened a new connection from " + remote + "; the previous connection was closed.");
                }
                else
                {
                    Log(LogLevel.Information, "Server connected from " + remote + ".");
                }
                reader.Start();
            }
        }

        private void ReadLoop(TcpClient p_client)
        {
            string closeReason = null;
            LogLevel closeLevel = LogLevel.Information;
            try
            {
                NetworkStream stream = p_client.GetStream();
                byte[] header = new byte[4];
                while (true)
                {
                    if (!ReadExactly(stream, header, header.Length, true))
                    {
                        closeReason = "Server closed the connection.";
                        break;
                    }

                    uint length = IotProtocol.ReadLengthPrefix(header);
                    if (length == 0 || length > Config.MAX_ENVELOPE_BYTES)
                    {
                        // The framing can no longer be trusted; only a new connection can resync.
                        closeLevel = LogLevel.Warning;
                        closeReason = "Received an invalid record length (" + length + " bytes); the connection was dropped.";
                        break;
                    }

                    byte[] payload = new byte[length];
                    ReadExactly(stream, payload, payload.Length, false);

                    IotEnvelope envelope;
                    try { envelope = IotProtocol.DecodeEnvelope(payload, 0, payload.Length); }
                    catch (InvalidDataException ex)
                    {
                        // Framing is intact, so skip just this record.
                        Log(LogLevel.Warning, "Discarded a malformed record of " + length + " bytes: " + ex.Message);
                        continue;
                    }

                    // An unexpected failure while handling one record must not end this
                    // thread: an exception escaping it would terminate the whole client.
                    try { Dispatch(envelope); }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Warning, "Could not handle a " + envelope.Kind + " record of " + length + " bytes: " +
                            ex.GetType().Name + ": " + ex.Message);
                    }
                }
            }
            catch (IOException ex)
            {
                closeLevel = LogLevel.Warning;
                closeReason = "Connection lost: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            finally
            {
                bool wasActive;
                lock (m_connectionLock)
                {
                    wasActive = ReferenceEquals(m_activeClient, p_client);
                    if (wasActive) { m_activeClient = null; m_activeReader = null; }
                }
                p_client.Close();
                // A replaced or deliberately closed connection is not worth reporting.
                if (wasActive && !m_stopping && closeReason != null) Log(closeLevel, closeReason);
            }
        }

        /// <summary>Returns false only on a clean end of stream before the first byte when allowed.</summary>
        private static bool ReadExactly(Stream p_stream, byte[] p_buffer, int p_count, bool p_allowCleanEnd)
        {
            int offset = 0;
            while (offset < p_count)
            {
                int read = p_stream.Read(p_buffer, offset, p_count - offset);
                if (read == 0)
                {
                    if (offset == 0 && p_allowCleanEnd) return false;
                    throw new IOException("The server closed the connection in the middle of a record (" +
                                          offset + " of " + p_count + " bytes).");
                }
                offset += read;
            }
            return true;
        }

        private void Dispatch(IotEnvelope p_envelope)
        {
            switch (p_envelope.Kind)
            {
                case IotPayloadKind.Image:
                    IotImageFrame image = p_envelope.Image;
                    OnSessionMessage(image.SessionId, String.Format(CultureInfo.InvariantCulture,
                        "{0} frames ({1}x{2})", String.IsNullOrEmpty(image.Encoding) ? "image" : image.Encoding,
                        image.ImageWidth, image.ImageHeight));
                    ShowJpeg(image);
                    break;

                case IotPayloadKind.VideoChunk:
                    IotVideoChunk chunk = p_envelope.VideoChunk;
                    OnSessionMessage(chunk.SessionId, chunk.Codec + " over " + chunk.Container + " chunks");
                    FeedVideo(chunk);
                    break;

                case IotPayloadKind.Result:
                    IotInferenceResult result = p_envelope.Result;
                    // The live stream is over: let FFmpeg flush its last frames.
                    StopDecoder(result.SessionId);
                    OnSessionResult(result.SessionId);
                    ClientEvent.Instance.notifyResult(IotProtocol.ToResult(result));
                    break;

                default:
                    Log(LogLevel.Warning, "Received a record without a payload (schema version " +
                        p_envelope.SchemaVersion + "); ignored.");
                    break;
            }
        }

        // ---------------------------------------------------------------- JPEG

        private void ShowJpeg(IotImageFrame p_image)
        {
            Bitmap bitmap;
            try { bitmap = FrameDecoding.DecodeJpeg(p_image.JpegImage); }
            catch (InvalidDataException ex)
            {
                // One report per session: a broken encoder would otherwise flood the log at 5 fps.
                if (!String.Equals(m_jpegErrorReportedSession, p_image.SessionId, StringComparison.Ordinal))
                {
                    m_jpegErrorReportedSession = p_image.SessionId;
                    Log(LogLevel.Warning, "Session " + p_image.SessionId + ": a JPEG frame of " + p_image.JpegImage.Length +
                        " bytes could not be shown (" + ex.Message + "). Further failures in this session are not reported.");
                }
                return;
            }
            ClientEvent.Instance.notifyFrame(new IFrame { image = bitmap, sessionId = p_image.SessionId, encoding = "JPEG" });
        }

        // ---------------------------------------------------------------- H.264

        private void FeedVideo(IotVideoChunk p_chunk)
        {
            if (m_ffmpegPath == null)
            {
                if (!String.Equals(m_noFfmpegReportedSession, p_chunk.SessionId, StringComparison.Ordinal))
                {
                    m_noFfmpegReportedSession = p_chunk.SessionId;
                    Log(LogLevel.Warning, "Session " + p_chunk.SessionId + ": H.264 video arrived but ffmpeg.exe was not found, so it cannot be shown.");
                }
                return;
            }

            FfmpegH264Decoder decoder;
            FfmpegH264Decoder replaced = null;
            string replacedSession = null;
            lock (m_decoderLock)
            {
                if (m_stopping) return;
                if (!String.Equals(m_decoderSessionId, p_chunk.SessionId, StringComparison.Ordinal))
                {
                    replaced = m_decoder;
                    replacedSession = m_decoderSessionId;
                    m_decoder = null;
                    m_decoderSessionId = p_chunk.SessionId;
                    m_decoderStarts = 0;
                }

                if (m_decoder == null && m_decoderStarts < Config.MAX_DECODER_STARTS_PER_SESSION)
                {
                    m_decoderStarts++;
                    m_decoder = StartDecoder(p_chunk.SessionId, m_decoderStarts);
                }
                decoder = m_decoder;
            }

            if (replaced != null) FinishDecoder(replaced, replacedSession);
            if (decoder == null) return;

            // Written outside the lock: FFmpeg may block, and Dispose must still be able to stop it.
            try { decoder.Write(p_chunk.Data); }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is ObjectDisposedException) && !(ex is InvalidOperationException)) throw;
                bool wasCurrent;
                int starts;
                lock (m_decoderLock)
                {
                    wasCurrent = ReferenceEquals(m_decoder, decoder);
                    if (wasCurrent) m_decoder = null;
                    starts = m_decoderStarts;
                }
                if (!wasCurrent) return;     // Replaced or stopped meanwhile
                decoder.Dispose();
                Log(LogLevel.Warning, "Session " + p_chunk.SessionId + ": the H.264 decoder stopped (" + ex.Message + ")" +
                    (starts < Config.MAX_DECODER_STARTS_PER_SESSION
                        ? "; it will be restarted at the next video data."
                        : "; it has failed " + starts + " times, so video is off for the rest of this session."));
            }
        }

        private FfmpegH264Decoder StartDecoder(string p_sessionId, int p_attempt)
        {
            bool first = true;
            Action<Bitmap> onFrame = p_bitmap =>
            {
                if (first)
                {
                    first = false;
                    Log(LogLevel.Information, "Session " + p_sessionId + ": first H.264 frame decoded.");
                }
                ClientEvent.Instance.notifyFrame(new IFrame { image = p_bitmap, sessionId = p_sessionId, encoding = "H264" });
            };
            Action<string> onMessage = p_message => Log(LogLevel.Warning, "Session " + p_sessionId + ": " + p_message);

            try
            {
                var decoder = new FfmpegH264Decoder(m_ffmpegPath, Config.VIDEO_WIDTH, Config.VIDEO_HEIGHT, onFrame, onMessage);
                if (p_attempt > 1)
                    Log(LogLevel.Information, "Session " + p_sessionId + ": H.264 decoder restarted (attempt " + p_attempt + ").");
                return decoder;
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, "Session " + p_sessionId + ": could not start " + m_ffmpegPath + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Stops the decoder of the given session (null = whichever is running).</summary>
        private void StopDecoder(string p_sessionId)
        {
            FfmpegH264Decoder decoder;
            string sessionId;
            lock (m_decoderLock)
            {
                if (m_decoder == null) return;
                if (p_sessionId != null && !String.Equals(m_decoderSessionId, p_sessionId, StringComparison.Ordinal)) return;
                decoder = m_decoder;
                sessionId = m_decoderSessionId;
                m_decoder = null;
            }
            FinishDecoder(decoder, sessionId);
        }

        private void FinishDecoder(FfmpegH264Decoder p_decoder, string p_sessionId)
        {
            if (p_decoder == null) return;
            p_decoder.Dispose();
            Log(LogLevel.Information, "Session " + p_sessionId + ": H.264 decoder stopped after " +
                p_decoder.FramesDecoded + " frames.");
        }

        // ---------------------------------------------------------------- sessions

        private void OnSessionMessage(string p_sessionId, string p_description)
        {
            bool isNew;
            lock (m_sessionLock)
            {
                isNew = !String.Equals(p_sessionId, m_currentSessionId, StringComparison.Ordinal);
                if (isNew)
                {
                    m_currentSessionId = p_sessionId;
                    m_sessionMessages = 0;
                }
                m_sessionMessages++;
            }
            if (isNew) Log(LogLevel.Information, "Session " + p_sessionId + " started: receiving " + p_description + ".");
        }

        private void OnSessionResult(string p_sessionId)
        {
            long messages = -1;
            lock (m_sessionLock)
            {
                if (String.Equals(p_sessionId, m_currentSessionId, StringComparison.Ordinal))
                {
                    messages = m_sessionMessages;
                    m_sessionMessages = 0;
                }
            }
            if (messages >= 0)
                Log(LogLevel.Information, "Session " + p_sessionId + " ended after " + messages + " live messages.");
            else
                Log(LogLevel.Information, "Result arrived for session " + p_sessionId + ", whose live stream this client did not receive.");
        }

        private static string Describe(TcpClient p_client)
        {
            try
            {
                var endPoint = (IPEndPoint)p_client.Client.RemoteEndPoint;
                IPAddress address = endPoint.Address.IsIPv4MappedToIPv6 ? endPoint.Address.MapToIPv4() : endPoint.Address;
                return address + ":" + endPoint.Port;
            }
            catch (Exception) { return "an unknown address"; }
        }

        private static void Log(LogLevel p_level, string p_message)
        {
            ClientEvent.Instance.log(p_level, LOG_SOURCE, p_message);
        }

        public void Dispose()
        {
            if (m_stopping) return;
            m_stopping = true;
            if (m_listener != null) m_listener.Stop();

            TcpClient active;
            Thread reader;
            lock (m_connectionLock)
            {
                active = m_activeClient;
                reader = m_activeReader;
                m_activeClient = null;
                m_activeReader = null;
            }
            if (active != null) active.Close();

            // Stopping FFmpeg also releases a reader blocked writing to it.
            StopDecoder(null);
            if (m_acceptThread != null) m_acceptThread.Join(TimeSpan.FromSeconds(2));
            if (reader != null) reader.Join(TimeSpan.FromSeconds(2));
        }
    }
}
