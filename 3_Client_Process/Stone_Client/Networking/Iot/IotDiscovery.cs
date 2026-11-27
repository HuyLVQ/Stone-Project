using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Stone_Client.Event;

namespace Stone_Client.Networking.Iot
{
    /// <summary>
    /// Announces this client to the server over UDP and keeps the server's
    /// 30-second registration lease alive.
    ///
    /// Searching: the handshake is sent every few seconds, either to the
    /// configured server or as a broadcast on every local IPv4 network.
    /// Online: once a server confirms, the handshake is unicast to that server
    /// only (so the server always sees the same source address and does not
    /// re-register us), every HandshakeIntervalSeconds. After
    /// OfflineAfterMissedHandshakes unanswered handshakes the server is
    /// reported offline and searching resumes.
    /// </summary>
    internal sealed class IotDiscovery : IDisposable
    {
        private const string LOG_SOURCE = "Discovery";
        private const int SIO_UDP_CONNRESET = -1744830452;
        private const int RECEIVE_POLL_MS = 250;

        private readonly string m_clientId;
        private readonly IPAddress m_configuredServer;
        private readonly int m_handshakePort;
        private readonly int m_clientTcpPort;
        private readonly IotImageEncoding m_requestedEncoding;
        private readonly CancellationTokenSource m_stop = new CancellationTokenSource();
        private UdpClient m_udp;
        private Thread m_thread;

        private IPEndPoint m_lockedServer;      // Server that last confirmed us (null while searching)
        private IotImageEncoding m_lastEncoding;
        private int m_missed;
        private bool m_online;
        private bool m_sendFailureReported;

        internal IotDiscovery(string p_clientId, IPAddress p_configuredServer, int p_handshakePort,
                              int p_clientTcpPort, IotImageEncoding p_requestedEncoding)
        {
            m_clientId = p_clientId;
            m_configuredServer = p_configuredServer;
            m_handshakePort = p_handshakePort;
            m_clientTcpPort = p_clientTcpPort;
            m_requestedEncoding = p_requestedEncoding;
        }

        internal void Start()
        {
            if (m_thread != null) return;
            m_udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
            // Without this, an ICMP "port unreachable" (no server yet) makes the next
            // Receive throw WSAECONNRESET on Windows.
            try { m_udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null); }
            catch (SocketException) { }
            m_udp.Client.ReceiveTimeout = RECEIVE_POLL_MS;

            m_thread = new Thread(Run) { IsBackground = true, Name = "IoT discovery" };
            m_thread.Start();

            Log(LogLevel.Information, m_configuredServer != null
                ? "Searching for the server at " + m_configuredServer + ":" + m_handshakePort + " (requesting " + Name(m_requestedEncoding) + ")."
                : "Searching for the server by broadcast on UDP port " + m_handshakePort + " (requesting " + Name(m_requestedEncoding) + ").");
        }

        private void Run()
        {
            byte[] handshake = Encoding.UTF8.GetBytes(IotProtocol.CreateHandshake(m_clientId, m_requestedEncoding));
            CancellationToken token = m_stop.Token;

            while (!token.IsCancellationRequested)
            {
                Send(handshake);
                bool confirmed = WaitForConfirmation(token);
                if (token.IsCancellationRequested) break;

                if (!confirmed) OnMissed();

                // Refresh slowly while healthy; retry quickly while searching or after a miss.
                int delaySeconds = m_online && m_missed == 0 ? Config.HandshakeIntervalSeconds : Config.SEARCH_RETRY_SECONDS;
                int remainingMs = delaySeconds * 1000 - Config.HANDSHAKE_REPLY_TIMEOUT_MS;
                if (remainingMs > 0 && token.WaitHandle.WaitOne(remainingMs)) break;
            }
        }

        private void Send(byte[] p_handshake)
        {
            List<IPEndPoint> targets = Targets();
            int sent = 0;
            string lastError = null;
            foreach (IPEndPoint target in targets)
            {
                try { m_udp.Send(p_handshake, p_handshake.Length, target); sent++; }
                catch (SocketException ex) { lastError = target + ": " + ex.Message; }
                catch (ObjectDisposedException) { return; }
            }

            if (sent == 0 && !m_sendFailureReported)
            {
                m_sendFailureReported = true;
                Log(LogLevel.Warning, "Could not send the handshake (" + (lastError ?? "no network") + "). Retrying.");
            }
            else if (sent > 0)
            {
                m_sendFailureReported = false;
            }
        }

        private List<IPEndPoint> Targets()
        {
            if (m_configuredServer != null) return new List<IPEndPoint> { new IPEndPoint(m_configuredServer, m_handshakePort) };
            if (m_lockedServer != null) return new List<IPEndPoint> { m_lockedServer };

            var addresses = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily != AddressFamily.InterNetwork || info.IPv4Mask == null) continue;
                        byte[] address = info.Address.GetAddressBytes();
                        byte[] mask = info.IPv4Mask.GetAddressBytes();
                        if (mask.Length != 4) continue;
                        for (int index = 0; index < 4; index++) address[index] = (byte)(address[index] | ~mask[index]);
                        addresses.Add(new IPAddress(address));
                    }
                }
            }
            catch (NetworkInformationException) { }

            var targets = new List<IPEndPoint>();
            foreach (IPAddress address in addresses) targets.Add(new IPEndPoint(address, m_handshakePort));
            return targets;
        }

        private bool WaitForConfirmation(CancellationToken p_token)
        {
            var timer = Stopwatch.StartNew();
            bool confirmed = false;
            // Keep draining until the reply window closes: a broadcast can be answered
            // more than once (e.g. via loopback and the LAN).
            while (!p_token.IsCancellationRequested && timer.ElapsedMilliseconds < Config.HANDSHAKE_REPLY_TIMEOUT_MS)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] reply;
                try { reply = m_udp.Receive(ref remote); }
                catch (SocketException) { continue; }        // Timeout poll or transient error
                catch (ObjectDisposedException) { return confirmed; }

                if (confirmed || reply.Length == 0 || reply.Length > Config.MAX_HANDSHAKE_BYTES) continue;
                IotHandshakeConfirmation confirmation;
                if (!IotProtocol.TryParseConfirmation(Encoding.UTF8.GetString(reply), out confirmation)) continue;
                if (!String.Equals(confirmation.ClientId, m_clientId, StringComparison.Ordinal)) continue;

                remote = new IPEndPoint(Normalize(remote.Address), remote.Port);
                if (m_configuredServer == null && m_lockedServer != null && !m_lockedServer.Equals(remote)) continue;

                OnConfirmed(remote, confirmation);
                confirmed = true;
            }
            return confirmed;
        }

        private void OnConfirmed(IPEndPoint p_server, IotHandshakeConfirmation p_confirmation)
        {
            m_missed = 0;
            bool wasOnline = m_online;
            bool encodingChanged = wasOnline && m_lastEncoding != p_confirmation.Encoding;
            m_online = true;
            m_lastEncoding = p_confirmation.Encoding;
            if (m_configuredServer == null) m_lockedServer = p_server;
            Common.SetOnline(p_server, p_confirmation.Encoding);

            if (!wasOnline)
            {
                Log(LogLevel.Information, "Server online at " + p_server.Address + " - image encoding " +
                    Name(p_confirmation.Encoding) + ", data expected on TCP port " + p_confirmation.TcpPort + ".");
                if (m_requestedEncoding == IotImageEncoding.H264 && p_confirmation.Encoding != IotImageEncoding.H264)
                    Log(LogLevel.Warning, "The server cannot provide H.264 (no FFmpeg with libx264); it will send JPEG.");
            }
            else if (encodingChanged)
            {
                Log(LogLevel.Information, "Server changed the image encoding to " + Name(p_confirmation.Encoding) + ".");
            }

            if (p_confirmation.TcpPort != m_clientTcpPort && !wasOnline)
                Log(LogLevel.Warning, "The server will connect to TCP port " + p_confirmation.TcpPort +
                    " but this client listens on " + m_clientTcpPort + ". Align ClientTcpPort in App.config.");
        }

        private void OnMissed()
        {
            if (!m_online) return;
            m_missed++;
            if (m_missed < Config.OfflineAfterMissedHandshakes) return;

            m_online = false;
            m_missed = 0;
            IPEndPoint lost = m_lockedServer;
            m_lockedServer = null;
            Common.SetOffline();
            Log(LogLevel.Warning, "Server offline: " + Config.OfflineAfterMissedHandshakes +
                " handshakes in a row were not confirmed" + (lost != null ? " by " + lost.Address : "") + ". Searching again.");
        }

        private static IPAddress Normalize(IPAddress p_address)
        {
            return p_address.IsIPv4MappedToIPv6 ? p_address.MapToIPv4() : p_address;
        }

        private static string Name(IotImageEncoding p_encoding)
        {
            return p_encoding == IotImageEncoding.H264 ? "H.264" : "JPEG";
        }

        private static void Log(LogLevel p_level, string p_message)
        {
            ClientEvent.Instance.log(p_level, LOG_SOURCE, p_message);
        }

        public void Dispose()
        {
            if (m_stop.IsCancellationRequested) return;
            m_stop.Cancel();
            if (m_udp != null) m_udp.Close();
            if (m_thread != null && !m_thread.Join(TimeSpan.FromSeconds(2)))
            {
                // Leave the token source alive for the straggling thread.
                Log(LogLevel.Warning, "The discovery thread did not stop within 2 seconds.");
                return;
            }
            m_stop.Dispose();
        }
    }
}
