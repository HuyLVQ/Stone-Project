using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Stone_Client.Event;

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

        internal IotStreamListener(int p_port)
        {
            m_port = p_port;
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
                    Dispatch(envelope);
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
                    // Phase 3: decode image.JpegImage and publish it to the picture box.
                    break;

                case IotPayloadKind.VideoChunk:
                    IotVideoChunk chunk = p_envelope.VideoChunk;
                    OnSessionMessage(chunk.SessionId, chunk.Codec + " over " + chunk.Container + " chunks");
                    // Phase 4: feed chunk.Data to the H.264 decoder.
                    break;

                case IotPayloadKind.Result:
                    IotInferenceResult result = p_envelope.Result;
                    OnSessionResult(result.SessionId);
                    ClientEvent.Instance.notifyResult(IotProtocol.ToResult(result));
                    break;

                default:
                    Log(LogLevel.Warning, "Received a record without a payload (schema version " +
                        p_envelope.SchemaVersion + "); ignored.");
                    break;
            }
        }

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

            if (m_acceptThread != null) m_acceptThread.Join(TimeSpan.FromSeconds(2));
            if (reader != null) reader.Join(TimeSpan.FromSeconds(2));
        }
    }
}
