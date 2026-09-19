using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

/// <summary>Deterministic RGB pixels. No native GDI pointers, windows, scaling, or antialiasing.</summary>
public sealed class PixelSurface
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

    /// <summary>Draw a solid COLORREF line, excluding its endpoint and clipping only the generated pixels.</summary>
    /// <returns>The count of pixels whose RGB value changed.</returns>
    public int Line(short startX, short startY, short endX, short endY, uint color)
    {
        // COLORREF is 0x00BBGGRR. The surface stores the channels in RGB byte order.
        byte red = (byte)color;
        byte green = (byte)(color >> 8);
        byte blue = (byte)(color >> 16);
        int changedPixels = 0;

        foreach (var pixel in CosmeticLineRasterizer.EnumeratePixels(startX, startY, endX, endY))
        {
            if (pixel.X < 0 || pixel.X >= Width || pixel.Y < 0 || pixel.Y >= Height)
            {
                continue;
            }

            int pixelByteOffset = (pixel.Y * Width + pixel.X) * 3;
            bool alreadySameColor = pixels[pixelByteOffset] == red &&
                pixels[pixelByteOffset + 1] == green && pixels[pixelByteOffset + 2] == blue;
            if (alreadySameColor)
            {
                continue;
            }

            pixels[pixelByteOffset] = red;
            pixels[pixelByteOffset + 1] = green;
            pixels[pixelByteOffset + 2] = blue;
            changedPixels++;
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

    // Win16 FillRect16/InvertRect16 pass (left, top, right-left, bottom-top)
    // to PatBlt. Our Windows memory-DC oracle demonstrates that, in this
    // identity-coordinate mode, backwards extents cover the sorted half-open
    // interval. Preserve the guest RECT; derive raster bounds only here.
    // This is a tested modern GDI compatibility choice, not Win3.1 fidelity proof.
    /// <summary>Fill black or invert a clipped rectangle using the tested half-open GDI bounds.</summary>
    public int Paint(Rectangle16 rectangle, bool invert)
    {
        int left = rectangle.Left;
        int right = rectangle.Right;
        int top = rectangle.Top;
        int bottom = rectangle.Bottom;
        if (left > right) (left, right) = (right, left);
        if (top > bottom) (top, bottom) = (bottom, top);
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
}
