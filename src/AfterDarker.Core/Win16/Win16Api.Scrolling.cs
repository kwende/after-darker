using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Checked guest RECT marshaling for image scrolling. The backend owns the pixel operation.</summary>
public sealed partial class Win16Api
{
    /// <summary>Move existing pixels and optionally return exposed bounds. Zero far pointers mean omitted rectangles.</summary>
    /// <remarks>All input/output ranges are checked before drawing. The renderer rejects nonzero update regions.
    /// See docs/research/puzzle-execution.md for the exact supported boundary.</remarks>
    public bool ScrollDC(ushort hdc, short horizontal, short vertical, FarPointer16 scrollAddress,
        FarPointer16 clipAddress, ushort updateRegion, FarPointer16 updateAddress)
    {
        Rectangle16? scroll = scrollAddress == default ? null : ReadRectangle(scrollAddress);
        Rectangle16? clip = clipAddress == default ? null : ReadRectangle(clipAddress);
        bool needsUpdate = updateAddress != default;
        // Validate the destination before the scrolling operation changes any pixels.
        if (needsUpdate) State.Memory.ValidateWrite(updateAddress, Rectangle16.ByteCount);
        bool success = Drawing.ScrollDC(hdc, horizontal, vertical, scroll, clip, updateRegion, needsUpdate, out Rectangle16 update);
        if (success && needsUpdate) WriteRectangle(updateAddress, update);
        return success;
    }
}
