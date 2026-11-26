using System;
using System.Collections.Generic;
using System.Threading;
using Basler.Pylon;

// @brief: Reuse fixed-size raw camera frame buffers across recording and live IPC workers
// @details:
//      Each rented instance starts with one owner reference. Asynchronous consumers add references before
//      queueing the frame and release them when finished. The underlying byte array returns to the shared,
//      bounded pool only after the final reference is disposed, preventing a consumer from observing pixels
//      that capture has already overwritten. When the pool reaches its capacity, TryRent reports a drop
//      instead of allocating unbounded image memory.
//      Buffers hold tightly packed frames in Config.CAMERA_PIXEL_FORMAT; PixelType records the grab's exact type.
public sealed class PooledFrameBuffer : IDisposable
{
    private static readonly object s_poolLock = new object();
    private static readonly Stack<byte[]> s_buffers = new Stack<byte[]>();
    private static int s_created;
    // Both pending queues, one buffer in each worker, and one in capture.
    private const int Capacity = Config.VIDEO_QUEUE_CAPACITY + Config.IPC_CAPTURE_QUEUE_CAPACITY + 3;
    private byte[] m_buffer;
    private int m_references = 1;

    public byte[] Buffer => m_buffer ?? throw new ObjectDisposedException(nameof(PooledFrameBuffer));
    public int Length => Config.RAW_FRAME_BYTE_COUNT;
    // Pixel layout of Buffer, set by capture before the frame is shared with any consumer.
    public PixelType PixelType { get; set; } = PixelType.Undefined;

    // @brief: Wrap a byte array rented from the shared frame buffer pool
    // @param:
    //      * buffer: Image storage owned by this reference-counted frame
    private PooledFrameBuffer(byte[] buffer) { m_buffer = buffer; }

    // @brief: Rent a frame buffer from the pool or allocate one within the configured capacity
    // @param:
    //      * frame: Receives the rented frame, or null when the bounded pool is exhausted
    // @return: True when a frame buffer was successfully obtained
    public static bool TryRent(out PooledFrameBuffer frame)
    {
        lock (s_poolLock)
        {
            byte[] buffer;
            if (s_buffers.Count > 0)
                buffer = s_buffers.Pop();
            else if (s_created < Capacity)
            {
                buffer = new byte[Config.RAW_FRAME_BYTE_COUNT];
                s_created++;
            }
            else
            {
                frame = null;
                return false;
            }
            frame = new PooledFrameBuffer(buffer);
            return true;
        }
    }

    // @brief: Add an ownership reference while the frame buffer is still alive
    // @details: Each asynchronous consumer must add one reference and release it exactly once with Dispose.
    public void AddReference()
    {
        int references;
        do
        {
            references = Volatile.Read(ref m_references);
            if (references <= 0) throw new ObjectDisposedException(nameof(PooledFrameBuffer));
        }
        while (Interlocked.CompareExchange(ref m_references, references + 1, references) != references);
    }

    // @brief: Release one ownership reference and return the byte array to the pool when the count reaches zero
    // @details: Releasing the same reference more than once raises InvalidOperationException.
    public void Dispose()
    {
        int references = Interlocked.Decrement(ref m_references);
        if (references < 0) throw new InvalidOperationException("Frame buffer released more than once.");
        if (references != 0) return;

        byte[] buffer = Interlocked.Exchange(ref m_buffer, null);
        if (buffer == null) throw new InvalidOperationException("Frame buffer released more than once.");
        lock (s_poolLock) s_buffers.Push(buffer);
    }
}
