using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

/// <summary>Deterministic RGB pixels. No native GDI pointers, windows, scaling, or antialiasing.</summary>
public sealed partial class PixelSurface
{
    private readonly byte[] pixels;
    /// <summary>Surface width in guest pixels.</summary>
    public int Width { get; }
    /// <summary>Surface height in guest pixels.</summary>
    public int Height { get; }
    /// <summary>Required size of a tightly packed RGB copy buffer.</summary>
    public int RgbByteCount => pixels.Length;
    /// <summary>Saturating counter incremented once per operation that changes at least one pixel.</summary>
    public long Revision { get; private set; }
    /// <summary>Allocate a bounded black RGB surface.</summary>
    public PixelSurface(int width, int height)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be 1..2048.");
        Width = width;
        Height = height;
        pixels = new byte[checked(width * height * 3)];
    }
    /// <summary>Create a detached RGB snapshot owned by the caller.</summary>
    public byte[] CopyRgb() => (byte[])pixels.Clone();

    /// <summary>Copy a complete host-provided RGB image into this surface, without retaining the source buffer.</summary>
    /// <remarks>
    /// This supplies initial content for modules that erase an existing image. It is not a guest GDI call.
    /// Bytes are tightly packed R,G,B rows in top-to-bottom order; dimensions must already match.
    /// See docs/research/fade-away-execution.md for the white-image policy and future desktop input boundary.
    /// </remarks>
    public void LoadRgb(ReadOnlySpan<byte> source)
    {
        if (source.Length != pixels.Length)
            throw new ArgumentException("Source must match the surface's RGB byte count.", nameof(source));
        if (source.SequenceEqual(pixels)) return;
        source.CopyTo(pixels);
        if (Revision < long.MaxValue) Revision++;
    }

    /// <summary>Draw a solid COLORREF line, excluding its endpoint and clipping only the generated pixels.</summary>
    /// <returns>The count of pixels whose RGB value changed.</returns>
    public int Line(short startX, short startY, short endX, short endY, uint color,
        RasterMix mix = RasterMix.CopyPen, Point16 origin = default)
    {
        // COLORREF is 0x00BBGGRR. The surface stores the channels in RGB byte order.
        int changedPixels = 0;

        foreach (var pixel in CosmeticLineRasterizer.EnumeratePixels(startX, startY, endX, endY))
        {
            int deviceX = pixel.X - origin.X, deviceY = pixel.Y - origin.Y;
            if (deviceX < 0 || deviceX >= Width || deviceY < 0 || deviceY >= Height)
            {
                continue;
            }

            if (WriteMixedPixel((deviceY * Width + deviceX) * 3, color, mix)) changedPixels++;
        }

        if (changedPixels != 0 && Revision < long.MaxValue)
        {
            Revision++;
        }
        return changedPixels;
    }

    /// <summary>Copy into the host's reusable tightly packed RGB buffer, without allocating a snapshot.</summary>
    public void CopyRgbTo(Span<byte> destination)
    {
        if (destination.Length != pixels.Length) throw new ArgumentException("Destination must match the surface's RGB byte count.", nameof(destination));
        pixels.AsSpan().CopyTo(destination);
    }

    /// <summary>Fill and outline a clipped ellipse with solid COLORREFs; return the number of changed pixels.</summary>
    /// <remarks>Here penWidth zero means NULL_PEN, not CreatePen's zero-width cosmetic pen.
    /// Wider stroke approximations are documented in EllipseRasterizer and the execution guides.</remarks>
    public int Ellipse(Rectangle16 rectangle, uint penColor, int penWidth, uint? brushColor,
        RasterMix mix = RasterMix.CopyPen, Point16 origin = default)
    {
        if (penWidth is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(penWidth));
        EllipseRasterizer.Row[] rows = EllipseRasterizer.BuildRows(rectangle, Math.Max(1, penWidth), Height, origin);
        int changedPixels = 0;
        for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            int firstColumn = Math.Max(0, row.Left), lastColumn = Math.Min(Width - 1, row.Right);
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                uint? color = penWidth == 0 || column >= row.BrushLeft && column <= row.BrushRight ? brushColor : penColor;
                int byteOffset = (rowIndex * Width + column) * 3;
                if (color is uint paintedColor && WriteMixedPixel(byteOffset, paintedColor, mix)) changedPixels++;
            }
        }
        if (changedPixels != 0 && Revision < long.MaxValue) Revision++;
        return changedPixels;
    }

    /// <summary>Draw a solid rectangle with the tested GDI bounds and optional one-pixel outline.</summary>
    /// <remarks>NULL_PEN contracts the right/bottom extent by one extra pixel in the native oracle.
    /// Clip after deciding the original edges, so off-screen edges do not become visible borders.</remarks>
    public int Rectangle(Rectangle16 rectangle, uint penColor, bool outline, uint? brushColor,
        RasterMix mix = RasterMix.CopyPen, Point16 origin = default)
    {
        int left = Math.Min(rectangle.Left, rectangle.Right), right = Math.Max(rectangle.Left, rectangle.Right);
        int top = Math.Min(rectangle.Top, rectangle.Bottom), bottom = Math.Max(rectangle.Top, rectangle.Bottom);
        left -= origin.X; right -= origin.X; top -= origin.Y; bottom -= origin.Y;
        // Native Rectangle differs from FillRect: a null pen removes the last
        // row/column; a 1x1 outlined rectangle paints nothing. These are checked
        // against Windows in SelectedRectangleRasterTests, including reversed bounds.
        if (!outline) { right--; bottom--; }
        else if (right - left == 1 && bottom - top == 1) return 0;
        int changedPixels = 0;
        for (int row = Math.Max(0, top); row < Math.Min(Height, bottom); row++)
        for (int column = Math.Max(0, left); column < Math.Min(Width, right); column++)
        {
            bool boundary = row == top || row == bottom - 1 || column == left || column == right - 1;
            uint? color = outline && boundary ? penColor : brushColor;
            int offset = (row * Width + column) * 3;
            if (color is uint paintedColor && WriteMixedPixel(offset, paintedColor, mix)) changedPixels++;
        }
        if (changedPixels != 0 && Revision < long.MaxValue) Revision++;
        return changedPixels;
    }

    // Win16 FillRect16/InvertRect16 pass (left, top, right-left, bottom-top)
    // to PatBlt. Our Windows memory-DC oracle demonstrates that, in this
    // identity-coordinate mode, backwards extents cover the sorted half-open
    // interval. Preserve the guest RECT; derive raster bounds only here.
    // This is a tested modern GDI compatibility choice, not Win3.1 fidelity proof.
    /// <summary>Fill black or invert a clipped rectangle using the tested half-open GDI bounds.</summary>
    public int Paint(Rectangle16 rectangle, bool invert, Point16 origin = default)
    {
        int left = rectangle.Left;
        int right = rectangle.Right;
        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        if (left > right) (left, right) = (right, left);
        if (top > bottom) (top, bottom) = (bottom, top);
        left -= origin.X; right -= origin.X; top -= origin.Y; bottom -= origin.Y;
        left = Math.Clamp(left, 0, Width);
        right = Math.Clamp(right, 0, Width);
        top = Math.Clamp(top, 0, Height);
        bottom = Math.Clamp(bottom, 0, Height);
        int changed = 0;
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
            {
                int index = (y * Width + x) * 3;
                if (invert || pixels[index] != 0 || pixels[index + 1] != 0 || pixels[index + 2] != 0) changed++;
                for (int channel = 0; channel < 3; channel++)
                    pixels[index + channel] = invert ? (byte)~pixels[index + channel] : (byte)0;
            }
        if (changed != 0 && Revision < long.MaxValue) Revision++;
        return changed;
    }

    /// <summary>Change one in-bounds pixel; callers aggregate revisions once per drawing operation.</summary>
    private bool WriteMixedPixel(int byteOffset, uint source, RasterMix mix)
    {
        uint destination = (uint)(pixels[byteOffset] | pixels[byteOffset + 1] << 8 | pixels[byteOffset + 2] << 16);
        uint color = RasterMixOperations.Apply(mix, source, destination);
        if (color == destination) return false;
        pixels[byteOffset] = (byte)color;
        pixels[byteOffset + 1] = (byte)(color >> 8);
        pixels[byteOffset + 2] = (byte)(color >> 16);
        return true;
    }
}
