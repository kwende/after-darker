namespace AfterDarker.Core.Win16;

/// <summary>Resolve supported COLORREF encodings for our true-color software device.</summary>
/// <remarks>PALETTERGB carries RGB plus a flag; use its components directly. PALETTEINDEX
/// still needs a palette. See docs/research/shapes-execution.md for this host adaptation.</remarks>
internal static class Win16Color
{
    private const uint RgbEncoding = 0, PaletteRelativeRgbEncoding = 2;
    private const uint RgbComponentMask = 0x00FFFFFF;
    /// <summary>Return 0x00BBGGRR, rejecting encodings that need unsupported palette state.</summary>
    public static uint ResolveSolidRgb(uint color)
    {
        uint encoding = color >> 24;
        if (encoding is not (RgbEncoding or PaletteRelativeRgbEncoding))
            throw new NotSupportedException("Only RGB and PALETTERGB colors are supported; indexed palettes are not implemented.");
        return color & RgbComponentMask;
    }
}
