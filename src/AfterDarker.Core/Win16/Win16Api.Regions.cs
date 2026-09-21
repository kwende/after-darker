using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Api
{
    /// <summary>Read a checked signed RECT and allocate a rectangular region.</summary>
    public ushort CreateRectRgnIndirect(FarPointer16 rectangle) => Drawing.CreateRegion(ReadRectangle(rectangle), false);
    /// <summary>Read a checked signed RECT and allocate an elliptic region.</summary>
    public ushort CreateEllipticRgnIndirect(FarPointer16 rectangle) => Drawing.CreateRegion(ReadRectangle(rectangle), true);
    /// <summary>Copy device-coordinate clipping geometry into a DC, or clear it with handle zero.</summary>
    public ushort SelectClipRgn(ushort hdc, ushort region) => Drawing.SelectClipRgn(hdc, region);
}
