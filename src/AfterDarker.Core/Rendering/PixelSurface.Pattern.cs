namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    /// <summary>Copy a solid pattern over signed extents, normalizing before clipping without 16-bit edge wrap.</summary>
    public int PaintPattern(int left, int top, int width, int height, uint color)
    {
        // Keep addition wide even for public callers. Guest coordinates/extents
        // are signed words but their summed edges may exceed the word range.
        long right = (long)left + width, bottom = (long)top + height;
        int firstColumn = (int)Math.Clamp(Math.Min(left, right), 0, Width);
        int lastColumn = (int)Math.Clamp(Math.Max(left, right), 0, Width);
        int firstRow = (int)Math.Clamp(Math.Min(top, bottom), 0, Height);
        int lastRow = (int)Math.Clamp(Math.Max(top, bottom), 0, Height);
        int changed = 0;
        for (int row = firstRow; row < lastRow; row++)
        for (int column = firstColumn; column < lastColumn; column++)
            if (WriteMixedPixel((row * Width + column) * 3, color, RasterMix.CopyPen)) changed++;
        if (changed != 0 && Revision < long.MaxValue) Revision++;
        return changed;
    }
}
