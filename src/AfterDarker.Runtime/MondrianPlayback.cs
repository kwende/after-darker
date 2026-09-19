using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Compatibility entry for the original playback tests; the loop is shared.</summary>
public static class MondrianPlayback
{
    public static Task<PlaybackResult> RunAsync(byte[] file, MondrianInitialization.Options options,
        LatestFrameMailbox frames, CancellationToken stop) =>
        AfterDarkPlayback.RunAsync(file, PlaybackOptions.From(options), frames, stop);
}
