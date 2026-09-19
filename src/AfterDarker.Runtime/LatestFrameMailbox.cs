namespace AfterDarker.Runtime;

public sealed record FrameInfo(long DrawCalls, long ChangedFrames, ushort Rectangles);

/// <summary>
/// One pending frame, not a queue. The producer and consumer own their buffers;
/// short locked copies keep pixels and metadata coherent without sharing mutable arrays.
/// </summary>
public sealed class LatestFrameMailbox(int byteCount)
{
    private readonly object sync = new();
    private readonly byte[] pending = byteCount > 0 ? new byte[byteCount] : throw new ArgumentOutOfRangeException(nameof(byteCount));
    private FrameInfo? info;
    public void Publish(ReadOnlySpan<byte> pixels, FrameInfo frame)
    {
        if (pixels.Length != pending.Length) throw new ArgumentException("Frame size differs from the mailbox.");
        lock (sync) { pixels.CopyTo(pending); info = frame; }
    }
    public bool TryCopyTo(Span<byte> destination, out FrameInfo? frame)
    {
        if (destination.Length != pending.Length) throw new ArgumentException("Destination size differs from the mailbox.");
        lock (sync)
        {
            frame = info;
            if (frame is null) return false;
            pending.CopyTo(destination);
            info = null;
            return true;
        }
    }
}
