using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;

namespace AfterDarker.Runtime;

/// <summary>Bounded surface snapshots after selected import returns, outside Unicorn's native hook.</summary>
/// <remarks>Does not change guest code, registers, pixels, or time. See docs/research/zot-execution.md.</remarks>
public sealed class ImportFrameCapture
{
    private const int MaximumCheckpointsPerDraw = 32;
    private readonly Dictionary<FarPointer16, ImportFrameCheckpoint> checkpoints;
    private readonly byte[] pixels;
    private int visitsThisDraw;

    /// <summary>Lifetime checkpoint visits, whether or not a live presenter is attached.</summary>
    public long TotalVisits { get; private set; }

    /// <summary>Resolve profile-owned NE addresses once and reserve one reusable snapshot buffer.</summary>
    public ImportFrameCapture(IReadOnlyList<ImportFrameCheckpoint> points,
        Func<NeAddress, FarPointer16> resolveCode, int pixelByteCount)
    {
        if (points.Count > 8) throw new ArgumentException("At most eight presentation checkpoints may be configured.", nameof(points));
        if (pixelByteCount is < 1 or > 2048 * 2048 * 3) throw new ArgumentOutOfRangeException(nameof(pixelByteCount));
        foreach (var point in points)
            if (point.MinimumDisplayTime < TimeSpan.Zero || point.MinimumDisplayTime > TimeSpan.FromMilliseconds(100))
                throw new ArgumentException("Presentation holds must be between zero and 100 milliseconds.", nameof(points));
        checkpoints = points.ToDictionary(point => resolveCode(point.ResumeAddress));
        pixels = new byte[pixelByteCount];
    }

    /// <summary>Reset only the per-DRAWFRAME bound, preserving lifetime diagnostics.</summary>
    public void BeginDraw() => visitsThisDraw = 0;

    /// <summary>Match the lifecycle, imported symbol and resumed PC before offering the current image.</summary>
    public void AfterImport(string phase, FarPointer16 resumedAt, NeImport import,
        PixelSurface surface, IntermediateFrameHandler? present)
    {
        if (phase != "DRAWFRAME" || !checkpoints.TryGetValue(resumedAt, out var checkpoint) || checkpoint.Import != import)
            return;
        if (++visitsThisDraw > MaximumCheckpointsPerDraw)
            throw new InvalidOperationException("Intermediate presentation budget exhausted during DRAWFRAME.");
        if (TotalVisits < long.MaxValue) TotalVisits++;
        if (present is null) return;
        surface.CopyRgbTo(pixels);
        present(pixels, checkpoint.MinimumDisplayTime);
    }
}
