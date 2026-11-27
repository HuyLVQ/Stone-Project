using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stone_Application.Event;

namespace Stone_Application.Networking.Iot
{
    public sealed class IotServer : IDisposable
    {
        private enum DeliveryMode { Stopped, Images, ResultPending }

        private static readonly Lazy<IotServer> s_instance = new Lazy<IotServer>(() => new IotServer());
        private readonly ConcurrentDictionary<string, IotClientConnection> m_clients =
            new ConcurrentDictionary<string, IotClientConnection>(StringComparer.Ordinal);
        private readonly object m_lifecycleLock = new object();
        private readonly object m_publishLock = new object();
        private readonly object m_registryLock = new object();
        private readonly object m_encoderLock = new object();
        private static readonly TimeSpan s_clientLease = TimeSpan.FromSeconds(30);
        private UdpClient m_listener;
        private CancellationTokenSource m_stop;
        private Task m_listenerTask;
        private Task m_expirationTask;
        private DeliveryMode m_mode = DeliveryMode.Stopped;
        private long m_sessionToken;
        private long m_sequence;
        private long m_lastImagePublishTimestamp;
        private static readonly long s_minimumImageIntervalTicks = Math.Max(1, Stopwatch.Frequency / 5);
        private string m_ffmpegPath;
        private FfmpegH264Encoder m_h264Encoder;
        private bool m_h264EncoderAttempted;
        private readonly TsStreamHeaders m_streamHeaders = new TsStreamHeaders();   // Guarded by m_publishLock

        public static IotServer Instance { get { return s_instance.Value; } }

        private IotServer() { }

        public void Start()
        {
            lock (m_lifecycleLock)
            {
                if (m_listener != null) return;
                m_stop = new CancellationTokenSource();
                m_ffmpegPath = FindFfmpegExecutable();
                m_listener = new UdpClient(new IPEndPoint(IPAddress.Any, IotProtocol.HandshakePort));
                m_listenerTask = Task.Run(() => ListenLoop(m_stop.Token));
                m_expirationTask = Task.Run(() => ExpireClientsLoop(m_stop.Token));
                Console.WriteLine("[INFO] [IOT] UDP handshake listener started on port " + IotProtocol.HandshakePort + ".");
            }
        }

        public void StartImageMode(long sessionToken)
        {
            lock (m_publishLock)
            {
                m_sessionToken = sessionToken;
                m_sequence = 0;
                m_lastImagePublishTimestamp = 0;
                m_mode = DeliveryMode.Images;
                foreach (IotClientConnection client in m_clients.Values) client.ClearPending();
            }
        }

        public void StopImageMode()
        {
            lock (m_publishLock)
            {
                if (m_mode == DeliveryMode.Stopped) return;
                m_mode = DeliveryMode.ResultPending;
                foreach (IotClientConnection client in m_clients.Values) client.StopImageDelivery();
            }
        }

        internal void PublishImage(IImage image)
        {
            IotClientConnection[] jpegClients;
            IotClientConnection[] h264Clients;
            FfmpegH264Encoder h264Encoder;
            lock (m_publishLock)
            {
                if (m_mode != DeliveryMode.Images || image == null || image.recvImage == null ||
                    image.liveSessionToken != m_sessionToken)
                    return;

                long now = Stopwatch.GetTimestamp();
                if (m_lastImagePublishTimestamp != 0 &&
                    now - m_lastImagePublishTimestamp < s_minimumImageIntervalTicks)
                    return;

                jpegClients = m_clients.Values.Where(client => !client.UsesH264).ToArray();
                h264Clients = m_clients.Values.Where(client => client.UsesH264).ToArray();
                if (jpegClients.Length == 0 && h264Clients.Length == 0) return;
                m_lastImagePublishTimestamp = now;
                h264Encoder = m_h264Encoder;
            }

            if (jpegClients.Length > 0)
            {
                try
                {
                    byte[] jpegImage = IotProtocol.CompressAnnotatedImage(image.recvImage);
                    lock (m_publishLock)
                    {
                        if (m_mode == DeliveryMode.Images && image.liveSessionToken == m_sessionToken)
                        {
                            byte[] message = IotProtocol.Frame(IotProtocol.CreateImageEnvelope(image, jpegImage, ++m_sequence));
                            foreach (IotClientConnection client in jpegClients) client.QueueImage(message);
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("[WARN] [IOT] JPEG image delivery failed: " + ex.Message); }
            }

            if (h264Clients.Length > 0 && h264Encoder != null)
            {
                try { h264Encoder.WriteFrame(image.recvImage); }
                catch (Exception ex) { Console.WriteLine("[WARN] [IOT] H.264 frame encoding failed: " + ex.Message); }
            }
        }

        public void PublishFinalResult(AIHelper.VideoInferenceResult result, long sessionToken)
        {
            if (result == null) return;
            lock (m_publishLock)
            {
                if (m_mode != DeliveryMode.ResultPending || sessionToken != m_sessionToken) return;
                long resultSequence = ++m_sequence;
                foreach (IotClientConnection client in m_clients.Values)
                {
                    byte[] message = IotProtocol.Frame(IotProtocol.CreateResultEnvelope(
                        result, sessionToken, resultSequence, client.ImageEncoding));
                    client.QueueResult(message);
                }
            }
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UdpClient listener = m_listener;
                if (listener == null) break;
                UdpReceiveResult packet;
                try { packet = await listener.ReceiveAsync().ConfigureAwait(false); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!cancellationToken.IsCancellationRequested) continue; else break; }

                if (packet.Buffer.Length == 0 || packet.Buffer.Length > IotProtocol.MaxHandshakeBytes) continue;
                string message = Encoding.UTF8.GetString(packet.Buffer).Trim('\0', '\r', '\n', ' ');
                string clientId;
                IotImageEncoding requestedEncoding;
                IPAddress address = IotProtocol.NormalizeAddress(packet.RemoteEndPoint.Address);
                if (!IotProtocol.TryParseHandshake(message, address, out clientId, out requestedEncoding)) continue;

                IotImageEncoding negotiatedEncoding = RegisterClient(clientId, address, requestedEncoding);
                string encodingName = negotiatedEncoding == IotImageEncoding.H264 ? "H264_MPEG_TS" : "JPEG";
                byte[] response = Encoding.UTF8.GetBytes("SERVER CONFIRMED - client id: " + clientId +
                    "; tcp port: " + IotProtocol.ClientTcpPort + "; image encoding: " + encodingName);
                try { await listener.SendAsync(response, response.Length, packet.RemoteEndPoint).ConfigureAwait(false); }
                catch (ObjectDisposedException) { break; }
                catch (SocketException ex) { Console.WriteLine("[WARN] [IOT] Handshake confirmation failed: " + ex.Message); }
            }
        }

        private async Task ExpireClientsLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    DateTime now = DateTime.UtcNow;
                    lock (m_registryLock)
                    {
                        foreach (var entry in m_clients)
                        {
                            if (entry.Value.IsLeaseExpired(now, s_clientLease))
                            {
                                IotClientConnection expired;
                                if (m_clients.TryRemove(entry.Key, out expired))
                                {
                                    expired.Dispose();
                                    Console.WriteLine("[INFO] [IOT] Expired client " + entry.Key + " after its handshake lease elapsed.");
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private IotImageEncoding RegisterClient(string clientId, IPAddress address, IotImageEncoding requestedEncoding)
        {
            string addressText = address.ToString();
            IotImageEncoding negotiatedEncoding = requestedEncoding == IotImageEncoding.H264 && EnsureH264Encoder()
                ? IotImageEncoding.H264
                : IotImageEncoding.Jpeg;

            lock (m_registryLock)
            {
                IotClientConnection existing;
                if (m_clients.TryGetValue(clientId, out existing))
                {
                    if (existing.ImageEncoding == negotiatedEncoding)
                    {
                        // A client on several networks (or on this machine, via loopback)
                        // can reach us from more than one address. Recreating the
                        // connection would drop its live stream and any pending result,
                        // so keep it and dial the newest address from now on.
                        if (!String.Equals(existing.Address, addressText, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("[INFO] [IOT] Client " + clientId + " moved from " + existing.Address +
                                " to " + addressText + ".");
                            existing.UpdateAddress(addressText);
                        }
                        existing.RefreshLease();
                        return negotiatedEncoding;
                    }

                    // A different encoding needs a fresh connection and, for H.264,
                    // a stream that starts at a keyframe.
                    IotClientConnection removed;
                    if (m_clients.TryRemove(clientId, out removed)) removed.Dispose();
                }

                m_clients[clientId] = new IotClientConnection(clientId, addressText, negotiatedEncoding);
            }
            Console.WriteLine("[INFO] [IOT] Registered client " + clientId + " at " + addressText + " using " + encodingName(negotiatedEncoding) + ".");
            return negotiatedEncoding;
        }

        private static string encodingName(IotImageEncoding encoding)
        {
            return encoding == IotImageEncoding.H264 ? "H.264 over MPEG-TS" : "JPEG";
        }

        private bool EnsureH264Encoder()
        {
            lock (m_encoderLock)
            {
                if (m_h264Encoder != null) return true;
                if (m_h264EncoderAttempted || String.IsNullOrWhiteSpace(m_ffmpegPath)) return false;
                m_h264EncoderAttempted = true;
                try
                {
                    m_h264Encoder = FfmpegH264Encoder.Start(m_ffmpegPath, PublishH264Chunk);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[WARN] [IOT] H.264 is unavailable; this client will use JPEG: " + ex.Message);
                    return false;
                }
            }
        }

        private void PublishH264Chunk(byte[] transportChunk)
        {
            lock (m_publishLock)
            {
                // Track the stream's PAT/PMT even between sessions: the encoder keeps
                // running, and the next session's first keyframe may need them.
                m_streamHeaders.Observe(transportChunk);
                if (m_mode != DeliveryMode.Images) return;
                // Each client frames its own envelope: one that is resyncing to a
                // keyframe may send only the tail of this chunk.
                long sequence = ++m_sequence;
                byte[] headers = m_streamHeaders.Packets;
                foreach (IotClientConnection client in m_clients.Values)
                {
                    if (client.UsesH264) client.QueueVideoChunk(transportChunk, headers, m_sessionToken, sequence);
                }
            }
        }

        private static string FindFfmpegExecutable()
        {
            string configuredPath = Environment.GetEnvironmentVariable("STONE_FFMPEG_PATH");
            if (!String.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)) return configuredPath;

            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(localPath)) return localPath;

            string pathEnvironment = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (string directory in pathEnvironment.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(directory)) continue;
                string candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        public void Stop()
        {
            lock (m_publishLock)
            {
                m_mode = DeliveryMode.Stopped;
                m_streamHeaders.Reset();        // The encoder below is discarded too
            }
            lock (m_lifecycleLock)
            {
                if (m_listener == null) return;
                m_stop.Cancel();
                m_listener.Close();
                m_listener = null;
                try { m_listenerTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
                try { m_expirationTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
                lock (m_registryLock)
                {
                    foreach (IotClientConnection client in m_clients.Values) client.Dispose();
                    m_clients.Clear();
                }
                m_stop.Dispose();
                m_stop = null;
                m_listenerTask = null;
                m_expirationTask = null;
            }

            lock (m_encoderLock)
            {
                m_h264Encoder?.Dispose();
                m_h264Encoder = null;
                m_h264EncoderAttempted = false;
                m_ffmpegPath = null;
            }
        }

        public void Dispose() { Stop(); }
    }
}
