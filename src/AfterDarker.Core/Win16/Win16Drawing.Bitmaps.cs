using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>Non-owned stock bitmap returned by a memory DC's first bitmap selection.</summary>
    public const ushort DefaultBitmapHandle = 0x206;
    private const ushort FirstBitmap = 0x500, BitmapCapacity = 256;
    private const ushort FirstMemoryDc = 0x600, MemoryDcCapacity = 64;
    /// <summary>Per-guest bitmap storage ceiling, independent of the visible surface.</summary>
    public const int BitmapByteCapacity = 32 * 1024 * 1024;
    private readonly Dictionary<ushort, PixelSurface> bitmaps = [];
    /// <summary>Guest-owned bitmaps; deleting a memory DC does not destroy its bitmap.</summary>
    public int LiveBitmapCount => bitmaps.Count;
    /// <summary>Guest-created memory DCs; excludes registered host display DCs.</summary>
    public int LiveMemoryDcCount => contexts.Values.Count(context => context.IsMemory);
    /// <summary>Maximum concurrent bitmap ownership.</summary>
    public int PeakBitmapCount { get; private set; }
    /// <summary>Maximum concurrent memory DC ownership.</summary>
    public int PeakMemoryDcCount { get; private set; }
    /// <summary>RGB bytes owned by the current bitmap pool.</summary>
    public int BitmapBytes { get; private set; }

    /// <summary>Create a memory DC with default drawing attributes and no selected color bitmap.</summary>
    public ushort CreateCompatibleDC(ushort sourceHdc)
    {
        if (sourceHdc != 0) _ = RequireDeviceContext(sourceHdc);
        for (ushort handle = FirstMemoryDc; handle < FirstMemoryDc + MemoryDcCapacity; handle++)
        {
            if (!contexts.TryAdd(handle, new(null, isMemory: true))) continue;
            PeakMemoryDcCount = Math.Max(PeakMemoryDcCount, LiveMemoryDcCount);
            return handle;
        }
        return 0; // Allocation failure is a null Win16 HDC, not a native pointer.
    }

    /// <summary>Allocate bounded RGB storage compatible with a color DC; initial pixels are deterministically black.</summary>
    /// <remarks>Windows does not promise initial bitmap contents. Zero-fill is our documented host policy.</remarks>
    public ushort CreateCompatibleBitmap(ushort sourceHdc, ushort width, ushort height)
    {
        _ = RequireDeviceContext(sourceHdc).Surface; // A default memory DC would request monochrome: fail explicitly.
        if (width == 0 || height == 0)
            throw new NotSupportedException("Zero-sized compatible bitmaps request a monochrome stock bitmap, which is unsupported.");
        if (width > 2048 || height > 2048) return 0;
        int byteCount = width * height * 3;
        if (byteCount > BitmapByteCapacity - BitmapBytes) return 0;
        for (ushort handle = FirstBitmap; handle < FirstBitmap + BitmapCapacity; handle++)
        {
            if (bitmaps.ContainsKey(handle)) continue;
            bitmaps.Add(handle, new(width, height));
            BitmapBytes += byteCount;
            PeakBitmapCount = Math.Max(PeakBitmapCount, LiveBitmapCount);
            return handle;
        }
        return 0;
    }

    /// <summary>Release an owned DC and its selections, retaining separately owned bitmap pixels.</summary>
    public bool DeleteDC(ushort hdc) => contexts.TryGetValue(hdc, out var context) && context.IsMemory && contexts.Remove(hdc);

    private ushort SelectBitmap(Win16DeviceContext context, ushort handle)
    {
        // A color bitmap has exactly one selected owner at a time. Default stock
        // placeholders can be selected by every memory DC, but cannot be rendered.
        if (!context.IsMemory || handle != DefaultBitmapHandle && contexts.Values.Any(
            other => other != context && other.SelectedBitmap == handle)) return 0;
        ushort previous = context.SelectedBitmap;
        context.SelectBitmap(handle, handle == DefaultBitmapHandle ? null : bitmaps[handle]);
        return previous;
    }

    private bool DeleteBitmap(ushort handle)
    {
        if (contexts.Values.Any(context => context.SelectedBitmap == handle)) return false;
        if (!bitmaps.Remove(handle, out var bitmap)) return false;
        BitmapBytes -= bitmap.RgbByteCount;
        return true;
    }
}
