using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        public static IotServer Instance { get { return s_instance.Value; } }

        private IotServer() { }

        public void Start()
        {
            lock (m_lifecycleLock)
            {
                if (m_listener != null) return;
                m_stop = new CancellationTokenSource();
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
                foreach (IotClientConnection client in m_clients.Values) client.ClearPendingImages();
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
            lock (m_publishLock)
            {
                if (m_mode != DeliveryMode.Images || image == null || image.recvImage == null ||
                    image.liveSessionToken != m_sessionToken)
                    return;

                long now = Stopwatch.GetTimestamp();
                if (m_lastImagePublishTimestamp != 0 &&
                    now - m_lastImagePublishTimestamp < s_minimumImageIntervalTicks)
                    return;

                byte[] jpegImage = IotProtocol.CompressAnnotatedImage(image.recvImage);
                byte[] message = IotProtocol.Frame(IotProtocol.CreateImageEnvelope(image, jpegImage, ++m_sequence));
                m_lastImagePublishTimestamp = now;
                foreach (IotClientConnection client in m_clients.Values) client.QueueImage(message);
            }
        }

        public void PublishFinalResult(AIHelper.VideoInferenceResult result, long sessionToken)
        {
            if (result == null) return;
            lock (m_publishLock)
            {
                if (m_mode != DeliveryMode.ResultPending || sessionToken != m_sessionToken) return;
                byte[] message = IotProtocol.Frame(IotProtocol.CreateResultEnvelope(result, sessionToken, ++m_sequence));
                foreach (IotClientConnection client in m_clients.Values) client.QueueResult(message);
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
                IPAddress address = IotProtocol.NormalizeAddress(packet.RemoteEndPoint.Address);
                if (!IotProtocol.TryParseHandshake(message, address, out clientId)) continue;

                RegisterClient(clientId, address);
                byte[] response = Encoding.UTF8.GetBytes("SERVER CONFIRMED - client id: " + clientId + "; tcp port: " + IotProtocol.ClientTcpPort);
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

        private void RegisterClient(string clientId, IPAddress address)
        {
            string addressText = address.ToString();
            lock (m_registryLock)
            {
                IotClientConnection existing;
                if (m_clients.TryGetValue(clientId, out existing))
                {
                    if (String.Equals(existing.Address, addressText, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.RefreshLease();
                        Console.WriteLine("[INFO] [IOT] Refreshed client " + clientId + " at " + addressText + ".");
                        return;
                    }

                    IotClientConnection removed;
                    if (m_clients.TryRemove(clientId, out removed)) removed.Dispose();
                }

                m_clients[clientId] = new IotClientConnection(clientId, addressText);
            }
            Console.WriteLine("[INFO] [IOT] Registered client " + clientId + " at " + addressText + ".");
        }

        public void Stop()
        {
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

            lock (m_publishLock) m_mode = DeliveryMode.Stopped;
        }

        public void Dispose() { Stop(); }
    }
}
