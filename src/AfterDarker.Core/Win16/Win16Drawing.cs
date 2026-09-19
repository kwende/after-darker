using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>
/// The supported device-context state: one persistent surface per guest HDC,
/// identity coordinates (MM_TEXT), full-surface clip, a stock black brush/pen,
/// selected solid pens and a current drawing position. Lines use copy-pen RGB.
/// Extend explicitly when a module needs other objects, widths or raster modes.
/// </summary>
public sealed class Win16Drawing
{
    public const short BlackBrushIndex = 4;
    public const ushort BlackBrushHandle = 0x201; // Guest identity, never a native pointer.
    public const ushort BlackPenHandle = 0x202;
    private const ushort FirstPen = 0x300, PenCapacity = 256;
    private sealed class Context(PixelSurface surface)
    {
        public PixelSurface Surface { get; } = surface;
        public ushort Pen = BlackPenHandle;
        public short X, Y;
    }
    private readonly Dictionary<ushort, Context> contexts = [];
    private readonly Dictionary<ushort, uint> pens = [];
    public int LivePenCount => pens.Count;
    public int PeakPenCount { get; private set; }
    public sealed record Operation(string Name, ushort Hdc, Rectangle16 Rectangle, int ChangedPixels);
    public Operation? LastOperation { get; private set; }
    public long OperationCount { get; private set; }
    public void Register(ushort hdc, PixelSurface surface)
    {
        if (hdc == 0) throw new ArgumentException("A null HDC is not a surface.");
        contexts.Add(hdc, new(surface));
    }
    public void Paint(ushort hdc, Rectangle16 rectangle, bool invert)
    {
        if (!contexts.TryGetValue(hdc, out var surface)) throw new NotSupportedException($"Unknown HDC {hdc:X4}.");
        int changed = surface.Surface.Paint(rectangle, invert);
        LastOperation = new(invert ? "InvertRect" : "FillRect", hdc, rectangle, changed);
        if (OperationCount < long.MaxValue) OperationCount++;
    }

    public ushort CreatePen(short style, short width, uint color)
    {
        if (style != 0 || width is < 0 or > 1 || (color >> 24) is not (0 or 2))
            throw new NotSupportedException("Only solid one-pixel RGB/PALETTERGB pens are supported.");
        // PALETTERGB requests a nearest solid RGB color on a palette device.
        // Our true-color surface uses the requested RGB directly, without a palette.
        for (ushort handle = FirstPen; handle < FirstPen + PenCapacity; handle++)
        {
            if (!pens.TryAdd(handle, color & 0xFFFFFF)) continue;
            PeakPenCount = Math.Max(PeakPenCount, pens.Count);
            return handle;
        }
        throw new InvalidOperationException("Guest pen capacity exhausted (256 objects).");
    }
    public ushort SelectObject(ushort hdc, ushort handle)
    {
        Context dc = Require(hdc);
        if (handle != BlackPenHandle && !pens.ContainsKey(handle)) throw new NotSupportedException($"Unknown pen {handle:X4}.");
        ushort previous = dc.Pen; dc.Pen = handle; return previous;
    }
    public bool DeleteObject(ushort handle)
    {
        if (handle == BlackPenHandle || handle == BlackBrushHandle) return true; // stock lifetime is host-owned
        if (contexts.Values.Any(dc => dc.Pen == handle)) return false;
        return pens.Remove(handle);
    }
    public uint MoveTo(ushort hdc, short x, short y)
    {
        Context dc = Require(hdc);
        uint previous = (ushort)dc.X | ((uint)(ushort)dc.Y << 16);
        dc.X = x; dc.Y = y; return previous;
    }
    public bool LineTo(ushort hdc, short x, short y)
    {
        Context dc = Require(hdc);
        uint color = dc.Pen == BlackPenHandle ? 0 : pens[dc.Pen];
        int changed = dc.Surface.Line(dc.X, dc.Y, x, y, color);
        LastOperation = new("LineTo", hdc, new(dc.X, dc.Y, x, y), changed);
        dc.X = x; dc.Y = y;
        if (OperationCount < long.MaxValue) OperationCount++;
        return true;
    }
    private Context Require(ushort hdc) => contexts.TryGetValue(hdc, out var dc) ? dc
        : throw new NotSupportedException($"Unknown HDC {hdc:X4}.");
}
