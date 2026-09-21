using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

/// <summary>Immutable device-coordinate coverage of a rectangular or elliptic clipping region.</summary>
/// <remarks>Ellipse coverage uses pixel centers; this is a documented software approximation.
/// Selecting a region copies this value into the DC. It does not retain the guest region handle.</remarks>
public sealed record RasterClipRegion(Rectangle16 Bounds, bool Elliptic)
{
    /// <summary>Reversed or zero extents define an empty region.</summary>
    public bool IsEmpty => Bounds.Left >= Bounds.Right || Bounds.Top >= Bounds.Bottom;

    /// <summary>Test a device pixel without applying the DC's logical window origin again.</summary>
    public bool Contains(int horizontal, int vertical)
    {
        if (horizontal < Bounds.Left || horizontal >= Bounds.Right || vertical < Bounds.Top || vertical >= Bounds.Bottom)
            return false;
        if (!Elliptic) return true;
        long width = Bounds.Right - Bounds.Left, height = Bounds.Bottom - Bounds.Top;
        long centeredX = 2L * horizontal + 1 - Bounds.Left - Bounds.Right;
        long centeredY = 2L * vertical + 1 - Bounds.Top - Bounds.Bottom;
        // Int128 keeps even extreme signed Win16 coordinates safe from overflow.
        return (Int128)centeredX * centeredX * height * height +
            (Int128)centeredY * centeredY * width * width <= (Int128)width * width * height * height;
    }
}
