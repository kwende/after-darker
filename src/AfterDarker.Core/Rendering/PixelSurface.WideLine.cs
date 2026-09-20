using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    /// <summary>Rasterize a three-pixel solid stroke as a round-ended capsule, mixing each covered pixel once.</summary>
    /// <remarks>
    /// This is a deterministic geometric approximation, not exact historical GDI edge coverage.
    /// A pixel receives ROP2 once even where the end caps overlap; repeated XOR writes would erase it.
    /// Work is bounded by the clipped surface rectangle. See the Stained Glass raster comparison.
    /// </remarks>
    public int WideLine(short startX, short startY, short endX, short endY, uint color,
        RasterMix mix = RasterMix.CopyPen, Point16 origin = default)
    {
        int firstX = startX - origin.X, firstY = startY - origin.Y;
        int lastX = endX - origin.X, lastY = endY - origin.Y;
        double horizontal = lastX - firstX, vertical = lastY - firstY;
        double lengthSquared = horizontal * horizontal + vertical * vertical;
        int left = Math.Max(0, Math.Min(firstX, lastX) - 1), right = Math.Min(Width - 1, Math.Max(firstX, lastX) + 1);
        int top = Math.Max(0, Math.Min(firstY, lastY) - 1), bottom = Math.Min(Height - 1, Math.Max(firstY, lastY) + 1);
        const double radiusSquared = 1.5 * 1.5;
        int changed = 0;
        for (int row = top; row <= bottom; row++)
        for (int column = left; column <= right; column++)
        {
            double position = lengthSquared == 0 ? 0 : Math.Clamp(
                ((column - firstX) * horizontal + (row - firstY) * vertical) / lengthSquared, 0, 1);
            double distanceX = column - (firstX + position * horizontal);
            double distanceY = row - (firstY + position * vertical);
            if (distanceX * distanceX + distanceY * distanceY > radiusSquared) continue;
            if (WriteMixedPixel((row * Width + column) * 3, color, mix)) changed++;
        }
        if (changed != 0 && Revision < long.MaxValue) Revision++;
        return changed;
    }
}
