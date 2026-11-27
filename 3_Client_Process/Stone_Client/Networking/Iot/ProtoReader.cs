using System;
using System.IO;
using System.Text;

namespace Stone_Client.Networking.Iot
{
    /// <summary>
    /// Minimal protobuf wire-format reader, the counterpart of the server's
    /// IotProtocol.ProtoWriter. Supports varint, fixed64, fixed32 and
    /// length-delimited fields; groups are rejected. Every read is bounds-checked
    /// and malformed input raises InvalidDataException.
    /// </summary>
    internal sealed class ProtoReader
    {
        internal const int WireVarint = 0;
        internal const int WireFixed64 = 1;
        internal const int WireLengthDelimited = 2;
        internal const int WireFixed32 = 5;

        private readonly byte[] m_buffer;
        private readonly int m_end;
        private int m_position;

        internal ProtoReader(byte[] p_buffer) : this(p_buffer, 0, p_buffer == null ? 0 : p_buffer.Length) { }

        internal ProtoReader(byte[] p_buffer, int p_offset, int p_count)
        {
            if (p_buffer == null) throw new ArgumentNullException("p_buffer");
            if (p_offset < 0 || p_count < 0 || p_count > p_buffer.Length - p_offset)
                throw new ArgumentOutOfRangeException("p_count");
            m_buffer = p_buffer;
            m_position = p_offset;
            m_end = p_offset + p_count;
        }

        internal bool TryReadTag(out int p_field, out int p_wireType)
        {
            p_field = 0;
            p_wireType = 0;
            if (m_position >= m_end) return false;

            ulong tag = ReadVarint();
            ulong field = tag >> 3;
            if (field == 0 || field > 536870911UL) throw new InvalidDataException("Invalid protobuf field number " + field + ".");
            p_field = (int)field;
            p_wireType = (int)(tag & 7);
            return true;
        }

        internal ulong ReadVarint()
        {
            ulong result = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                if (m_position >= m_end) throw new InvalidDataException("Truncated protobuf varint.");
                byte current = m_buffer[m_position++];
                result |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0) return result;
            }
            throw new InvalidDataException("Protobuf varint is longer than 10 bytes.");
        }

        internal uint ReadUInt32() { return unchecked((uint)ReadVarint()); }

        internal ulong ReadUInt64() { return ReadVarint(); }

        internal long ReadInt64() { return unchecked((long)ReadVarint()); }

        internal ulong ReadFixed64()
        {
            Require(8);
            ulong value = 0;
            for (int index = 7; index >= 0; index--) value = (value << 8) | m_buffer[m_position + index];
            m_position += 8;
            return value;
        }

        internal uint ReadFixed32()
        {
            Require(4);
            uint value = (uint)(m_buffer[m_position] | m_buffer[m_position + 1] << 8 |
                                m_buffer[m_position + 2] << 16 | m_buffer[m_position + 3] << 24);
            m_position += 4;
            return value;
        }

        internal double ReadDouble() { return BitConverter.Int64BitsToDouble(unchecked((long)ReadFixed64())); }

        internal ArraySegment<byte> ReadLengthDelimited()
        {
            ulong length = ReadVarint();
            if (length > (ulong)(m_end - m_position))
                throw new InvalidDataException("Protobuf length-delimited field exceeds its enclosing message.");
            var segment = new ArraySegment<byte>(m_buffer, m_position, (int)length);
            m_position += (int)length;
            return segment;
        }

        internal byte[] ReadBytes()
        {
            ArraySegment<byte> segment = ReadLengthDelimited();
            byte[] copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
            return copy;
        }

        internal string ReadString()
        {
            ArraySegment<byte> segment = ReadLengthDelimited();
            return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
        }

        internal ProtoReader ReadMessage()
        {
            ArraySegment<byte> segment = ReadLengthDelimited();
            return new ProtoReader(segment.Array, segment.Offset, segment.Count);
        }

        /// <summary>Skips an unknown field so newer servers can add fields safely.</summary>
        internal void Skip(int p_wireType)
        {
            switch (p_wireType)
            {
                case WireVarint: ReadVarint(); break;
                case WireFixed64: Require(8); m_position += 8; break;
                case WireLengthDelimited: ReadLengthDelimited(); break;
                case WireFixed32: Require(4); m_position += 4; break;
                default: throw new InvalidDataException("Unsupported protobuf wire type " + p_wireType + ".");
            }
        }

        /// <summary>Rejects a known field encoded with the wrong wire type.</summary>
        internal static void Expect(int p_field, int p_actualWireType, int p_expectedWireType)
        {
            if (p_actualWireType != p_expectedWireType)
                throw new InvalidDataException("Protobuf field " + p_field + " has wire type " + p_actualWireType +
                                               "; expected " + p_expectedWireType + ".");
        }

        private void Require(int p_count)
        {
            if (m_end - m_position < p_count) throw new InvalidDataException("Truncated protobuf fixed-width field.");
        }
    }
}
