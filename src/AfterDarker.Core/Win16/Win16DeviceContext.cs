using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>Persistent drawing state behind one guest HDC handle.</summary>
/// <remarks>
/// The supported subset uses MM_TEXT coordinates with a window origin, full-surface clipping,
/// solid/null pens and brushes. Memory DCs select separately owned color bitmaps.
/// Selecting objects or moving the current point changes state without drawing pixels.
/// See docs/win16-implementations.md; this is not a native Windows device context.
/// </remarks>
internal sealed class Win16DeviceContext(PixelSurface? surface, bool isMemory = false)
{
    private PixelSurface? selectedSurface = surface;
    /// <summary>True for a guest-created DC which accepts bitmap selection; host display DCs do not.</summary>
    public bool IsMemory { get; } = isMemory;
    /// <summary>Selected bitmap identity, including the default stock placeholder for a memory DC.</summary>
    public ushort SelectedBitmap { get; private set; } = isMemory ? Win16Drawing.DefaultBitmapHandle : (ushort)0;
    /// <summary>Current pixel destination; unsupported default monochrome drawing fails explicitly.</summary>
    public PixelSurface Surface => selectedSurface ?? throw new NotSupportedException(
        "Select a color bitmap before drawing into a memory DC; default monochrome bitmap rendering is unsupported.");
    /// <summary>Change only bitmap selection; drawing attributes remain owned by this DC.</summary>
    public void SelectBitmap(ushort handle, PixelSurface? bitmapSurface)
    {
        SelectedBitmap = handle;
        selectedSurface = bitmapSurface;
    }
    /// <summary>Logical coordinate mapped to device pixel (0,0) in the supported MM_TEXT mode.</summary>
    public Point16 WindowOrigin { get; set; } = new(0, 0);
    /// <summary>Boolean combination used by pen strokes and filled shapes, initially ordinary copy.</summary>
    public RasterMix Mix { get; set; } = RasterMix.CopyPen;
    /// <summary>Guest handle of the selected pen; a selected owned pen cannot be deleted.</summary>
    public ushort SelectedPen { get; set; } = Win16Drawing.BlackPenHandle;
    /// <summary>Brush selection is independent of the pen; a new DC begins with the stock white brush.</summary>
    public ushort SelectedBrush { get; set; } = Win16Drawing.WhiteBrushHandle;
    /// <summary>Current horizontal coordinate used as the next LineTo starting point.</summary>
    public short CurrentX { get; set; }
    /// <summary>Current vertical coordinate used as the next LineTo starting point.</summary>
    public short CurrentY { get; set; }
}
