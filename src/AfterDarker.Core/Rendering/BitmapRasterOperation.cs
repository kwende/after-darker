namespace AfterDarker.Core.Rendering;

/// <summary>Verified ROP3 encodings: these combine source, destination and sometimes the selected brush.</summary>
public enum BitmapRasterOperation : uint
{
    /// <summary>SRCCOPY: replace destination pixels with source pixels.</summary>
    SourceCopy = 0x00CC0020,
    /// <summary>SRCAND: keep bits present in both source and destination.</summary>
    SourceAnd = 0x008800C6,
    /// <summary>DSPDxax: source bits select brush bits (one) or existing destination bits (zero).</summary>
    BrushThroughSourceMask = 0x00E20746,
    /// <summary>PATCOPY: replace destination pixels with selected brush pixels.</summary>
    PatternCopy = 0x00F00021
}
