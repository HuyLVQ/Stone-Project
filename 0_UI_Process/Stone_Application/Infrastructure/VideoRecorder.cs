using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Basler.Pylon;

public sealed class VideoRecorder : IDisposable
{
    public sealed class Segment
    {
        public string Path { get; set; }
        public long Frames { get; set; }
        public List<long> CaptureOffsetsMicroseconds { get; set; }
    }

    public sealed class RecordingManifest
    {
        public int Version { get; set; } = 1;
        public long SessionToken { get; set; }
        public List<Segment> Segments { get; set; } = new List<Segment>();
        public long Captured { get; set; }
        public long Dropped { get; set; }
        public long QueueDrops { get; set; }
        public long BufferPoolDrops { get; set; }
        public long CaptureFailures { get; set; }
        // Included in CaptureFailures for compatibility with existing manifest readers.
        public long CameraSkippedFrames { get; set; }
        public long FailedFrames { get; set; }
        public long DurationMilliseconds { get; set; }
        public double PlaybackFramesPerSecond { get; set; }
        // Camera pixel format fed to the encoder (BGR8packed or for example BayerRG8). Readers may ignore it.
        public string SourcePixelFormat { get; set; }
        // "ffmpeg-libx264-vfr" (frames carry their capture times) or "pylon-cfr" (fixed PlaybackFramesPerSecond).
        public string VideoWriter { get; set; }
        // True when playback timing follows CaptureOffsetsMicroseconds rather than PlaybackFramesPerSecond.
        public bool TimestampedPlayback { get; set; }
        public string Error { get; set; }
    }

    private struct QueuedFrame
    {
        public PooledFrameBuffer Pixels;
        public long CaptureOffsetMicroseconds;
    }

    private readonly object m_lock = new object();
    private readonly object m_stopLock = new object();
    private readonly string m_outputPath;
    private readonly long m_segmentLimit;
    private BlockingCollection<QueuedFrame> m_queue;
    private Task m_worker;
    private long m_startTimestamp;
    private bool m_writerFaulted;
    private IVideoSegmentWriter m_videoWriter;
    private string m_ffmpegPath;
    private bool m_accepting;
    private long m_session;
    private string m_prefix, m_path;
    private PixelType m_sourcePixelType;
    private List<long> m_captureOffsets;
    private RecordingManifest m_manifest;
    public string LastCompletedPath { get; private set; }
    public RecordingManifest LastRecording { get; private set; }

    // @brief: Create a recorder with an output directory and maximum encoded segment size
    // @param:
    //      * outputPath: Directory for session recordings, or null to use the configured output directory
    //      * segmentLimit: Maximum encoded image data allowed in one segment
    public VideoRecorder(string outputPath = null, long segmentLimit = Config.VIDEO_SEGMENT_MAX_BYTES)
    {
        if (segmentLimit < 1024 || segmentLimit > 1_900_000_000) throw new ArgumentOutOfRangeException(nameof(segmentLimit));
        m_outputPath = outputPath ?? Config.s_videoOutputPath;
        m_segmentLimit = segmentLimit;
    }

    // @brief: Initialize a recording session and start its dedicated video writer worker
    // @param:
    //      * sessionToken: Immutable identity assigned to this recording session
    //      * playbackFramesPerSecond: Frame rate used to encode the session video
    //      * sourcePixelType: Camera pixel type of recorded frames (BGR8packed or 8-bit Bayer)
    // @details:
    //      Creates a unique session directory and manifest, starts the writer, waits for its first segment
    //      to open, and only then accepts frames. A previous session must be fully finalized first.
    //      With FFmpeg available, each frame is encoded with its capture time (variable frame rate), so
    //      playback matches real time even when frames are dropped. Otherwise the Pylon writer encodes at a
    //      fixed playbackFramesPerSecond.
    public void StartSession(long sessionToken, double playbackFramesPerSecond, PixelType sourcePixelType)
    {
        lock (m_stopLock)
        {
            if (m_worker != null)
                throw new InvalidOperationException("Previous recording has not been finalized.");
            if (sourcePixelType == PixelType.Undefined)
                throw new ArgumentException("The camera pixel format is unknown.", nameof(sourcePixelType));
            m_sourcePixelType = sourcePixelType;
            // FFmpeg keeps real capture timing; Pylon's writer is the constant-rate fallback.
            m_ffmpegPath = null;
            if (FfmpegSegmentWriter.Supports(sourcePixelType) && FfmpegSegmentWriter.TryFindEncoder(out string ffmpegPath))
                m_ffmpegPath = ffmpegPath;
            else
                Console.WriteLine("[WARN] [VIDEO] FFmpeg with libx264 was not found; recording at a fixed frame rate " +
                    "with the Pylon writer. Playback speed is wrong whenever frames are dropped.");
            LastCompletedPath = null;
            LastRecording = null;
            string recordingName = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");
            string recordingDirectory = Path.Combine(Path.GetFullPath(m_outputPath), recordingName);
            Directory.CreateDirectory(recordingDirectory);
            m_prefix = Path.Combine(recordingDirectory, recordingName);
            lock (m_lock)
            {
                m_manifest = new RecordingManifest
                {
                    SessionToken = sessionToken,
                    PlaybackFramesPerSecond = playbackFramesPerSecond,
                    SourcePixelFormat = sourcePixelType.ToString(),
                    VideoWriter = m_ffmpegPath != null ? "ffmpeg-libx264-vfr" : "pylon-cfr",
                    TimestampedPlayback = m_ffmpegPath != null
                };
                m_session = sessionToken;
                m_writerFaulted = false;
                m_queue = new BlockingCollection<QueuedFrame>(Config.VIDEO_QUEUE_CAPACITY);
            }

            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                m_worker = Task.Factory.StartNew(() => RecordLoop(started), CancellationToken.None,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
                started.Task.GetAwaiter().GetResult();
                lock (m_lock)
                {
                    if (m_queue.IsAddingCompleted)
                        throw new InvalidOperationException("Recording stopped while starting.");
                    m_startTimestamp = Stopwatch.GetTimestamp();
                    m_accepting = true;
                }
            }
            catch
            {
                StopAccepting();
                m_worker?.GetAwaiter().GetResult();
                lock (m_lock) ResetSession();
                throw;
            }
        }
    }

    // The capture thread only offers an owned frame. The worker does all video file I/O.
    // @brief: Offer an owned camera frame to the active recording queue
    // @param:
    //      * sessionToken: Identity of the session that captured the frame
    //      * pixels: Pooled frame buffer containing packed image data in the configured camera format
    //      * captureTimestamp: Stopwatch timestamp captured when the camera frame was retrieved
    // @return: True if the frame was accepted by the recorder queue
    // @details: The recorder adds its own buffer reference for accepted frames and releases it after writing.
    public bool TryAppend(long sessionToken, PooledFrameBuffer pixels, long captureTimestamp)
    {
        if (pixels == null) throw new ArgumentNullException(nameof(pixels));
        lock (m_lock)
        {
            if (!m_accepting || sessionToken != m_session) return false;
            m_manifest.Captured++;
            if (m_writerFaulted)
            {
                m_manifest.FailedFrames++;
                return false;
            }

            var frame = new QueuedFrame
            {
                Pixels = pixels,
                CaptureOffsetMicroseconds = (long)(Math.Max(0L, captureTimestamp - m_startTimestamp)
                    * (1_000_000.0 / Stopwatch.Frequency))
            };
            pixels.AddReference();
            bool accepted = false;
            try
            {
                accepted = m_queue.TryAdd(frame);
                if (accepted) return true;
                m_manifest.Dropped++;
                m_manifest.QueueDrops++;
                return false;
            }
            finally { if (!accepted) pixels.Dispose(); }
        }
    }

    // @brief: Add one camera capture failure to the active session manifest
    // @param:
    //      * sessionToken: Identity of the session associated with the failed capture
    public void NoteCaptureFailure(long sessionToken)
    {
        lock (m_lock)
            if (m_accepting && sessionToken == m_session) m_manifest.CaptureFailures++;
    }

    // @brief: Record camera-reported skipped frames in the active session manifest
    // @param:
    //      * sessionToken: Identity of the session associated with the skipped frames
    //      * count: Number of frames reported as skipped by the camera grab result
    public void NoteCameraSkipped(long sessionToken, long count)
    {
        if (count <= 0) return;
        lock (m_lock)
        {
            if (!m_accepting || sessionToken != m_session) return;
            m_manifest.CaptureFailures += count;
            m_manifest.CameraSkippedFrames += count;
        }
    }

    // @brief: Record a frame dropped because no pooled buffer was available
    // @param:
    //      * sessionToken: Identity of the session associated with the dropped frame
    public void NotePoolDrop(long sessionToken)
    {
        lock (m_lock)
        {
            if (!m_accepting || sessionToken != m_session) return;
            m_manifest.Captured++;
            m_manifest.Dropped++;
            m_manifest.BufferPoolDrops++;
        }
    }

    // @brief: Close the recorder queue to new frames and capture the session duration
    // @details: The writer worker continues draining frames already accepted into the queue.
    public void StopAccepting()
    {
        lock (m_lock)
        {
            if (m_accepting)
            {
                m_accepting = false;
                m_manifest.DurationMilliseconds = Math.Max(1L, (long)((Stopwatch.GetTimestamp() - m_startTimestamp)
                    * (1000.0 / Stopwatch.Frequency)));
            }
            if (m_queue != null && !m_queue.IsAddingCompleted) m_queue.CompleteAdding();
        }
    }

    // @brief: Drain and finalize the active recording and write its JSON manifest
    // @return: Path to the finalized manifest, or the previous completed path when no session is active
    public string StopSession()
    {
        lock (m_stopLock) return StopSessionCore();
    }

    // @brief: Perform session finalization while holding the stop lifecycle lock
    // @return: Path to the finalized manifest
    private string StopSessionCore()
    {
        StopAccepting();
        if (m_worker == null) return LastCompletedPath;
        m_worker.GetAwaiter().GetResult();
        RecordingManifest manifest;
        lock (m_lock)
        {
            manifest = m_manifest;
            LastRecording = manifest;
            ResetSession();
        }

        string manifestPath = m_prefix + ".json";
        if (manifest.Error == null && manifest.Segments.Count == 0)
            manifest.Error = "No frames were recorded.";
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        if (manifest.Error != null)
            throw new IOException("Recording failed. Diagnostic manifest: " + manifestPath + "\n" + manifest.Error);
        LastCompletedPath = manifestPath;
        return manifestPath;
    }

    // @brief: Release queue state and clear per-session recorder references
    private void ResetSession()
    {
        m_queue.Dispose();
        m_queue = null;
        m_worker = null;
        m_manifest = null;
    }

    // @brief: Run the writer worker that drains queued frames and finalizes the last segment
    // @param:
    //      * started: Completion source signaled when the first video segment is ready or startup fails
    // @details:
    //      The worker owns video file I/O, releases each frame reference after processing, accounts for
    //      failed or abandoned frames, and closes the writer on every exit path.
    private void RecordLoop(TaskCompletionSource<bool> started)
    {
        try
        {
            OpenSegment();
            started.SetResult(true);
            foreach (QueuedFrame frame in m_queue.GetConsumingEnumerable())
            {
                using (frame.Pixels)
                {
                    try { WriteFrame(frame); }
                    catch (Exception ex)
                    {
                        lock (m_lock) m_manifest.FailedFrames++;
                        if (m_videoWriter == null || !m_videoWriter.IsOpen) throw;
                        Console.WriteLine("[WARN] [VIDEO] Video writer skipped a frame: " + ex.Message);
                    }
                }
            }
            FinalizeSegment();
        }
        catch (Exception ex)
        {
            m_manifest.Error = ex.Message;
            lock (m_lock)
            {
                m_writerFaulted = true;
                if (!m_queue.IsAddingCompleted) m_queue.CompleteAdding();
            }
            started.TrySetException(ex);
            long abandoned = 0;
            while (m_queue.TryTake(out QueuedFrame frame))
            {
                frame.Pixels.Dispose();
                abandoned++;
            }
            lock (m_lock) m_manifest.FailedFrames += abandoned;
        }
        finally
        {
            try { CloseSegment(); }
            catch (Exception ex)
            {
                m_manifest.Error = (m_manifest.Error ?? "") + " Video writer close failed: " + ex.Message;
            }
        }
    }

    // @brief: Write one queued frame and roll over to a new segment when the byte limit is reached
    // @param:
    //      * frame: Frame buffer and capture offset to write
    private void WriteFrame(QueuedFrame frame)
    {
        if (m_captureOffsets.Count > 0 && m_videoWriter.BytesWritten >= m_segmentLimit)
        {
            FinalizeSegment();
            OpenSegment();
        }

        m_videoWriter.Write(frame.Pixels, frame.CaptureOffsetMicroseconds);
        m_captureOffsets.Add(frame.CaptureOffsetMicroseconds);
    }

    // @brief: Create and initialize the next MP4 segment with the session's writer backend
    private void OpenSegment()
    {
        m_path = m_prefix + "_part" + (m_manifest.Segments.Count + 1).ToString("D4") + ".mp4";
        m_videoWriter = m_ffmpegPath != null
            ? (IVideoSegmentWriter)new FfmpegSegmentWriter(m_ffmpegPath, m_path, m_sourcePixelType)
            : new PylonSegmentWriter(m_path, m_manifest.PlaybackFramesPerSecond, m_sourcePixelType);
        m_captureOffsets = new List<long>();
    }

    // @brief: Close the current segment and add its path, frame count, and timestamps to the manifest
    private void FinalizeSegment()
    {
        CloseSegment();
        if (m_captureOffsets.Count > 0)
        {
            if (new FileInfo(m_path).Length == 0)
                throw new IOException("The video writer produced an empty segment: " + m_path);
            m_manifest.Segments.Add(new Segment
            {
                Path = m_path,
                Frames = m_captureOffsets.Count,
                CaptureOffsetsMicroseconds = m_captureOffsets
            });
        }
        else if (File.Exists(m_path))
            File.Delete(m_path);
    }

    // @brief: Close and dispose the active video writer when one is open
    private void CloseSegment()
    {
        IVideoSegmentWriter writer = m_videoWriter;
        if (writer == null) return;
        m_videoWriter = null;
        try { writer.Close(); }
        finally { writer.Dispose(); }
    }

    // @brief: Finalize any active recording and release recorder resources
    public void Dispose() { StopSession(); }
}
