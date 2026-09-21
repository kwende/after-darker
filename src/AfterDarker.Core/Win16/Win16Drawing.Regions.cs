using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    private const ushort FirstRegion = 0x700, RegionCapacity = 256;
    private readonly Dictionary<ushort, RasterClipRegion> regions = [];
    /// <summary>Owned region handles; a copied DC clip is not a live region handle.</summary>
    public int LiveRegionCount => regions.Count;
    /// <summary>Maximum concurrent owned regions.</summary>
    public int PeakRegionCount { get; private set; }

    /// <summary>Create bounded immutable region geometry, independent of any bitmap or DC.</summary>
    public ushort CreateRegion(Rectangle16 rectangle, bool elliptic)
    {
        for (ushort handle = FirstRegion; handle < FirstRegion + RegionCapacity; handle++)
        {
            if (!regions.TryAdd(handle, new(rectangle, elliptic))) continue;
            PeakRegionCount = Math.Max(PeakRegionCount, regions.Count);
            return handle;
        }
        throw new InvalidOperationException("Guest region capacity exhausted (256 objects).");
    }

    /// <summary>Copy region geometry into the DC; zero removes its explicit clip.</summary>
    /// <returns>NULLREGION (1), SIMPLEREGION (2), COMPLEXREGION (3), or ERROR (0).</returns>
    public ushort SelectClipRgn(ushort hdc, ushort region)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        if (region == 0) { context.Clip = null; return 2; }
        if (!regions.TryGetValue(region, out RasterClipRegion? geometry)) return 0;
        context.Clip = geometry with { };
        return geometry.IsEmpty ? (ushort)1 : geometry.Elliptic ? (ushort)3 : (ushort)2;
    }
}
