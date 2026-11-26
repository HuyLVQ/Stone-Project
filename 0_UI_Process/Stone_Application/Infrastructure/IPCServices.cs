using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Basler.Pylon;
using Stone_Application.Event;
using IImage = Stone_Application.Event.IImage;

namespace Stone_Application.IPC
{
    public class IPCServices
    {
        private static IPCServices s_instance;
        private readonly object m_ringLock = new object();
        private readonly object m_publishLock = new object();
        private int m_nextSlot;
        private int m_latestSlot = -1;
        private int m_inferenceSlot = -1;
        private bool m_sessionActive;
        private long m_sessionToken;
        private long m_sent, m_received, m_failed;
        private long m_lastIpcCaptureTimestamp;
        private readonly Func<bool> m_isAiReady;
        private readonly Action m_stopAi;
        private readonly int m_stopTimeout;
        public long SessionToken { get { lock (m_ringLock) return m_sessionActive ? m_sessionToken : 0; } }
        public long SentCount { get { lock (m_ringLock) return m_sent; } }
        public long ReceivedCount { get { lock (m_ringLock) return m_received; } }
        public long FailedCount { get { lock (m_ringLock) return m_failed; } }
        public bool IsCurrentSession(long token) { lock (m_ringLock) return m_sessionActive && token == m_sessionToken; }
        private readonly ManualResetEventSlim m_noInferenceInFlight = new ManualResetEventSlim(true);
        private readonly BlockingCollection<CaptureFrame> m_captureQueue = new BlockingCollection<CaptureFrame>(Config.IPC_CAPTURE_QUEUE_CAPACITY);
        // In Bayer8 mode, frames selected for live inference are converted to BGR8 here, off the camera thread.
        // PixelDataConverter is not thread-safe, so conversion and its scratch buffer share one lock.
        // Both are created on first use, so Bgr8 mode never allocates them.
        private readonly object m_convertLock = new object();
        private PixelDataConverter m_bgrConverter;
        private byte[] m_bgrFrame;

        private IPCServices() : this("", () => AIHelper.IsLiveReady && Common.pythonProcess != null && !Common.pythonProcess.HasExited,
            StopLiveAi, Config.AI_STOP_TIMEOUT_MS) { }

        // Injectable dependencies and resource suffix isolate the hardware-free protocol harness.
        private IPCServices(string suffix, Func<bool> isAiReady, Action stopAi, int stopTimeout)
        {
            m_isAiReady = isAiReady; m_stopAi = stopAi; m_stopTimeout = stopTimeout;
            Common.ui2aiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.UI_2_AI_EVENT_TAGNAME + suffix);
            Common.ai2uiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.AI_2_UI_EVENT_TAGNAME + suffix);

            Common.mmf = MemoryMappedFile.CreateOrOpen(Config.MMF_TAGNAME + suffix, Config.MAP_SIZE);
            Task.Run((Action)CapturePublishLoop);
        }

        private sealed class CaptureFrame
        {
            public PooledFrameBuffer Image;
            public long SessionToken;
        }

        // Reserve a monotonic 10 FPS IPC cadence independently of the camera and
        // recording cadence. The timestamp is reset for every session.
        public bool TryReserveCaptureFrame(long sessionToken)
        {
            long now = Stopwatch.GetTimestamp();
            long intervalTicks = Math.Max(1L,
                (long)Stopwatch.Frequency * Config.IPC_CAPTURE_INTERVAL_MS / 1000L);
            lock (m_ringLock)
            {
                if (!m_sessionActive || sessionToken != m_sessionToken)
                    return false;
                if (m_lastIpcCaptureTimestamp != 0 &&
                    now - m_lastIpcCaptureTimestamp < intervalTicks)
                    return false;
                m_lastIpcCaptureTimestamp = now;
                return true;
            }
        }

        // Keep shared-memory writes off the camera thread. The bounded worker
        // drops the oldest pending frame if publication falls behind.
        public bool QueueCaptureFrame(PooledFrameBuffer image, long sessionToken)
        {
            if (image == null || image.Length != Config.RAW_FRAME_BYTE_COUNT)
                throw new ArgumentException("Camera frame size does not match the configured image dimensions.", nameof(image));
            if (!IsCurrentSession(sessionToken)) return false;
            var frame = new CaptureFrame { Image = image, SessionToken = sessionToken };
            image.AddReference();
            bool accepted = false;
            try
            {
                accepted = m_captureQueue.TryAdd(frame);
                if (accepted) return true;
                if (m_captureQueue.TryTake(out CaptureFrame discarded)) discarded.Image.Dispose();
                accepted = m_captureQueue.TryAdd(frame);
                return accepted;
            }
            finally { if (!accepted) image.Dispose(); }
        }

        private void CapturePublishLoop()
        {
            foreach (CaptureFrame frame in m_captureQueue.GetConsumingEnumerable())
            {
                try { captureFrame(frame.Image, frame.SessionToken); }
                catch (Exception ex) { Console.WriteLine("[ERROR] [IPC] Frame publication failed: " + ex.Message); }
                finally { frame.Image.Dispose(); }
            }
        }

        private static void StopLiveAi()
        {
            var process = Common.pythonProcess;
            if (process != null && !process.HasExited)
            {
                process.Kill();
                if (!process.WaitForExit(Config.AI_EXIT_TIMEOUT_MS))
                    throw new TimeoutException("AI did not exit; shared memory was retained and START remains unavailable.");
            }
        }

        static public void IPCCleanUp()
        {
            if (Common.ui2aiEvent != null)
            {
                Common.ui2aiEvent.Dispose();
                Common.ui2aiEvent = null;
            }

            if (Common.ai2uiEvent != null)
            {
                Common.ai2uiEvent.Dispose();
                Common.ai2uiEvent = null;
            }

            if (Common.mmf != null)
            {
                Common.mmf.Dispose();
                Common.mmf = null;
            }

            if (Common.pythonProcess != null && !Common.pythonProcess.HasExited)
            {
                try
                {
                    Common.pythonProcess.Kill();
                    Common.pythonProcess.WaitForExit();
                    Console.WriteLine("[INFO] [IPC] Python process terminated.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] [IPC] Failed to terminate Python process: {ex.Message}");
                }
                Common.pythonProcess = null;
            }

            Console.WriteLine("[INFO] [IPC] IPC resources cleaned up.");
        }

        public static IPCServices getInstance()
        {
            if (s_instance == null)
            {
                s_instance = new IPCServices();
            }
            return s_instance;
        }

        public void beginSession()
        {
            lock (m_ringLock)
            {
                if (m_inferenceSlot >= 0)
                    throw new InvalidOperationException("Cannot begin a session while an inference is in flight.");

                if (!m_isAiReady())
                    throw new InvalidOperationException("Live AI must be initialized before START.");
                m_nextSlot = 0; m_latestSlot = -1;
                m_sessionToken++;
                m_sent = m_received = m_failed = 0;
                m_lastIpcCaptureTimestamp = 0;
                m_sessionActive = true;
            }
        }

        // STOP first prevents new frames/dispatches, then waits for the already
        // dispatched request to finish before it erases either shared-memory region.
        public void deactivateSession()
        {
            lock (m_publishLock) lock (m_ringLock) m_sessionActive = false;
        }

        public void PublishIfCurrent(long token, Action publish)
        {
            // STOP serializes with persistence, while no UI callback is synchronously awaited.
            lock (m_publishLock) if (IsCurrentSession(token)) publish();
        }

        // Returns false when a pending request had to be failed by stopping Python.
        // Never erase memory until the previous writer has demonstrably exited.
        public bool endSessionAndClear()
        {
            deactivateSession();
            bool settled = m_noInferenceInFlight.Wait(m_stopTimeout);
            if (!settled)
            {
                AIHelper.MarkUnavailable();
                m_stopAi();
                lock (m_ringLock)
                {
                    m_failed += m_sent - m_received - m_failed;
                    m_inferenceSlot = -1;
                    m_noInferenceInFlight.Set();
                }
            }
            lock (m_ringLock)
            {
                if (m_sent != m_received + m_failed)
                    throw new InvalidOperationException("Inference completion accounting is inconsistent.");
                clearSharedMemory();
                m_nextSlot = 0; m_latestSlot = -1; m_inferenceSlot = -1;
                Common.ui2aiEvent.Reset(); Common.ai2uiEvent.Reset();
            }
            return settled && m_failed == 0;
        }

        public bool isSessionActive()
        {
            lock (m_ringLock)
                return m_sessionActive;
        }

        // @brief: Publish a camera frame to the next free ring slot, converting it to BGR8 when needed
        // @param:
        //      * p_image: Pooled camera frame; its PixelType is BGR8packed or names the Bayer layout
        //      * sessionToken: Session that captured the frame
        // @return: True if the frame was written to shared memory
        // @details: BGR8 frames are published as-is. Bayer conversion runs before the ring lock is taken,
        //      so the lock still covers only a memory copy.
        public bool captureFrame(PooledFrameBuffer p_image, long sessionToken)
        {
            if (p_image == null || p_image.Length != Config.RAW_FRAME_BYTE_COUNT)
                throw new ArgumentException("Camera frame size does not match the configured image dimensions.", nameof(p_image));
            if (!IsCurrentSession(sessionToken))
                return false;

            if (p_image.PixelType == PixelType.BGR8packed)
                return publishBgrFrame(p_image.Buffer, sessionToken);

            lock (m_convertLock)
            {
                if (m_bgrConverter == null)
                {
                    m_bgrConverter = new PixelDataConverter { OutputPixelFormat = PixelType.BGR8packed };
                    m_bgrFrame = new byte[Config.IMAGE_BYTE_COUNT];
                }
                m_bgrConverter.Convert(m_bgrFrame, p_image.Buffer, p_image.PixelType,
                    Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT, 0, ImageOrientation.TopDown);
                return publishBgrFrame(m_bgrFrame, sessionToken);
            }
        }

        // @brief: Write one BGR8 frame into the next ring slot that is not being inferred
        // @param:
        //      * bgrFrame: Packed BGR8 pixels, IMAGE_BYTE_COUNT bytes
        //      * sessionToken: Session that captured the frame
        // @return: True if the frame was written to shared memory
        private bool publishBgrFrame(byte[] bgrFrame, long sessionToken)
        {
            lock (m_ringLock)
            {
                if (!m_sessionActive || sessionToken != m_sessionToken)
                    return false;

                // Do not overwrite the slot the AI process is currently reading.
                int slot = m_nextSlot;
                for (int i = 0; i < Config.IMAGE_BUFFER_SLOT_COUNT; i++)
                {
                    int candidate = (m_nextSlot + i) % Config.IMAGE_BUFFER_SLOT_COUNT;
                    if (candidate != m_inferenceSlot)
                    {
                        slot = candidate;
                        break;
                    }
                }

                long offset = Config.WRITE_OFFSET + Config.IMAGE_BUFFER_HEADER_SIZE + (long)slot * Config.IMAGE_BYTE_COUNT;
                using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(offset, Config.IMAGE_BYTE_COUNT))
                    stream.Write(bgrFrame, 0, Config.IMAGE_BYTE_COUNT);

                m_latestSlot = slot;
                m_nextSlot = (slot + 1) % Config.IMAGE_BUFFER_SLOT_COUNT;
                return true;
            }
        }

        public bool TryDispatch(out int slot, out long sessionToken)
        {
            lock (m_ringLock)
            {
                slot = m_latestSlot; sessionToken = m_sessionToken;
                if (!m_sessionActive || slot < 0 || m_inferenceSlot >= 0) return false;
                // Publish the request and account for it atomically with STOP gating.
                writeTask(slot);
                m_inferenceSlot = slot;
                m_noInferenceInFlight.Reset();
                m_sent++;
                try { Common.ui2aiEvent.Set(); }
                catch { m_sent--; m_inferenceSlot = -1; m_noInferenceInFlight.Set(); throw; }
                return true;
            }
        }

        public bool HasPendingRequest(long token)
        {
            lock (m_ringLock) return token == m_sessionToken && m_inferenceSlot >= 0;
        }

        public bool CompleteInference(int slot, long token, out IInformation information, out IImage image)
        {
            lock (m_ringLock)
            {
                information = null; image = null;
                if (token != m_sessionToken || slot != m_inferenceSlot) return false;
                if (!Common.ai2uiEvent.WaitOne(0)) return false;
                try
                {
                    (information, image) = readTask();
                    information.liveSessionToken = image.liveSessionToken = token;
                    Common.PrepareInformationForPersistence(information);
                    m_received++;
                    return true;
                }
                catch { m_failed++; throw; }
                finally
                {
                    // Completion does not depend on a PictureBox callback or a DB observer.
                    m_inferenceSlot = -1;
                    m_noInferenceInFlight.Set();
                }
            }
        }

        private void clearSharedMemory()
        {
            if (Common.mmf == null)
                return;

            byte[] zeroBytes = new byte[64 * 1024];
            using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(0, Config.MAP_SIZE, MemoryMappedFileAccess.ReadWrite))
            {
                long bytesRemaining = Config.MAP_SIZE;
                while (bytesRemaining > 0)
                {
                    int length = (int)Math.Min(zeroBytes.Length, bytesRemaining);
                    stream.Write(zeroBytes, 0, length);
                    bytesRemaining -= length;
                }
                stream.Flush();
            }
        }

        public void writeTask(int p_slot)
        {
            using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(Config.WRITE_OFFSET, Config.IMAGE_BUFFER_HEADER_SIZE))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, false))
                writer.Write(p_slot);
        }

        public (IInformation, IImage) readTask()
        { 
            using (MemoryMappedViewStream stream = Common.mmf.CreateViewStream(Config.READ_OFFSET, Config.RESULT_REGION_SIZE))
            {

                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    Int64 delta_perct_misang = reader.ReadInt64();
                    Int64 delta_perct_1_2 = reader.ReadInt64();
                    Int64 delta_perct_2_4 = reader.ReadInt64();
                    Int64 delta_perct_4_6 = reader.ReadInt64();
                    float measured_weight1 = reader.ReadSingle();
                    float measured_weight2 = Config.s_isDebugMode ? reader.ReadSingle() : 0.0f;
                    float measured_weight3 = Config.s_isDebugMode ? reader.ReadSingle() : 0.0f;
                    float measured_weight4 = Config.s_isDebugMode ? reader.ReadSingle() : 0.0f;

                    stream.Seek(Config.OFFSET_IMAGE, SeekOrigin.Begin);
                    byte[] image_data = reader.ReadBytes(Config.IMAGE_HEIGHT * Config.IMAGE_WIDTH * 3);

                    IInformation information = new IInformation
                    {
                        countMiSang = delta_perct_misang,
                        count1x2 = delta_perct_1_2,
                        count2x4 = delta_perct_2_4,
                        count4x6 = delta_perct_4_6,
                        measuredWeight1 = measured_weight1,
                        measuredWeight2 = measured_weight2,
                        measuredWeight3 = measured_weight3,
                        measuredWeight4 = measured_weight4
                    };

                    IImage image = new IImage
                    {
                        recvImage = image_data
                    };

                    return (information, image);
                }
            }
        }
    }
}
