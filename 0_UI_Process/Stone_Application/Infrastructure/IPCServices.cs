using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;
using Stone.Broker;
using Stone_Application.Event;

namespace Stone_Application.IPC
{
    public sealed class IPCServices : IDisposable
    {
        private static IPCServices s_instance;
        private readonly MemoryMappedFile m_inputMap;
        private readonly MemoryMappedFile m_outputMap;
        private readonly PushSocket m_rxSocket;
        private readonly PullSocket m_txSocket;
        private readonly object m_sendLock = new object();

        private IPCServices()
        {
            m_inputMap = MemoryMappedFile.CreateOrOpen(Config.INPUT_MMF_TAGNAME, Config.MAP_SIZE);
            m_outputMap = MemoryMappedFile.CreateOrOpen(Config.OUTPUT_MMF_TAGNAME, Config.MAP_SIZE);

            m_rxSocket = new PushSocket();
            m_rxSocket.Connect(Config.BROKER_RX_ENDPOINT);
            m_txSocket = new PullSocket();
            m_txSocket.Connect(Config.BROKER_TX_ENDPOINT);
        }

        public static IPCServices getInstance()
        {
            if (s_instance == null)
                s_instance = new IPCServices();
            return s_instance;
        }

        public bool TryWriteAndSend(IImage p_image, out ulong p_imageLocation)
        {
            p_imageLocation = 0;
            if (p_image == null || p_image.recvImage == null || p_image.recvImage.Length != Config.IMAGE_BYTE_SIZE)
                return false;

            int slot;
            if (!Common.s_availableImageSlots.TryDequeue(out slot))
                return false;
            p_imageLocation = (ulong)slot;

            using (var accessor = m_inputMap.CreateViewAccessor((long)p_imageLocation, Config.IMAGE_BYTE_SIZE, MemoryMappedFileAccess.Write))
                accessor.WriteArray(0, p_image.recvImage, 0, p_image.recvImage.Length);

            var message = new RxPullMessage
            {
                MessageType = RxPullMessageType.Rawimage,
                DataPayload = new RxFrame { ImageLocation = p_imageLocation }
            };
            try
            {
                lock (m_sendLock)
                    m_rxSocket.SendFrame(message.ToByteArray());
                return true;
            }
            catch
            {
                Common.s_availableImageSlots.Enqueue(slot);
                throw;
            }
        }

        public void ReleaseImageSlot(ulong p_imageLocation)
        {
            if (p_imageLocation % Config.IMAGE_SLOT_STRIDE == 0 && p_imageLocation < (ulong)Config.MAP_SIZE)
                Common.s_availableImageSlots.Enqueue((int)p_imageLocation);
        }

        public bool TryReceiveResult(out TxPushMessage p_message)
        {
            byte[] bytes;
            if (!m_txSocket.TryReceiveFrameBytes(TimeSpan.Zero, out bytes))
            {
                p_message = null;
                return false;
            }
            p_message = TxPushMessage.Parser.ParseFrom(bytes);
            return true;
        }

        public IImage ReadOutputImage(ulong p_imageLocation)
        {
            if (p_imageLocation + Config.IMAGE_BYTE_SIZE > (ulong)Config.MAP_SIZE)
                throw new ArgumentOutOfRangeException(nameof(p_imageLocation));
            byte[] image = new byte[Config.IMAGE_BYTE_SIZE];
            using (var accessor = m_outputMap.CreateViewAccessor((long)p_imageLocation, Config.IMAGE_BYTE_SIZE, MemoryMappedFileAccess.Read))
                accessor.ReadArray(0, image, 0, image.Length);
            return new IImage { recvImage = image };
        }

        public static void IPCCleanUp()
        {
            AIHelper.aiClosing();
            if (s_instance == null)
                return;
            s_instance.Dispose();
            s_instance = null;
            NetMQConfig.Cleanup(false);
        }

        public void Dispose()
        {
            m_rxSocket?.Dispose();
            m_txSocket?.Dispose();
            m_inputMap?.Dispose();
            m_outputMap?.Dispose();
        }
    }
}
