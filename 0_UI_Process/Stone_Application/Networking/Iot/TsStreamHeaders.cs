using System;

namespace Stone_Application.Networking.Iot
{
    /// <summary>
    /// Remembers the latest PAT and PMT packets of the encoder's MPEG-TS
    /// stream. A demuxer cannot use any video packet before it has seen both,
    /// so a client that resumes at a keyframe gets them in front of it.
    /// FFmpeg writes them just before each keyframe, but they can end the
    /// previous chunk; without this cache the keyframe would then be
    /// discarded and the first picture would wait for the next one.
    /// Not thread-safe: IotServer uses it under its publish lock.
    /// </summary>
    internal sealed class TsStreamHeaders
    {
        private const int PatPid = 0x0000;
        private byte[] m_pat;
        private byte[] m_pmt;
        private int m_pmtPid = -1;

        /// <summary>PAT followed by PMT, or null until both have been seen.</summary>
        internal byte[] Packets
        {
            get
            {
                if (m_pat == null || m_pmt == null) return null;
                byte[] packets = new byte[m_pat.Length + m_pmt.Length];
                Buffer.BlockCopy(m_pat, 0, packets, 0, m_pat.Length);
                Buffer.BlockCopy(m_pmt, 0, packets, m_pat.Length, m_pmt.Length);
                return packets;
            }
        }

        internal void Reset()
        {
            m_pat = null;
            m_pmt = null;
            m_pmtPid = -1;
        }

        /// <summary>Scans a chunk of whole TS packets and keeps the newest PAT and PMT.</summary>
        internal void Observe(byte[] transportChunk)
        {
            if (transportChunk == null) return;
            for (int offset = 0; offset + IotProtocol.TsPacketBytes <= transportChunk.Length; offset += IotProtocol.TsPacketBytes)
            {
                if (transportChunk[offset] != 0x47) return;
                bool unitStart = (transportChunk[offset + 1] & 0x40) != 0;
                if (!unitStart) continue;       // Single-packet tables always start a unit

                int pid = IotProtocol.TsPid(transportChunk, offset);
                if (pid == PatPid)
                {
                    int pmtPid = ReadPmtPid(transportChunk, offset);
                    if (pmtPid < 0) continue;
                    m_pat = Copy(transportChunk, offset);
                    if (pmtPid != m_pmtPid) { m_pmtPid = pmtPid; m_pmt = null; }
                }
                else if (pid == m_pmtPid)
                {
                    m_pmt = Copy(transportChunk, offset);
                }
            }
        }

        /// <summary>PID of the first program's PMT in a PAT packet, or -1 if unreadable.</summary>
        internal static int ReadPmtPid(byte[] packet, int offset)
        {
            int end = offset + IotProtocol.TsPacketBytes;
            int payload = offset + 4;
            int adaptationFieldControl = (packet[offset + 3] >> 4) & 0x3;
            if (adaptationFieldControl == 0 || adaptationFieldControl == 2) return -1;   // No payload
            if (adaptationFieldControl == 3) payload += 1 + packet[offset + 4];
            if (payload >= end) return -1;

            int table = payload + 1 + packet[payload];      // Skip pointer_field
            if (table + 8 > end || packet[table] != 0x00) return -1;    // table_id 0 = PAT
            int sectionLength = ((packet[table + 1] & 0x0F) << 8) | packet[table + 2];
            int entriesEnd = Math.Min(table + 3 + sectionLength - 4, end);  // Exclude CRC32
            for (int entry = table + 8; entry + 4 <= entriesEnd; entry += 4)
            {
                int programNumber = (packet[entry] << 8) | packet[entry + 1];
                if (programNumber == 0) continue;           // Network information table
                return ((packet[entry + 2] & 0x1F) << 8) | packet[entry + 3];
            }
            return -1;
        }

        private static byte[] Copy(byte[] source, int offset)
        {
            byte[] packet = new byte[IotProtocol.TsPacketBytes];
            Buffer.BlockCopy(source, offset, packet, 0, packet.Length);
            return packet;
        }
    }
}
