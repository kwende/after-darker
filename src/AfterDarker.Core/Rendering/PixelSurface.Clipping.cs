namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    private RasterClipRegion? activeClip;

    /// <summary>Apply one DC's clip only for the duration of a synchronous drawing operation.</summary>
    /// <remarks>Surfaces, like the guest, have one execution owner. The finally restores state even
    /// after errors, so another DC sharing these pixels never inherits this DC's clip.</remarks>
    public T WithClip<T>(RasterClipRegion? clip, Func<PixelSurface, T> operation)
    {
        RasterClipRegion? previous = activeClip;
        activeClip = clip;
        try { return operation(this); }
        finally { activeClip = previous; }
    }

    private bool IsPixelVisible(int horizontal, int vertical) => activeClip?.Contains(horizontal, vertical) ?? true;
}
