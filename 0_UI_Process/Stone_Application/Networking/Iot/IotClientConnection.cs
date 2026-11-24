using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Stone_Application.Networking.Iot
{
    internal sealed class IotClientConnection : IDisposable
    {
        private sealed class OutboundFrame
        {
            internal byte[] Bytes;
            internal bool IsFinalResult;
        }

        private readonly string m_clientId;
        private readonly string m_address;
        private readonly BlockingCollection<OutboundFrame> m_queue = new BlockingCollection<OutboundFrame>(1);
        private readonly CancellationTokenSource m_stop = new CancellationTokenSource();
        private readonly Task m_worker;
        private readonly object m_queueLock = new object();
        private readonly object m_activeClientLock = new object();
        private TcpClient m_activeClient;
        private bool m_activeIsResult;
        private long m_lastSeenUtcTicks;
        private volatile bool m_disposed;

        internal IotClientConnection(string clientId, string address)
        {
            m_clientId = clientId;
            m_address = address;
            RefreshLease();
            m_worker = Task.Run((Action)SendLoop);
        }

        internal string ClientId { get { return m_clientId; } }
        internal string Address { get { return m_address; } }

        internal void RefreshLease()
        {
            Interlocked.Exchange(ref m_lastSeenUtcTicks, DateTime.UtcNow.Ticks);
        }

        internal bool IsLeaseExpired(DateTime utcNow, TimeSpan lease)
        {
            long lastSeenTicks = Interlocked.Read(ref m_lastSeenUtcTicks);
            return utcNow.Ticks - lastSeenTicks > lease.Ticks;
        }

        internal void QueueImage(byte[] framedMessage)
        {
            QueueLatest(new OutboundFrame { Bytes = framedMessage, IsFinalResult = false });
        }

        internal void QueueResult(byte[] framedMessage)
        {
            QueueLatest(new OutboundFrame { Bytes = framedMessage, IsFinalResult = true });
        }

        internal void ClearPendingImages()
        {
            lock (m_queueLock)
            {
                OutboundFrame queued;
                while (m_queue.TryTake(out queued)) { }
            }
        }

        internal void StopImageDelivery()
        {
            ClearPendingImages();
            lock (m_activeClientLock)
            {
                if (m_activeClient != null && !m_activeIsResult)
                    m_activeClient.Close();
            }
        }

        private void QueueLatest(OutboundFrame frame)
        {
            if (m_disposed) return;
            lock (m_queueLock)
            {
                OutboundFrame queued;
                while (m_queue.TryTake(out queued)) { }
                if (!m_disposed) m_queue.TryAdd(frame);
            }
        }

        private void SendLoop()
        {
            TcpClient client = null;
            NetworkStream stream = null;
            while (!m_stop.IsCancellationRequested)
            {
                OutboundFrame frame;
                try { frame = m_queue.Take(m_stop.Token); }
                catch (OperationCanceledException) { break; }
                catch (InvalidOperationException) { break; }

                try
                {
                    if (client == null)
                    {
                        client = new TcpClient();
                        lock (m_activeClientLock)
                        {
                            m_activeClient = client;
                            m_activeIsResult = frame.IsFinalResult;
                        }
                        Task connect = client.ConnectAsync(m_address, IotProtocol.ClientTcpPort);
                        if (!connect.Wait(TimeSpan.FromSeconds(2)))
                            throw new TimeoutException("TCP client connection timed out.");
                        if (connect.IsFaulted) throw connect.Exception.GetBaseException();

                        client.SendTimeout = 5000;
                        client.NoDelay = true;
                        stream = client.GetStream();
                    }

                    lock (m_activeClientLock)
                    {
                        m_activeClient = client;
                        m_activeIsResult = frame.IsFinalResult;
                    }
                    stream.Write(frame.Bytes, 0, frame.Bytes.Length);
                    stream.Flush();
                }
                catch (Exception ex)
                {
                    CloseClient(ref client, ref stream);
                    if (frame.IsFinalResult && !m_stop.IsCancellationRequested)
                    {
                        // Keep the final result pending for a later reconnect.
                        lock (m_queueLock)
                        {
                            OutboundFrame queued;
                            while (m_queue.TryTake(out queued)) { }
                            if (!m_disposed) m_queue.TryAdd(frame);
                        }
                        if (m_stop.Token.WaitHandle.WaitOne(1000)) break;
                    }
                    else
                    {
                        Console.WriteLine("[WARN] [IOT] Image delivery failed for " + m_clientId + ": " + ex.Message);
                    }
                }
            }

            CloseClient(ref client, ref stream);
        }

        private void CloseClient(ref TcpClient client, ref NetworkStream stream)
        {
            TcpClient closed = client;
            client = null;
            stream = null;
            lock (m_activeClientLock)
            {
                if (ReferenceEquals(m_activeClient, closed)) m_activeClient = null;
            }
            if (closed != null) closed.Close();
        }

        public void Dispose()
        {
            lock (m_queueLock)
            {
                if (m_disposed) return;
                m_disposed = true;
                OutboundFrame queued;
                while (m_queue.TryTake(out queued)) { }
                m_queue.CompleteAdding();
            }
            lock (m_activeClientLock)
            {
                if (m_activeClient != null) m_activeClient.Close();
            }
            m_stop.Cancel();
            try { m_worker.Wait(TimeSpan.FromSeconds(2)); } catch { }
            m_queue.Dispose();
            m_stop.Dispose();
        }
    }
}
