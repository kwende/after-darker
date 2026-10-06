namespace AfterDarker.Runtime;

/// <summary>Own one worker and mailbox. Stop cancels pacing, then awaits guest cleanup and native disposal.</summary>
/// <remarks>Call lifecycle methods from one host owner. Only copied pixels cross the presentation boundary.</remarks>
public sealed class AfterDarkPlayer : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private Task? stopping;

    /// <summary>The single consumer reads copied RGB24 frames here, including intermediate checkpoints.</summary>
    public LatestFrameMailbox Frames { get; }
    /// <summary>Observe faults during playback; successful completion contains CLOSE/WEP diagnostics.</summary>
    public Task<PlaybackResult> Completion { get; }

    /// <summary>Load verified bytes off the UI thread and start one serialized guest worker.</summary>
    public AfterDarkPlayer(LocalAfterDarkArtifact artifact, PlaybackOptions? options = null)
    {
        options ??= new();
        options.Validate();
        Frames = new(options.Width * options.Height * 3);
        Completion = RunAsync(artifact, options);
    }

    private async Task<PlaybackResult> RunAsync(LocalAfterDarkArtifact artifact, PlaybackOptions options)
    {
        byte[] bytes = await Task.Run(() => AfterDarkLibrary.ReadVerified(artifact), stop.Token).ConfigureAwait(false);
        return await AfterDarkPlayback.RunAsync(bytes, options, Frames, stop.Token).ConfigureAwait(false);
    }

    /// <summary>Idempotently request and await stop; execution failures remain observable to the caller.</summary>
    public Task StopAsync() => stopping ??= StopCoreAsync();

    private async Task StopCoreAsync()
    {
        stop.Cancel();
        try { await Completion.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { stop.Dispose(); }
    }

    /// <summary>Release only after the worker has completed. Never block the UI dispatcher.</summary>
    public ValueTask DisposeAsync() => new(StopAsync());
}
