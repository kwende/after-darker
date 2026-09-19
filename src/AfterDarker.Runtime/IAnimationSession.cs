namespace AfterDarker.Runtime;

/// <summary>One loaded guest, called sequentially by a single owner.</summary>
/// <remarks>
/// Initialize, Blank, then DrawFrame repeatedly; Shutdown executes guest cleanup, while
/// Dispose releases native resources. See docs/mondrian-session.md for lifecycle rules.
/// No UI types, native pointers, or CPU-engine calls cross this host-facing boundary.
/// </remarks>
public interface IAnimationSession : IDisposable
{
    /// <summary>Verified module identity used for UI labels and diagnostics.</summary>
    string ModuleName { get; }
    /// <summary>Required size of a tightly packed RGB snapshot buffer.</summary>
    int PixelByteCount { get; }
    /// <summary>Execute DLL startup, PREINITIALIZE and INITIALIZE once.</summary>
    void Initialize();
    /// <summary>Execute the original BLANK handler before drawing.</summary>
    void Blank();
    /// <summary>Execute one bounded DRAWFRAME invocation, preserving guest state between calls.</summary>
    void DrawFrame();
    /// <summary>Copy completed RGB pixels to caller-owned storage; no guest memory is exposed.</summary>
    void CopyPixelsTo(Span<byte> destination);
    /// <summary>Execute CLOSE and WEP from a healthy call boundary; do not call after a guest fault.</summary>
    void Shutdown();
    /// <summary>Return detached, bounded diagnostics before disposing the session.</summary>
    PlaybackResult GetPlaybackResult();
}
