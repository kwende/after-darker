namespace AfterDarker.Core.Rendering;

/// <summary>GDI's binary ROP2 modes: combine a pen/brush color with the existing destination color.</summary>
/// <remarks>Bit blits have a separate ternary ROP contract. See docs/research/stained-glass-execution.md.</remarks>
public enum RasterMix : ushort
{
    /// <summary>Write zero to each RGB channel.</summary>
    Black = 1,
    /// <summary>Invert the bitwise OR of source and destination.</summary>
    NotMergePen = 2,
    /// <summary>Keep destination bits absent from the source.</summary>
    MaskNotPen = 3,
    /// <summary>Write the inverted source color.</summary>
    NotCopyPen = 4,
    /// <summary>Keep source bits absent from the destination.</summary>
    MaskPenNot = 5,
    /// <summary>Invert the destination, independently of the source color.</summary>
    InvertDestination = 6,
    /// <summary>XOR source and destination; repeating identical coverage restores the image.</summary>
    XorPen = 7,
    /// <summary>Invert the bitwise AND of source and destination.</summary>
    NotMaskPen = 8,
    /// <summary>Keep bits present in both source and destination.</summary>
    MaskPen = 9,
    /// <summary>Invert the bitwise XOR of source and destination.</summary>
    NotXorPen = 10,
    /// <summary>Leave the destination unchanged.</summary>
    LeaveDestination = 11,
    /// <summary>OR the inverted source with the destination.</summary>
    MergeNotPen = 12,
    /// <summary>Replace destination with source; this is a new DC's default.</summary>
    CopyPen = 13,
    /// <summary>OR the source with the inverted destination.</summary>
    MergePenNot = 14,
    /// <summary>Keep bits present in either source or destination.</summary>
    MergePen = 15,
    /// <summary>Write all ones to each RGB channel.</summary>
    White = 16
}

/// <summary>Pure RGB Boolean operations; the top COLORREF byte is not a color channel.</summary>
internal static class RasterMixOperations
{
    public static uint Apply(RasterMix mode, uint source, uint destination) => (mode switch
    {
        RasterMix.Black => 0u,
        RasterMix.NotMergePen => ~(source | destination),
        RasterMix.MaskNotPen => ~source & destination,
        RasterMix.NotCopyPen => ~source,
        RasterMix.MaskPenNot => source & ~destination,
        RasterMix.InvertDestination => ~destination,
        RasterMix.XorPen => source ^ destination,
        RasterMix.NotMaskPen => ~(source & destination),
        RasterMix.MaskPen => source & destination,
        RasterMix.NotXorPen => ~(source ^ destination),
        RasterMix.LeaveDestination => destination,
        RasterMix.MergeNotPen => ~source | destination,
        RasterMix.CopyPen => source,
        RasterMix.MergePenNot => source | ~destination,
        RasterMix.MergePen => source | destination,
        RasterMix.White => 0xFFFFFFu,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    }) & 0xFFFFFF;
}
