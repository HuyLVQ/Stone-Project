using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

// Writes a broadly playable Motion-JPEG AVI without requiring a machine-wide codec.
public sealed class VideoRecorder : IDisposable
{
    private readonly object m_lock = new object();
    private FileStream m_stream;
    private BinaryWriter m_writer;
    private long m_riffSizeOffset, m_moviSizeOffset, m_moviDataOffset, m_frameCountOffset, m_streamLengthOffset;
    private readonly List<AviIndexEntry> m_index = new List<AviIndexEntry>();
    public string LastCompletedPath { get; private set; }

    public void StartSession()
    {
        lock (m_lock)
        {
            StopSession();
            Directory.CreateDirectory(Config.s_videoOutputPath);
            string filename = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".avi";
            m_stream = new FileStream(Path.Combine(Config.s_videoOutputPath, filename), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            LastCompletedPath = null;
            m_writer = new BinaryWriter(m_stream);
            WriteHeader();
            Console.WriteLine("[INFO] [VIDEO] Recording " + m_stream.Name);
        }
    }

    public void Append(byte[] p_bgrFrame)
    {
        lock (m_lock)
        {
            if (m_writer == null) return;
            byte[] jpeg = EncodeJpeg(p_bgrFrame);
            long chunkOffset = m_stream.Position - m_moviDataOffset;
            WriteFourCC("00dc");
            m_writer.Write(jpeg.Length);
            m_writer.Write(jpeg);
            if ((jpeg.Length & 1) != 0) m_writer.Write((byte)0);
            m_index.Add(new AviIndexEntry { Offset = (uint)chunkOffset, Size = (uint)jpeg.Length });
        }
    }

    public string StopSession()
    {
        lock (m_lock)
        {
            if (m_writer == null) return LastCompletedPath;
            string completedPath = m_stream.Name;
            long moviEnd = m_stream.Position;
            WriteFourCC("idx1");
            m_writer.Write(m_index.Count * 16);
            foreach (AviIndexEntry entry in m_index)
            {
                WriteFourCC("00dc"); m_writer.Write(0x10); m_writer.Write(entry.Offset); m_writer.Write(entry.Size);
            }
            long fileEnd = m_stream.Position;
            PatchInt32(m_moviSizeOffset, checked((int)(moviEnd - m_moviSizeOffset - 4)));
            PatchInt32(m_frameCountOffset, m_index.Count);
            PatchInt32(m_streamLengthOffset, m_index.Count);
            PatchInt32(m_riffSizeOffset, checked((int)(fileEnd - 8)));
            m_writer.Dispose(); m_stream.Dispose(); m_writer = null; m_stream = null; m_index.Clear();
            LastCompletedPath = completedPath;
            return LastCompletedPath;
        }
    }

    private void WriteHeader()
    {
        WriteFourCC("RIFF"); m_riffSizeOffset = m_stream.Position; m_writer.Write(0); WriteFourCC("AVI ");
        WriteFourCC("LIST"); long hdrlSize = m_stream.Position; m_writer.Write(0); WriteFourCC("hdrl");
        WriteFourCC("avih"); m_writer.Write(56);
        m_writer.Write(1000000 / Config.VIDEO_FRAME_RATE); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0x10);
        m_frameCountOffset = m_stream.Position; m_writer.Write(0); m_writer.Write(0); m_writer.Write(1); m_writer.Write(0);
        m_writer.Write(Config.IMAGE_WIDTH); m_writer.Write(Config.IMAGE_HEIGHT); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0);
        WriteFourCC("LIST"); long strlSize = m_stream.Position; m_writer.Write(0); WriteFourCC("strl");
        WriteFourCC("strh"); m_writer.Write(56); WriteFourCC("vids"); WriteFourCC("MJPG");
        m_writer.Write(0); m_writer.Write((short)0); m_writer.Write((short)0); m_writer.Write(0); m_writer.Write(1); m_writer.Write(Config.VIDEO_FRAME_RATE);
        m_writer.Write(0); m_streamLengthOffset = m_stream.Position; m_writer.Write(0); m_writer.Write(0); m_writer.Write(-1); m_writer.Write(0);
        m_writer.Write((short)0); m_writer.Write((short)0); m_writer.Write((short)Config.IMAGE_WIDTH); m_writer.Write((short)Config.IMAGE_HEIGHT);
        WriteFourCC("strf"); m_writer.Write(40); m_writer.Write(40); m_writer.Write(Config.IMAGE_WIDTH); m_writer.Write(Config.IMAGE_HEIGHT);
        m_writer.Write((short)1); m_writer.Write((short)24); WriteFourCC("MJPG"); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0); m_writer.Write(0);
        long afterStrl = m_stream.Position; PatchInt32(strlSize, checked((int)(afterStrl - strlSize - 4)));
        PatchInt32(hdrlSize, checked((int)(afterStrl - hdrlSize - 4)));
        WriteFourCC("LIST"); m_moviSizeOffset = m_stream.Position; m_writer.Write(0); WriteFourCC("movi"); m_moviDataOffset = m_stream.Position;
    }

    private static byte[] EncodeJpeg(byte[] p_bgrFrame)
    {
        using (Bitmap bitmap = new Bitmap(Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT, PixelFormat.Format24bppRgb))
        {
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                int rowBytes = Config.IMAGE_WIDTH * 3;
                for (int row = 0; row < Config.IMAGE_HEIGHT; row++)
                    Marshal.Copy(p_bgrFrame, row * rowBytes, IntPtr.Add(data.Scan0, row * data.Stride), rowBytes);
            }
            finally { bitmap.UnlockBits(data); }
            using (MemoryStream output = new MemoryStream()) { bitmap.Save(output, ImageFormat.Jpeg); return output.ToArray(); }
        }
    }

    private void PatchInt32(long p_offset, int p_value) { long current = m_stream.Position; m_stream.Position = p_offset; m_writer.Write(p_value); m_stream.Position = current; }
    private void WriteFourCC(string p_value) { m_writer.Write((byte)p_value[0]); m_writer.Write((byte)p_value[1]); m_writer.Write((byte)p_value[2]); m_writer.Write((byte)p_value[3]); }
    public void Dispose() { StopSession(); }
    private struct AviIndexEntry { public uint Offset; public uint Size; }
}
