using AfterDarker.Core.Win16;

namespace AfterDarker.Core.Rendering;

public sealed partial class PixelSurface
{
    /// <summary>Fill a half-open rectangle using an explicit brush color, independently of ROP2.</summary>
    public int Fill(Rectangle16 rectangle, uint color, Point16 origin = default) =>
        PaintBrush(rectangle, color, origin, borderOnly: false);

    /// <summary>Paint the four one-pixel edge strips. Zero extents retain GDI's adjacent-edge behavior; reversed edges paint nothing.</summary>
    public int Frame(Rectangle16 rectangle, uint color, Point16 origin = default) =>
        PaintBrush(rectangle, color, origin, borderOnly: true);

    private int PaintBrush(Rectangle16 rectangle, uint color, Point16 origin, bool borderOnly)
    {
        int left = rectangle.Left, top = rectangle.Top, right = rectangle.Right, bottom = rectangle.Bottom;
        if (borderOnly && (left > right || top > bottom)) return 0;
        if (left > right) (left, right) = (right, left);
        if (top > bottom) (top, bottom) = (bottom, top);
        left -= origin.X; right -= origin.X; top -= origin.Y; bottom -= origin.Y;
        // FrameRect is four brush strips, not a selected-pen Rectangle. For a
        // zero-width rectangle its vertical strips occupy left and left-1;
        // zero height similarly preserves the two horizontal strips.
        int firstColumn = borderOnly ? Math.Min(left, right - 1) : left;
        int lastColumn = borderOnly ? Math.Max(right, left + 1) : right;
        int firstRow = borderOnly ? Math.Min(top, bottom - 1) : top;
        int lastRow = borderOnly ? Math.Max(bottom, top + 1) : bottom;
        int changed = 0;
        for (int row = Math.Max(0, firstRow); row < Math.Min(Height, lastRow); row++)
            for (int column = Math.Max(0, firstColumn); column < Math.Min(Width, lastColumn); column++)
            {
                // Side strips exclude the top/bottom rows. GDI normalizes their
                // negative height for degenerate rectangles, just as PatBlt does.
                bool verticalEdge = (column == left || column == right - 1) &&
                    row >= Math.Min(top + 1, bottom - 1) && row < Math.Max(top + 1, bottom - 1);
                bool horizontalEdge = (row == top || row == bottom - 1) && column >= left && column < right;
                if (borderOnly && !verticalEdge && !horizontalEdge) continue;
                if (WriteMixedPixel((row * Width + column) * 3, color, RasterMix.CopyPen)) changed++;
            }
        if (changed != 0 && Revision < long.MaxValue) Revision++;
        return changed;
    }

    /// <summary>Set one device pixel to an RGB COLORREF, or return CLR_INVALID when clipped. ROP2 does not apply.</summary>
    public uint SetPixel(int horizontal, int vertical, uint color)
    {
        if (horizontal < 0 || horizontal >= Width || vertical < 0 || vertical >= Height || !IsPixelVisible(horizontal, vertical)) return uint.MaxValue;
        uint rgb = color & 0xFFFFFF;
        if (WriteMixedPixel((vertical * Width + horizontal) * 3, rgb, RasterMix.CopyPen) && Revision < long.MaxValue) Revision++;
        return rgb;
    }
}
