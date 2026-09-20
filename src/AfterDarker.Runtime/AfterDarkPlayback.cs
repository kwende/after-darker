namespace AfterDarker.Runtime;

/// <summary>One serialized worker task owns the guest. It never touches UI objects.</summary>
public static class AfterDarkPlayback
{
    /// <summary>Run original module code and publish completed frames plus explicitly configured intermediate images.</summary>
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
        session.IntermediateFrameReady += (pixels, minimumDisplayTime) =>
        {
            // We are outside the native hook, on the guest's one worker. Copy to
            // the mailbox, then give the UI time to present this transient image.
            // Stop wakes this short wait, but never interrupts the guest stack:
            // the current DRAWFRAME must still finish before CLOSE/WEP.
            long currentDraw = drawCalls == long.MaxValue ? long.MaxValue : drawCalls + 1;
            if (stop.IsCancellationRequested || !PublishChanged(pixels, currentDraw)) return;
            stop.WaitHandle.WaitOne(minimumDisplayTime);
        };
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                session.DrawFrame();
                if (drawCalls < long.MaxValue) drawCalls++;
                session.CopyPixelsTo(currentFrame);
                PublishChanged(currentFrame, drawCalls);
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

        bool PublishChanged(ReadOnlySpan<byte> pixels, long currentDraw)
        {
            if (pixels.SequenceEqual(previousFrame)) return false;
            if (changedFrames < long.MaxValue) changedFrames++;
            frames.Publish(pixels, new(currentDraw, changedFrames));
            pixels.CopyTo(previousFrame);
            return true;
        }
    });
}
