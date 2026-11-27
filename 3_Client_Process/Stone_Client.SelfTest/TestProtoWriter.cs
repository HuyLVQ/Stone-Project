using System;
using System.IO;
using System.Text;

namespace Stone_Client.SelfTest
{
    /// <summary>
    /// Byte-for-byte copy of the server's IotProtocol.ProtoWriter
    /// (0_UI_Process/Stone_Application/Networking/Iot/IotProtocol.cs), including
    /// its proto3 rule of omitting zero and empty values, so the tests decode
    /// exactly what the server produces. Raw* helpers exist only to build
    /// unknown or malformed input.
    /// </summary>
    internal sealed class TestProtoWriter
    {
        private readonly MemoryStream m_stream = new MemoryStream();

        internal byte[] ToArray() { return m_stream.ToArray(); }

        internal TestProtoWriter UInt32(int field, uint value)
        {
            if (value == 0) return this;
            Tag(field, 0); Varint(value);
            return this;
        }

        internal TestProtoWriter UInt64(int field, ulong value)
        {
            if (value == 0) return this;
            Tag(field, 0); Varint(value);
            return this;
        }

        internal TestProtoWriter Int64(int field, long value)
        {
            if (value == 0) return this;
            Tag(field, 0); Varint(unchecked((ulong)value));
            return this;
        }

        internal TestProtoWriter String(int field, string value)
        {
            if (System.String.IsNullOrEmpty(value)) return this;
            return Bytes(field, Encoding.UTF8.GetBytes(value));
        }

        internal TestProtoWriter Message(int field, byte[] value) { return Bytes(field, value); }

        internal TestProtoWriter Bytes(int field, byte[] value)
        {
            if (value == null) throw new ArgumentNullException("value");
            Tag(field, 2); Varint(checked((ulong)value.Length));
            m_stream.Write(value, 0, value.Length);
            return this;
        }

        internal TestProtoWriter Double(int field, double value)
        {
            if (value == 0.0) return this;
            Tag(field, 1);
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            m_stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        internal TestProtoWriter RawFixed32(int field, uint value)
        {
            Tag(field, 5);
            m_stream.Write(BitConverter.GetBytes(value), 0, 4);
            return this;
        }

        internal TestProtoWriter RawTag(int field, int wireType) { Tag(field, wireType); return this; }

        internal TestProtoWriter RawBytes(params byte[] bytes) { m_stream.Write(bytes, 0, bytes.Length); return this; }

        private void Tag(int field, int wireType) { Varint(checked((ulong)((field << 3) | wireType))); }

        private void Varint(ulong value)
        {
            while (value >= 0x80)
            {
                m_stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            m_stream.WriteByte((byte)value);
        }
    }
}
