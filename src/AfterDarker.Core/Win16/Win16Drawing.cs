using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

/// <summary>
/// The supported device-context state: one persistent surface per guest HDC,
/// identity coordinates (MM_TEXT), full-surface clip, and one stock black brush.
/// Extend this state explicitly when a module needs selections or other modes.
/// </summary>
public sealed class Win16Drawing
{
    public const short BlackBrushIndex = 4;
    public const ushort BlackBrushHandle = 0x201; // Guest identity, never a native pointer.
    private readonly Dictionary<ushort, PixelSurface> contexts = [];
    public sealed record Operation(string Name, ushort Hdc, Rectangle16 Rectangle, int ChangedPixels);
    public Operation? LastOperation { get; private set; }
    public long OperationCount { get; private set; }
    public void Register(ushort hdc, PixelSurface surface)
    {
        if (hdc == 0) throw new ArgumentException("A null HDC is not a surface.");
        contexts.Add(hdc, surface);
    }
    public void Paint(ushort hdc, Rectangle16 rectangle, bool invert)
    {
        if (!contexts.TryGetValue(hdc, out var surface)) throw new NotSupportedException($"Unknown HDC {hdc:X4}.");
        int changed = surface.Paint(rectangle, invert);
        LastOperation = new(invert ? "InvertRect" : "FillRect", hdc, rectangle, changed);
        if (OperationCount < long.MaxValue) OperationCount++;
    }
}
