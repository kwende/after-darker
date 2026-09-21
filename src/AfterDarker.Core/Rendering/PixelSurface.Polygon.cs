using System.Buffers;
using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    /// <summary>Fill a closed polygon using alternate crossing pairs, then cover its cosmetic outline.</summary>
    /// <remarks>Coverage is collected first so XOR and other ROP2 modes touch each pixel only once.
    /// Integer scanlines and the existing line raster define our software edge policy; see bitmap-module-family.md.</remarks>
    public int Polygon(IReadOnlyList<Point16> points, uint? penColor, uint? brushColor,
        RasterMix mix = RasterMix.CopyPen, Point16 origin = default)
    {
        if (points.Count is < 2 or > 256) throw new ArgumentOutOfRangeException(nameof(points));
        byte[] coverage = ArrayPool<byte>.Shared.Rent(Width * Height);
        Array.Clear(coverage, 0, Width * Height);
        try
        {
            if (brushColor is not null)
            {
                double[] crossings = new double[points.Count];
                for (int row = 0; row < Height; row++)
                {
                    int crossingCount = 0, logicalY = row + origin.Y;
                    for (int edge = 0; edge < points.Count; edge++)
                    {
                        Point16 first = points[edge], second = points[(edge + 1) % points.Count];
                        // Half-open vertical edges count each shared vertex once.
                        if (logicalY < Math.Min(first.Y, second.Y) || logicalY >= Math.Max(first.Y, second.Y)) continue;
                        crossings[crossingCount++] = first.X - origin.X +
                            (double)(logicalY - first.Y) * (second.X - first.X) / (second.Y - first.Y);
                    }
                    Array.Sort(crossings, 0, crossingCount);
                    for (int crossing = 0; crossing + 1 < crossingCount; crossing += 2)
                    {
                        int left = Math.Clamp((int)Math.Ceiling(crossings[crossing]), 0, Width);
                        int right = Math.Clamp((int)Math.Ceiling(crossings[crossing + 1]), 0, Width);
                        for (int column = left; column < right; column++) coverage[row * Width + column] = 1;
                    }
                }
            }
            if (penColor is not null)
                for (int edge = 0; edge < points.Count; edge++)
                {
                    Point16 first = points[edge], second = points[(edge + 1) % points.Count];
                    foreach (var pixel in CosmeticLineRasterizer.EnumeratePixels(first.X, first.Y, second.X, second.Y))
                    {
                        int column = pixel.X - origin.X, row = pixel.Y - origin.Y;
                        if (column >= 0 && column < Width && row >= 0 && row < Height) coverage[row * Width + column] = 2;
                    }
                }
            int changed = 0;
            for (int pixel = 0; pixel < Width * Height; pixel++)
                if (coverage[pixel] != 0 && WriteMixedPixel(pixel * 3,
                    coverage[pixel] == 2 ? penColor!.Value : brushColor!.Value, mix)) changed++;
            if (changed != 0 && Revision < long.MaxValue) Revision++;
            return changed;
        }
        finally { ArrayPool<byte>.Shared.Return(coverage); }
    }
}
