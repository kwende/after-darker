using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>Persistent drawing state behind one guest HDC handle.</summary>
/// <remarks>
/// The supported subset uses MM_TEXT coordinates with a window origin and copied region clipping,
/// solid/null pens and brushes. Memory DCs select separately owned color bitmaps.
/// Selecting objects or moving the current point changes state without drawing pixels.
/// See docs/win16-implementations.md; this is not a native Windows device context.
/// </remarks>
internal sealed class Win16DeviceContext(PixelSurface? surface, bool isMemory = false)
{
    private PixelSurface? selectedSurface = surface;
    private readonly PixelSurface defaultMonochromePixel = new(1, 1);
    /// <summary>Device-coordinate clip copied from a region; null means the full destination.</summary>
    public RasterClipRegion? Clip { get; set; }
    /// <summary>Draw synchronously with this DC's clip, without attaching clipping state to the bitmap.</summary>
    public T Draw<T>(Func<PixelSurface, T> operation)
    {
        if (IsMonochrome) throw new NotSupportedException("Drawing into loaded monochrome resources is not implemented.");
        return Surface.WithClip(Clip, operation);
    }
    /// <summary>Loaded black/white one-bit source; supported as a blit source only.</summary>
    public bool IsMonochrome { get; private set; }
    /// <summary>Color used for zero bits when expanding a monochrome blit source.</summary>
    public uint TextColor { get; set; }
    /// <summary>Color used for one bits when expanding a monochrome blit source.</summary>
    public uint BackgroundColor { get; set; } = 0xFFFFFF;
    /// <summary>OPAQUE (2) initially, or TRANSPARENT (1); retained independently of BitBlt.</summary>
    public ushort BackgroundMode { get; set; } = 2;
    /// <summary>The narrow default-bitmap operation observed before Punch Out selects its owned bitmap.</summary>
    public int DrawRectangle(Rectangle16 rectangle, uint? pen, uint? brush)
    {
        if (IsMonochrome) throw new NotSupportedException("Drawing into loaded monochrome resources is not implemented.");
        if (selectedSurface is null && (pen is not (null or 0 or 0xFFFFFF) || brush is not (null or 0 or 0xFFFFFF)))
            throw new NotSupportedException("Default monochrome Rectangle supports black/white objects only.");
        return (selectedSurface ?? defaultMonochromePixel).WithClip(Clip, destination =>
            destination.Rectangle(rectangle, pen ?? 0, pen is not null, brush, Mix, WindowOrigin));
    }
    /// <summary>True for a guest-created DC which accepts bitmap selection; host display DCs do not.</summary>
    public bool IsMemory { get; } = isMemory;
    /// <summary>Selected bitmap identity, including the default stock placeholder for a memory DC.</summary>
    public ushort SelectedBitmap { get; private set; } = isMemory ? Win16Drawing.DefaultBitmapHandle : (ushort)0;
    /// <summary>Selected owned pixels; default-bitmap access is limited to DrawRectangle.</summary>
    public PixelSurface Surface => selectedSurface ?? throw new NotSupportedException(
        "Select a color bitmap before drawing into a memory DC; default monochrome bitmap rendering is unsupported.");
    /// <summary>Change only bitmap selection; drawing attributes remain owned by this DC.</summary>
    public void SelectBitmap(ushort handle, PixelSurface? bitmapSurface, bool monochrome)
    {
        SelectedBitmap = handle;
        selectedSurface = bitmapSurface;
        IsMonochrome = monochrome;
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
