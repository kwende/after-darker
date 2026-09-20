using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>
/// The supported device-context state: a display surface or selected bitmap per guest HDC,
/// MM_TEXT coordinates with a translated window origin, full-surface clip, stock objects,
/// selected solid pens/brushes, current drawing position and per-DC binary raster mixing.
/// Extend explicitly when a module needs other objects, widths or raster modes.
/// </summary>
public sealed partial class Win16Drawing
{
    /// <summary>Win16 WHITE_BRUSH stock index.</summary>
    public const short WhiteBrushIndex = 0;
    /// <summary>Win16 stock-object index understood by the supported GetStockObject handler.</summary>
    public const short BlackBrushIndex = 4;
    /// <summary>Win16 BLACK_PEN stock-object index, distinct from our guest pen handle.</summary>
    public const short BlackPenIndex = 7;
    /// <summary>Win16 NULL_PEN stock index: draw fills without an outline.</summary>
    public const short NullPenIndex = 8;
    /// <summary>NULL_BRUSH/HOLLOW_BRUSH stock index: outline a shape without filling its interior.</summary>
    public const short NullBrushIndex = 5;
    /// <summary>Host-owned guest identity for the stock black brush; never a native pointer.</summary>
    public const ushort BlackBrushHandle = 0x201; // Guest identity, never a native pointer.
    /// <summary>Host-owned guest identity for the default selected black pen.</summary>
    public const ushort BlackPenHandle = 0x202;
    /// <summary>Host-owned identity for the default white brush, returned when first selecting another brush.</summary>
    public const ushort WhiteBrushHandle = 0x203;
    /// <summary>Stock pen which suppresses strokes; this is not a null/invalid handle.</summary>
    public const ushort NullPenHandle = 0x204;
    /// <summary>Host-owned identity for a brush which leaves the interior untouched.</summary>
    public const ushort NullBrushHandle = 0x205;
    private const ushort FirstPen = 0x300, PenCapacity = 256;
    private const ushort FirstBrush = 0x400, BrushCapacity = 256;
    private readonly Dictionary<ushort, Win16DeviceContext> contexts = [];
    private readonly Dictionary<ushort, Win16Pen> pens = [];
    private readonly Dictionary<ushort, uint> brushColors = [];
    /// <summary>Owned solid brushes, excluding stock objects.</summary>
    public int LiveBrushCount => brushColors.Count;
    /// <summary>Maximum simultaneous owned brushes observed by this guest.</summary>
    public int PeakBrushCount { get; private set; }
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
        if (hdc >= FirstPen && hdc < FirstMemoryDc + MemoryDcCapacity)
            throw new ArgumentException("Host HDC identity overlaps the reserved guest GDI object ranges.");
        contexts.Add(hdc, new(surface));
    }
    /// <summary>Return the signed logical origin packed as Y:X, as required by Win16 GetWindowOrg.</summary>
    public uint GetWindowOrg(ushort hdc)
    {
        Point16 origin = RequireDeviceContext(hdc).WindowOrigin;
        return (ushort)origin.X | ((uint)(ushort)origin.Y << 16);
    }
    /// <summary>Map the given logical point to pixel zero and return the previous packed origin.</summary>
    public uint SetWindowOrg(ushort hdc, short horizontal, short vertical)
    {
        uint previous = GetWindowOrg(hdc);
        RequireDeviceContext(hdc).WindowOrigin = new(horizontal, vertical);
        return previous;
    }
    /// <summary>Set the pen/brush mix mode and return its previous value; invalid modes fail before changing state.</summary>
    public ushort SetROP2(ushort hdc, ushort mode)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        if (mode is < 1 or > 16) return 0;
        ushort previous = (ushort)context.Mix;
        context.Mix = (RasterMix)mode;
        return previous;
    }
    /// <summary>Fill black or invert a rectangle on the selected device context surface.</summary>
    public void Paint(ushort hdc, Rectangle16 rectangle, bool invert)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        int changedPixels = deviceContext.Surface.Paint(rectangle, invert, deviceContext.WindowOrigin);
        LastOperation = new(invert ? "InvertRect" : "FillRect", hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
    }

    /// <summary>Allocate a bounded guest pen; width two is currently supported by Ellipse only.</summary>
    public ushort CreatePen(short style, short width, uint color)
    {
        if (style != 0 || width is < 0 or > 3)
            throw new NotSupportedException($"Only solid RGB/PALETTERGB pens of width zero through three are supported (style={style}, width={width}, color={color:X8}).");
        uint rgb = Win16Color.ResolveSolidRgb(color);
        for (ushort handle = FirstPen; handle < FirstPen + PenCapacity; handle++)
        {
            if (!pens.TryAdd(handle, new(rgb, Math.Max(1, (int)width)))) continue;
            PeakPenCount = Math.Max(PeakPenCount, pens.Count);
            return handle;
        }
        throw new InvalidOperationException("Guest pen capacity exhausted (256 objects).");
    }
    /// <summary>Create a solid brush in a bounded handle range disjoint from pens and stock objects.</summary>
    public ushort CreateSolidBrush(uint color)
    {
        uint rgb = Win16Color.ResolveSolidRgb(color);
        for (ushort handle = FirstBrush; handle < FirstBrush + BrushCapacity; handle++)
        {
            if (!brushColors.TryAdd(handle, rgb)) continue;
            PeakBrushCount = Math.Max(PeakBrushCount, brushColors.Count);
            return handle;
        }
        throw new InvalidOperationException("Guest brush capacity exhausted (256 objects).");
    }
    /// <summary>Select an object in its own HDC slot and return the previous object of the same kind.</summary>
    public ushort SelectObject(ushort hdc, ushort handle)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        if (handle == DefaultBitmapHandle || bitmaps.ContainsKey(handle)) return SelectBitmap(deviceContext, handle);
        if (handle is BlackBrushHandle or WhiteBrushHandle or NullBrushHandle || brushColors.ContainsKey(handle))
        {
            ushort previousBrush = deviceContext.SelectedBrush;
            deviceContext.SelectedBrush = handle;
            return previousBrush;
        }
        if (handle is not (BlackPenHandle or NullPenHandle) && !pens.ContainsKey(handle)) throw new NotSupportedException($"Unknown GDI object {handle:X4}.");
        ushort previouslySelectedPen = deviceContext.SelectedPen;
        deviceContext.SelectedPen = handle;
        return previouslySelectedPen;
    }
    /// <summary>Release an unselected pen, brush or bitmap, or delete a memory DC; stock lifetimes remain host-owned.</summary>
    public bool DeleteObject(ushort handle)
    {
        // Gravity deletes its per-frame memory DC with DeleteObject. GDI permits
        // this alias of DeleteDC; removing the DC releases its bitmap selection.
        if (contexts.ContainsKey(handle)) return DeleteDC(handle);
        if (handle == DefaultBitmapHandle) return true;
        if (bitmaps.ContainsKey(handle)) return DeleteBitmap(handle);
        if (handle is BlackPenHandle or NullPenHandle or BlackBrushHandle or WhiteBrushHandle or NullBrushHandle) return true;
        if (contexts.Values.Any(context => context.SelectedPen == handle || context.SelectedBrush == handle)) return false;
        return pens.Remove(handle) || brushColors.Remove(handle);
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
        Win16Pen? pen = SelectedPen(deviceContext);
        if (pen is { Width: 2 }) throw new NotSupportedException("Width-two LineTo is not implemented; Ellipse supports that width.");
        int changedPixels = pen is null ? 0 : pen.Width == 3
            ? deviceContext.Surface.WideLine(deviceContext.CurrentX, deviceContext.CurrentY, destinationX, destinationY,
                pen.Color, deviceContext.Mix, deviceContext.WindowOrigin)
            : deviceContext.Surface.Line(deviceContext.CurrentX, deviceContext.CurrentY, destinationX, destinationY,
                pen.Color, deviceContext.Mix, deviceContext.WindowOrigin);
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
        Win16Pen? pen = SelectedPen(deviceContext);
        uint? brushColor = SelectedBrushColor(deviceContext);
        int changedPixels = deviceContext.Surface.Ellipse(rectangle, pen?.Color ?? 0, pen?.Width ?? 0, brushColor,
            deviceContext.Mix, deviceContext.WindowOrigin);
        LastOperation = new("Ellipse", hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
        return true;
    }

    /// <summary>Fill a rectangle and optionally outline it with a one-pixel pen; leave the current point unchanged.</summary>
    public bool Rectangle(ushort hdc, Rectangle16 rectangle)
    {
        Win16DeviceContext deviceContext = RequireDeviceContext(hdc);
        Win16Pen? pen = SelectedPen(deviceContext);
        if (pen is { Width: not 1 }) throw new NotSupportedException("Rectangle supports a null or one-pixel pen only.");
        int changedPixels = deviceContext.Surface.Rectangle(rectangle, pen?.Color ?? 0, pen is not null,
            SelectedBrushColor(deviceContext), deviceContext.Mix, deviceContext.WindowOrigin);
        LastOperation = new("Rectangle", hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
        return true;
    }

    private uint ResolveBrushColor(ushort handle) => handle switch
    {
        BlackBrushHandle => 0,
        WhiteBrushHandle => 0xFFFFFF,
        _ => brushColors.TryGetValue(handle, out uint color) ? color
            : throw new NotSupportedException($"Unknown brush {handle:X4}.")
    };

    private uint? SelectedBrushColor(Win16DeviceContext context) =>
        context.SelectedBrush == NullBrushHandle ? null : ResolveBrushColor(context.SelectedBrush);

    // Managed null means the selected NULL_PEN suppresses strokes. An unknown
    // guest object is rejected by SelectObject; it cannot silently land here.
    private Win16Pen? SelectedPen(Win16DeviceContext deviceContext) => deviceContext.SelectedPen switch
    {
        NullPenHandle => null,
        BlackPenHandle => new(0, 1),
        _ => pens[deviceContext.SelectedPen]
    };
    /// <summary>Resolve a guest HDC or fail before any drawing state changes.</summary>
    private Win16DeviceContext RequireDeviceContext(ushort hdc) =>
        contexts.TryGetValue(hdc, out var deviceContext) ? deviceContext
        : throw new NotSupportedException($"Unknown HDC {hdc:X4}.");
}
