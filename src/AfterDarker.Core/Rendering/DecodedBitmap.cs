namespace AfterDarker.Core.Rendering;

/// <summary>A decoded DIB resource, independent of NE loading, CPU emulation, GDI handles and WPF.</summary>
/// <param name="Width">Positive pixel width.</param>
/// <param name="Height">Positive pixel height; output rows always run top to bottom.</param>
/// <param name="SourceBitsPerPixel">Bit depth retained for inspection; output is always RGB.</param>
/// <param name="Rgb">Owned tightly packed R,G,B bytes.</param>
public sealed record DecodedBitmap(int Width, int Height, ushort SourceBitsPerPixel, byte[] Rgb)
{
    /// <summary>Canonical black/white one-bit bitmap: GDI expands zero/one through destination text/background colors.</summary>
    public bool IsMonochrome { get; init; }
}
