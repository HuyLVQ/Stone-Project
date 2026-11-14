using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

// Capture only enqueues owned frame buffers. One worker owns all AVI file state.
public sealed class VideoRecorder : IDisposable
{
    public sealed class Segment { public string Path { get; set; } public long Frames { get; set; } }
    public sealed class RecordingManifest
    {
        public int Version { get; set; } = 1;
        public long SessionToken { get; set; }
        public List<Segment> Segments { get; set; } = new List<Segment>();
        public long Captured { get; set; }
        public long Dropped { get; set; }
        public long CaptureFailures { get; set; }
        public long FailedFrames { get; set; }
        public long DurationMilliseconds { get; set; }
        public string Error { get; set; }
    }

    private readonly object m_lock = new object();
    private readonly object m_stopLock = new object();
    private readonly string m_outputPath;
    private readonly long m_segmentLimit;
    private readonly int m_capacity;
    private readonly Func<byte[], byte[]> m_encoder;
    private readonly Func<string, Stream> m_open;
    private readonly Stopwatch m_sessionClock = new Stopwatch();
    private BlockingCollection<byte[]> m_queue;
    private Task m_worker;
    private bool m_accepting;
    private long m_session;
    private string m_prefix, m_path;
    private RecordingManifest m_manifest;
    private Stream m_stream;
    private BinaryWriter m_writer;
    private long m_riffSizeOffset, m_moviSizeOffset, m_moviDataOffset, m_frameCountOffset, m_streamLengthOffset;
    private readonly List<AviIndexEntry> m_index = new List<AviIndexEntry>();
    public string LastCompletedPath { get; private set; }
    public RecordingManifest LastRecording { get; private set; }

    public VideoRecorder(string outputPath = null, long segmentLimit = Config.VIDEO_SEGMENT_MAX_BYTES,
        int capacity = Config.VIDEO_QUEUE_CAPACITY, Func<byte[], byte[]> encoder = null,
        Func<string, Stream> open = null)
    {
        if (segmentLimit < 1024 || segmentLimit > 1_900_000_000) throw new ArgumentOutOfRangeException(nameof(segmentLimit));
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        m_outputPath = outputPath ?? Config.s_videoOutputPath;
        m_segmentLimit = segmentLimit; m_capacity = capacity;
        m_encoder = encoder ?? EncodeJpeg;
        m_open = open ?? (path => new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read));
    }

    public void StartSession(long sessionToken)
    {
        lock (m_stopLock)
        lock (m_lock)
        {
            if (m_worker != null) throw new InvalidOperationException("Previous recording has not been drained.");
            Directory.CreateDirectory(m_outputPath);
            string recordingName = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");
            string recordingDirectory = Path.Combine(m_outputPath, recordingName);
            Directory.CreateDirectory(recordingDirectory);
            m_prefix = Path.Combine(recordingDirectory, recordingName);
            m_manifest = new RecordingManifest { SessionToken = sessionToken };
            m_session = sessionToken;
            m_sessionClock.Reset();
            LastCompletedPath = null; LastRecording = null;
            m_queue = new BlockingCollection<byte[]>(m_capacity);
            // Open synchronously so START errors do not masquerade as a running recorder.
            try { OpenSegment(); }
            catch { m_queue.Dispose(); m_queue = null; throw; }
            m_sessionClock.Start();
            m_accepting = true;
            m_worker = Task.Run((Action)RecordLoop);
        }
    }

    public bool TryAppend(long sessionToken, byte[] frame)
    {
        lock (m_lock)
        {
            if (!m_accepting || sessionToken != m_session) return false;
            m_manifest.Captured++;
            // Caller transfers an immutable buffer; the camera allocates one per grab.
            if (m_queue.TryAdd(frame)) return true;
            m_manifest.Dropped++;
            return false;
        }
    }

    public void NoteCaptureFailure(long sessionToken, long count = 1)
    {
        lock (m_lock)
            if (m_accepting && sessionToken == m_session) m_manifest.CaptureFailures += count;
    }

    public void StopAccepting()
    {
        lock (m_lock)
        {
            m_accepting = false;
            if (m_sessionClock.IsRunning)
            {
                m_sessionClock.Stop();
                m_manifest.DurationMilliseconds = Math.Max(1, m_sessionClock.ElapsedMilliseconds);
            }
            if (m_queue != null && !m_queue.IsAddingCompleted) m_queue.CompleteAdding();
        }
    }

    public string StopSession()
    {
        lock (m_stopLock) return StopSessionCore();
    }

    private string StopSessionCore()
    {
        StopAccepting();
        Task worker;
        lock (m_lock) worker = m_worker;
        if (worker == null) return LastCompletedPath;
        try { worker.GetAwaiter().GetResult(); }
        catch (Exception ex) { m_manifest.Error = "Recording worker failed: " + ex.Message; }
        finally
        {
            lock (m_lock)
            {
                m_worker = null;
                m_queue.Dispose(); m_queue = null;
                LastRecording = m_manifest;
            }
        }
        lock (m_lock)
        {
            if (String.IsNullOrEmpty(m_manifest.Error))
            {
                try { PatchPlaybackTiming(); }
                catch (Exception ex) { m_manifest.Error = "Could not set playback duration: " + ex.Message; }
            }
            string manifestPath = m_prefix + ".json";
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(m_manifest));
            if (!String.IsNullOrEmpty(m_manifest.Error))
                throw new IOException("Recording failed. Diagnostic manifest: " + manifestPath + "\n" + m_manifest.Error);
            LastCompletedPath = manifestPath;
            return manifestPath;
        }
    }

    // AVI stores one constant frame interval. Set it from measured session
    // duration so the recording plays back at the capture rate.
    private void PatchPlaybackTiming()
    {
        long frameCount = 0;
        foreach (Segment segment in m_manifest.Segments) frameCount += segment.Frames;
        if (frameCount <= 0 || m_manifest.DurationMilliseconds <= 0) return;

        double framesPerSecond = frameCount * 1000.0 / m_manifest.DurationMilliseconds;
        int rate = (int)Math.Max(1, Math.Min(Int32.MaxValue, Math.Round(framesPerSecond * 1000.0)));
        int scale = 1000;
        int microsecondsPerFrame = (int)Math.Max(1, Math.Min(Int32.MaxValue,
            Math.Round(m_manifest.DurationMilliseconds * 1000.0 / frameCount)));

        foreach (Segment segment in m_manifest.Segments)
        {
            using (FileStream stream = new FileStream(segment.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // Offsets in WriteHeader(): avih.dwMicroSecPerFrame, strh.dwScale, and strh.dwRate.
                stream.Position = 32; writer.Write(microsecondsPerFrame);
                stream.Position = 128; writer.Write(scale);
                stream.Position = 132; writer.Write(rate);
            }
        }
    }

    private void RecordLoop()
    {
        try
        {
            foreach (byte[] frame in m_queue.GetConsumingEnumerable())
            {
                byte[] jpeg;
                try { jpeg = m_encoder(frame); if (jpeg == null || jpeg.Length == 0) throw new InvalidDataException("Empty encoded frame"); }
                catch (Exception ex) { m_manifest.FailedFrames++; Console.WriteLine("[WARN] [VIDEO] Encoding skipped: " + ex.Message); continue; }

                long chunkBytes = 8L + jpeg.Length + (jpeg.Length & 1);
                if (chunkBytes + 256 + 24 > m_segmentLimit) { m_manifest.FailedFrames++; continue; }
                if (m_stream.Position + chunkBytes + 8L + (m_index.Count + 1L) * 16 > m_segmentLimit)
                { FinalizeSegment(); OpenSegment(); }

                long start = m_stream.Position;
                try
                {
                    WriteFourCC("00dc"); m_writer.Write(jpeg.Length); m_writer.Write(jpeg);
                    if ((jpeg.Length & 1) != 0) m_writer.Write((byte)0);
                    m_index.Add(new AviIndexEntry { Offset = checked((uint)(start - m_moviDataOffset + 4)), Size = (uint)jpeg.Length });
                }
                catch (Exception ex)
                {
                    m_manifest.FailedFrames++;
                    try { m_writer.Flush(); m_stream.SetLength(start); m_stream.Position = start; }
                    catch (Exception rollback) { throw new IOException("Frame write and rollback failed: " + ex.Message, rollback); }
                    Console.WriteLine("[WARN] [VIDEO] Write skipped after rollback: " + ex.Message);
                }
            }
            FinalizeSegment();
        }
        catch (Exception ex)
        {
            m_manifest.Error = ex.Message;
            lock (m_lock) { m_accepting = false; if (!m_queue.IsAddingCompleted) m_queue.CompleteAdding(); }
            while (m_queue.TryTake(out _)) m_manifest.FailedFrames++;
        }
        finally
        {
            try { CloseSegment(); }
            catch (Exception ex) { m_manifest.Error = (m_manifest.Error ?? "") + " Close failed: " + ex.Message; }
        }
    }

    private void OpenSegment()
    {
        m_path = m_prefix + "_part" + (m_manifest.Segments.Count + 1).ToString("D4") + ".avi";
        m_index.Clear();
        try { m_stream = m_open(m_path); m_writer = new BinaryWriter(m_stream); WriteHeader(); }
        catch { CloseSegment(); throw; }
    }

    private void FinalizeSegment()
    {
        if (m_writer == null) return;
        try
        {
            long moviEnd = m_stream.Position;
            WriteFourCC("idx1"); m_writer.Write(checked(m_index.Count * 16));
            foreach (AviIndexEntry entry in m_index)
            { WriteFourCC("00dc"); m_writer.Write(0x10); m_writer.Write(entry.Offset); m_writer.Write(entry.Size); }
            long fileEnd = m_stream.Position;
            PatchInt32(m_moviSizeOffset, checked((int)(moviEnd - m_moviSizeOffset - 4)));
            PatchInt32(m_frameCountOffset, m_index.Count); PatchInt32(m_streamLengthOffset, m_index.Count);
            PatchInt32(m_riffSizeOffset, checked((int)(fileEnd - 8)));
            m_writer.Flush();
            int frames = m_index.Count;
            CloseSegment();
            m_manifest.Segments.Add(new Segment { Path = m_path, Frames = frames });
        }
        finally { CloseSegment(); }
    }

    private void CloseSegment()
    {
        try { if (m_writer != null) m_writer.Dispose(); }
        finally { m_writer = null; try { if (m_stream != null) m_stream.Dispose(); } finally { m_stream = null; } }
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
