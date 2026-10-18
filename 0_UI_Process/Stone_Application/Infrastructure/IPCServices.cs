using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stone_Application.Event;

namespace Stone_Application.IPC
{
    public class IPCServices
    {
        private static IPCServices s_instance;
        private readonly object m_ringLock = new object();
        private int m_nextSlot;
        private int m_latestSlot = -1;
        private int m_inferenceSlot = -1;
        private bool m_sessionActive;
        private readonly ManualResetEventSlim m_noInferenceInFlight = new ManualResetEventSlim(true);

        private IPCServices()
        {
            Common.ui2aiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.UI_2_AI_EVENT_TAGNAME);
            Common.ai2uiEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Config.AI_2_UI_EVENT_TAGNAME);

            Common.mmf = MemoryMappedFile.CreateOrOpen(Config.MMF_TAGNAME, Config.MAP_SIZE);
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

                m_nextSlot = 0;
                m_latestSlot = -1;
                m_sessionActive = true;
            }
        }

        // STOP first prevents new frames/dispatches, then waits for the already
        // dispatched request to finish before it erases either shared-memory region.
        public void endSessionAndClear()
        {
            lock (m_ringLock)
            {
                m_sessionActive = false;
            }

            m_noInferenceInFlight.Wait();

            lock (m_ringLock)
            {
                clearSharedMemory();
                m_nextSlot = 0;
                m_latestSlot = -1;
                m_inferenceSlot = -1;
            }
        }

        public bool isSessionActive()
        {
            lock (m_ringLock)
                return m_sessionActive;
        }

        public bool captureFrame(byte[] p_image)
        {
            if (p_image == null || p_image.Length != Config.IMAGE_BYTE_COUNT)
                throw new ArgumentException("Camera frame size does not match the configured image dimensions.", nameof(p_image));

            lock (m_ringLock)
            {
                if (!m_sessionActive)
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
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, false))
                    writer.Write(p_image);

                m_latestSlot = slot;
                m_nextSlot = (slot + 1) % Config.IMAGE_BUFFER_SLOT_COUNT;
                return true;
            }
        }

        public bool tryAcquireLatestFrame(out int p_slot)
        {
            lock (m_ringLock)
            {
                p_slot = m_latestSlot;
                if (!m_sessionActive || p_slot < 0 || m_inferenceSlot >= 0)
                    return false;

                m_inferenceSlot = p_slot;
                m_noInferenceInFlight.Reset();
                return true;
            }
        }

        public void releaseInferenceFrame(int p_slot)
        {
            lock (m_ringLock)
            {
                if (m_inferenceSlot == p_slot)
                {
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
