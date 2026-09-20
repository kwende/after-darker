using System.Buffers;

namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    /// <summary>Combine source, destination and optional brush pixels with a supported ROP3, preserving overlapping source pixels.</summary>
    /// <remarks>Clips to both surfaces. The explicit bitmap operation is independent of a DC's pen/brush ROP2 setting.</remarks>
    public int CopyRegion(PixelSurface source, int destinationX, int destinationY, int width, int height,
        int sourceX, int sourceY, BitmapRasterOperation operation = BitmapRasterOperation.SourceCopy, uint brushColor = 0)
    {
        if (operation is not (BitmapRasterOperation.SourceCopy or BitmapRasterOperation.SourceAnd or BitmapRasterOperation.BrushThroughSourceMask))
            throw new NotSupportedException($"Unsupported bitmap raster operation {(uint)operation:X8}.");
        if (width < 0 || height < 0) throw new NotSupportedException("Negative BitBlt extents are not supported.");
        // Bound work by actual destination pixels, not the caller's requested extents.
        int firstColumn = Math.Max(0, Math.Max(-destinationX, -sourceX));
        int lastColumn = Math.Min(width, Math.Min(Width - destinationX, source.Width - sourceX));
        int firstRow = Math.Max(0, Math.Max(-destinationY, -sourceY));
        int lastRow = Math.Min(height, Math.Min(Height - destinationY, source.Height - sourceY));
        if (firstColumn >= lastColumn || firstRow >= lastRow) return 0;

        // A single DC may be source and destination. Snapshot before any write so
        // a rightward/downward move cannot read pixels it just overwrote. Pooling
        // bounds transient storage to one source surface, without retaining history.
        byte[] snapshot = ArrayPool<byte>.Shared.Rent(source.pixels.Length);
        try
        {
            source.pixels.CopyTo(snapshot, 0);
            int changed = 0;
            for (int row = firstRow; row < lastRow; row++)
            for (int column = firstColumn; column < lastColumn; column++)
            {
                int sourceOffset = ((sourceY + row) * source.Width + sourceX + column) * 3;
                uint color = (uint)(snapshot[sourceOffset] | snapshot[sourceOffset + 1] << 8 | snapshot[sourceOffset + 2] << 16);
                int destinationOffset = ((destinationY + row) * Width + destinationX + column) * 3;
                uint destinationColor = (uint)(pixels[destinationOffset] | pixels[destinationOffset + 1] << 8 | pixels[destinationOffset + 2] << 16);
                uint result = operation switch
                {
                    BitmapRasterOperation.SourceCopy => color,
                    BitmapRasterOperation.SourceAnd => color & destinationColor,
                    // The source is a per-bit mask; zero preserves old pixels,
                    // one paints the DC's brush. This is ROP3, independent of ROP2.
                    _ => (color & brushColor) | (~color & destinationColor)
                };
                if (WriteMixedPixel(destinationOffset, result, RasterMix.CopyPen)) changed++;
            }
            if (changed != 0 && Revision < long.MaxValue) Revision++;
            return changed;
        }
        finally { ArrayPool<byte>.Shared.Return(snapshot); }
    }
}
