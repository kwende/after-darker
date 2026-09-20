using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>
/// The supported device-context state: one persistent surface per guest HDC,
/// identity coordinates (MM_TEXT), full-surface clip, stock black/white brushes and black pen,
/// selected solid pens/brushes and a current drawing position. Lines use copy-pen RGB.
/// Extend explicitly when a module needs other objects, widths or raster modes.
/// </summary>
public sealed class Win16Drawing
{
    /// <summary>Win16 stock-object index understood by the supported GetStockObject handler.</summary>
    public const short BlackBrushIndex = 4;
    /// <summary>Win16 BLACK_PEN stock-object index, distinct from our guest pen handle.</summary>
    public const short BlackPenIndex = 7;
    /// <summary>Host-owned guest identity for the stock black brush; never a native pointer.</summary>
    public const ushort BlackBrushHandle = 0x201; // Guest identity, never a native pointer.
    /// <summary>Host-owned guest identity for the default selected black pen.</summary>
    public const ushort BlackPenHandle = 0x202;
    /// <summary>Host-owned identity for the default white brush, returned when first selecting another brush.</summary>
    public const ushort WhiteBrushHandle = 0x203;
    private const ushort FirstPen = 0x300, PenCapacity = 256;
    private readonly Dictionary<ushort, Win16DeviceContext> contexts = [];
    private readonly Dictionary<ushort, Win16Pen> pens = [];
    /// <summary>Number of owned pen handles; stock objects do not count.</summary>
    public int LivePenCount => pens.Count;
    /// <summary>Maximum concurrent owned pen count observed by this guest.</summary>
    public int PeakPenCount { get; private set; }
    /// <summary>One software-raster observation; LineTo uses Rectangle to carry start/end points.</summary>
    /// <param name="Name">API name responsible for the pixels.</param>
    /// <param name="Hdc">Guest device-context handle.</param>
    /// <param name="Rectangle">Rectangle corners or line endpoints, before clipping.</param>
    /// <param name="ChangedPixels">Pixels whose RGB value actually changed.</param>
    public sealed record Operation(string Name, ushort Hdc, Rectangle16 Rectangle, int ChangedPixels);
    /// <summary>Most recent raster operation, retained independently of full instruction tracing.</summary>
    public Operation? LastOperation { get; private set; }
    /// <summary>Lifetime drawing-operation total, saturating at long.MaxValue.</summary>
    public long OperationCount { get; private set; }
    /// <summary>Associate a nonzero guest HDC handle with a persistent device context.</summary>
    public void Register(ushort hdc, PixelSurface surface)
    {
        if (hdc == 0) throw new ArgumentException("A null HDC is not a surface.");
        contexts.Add(hdc, new(surface));
    }
    /// <summary>Fill black or invert a rectangle on the selected device context surface.</summary>
    public void Paint(ushort hdc, Rectangle16 rectangle, bool invert)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        int changedPixels = deviceContext.Surface.Paint(rectangle, invert);
        LastOperation = new(invert ? "InvertRect" : "FillRect", hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
    }

    /// <summary>Allocate a bounded guest pen; width two is currently supported by Ellipse only.</summary>
    public ushort CreatePen(short style, short width, uint color)
    {
        if (style != 0 || width is < 0 or > 2 || (color >> 24) is not (0 or 2))
            throw new NotSupportedException("Only solid RGB/PALETTERGB pens of width zero, one or two are supported.");
        // PALETTERGB requests a nearest solid RGB color on a palette device.
        // Our true-color surface uses the requested RGB directly, without a palette.
        for (ushort handle = FirstPen; handle < FirstPen + PenCapacity; handle++)
        {
            if (!pens.TryAdd(handle, new(color & 0xFFFFFF, Math.Max(1, (int)width)))) continue;
            PeakPenCount = Math.Max(PeakPenCount, pens.Count);
            return handle;
        }
        throw new InvalidOperationException("Guest pen capacity exhausted (256 objects).");
    }
    /// <summary>Select an object in its own HDC slot and return the previous object of the same kind.</summary>
    public ushort SelectObject(ushort hdc, ushort handle)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        if (handle is BlackBrushHandle or WhiteBrushHandle)
        {
            ushort previousBrush = deviceContext.SelectedBrush;
            deviceContext.SelectedBrush = handle;
            return previousBrush;
        }
        if (handle != BlackPenHandle && !pens.ContainsKey(handle)) throw new NotSupportedException($"Unknown GDI object {handle:X4}.");
        ushort previouslySelectedPen = deviceContext.SelectedPen;
        deviceContext.SelectedPen = handle;
        return previouslySelectedPen;
    }
    /// <summary>Release an unselected owned pen; selected pens cannot be deleted and stock lifetimes are host-owned.</summary>
    public bool DeleteObject(ushort handle)
    {
        if (handle is BlackPenHandle or BlackBrushHandle or WhiteBrushHandle) return true; // stock lifetime is host-owned
        if (contexts.Values.Any(deviceContext => deviceContext.SelectedPen == handle)) return false;
        return pens.Remove(handle);
    }
    /// <summary>Update the current point without drawing, returning the previous signed coordinates packed as Y:X.</summary>
    public uint MoveTo(ushort hdc, short destinationX, short destinationY)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        uint previous = (ushort)deviceContext.CurrentX | ((uint)(ushort)deviceContext.CurrentY << 16);
        deviceContext.CurrentX = destinationX;
        deviceContext.CurrentY = destinationY;
        return previous;
    }
    /// <summary>Draw from the current point, excluding the endpoint, then move the current point to that endpoint.</summary>
    public bool LineTo(ushort hdc, short destinationX, short destinationY)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        Win16Pen pen = SelectedPen(deviceContext);
        if (pen.Width != 1) throw new NotSupportedException("LineTo with a wide pen is not implemented; width two is supported by Ellipse.");
        int changedPixels = deviceContext.Surface.Line(deviceContext.CurrentX, deviceContext.CurrentY, destinationX, destinationY, pen.Color);
        LastOperation = new("LineTo", hdc, new(deviceContext.CurrentX, deviceContext.CurrentY, destinationX, destinationY), changedPixels);
        deviceContext.CurrentX = destinationX;
        deviceContext.CurrentY = destinationY;
        if (OperationCount < long.MaxValue) OperationCount++;
        return true;
    }

    /// <summary>Outline with the selected pen and fill with the selected brush, without using or changing the current point.</summary>
    public bool Ellipse(ushort hdc, Rectangle16 rectangle)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        Win16Pen pen = SelectedPen(deviceContext);
        uint brushColor = deviceContext.SelectedBrush == BlackBrushHandle ? 0u : 0xFFFFFFu;
        int changedPixels = deviceContext.Surface.Ellipse(rectangle, pen.Color, pen.Width, brushColor);
        LastOperation = new("Ellipse", hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
        return true;
    }

    private Win16Pen SelectedPen(Win16DeviceContext deviceContext) =>
        deviceContext.SelectedPen == BlackPenHandle ? new(0, 1) : pens[deviceContext.SelectedPen];
    /// <summary>Resolve a guest HDC or fail before any drawing state changes.</summary>
    private Win16DeviceContext RequireDeviceContext(ushort hdc) =>
        contexts.TryGetValue(hdc, out var deviceContext) ? deviceContext
        : throw new NotSupportedException($"Unknown HDC {hdc:X4}.");
}
