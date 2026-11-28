using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using Stone_Application.Networking.Iot;

// @brief: One open MP4 segment that the recorder worker writes frames into
internal interface IVideoSegmentWriter : IDisposable
{
    // Approximate encoded bytes so far, used for segment rollover.
    long BytesWritten { get; }
    // False once the writer can no longer accept frames; a write failure then faults the recording.
    bool IsOpen { get; }
    // @param:
    //      * pixels: Packed frame in the segment's pixel type
    //      * captureOffsetMicroseconds: Capture time relative to the session start
    void Write(PooledFrameBuffer pixels, long captureOffsetMicroseconds);
    // Flush and finalize the file; throws when the segment could not be completed.
    void Close();
}

// @brief: Locate an FFmpeg executable shared by IoT streaming and session recording
internal static class FfmpegLocator
{
    // @return: STONE_FFMPEG_PATH, ffmpeg.exe beside the application, or the first ffmpeg.exe on PATH; null if none
    public static string Find()
    {
        string configuredPath = Environment.GetEnvironmentVariable("STONE_FFMPEG_PATH");
        if (!String.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath)) return configuredPath;

        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(localPath)) return localPath;

        string pathEnvironment = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
        foreach (string directory in pathEnvironment.Split(Path.PathSeparator))
        {
            if (String.IsNullOrWhiteSpace(directory)) continue;
            try
            {
                string candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }
}

// @brief: Constant-frame-rate fallback writer using the Pylon VideoWriter
// @details: Pylon has no per-frame timestamps, so playback speed is only correct when no frames are lost.
internal sealed class PylonSegmentWriter : IVideoSegmentWriter
{
    private VideoWriter m_writer;

    public PylonSegmentWriter(string path, double framesPerSecond, PixelType pixelType)
    {
        if (!VideoWriter.IsSupported)
            throw new NotSupportedException("The installed Pylon video writer libraries are unavailable.");
        var writer = new VideoWriter();
        try
        {
            writer.Create(path, framesPerSecond, pixelType, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT,
                Config.VIDEO_WRITER_QUALITY);
            m_writer = writer;
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    public long BytesWritten => m_writer.ImageDataBytesWritten;
    public bool IsOpen => m_writer != null && m_writer.IsOpen;

    public void Write(PooledFrameBuffer pixels, long captureOffsetMicroseconds)
    {
        // Pylon converts the grab's own pixel type to YUV420p before encoding.
        m_writer.Write(pixels.Buffer, pixels.PixelType, Config.IMAGE_WIDTH, Config.IMAGE_HEIGHT, 0,
            ImageOrientation.TopDown);
    }

    public void Close()
    {
        VideoWriter writer = m_writer;
        if (writer == null) return;
        m_writer = null;
        try { writer.Close(); }
        finally { writer.Dispose(); }
    }

    public void Dispose() { Close(); }
}

// @brief: Variable-frame-rate writer that encodes frames with their real capture times through FFmpeg
// @details:
//      Frames go to FFmpeg's stdin as a minimal Matroska stream: one uncompressed video track whose
//      ColourSpace FourCC names the raw layout (8-bit Bayer or BGR24), and one cluster per frame stamped with
//      the capture time in microseconds. FFmpeg debayers, encodes with libx264, and keeps those timestamps
//      in the MP4 (-fps_mode passthrough), so playback runs at real speed even when frames were dropped.
internal sealed class FfmpegSegmentWriter : IVideoSegmentWriter
{
    private const int StderrLimit = 4000;
    private readonly Process m_process;
    private readonly Stream m_input;
    private readonly Task m_errorTask;
    private readonly StringBuilder m_errors = new StringBuilder();
    private readonly string m_path;
    private readonly PixelType m_pixelType;
    private readonly byte[] m_header = new byte[64];
    private long m_firstOffset = -1, m_lastTimestamp = -1, m_bytesWritten;
    private int m_framesSinceSizeCheck;
    private bool m_closed, m_broken;

    // @brief: Report whether FFmpeg can be used for recording
    // @param:
    //      * executablePath: Receives the FFmpeg path when it exists and includes libx264
    public static bool TryFindEncoder(out string executablePath)
    {
        executablePath = FfmpegLocator.Find();
        return executablePath != null && FfmpegH264Encoder.HasLibX264(executablePath);
    }

    // @brief: Report whether a camera pixel type can be described to FFmpeg
    public static bool Supports(PixelType pixelType) { return FourCc(pixelType) != null; }

    public FfmpegSegmentWriter(string executablePath, string path, PixelType pixelType)
    {
        byte[] fourCc = FourCc(pixelType) ?? throw new NotSupportedException("FFmpeg recording does not support " + pixelType + ".");
        m_path = path;
        m_pixelType = pixelType;
        m_process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-hide_banner -loglevel error -nostdin -y -f matroska -i pipe:0 -an " +
                    "-c:v libx264 -preset " + Config.VIDEO_FFMPEG_PRESET + " -crf " + Config.VIDEO_FFMPEG_CRF +
                    " -pix_fmt yuv420p -fps_mode passthrough -video_track_timescale 90000 -f mp4 \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            }
        };
        if (!m_process.Start()) throw new InvalidOperationException("FFmpeg did not start.");
        m_errorTask = Task.Run((Action)PumpErrors);
        m_input = m_process.StandardInput.BaseStream;
        try
        {
            WriteStreamHeader(fourCc);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public long BytesWritten => m_bytesWritten;
    public bool IsOpen => !m_closed && !m_broken && !m_process.HasExited;

    public void Write(PooledFrameBuffer pixels, long captureOffsetMicroseconds)
    {
        if (!IsOpen) throw new IOException("FFmpeg recording stopped: " + ErrorText());
        if (pixels.PixelType != m_pixelType)
            throw new InvalidOperationException("Frame pixel type " + pixels.PixelType + " does not match the segment's " + m_pixelType + ".");

        // Segment-relative, strictly increasing timestamps.
        if (m_firstOffset < 0) m_firstOffset = captureOffsetMicroseconds;
        long timestamp = Math.Max(captureOffsetMicroseconds - m_firstOffset, m_lastTimestamp + 1);
        m_lastTimestamp = timestamp;

        // Cluster { Timestamp, SimpleBlock { track 1, relative time 0, keyframe, pixels } }
        int frameBytes = Config.RAW_FRAME_BYTE_COUNT;
        int timestampBytes = UnsignedLength(timestamp);
        long blockSize = 4 + frameBytes;
        long clusterSize = 1 + 1 + timestampBytes + 1 + 8 + blockSize;
        int n = 0;
        n = PutId(m_header, n, 0x1F43B675);
        n = PutSize(m_header, n, clusterSize);
        m_header[n++] = 0xE7;
        m_header[n++] = (byte)(0x80 | timestampBytes);
        n = PutUnsigned(m_header, n, timestamp, timestampBytes);
        m_header[n++] = 0xA3;
        n = PutSize(m_header, n, blockSize);
        m_header[n++] = 0x81;
        m_header[n++] = 0;
        m_header[n++] = 0;
        m_header[n++] = 0x80;
        try
        {
            m_input.Write(m_header, 0, n);
            m_input.Write(pixels.Buffer, 0, frameBytes);
        }
        catch (IOException ex)
        {
            // A partial frame leaves the stream unusable, so the segment cannot continue.
            m_broken = true;
            throw new IOException("FFmpeg stopped accepting frames: " + ErrorText(), ex);
        }

        // Encoded size for rollover: the MP4's media data grows as FFmpeg writes it.
        if (++m_framesSinceSizeCheck >= 30)
        {
            m_framesSinceSizeCheck = 0;
            try { m_bytesWritten = new FileInfo(m_path).Length; } catch (IOException) { }
        }
    }

    public void Close()
    {
        if (m_closed) return;
        m_closed = true;
        try { m_input.Close(); }
        catch (IOException) { }
        if (!m_process.WaitForExit(Config.VIDEO_FFMPEG_EXIT_TIMEOUT_MS))
        {
            try { m_process.Kill(); } catch (Exception) { }
            throw new IOException("FFmpeg did not finish writing " + m_path + " in time.");
        }
        m_errorTask.Wait(5000);
        if (m_process.ExitCode != 0)
            throw new IOException("FFmpeg failed to write " + m_path + " (exit " + m_process.ExitCode + "): " + ErrorText());
    }

    public void Dispose()
    {
        try
        {
            if (!m_closed)
            {
                m_closed = true;
                try { m_input.Close(); } catch (IOException) { }
                if (!m_process.WaitForExit(Config.VIDEO_FFMPEG_EXIT_TIMEOUT_MS)) m_process.Kill();
            }
        }
        catch (Exception) { }
        finally { m_process.Dispose(); }
    }

    // @brief: Write the EBML header, an unknown-size Segment, Info, and the single video track
    private void WriteStreamHeader(byte[] fourCc)
    {
        var buffer = new MemoryStream();
        WriteElement(buffer, 0x1A45DFA3, Concat(
            UnsignedElement(0x4286, 1), UnsignedElement(0x42F7, 1), UnsignedElement(0x42F2, 4),
            UnsignedElement(0x42F3, 8), StringElement(0x4282, "matroska"),
            UnsignedElement(0x4287, 4), UnsignedElement(0x4285, 2)));
        // Segment with unknown size: the stream ends when stdin closes.
        buffer.Write(new byte[] { 0x18, 0x53, 0x80, 0x67, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 0, 12);
        // TimestampScale 1000 ns: cluster timestamps are microseconds.
        WriteElement(buffer, 0x1549A966, Concat(UnsignedElement(0x2AD7B1, 1000),
            StringElement(0x4D80, "Stone_Application"), StringElement(0x5741, "Stone_Application")));
        byte[] video = Concat(UnsignedElement(0xB0, Config.IMAGE_WIDTH), UnsignedElement(0xBA, Config.IMAGE_HEIGHT),
            Element(0x2EB524, fourCc));
        byte[] track = Concat(UnsignedElement(0xD7, 1), UnsignedElement(0x73C5, 1), UnsignedElement(0x83, 1),
            StringElement(0x86, "V_UNCOMPRESSED"), Element(0xE0, video));
        WriteElement(buffer, 0x1654AE6B, Element(0xAE, track));
        buffer.WriteTo(m_input);
    }

    // FFmpeg raw-video FourCCs (libavcodec/raw.c).
    private static byte[] FourCc(PixelType pixelType)
    {
        switch (pixelType)
        {
            case PixelType.BayerRG8: return new byte[] { 0xBA, (byte)'R', (byte)'G', 8 };
            case PixelType.BayerBG8: return new byte[] { 0xBA, (byte)'B', (byte)'G', 8 };
            case PixelType.BayerGB8: return new byte[] { 0xBA, (byte)'G', (byte)'B', 8 };
            case PixelType.BayerGR8: return new byte[] { 0xBA, (byte)'G', (byte)'R', 8 };
            case PixelType.BGR8packed: return new byte[] { (byte)'B', (byte)'G', (byte)'R', 24 };
            default: return null;
        }
    }

    private void PumpErrors()
    {
        try
        {
            string line;
            while ((line = m_process.StandardError.ReadLine()) != null)
                lock (m_errors)
                {
                    if (m_errors.Length < StderrLimit) m_errors.AppendLine(line);
                }
        }
        catch (Exception) { }
    }

    private string ErrorText()
    {
        lock (m_errors) return m_errors.Length == 0 ? "no diagnostic output" : m_errors.ToString().Trim();
    }

    private static int UnsignedLength(long value)
    {
        int length = 1;
        while (length < 8 && (value >> (8 * length)) != 0) length++;
        return length;
    }

    private static int PutUnsigned(byte[] target, int offset, long value, int length)
    {
        for (int i = length - 1; i >= 0; i--) target[offset++] = (byte)(value >> (8 * i));
        return offset;
    }

    private static int PutId(byte[] target, int offset, uint id)
    {
        int length = id > 0xFFFFFF ? 4 : id > 0xFFFF ? 3 : id > 0xFF ? 2 : 1;
        return PutUnsigned(target, offset, id, length);
    }

    // Eight-byte EBML size, valid for any element written here.
    private static int PutSize(byte[] target, int offset, long size)
    {
        target[offset++] = 0x01;
        return PutUnsigned(target, offset, size, 7);
    }

    private static byte[] Element(uint id, byte[] payload)
    {
        var buffer = new MemoryStream();
        WriteElement(buffer, id, payload);
        return buffer.ToArray();
    }

    private static void WriteElement(Stream stream, uint id, byte[] payload)
    {
        var header = new byte[12];
        int n = PutId(header, 0, id);
        n = PutSize(header, n, payload.Length);
        stream.Write(header, 0, n);
        stream.Write(payload, 0, payload.Length);
    }

    private static byte[] UnsignedElement(uint id, long value)
    {
        var payload = new byte[UnsignedLength(value)];
        PutUnsigned(payload, 0, value, payload.Length);
        return Element(id, payload);
    }

    private static byte[] StringElement(uint id, string value) { return Element(id, Encoding.ASCII.GetBytes(value)); }

    private static byte[] Concat(params byte[][] parts)
    {
        var buffer = new MemoryStream();
        foreach (byte[] part in parts) buffer.Write(part, 0, part.Length);
        return buffer.ToArray();
    }
}
