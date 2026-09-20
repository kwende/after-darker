using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>Persistent drawing state behind one guest HDC handle.</summary>
/// <remarks>
/// The supported subset uses identity coordinates, full-surface clipping, solid/null pens and solid brushes.
/// Selecting a pen/brush or moving the current point changes this state without drawing pixels.
/// See docs/win16-implementations.md; this is not a native Windows device context.
/// </remarks>
internal sealed class Win16DeviceContext(PixelSurface surface)
{
    /// <summary>Deterministic pixel destination shared across this guest's drawing calls.</summary>
    public PixelSurface Surface { get; } = surface;
    /// <summary>Guest handle of the selected pen; a selected owned pen cannot be deleted.</summary>
    public ushort SelectedPen { get; set; } = Win16Drawing.BlackPenHandle;
    /// <summary>Brush selection is independent of the pen; a new DC begins with the stock white brush.</summary>
    public ushort SelectedBrush { get; set; } = Win16Drawing.WhiteBrushHandle;
    /// <summary>Current horizontal coordinate used as the next LineTo starting point.</summary>
    public short CurrentX { get; set; }
    /// <summary>Current vertical coordinate used as the next LineTo starting point.</summary>
    public short CurrentY { get; set; }
}
