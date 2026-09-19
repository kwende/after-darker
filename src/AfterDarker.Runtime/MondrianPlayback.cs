using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>One serialized worker task owns the guest. It never touches UI objects.</summary>
public static class MondrianPlayback
{
    public static Task<MondrianSession.Result> RunAsync(byte[] file, MondrianInitialization.Options options,
        LatestFrameMailbox frames, CancellationToken stop) => Task.Run(async () =>
    {
        stop.ThrowIfCancellationRequested();
        using var session = new MondrianSession(file, options, timing: SessionTiming.Live());
        session.Initialize();
        session.Blank();
        byte[] previous = new byte[session.PixelByteCount], current = new byte[session.PixelByteCount];
        session.CopyPixelsTo(previous);
        frames.Publish(previous, new(0, 0, 0));
        var pacer = new FramePacer(TimeSpan.FromSeconds(1.0 / 60));
        long draws = 0, changes = 0;
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                var returned = session.DrawFrame();
                if (draws < long.MaxValue) draws++;
                session.CopyPixelsTo(current);
                if (!current.AsSpan().SequenceEqual(previous))
                {
                    if (changes < long.MaxValue) changes++;
                    frames.Publish(current, new(draws, changes, returned.State.Rectangles));
                    (previous, current) = (current, previous);
                }
                await pacer.WaitForNextFrameAsync(stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        return session.GetResult();
    });
}
