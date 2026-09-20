namespace AfterDarker.Runtime;

/// <summary>One serialized worker task owns the guest. It never touches UI objects.</summary>
public static class AfterDarkPlayback
{
    /// <summary>Run original module code on a background task and publish completed changed frames.</summary>
    /// <param name="file">Original bytes of a supported AD module.</param>
    /// <param name="options">Host settings interpreted by the selected module profile.</param>
    /// <param name="frames">Latest-frame mailbox; the worker never accesses WPF objects.</param>
    /// <param name="stop">Cancellation observed between bounded guest calls, allowing orderly cleanup.</param>
    /// <remarks>See docs/wpf-player.md for ownership and docs/mondrian-session.md for cleanup rules.</remarks>
    public static Task<PlaybackResult> RunAsync(byte[] file, PlaybackOptions options,
        LatestFrameMailbox frames, CancellationToken stop) => Task.Run(async () =>
    {
        stop.ThrowIfCancellationRequested();
        using var session = SupportedModules.Open(file, options, timing: SessionTiming.Live());
        session.Initialize();
        session.Blank();
        byte[] previousFrame = new byte[session.PixelByteCount];
        byte[] currentFrame = new byte[session.PixelByteCount];
        session.CopyPixelsTo(previousFrame);
        frames.Publish(previousFrame, new(0, 0));
        var pacer = new FramePacer(TimeSpan.FromSeconds(1.0 / 60));
        long drawCalls = 0;
        long changedFrames = 0;
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                session.DrawFrame();
                if (drawCalls < long.MaxValue) drawCalls++;
                session.CopyPixelsTo(currentFrame);
                if (!currentFrame.AsSpan().SequenceEqual(previousFrame))
                {
                    if (changedFrames < long.MaxValue) changedFrames++;
                    frames.Publish(currentFrame, new(drawCalls, changedFrames));
                    (previousFrame, currentFrame) = (currentFrame, previousFrame);
                }
                await pacer.WaitForNextFrameAsync(stop).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Expected host stop: the guest is between completed calls and can run cleanup.
        }
        // Cancellation belongs to the host loop/pacer. We deliberately let an
        // active bounded guest call return, so SS:SP is safe for CLOSE and WEP.
        // An execution failure bypasses this code; using still releases Unicorn.
        // Cleanup ignores the already-cancelled pacing token and has its own
        // per-invocation instruction/service/time budgets.
        session.Shutdown();
        return session.GetPlaybackResult();
    });
}
