using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

/// <summary>Deterministic RGB pixels. No native GDI pointers, windows, scaling, or antialiasing.</summary>
public sealed class PixelSurface
{
    private readonly byte[] pixels;
    public int Width { get; }
    public int Height { get; }
    public int RgbByteCount => pixels.Length;
    public long Revision { get; private set; }
    public PixelSurface(int width, int height)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048)
            throw new ArgumentOutOfRangeException(nameof(width), "Surface dimensions must be 1..2048.");
        Width = width; Height = height;
        pixels = new byte[checked(width * height * 3)];
    }
    public byte[] CopyRgb() => (byte[])pixels.Clone();

    /// <summary>One-pixel solid line, endpoint excluded, clipped to the surface.
    /// Integer major-axis stepping preserves the original line's error phase when clipped.</summary>
    public int Line(short x0, short y0, short x1, short y1, uint color)
    {
        int dx = Math.Abs((int)x1 - x0), dy = Math.Abs((int)y1 - y0);
        int sx = x1 >= x0 ? 1 : -1, sy = y1 >= y0 ? 1 : -1;
        int major = Math.Max(dx, dy), changed = 0;
        // Windows cosmetic lines resolve exact half-pixel ties toward the top/left.
        // Signed endpoints bound this loop to 65,535 steps, even far outside the clip.
        for (int step = 0; step < major; step++)
        {
            int x, y;
            if (dx >= dy)
            {
                x = x0 + sx * step;
                y = y0 + sy * (int)(((long)step * dy + (dx - (sy > 0 ? 1 : 0)) / 2) / dx);
            }
            else
            {
                y = y0 + sy * step;
                x = x0 + sx * (int)(((long)step * dx + (dy - (sx > 0 ? 1 : 0)) / 2) / dy);
            }
            if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
            int index = (y * Width + x) * 3;
            byte r = (byte)color, g = (byte)(color >> 8), b = (byte)(color >> 16);
            if (pixels[index] == r && pixels[index + 1] == g && pixels[index + 2] == b) continue;
            pixels[index] = r; pixels[index + 1] = g; pixels[index + 2] = b; changed++;
        }
        if (changed != 0 && Revision < long.MaxValue) Revision++;
        return changed;
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
    public int Paint(Rectangle16 rectangle, bool invert)
    {
        int left = rectangle.Left, right = rectangle.Right, top = rectangle.Top, bottom = rectangle.Bottom;
        if (left > right) (left, right) = (right, left);
        if (top > bottom) (top, bottom) = (bottom, top);
        left = Math.Clamp(left, 0, Width); right = Math.Clamp(right, 0, Width);
        top = Math.Clamp(top, 0, Height); bottom = Math.Clamp(bottom, 0, Height);
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
