using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Stone_Application.Networking.Iot
{
    /// <summary>
    /// Delivers framed envelopes to one client over a TCP connection that this
    /// server dials.
    ///
    /// JPEG frames and the final result use a latest-only slot: an undelivered
    /// frame is simply replaced by a newer one. H.264 chunks are pieces of one
    /// continuous MPEG-TS stream, so they are queued in order. If that queue
    /// exceeds its byte budget, or a send fails, the queued chunks are dropped
    /// and delivery resumes at the next keyframe so the client's decoder
    /// restarts cleanly instead of receiving a corrupted stream.
    /// </summary>
    internal sealed class IotClientConnection : IDisposable
    {
        // About four seconds of video at the encoder's 2 Mbit/s rate.
        private const int MaxQueuedVideoBytes = 1024 * 1024;

        private sealed class OutboundFrame
        {
            internal byte[] Bytes;
            internal bool IsFinalResult;
            internal bool IsVideo;
            internal long Generation;       // m_generation when the frame was taken for sending
        }

        private readonly string m_clientId;
        private volatile string m_address;
        private readonly CancellationTokenSource m_stop = new CancellationTokenSource();
        private readonly Task m_worker;
        private readonly object m_queueLock = new object();
        private readonly object m_activeClientLock = new object();
        private OutboundFrame m_latest;                                     // Guarded by m_queueLock
        private readonly Queue<byte[]> m_video = new Queue<byte[]>();       // Guarded by m_queueLock
        private int m_videoBytes;                                           // Guarded by m_queueLock
        private bool m_videoNeedsKeyframe = true;                           // Guarded by m_queueLock
        private long m_generation;                                          // Guarded by m_queueLock; bumped by ClearPending
        private TcpClient m_activeClient;
        private bool m_activeIsResult;
        private long m_lastSeenUtcTicks;
        private volatile bool m_disposed;

        internal IotClientConnection(string clientId, string address, IotImageEncoding imageEncoding)
        {
            m_clientId = clientId;
            m_address = address;
            ImageEncoding = imageEncoding;
            RefreshLease();
            m_worker = Task.Run((Action)SendLoop);
        }

        internal string ClientId { get { return m_clientId; } }
        internal string Address { get { return m_address; } }
        internal IotImageEncoding ImageEncoding { get; private set; }
        internal bool UsesH264 { get { return ImageEncoding == IotImageEncoding.H264; } }

        /// <summary>
        /// Points later connection attempts at a new address. The current TCP
        /// connection and everything queued (including an undelivered result)
        /// are kept; if the old address stops working, the next send fails and
        /// the reconnect dials the new one.
        /// </summary>
        internal void UpdateAddress(string address)
        {
            m_address = address;
        }

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

        /// <summary>
        /// Queues one chunk of whole MPEG-TS packets. While the client needs a
        /// keyframe (new client, new session, overflow or failed send), chunks
        /// are skipped until one contains a keyframe, which is sent from there.
        /// </summary>
        /// <param name="streamHeaders">The stream's latest PAT and PMT packets (or null),
        /// sent ahead of the keyframe when the chunk does not start with them.</param>
        internal void QueueVideoChunk(byte[] transportChunk, byte[] streamHeaders, long sessionToken, long sequence)
        {
            lock (m_queueLock)
            {
                if (m_disposed || transportChunk == null || transportChunk.Length == 0) return;

                if (!m_videoNeedsKeyframe && m_videoBytes + transportChunk.Length > MaxQueuedVideoBytes)
                {
                    DropVideoLocked();
                    Console.WriteLine("[WARN] [IOT] Video backlog for " + m_clientId + " exceeded " +
                        (MaxQueuedVideoBytes / 1024) + " KB; skipping to the next keyframe.");
                }

                byte[] data = transportChunk;
                if (m_videoNeedsKeyframe)
                {
                    int offset = IotProtocol.FindKeyframeOffset(transportChunk);
                    if (offset < 0) return;
                    bool startsWithPat = IotProtocol.TsPid(transportChunk, offset) == 0;
                    int prefix = startsWithPat || streamHeaders == null ? 0 : streamHeaders.Length;
                    if (offset > 0 || prefix > 0)
                    {
                        data = new byte[prefix + transportChunk.Length - offset];
                        if (prefix > 0) Buffer.BlockCopy(streamHeaders, 0, data, 0, prefix);
                        Buffer.BlockCopy(transportChunk, offset, data, prefix, transportChunk.Length - offset);
                    }
                    m_videoNeedsKeyframe = false;
                }

                byte[] message = IotProtocol.Frame(IotProtocol.CreateH264ChunkEnvelope(data, sessionToken, sequence));
                m_video.Enqueue(message);
                m_videoBytes += message.Length;
                Monitor.Pulse(m_queueLock);
            }
        }

        /// <summary>
        /// Drops every undelivered frame, result and video chunk. A send already
        /// in progress is abandoned too: if it fails, it is not put back.
        /// </summary>
        internal void ClearPending()
        {
            lock (m_queueLock)
            {
                m_generation++;
                m_latest = null;
                DropVideoLocked();
            }
        }

        internal void StopImageDelivery()
        {
            ClearPending();
            lock (m_activeClientLock)
            {
                if (m_activeClient != null && !m_activeIsResult)
                    m_activeClient.Close();
            }
        }

        private void QueueLatest(OutboundFrame frame)
        {
            lock (m_queueLock)
            {
                if (m_disposed) return;
                m_latest = frame;
                Monitor.Pulse(m_queueLock);
            }
        }

        private void DropVideoLocked()
        {
            m_video.Clear();
            m_videoBytes = 0;
            m_videoNeedsKeyframe = true;
        }

        private OutboundFrame TakeNext()
        {
            lock (m_queueLock)
            {
                while (!m_disposed && m_latest == null && m_video.Count == 0)
                    Monitor.Wait(m_queueLock);
                if (m_disposed) return null;

                if (m_latest != null)
                {
                    OutboundFrame latest = m_latest;
                    m_latest = null;
                    latest.Generation = m_generation;
                    return latest;
                }

                byte[] chunk = m_video.Dequeue();
                m_videoBytes -= chunk.Length;
                return new OutboundFrame { Bytes = chunk, IsVideo = true, Generation = m_generation };
            }
        }

        private void SendLoop()
        {
            TcpClient client = null;
            NetworkStream stream = null;
            while (!m_stop.IsCancellationRequested)
            {
                OutboundFrame frame = TakeNext();
                if (frame == null) break;

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
                        // Keep the final result pending for a later reconnect, unless a
                        // new session started meanwhile (ClearPending) or something newer
                        // was queued: an old result must not replace the new session's frames.
                        lock (m_queueLock)
                        {
                            if (!m_disposed && m_latest == null && frame.Generation == m_generation) m_latest = frame;
                        }
                        if (m_stop.Token.WaitHandle.WaitOne(1000)) break;
                    }
                    else
                    {
                        // A video chunk may have been partly written, and the client's
                        // decoder has now lost data: restart it at the next keyframe.
                        if (frame.IsVideo)
                        {
                            // After ClearPending the new session already restarts at a keyframe,
                            // and its chunks queued since then are intact.
                            lock (m_queueLock)
                            {
                                if (frame.Generation == m_generation) DropVideoLocked();
                            }
                        }
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
                m_latest = null;
                DropVideoLocked();
                Monitor.PulseAll(m_queueLock);
            }
            lock (m_activeClientLock)
            {
                if (m_activeClient != null) m_activeClient.Close();
            }
            m_stop.Cancel();
            bool stopped = false;
            try { stopped = m_worker.Wait(TimeSpan.FromSeconds(2)); } catch { }
            // A worker still blocked in a send may yet touch the token source.
            if (stopped) m_stop.Dispose();
        }
    }
}
