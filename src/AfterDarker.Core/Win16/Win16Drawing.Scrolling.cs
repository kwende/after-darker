using AfterDarker.Core.Rendering;

namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>Move pixels within a DC and return the bounding rectangle of newly exposed pixels.</summary>
    /// <remarks>Identity-scale scrolling with rectangular clipping. The existing bitmap copier preserves overlapping
    /// source pixels; this method supplies the geometry and exposed bounds. See docs/research/puzzle-execution.md.</remarks>
    public bool ScrollDC(ushort hdc, short horizontal, short vertical, Rectangle16? scroll, Rectangle16? clip,
        ushort updateRegion, bool needsUpdate, out Rectangle16 update)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        if (updateRegion != 0) throw new NotSupportedException("ScrollDC update regions are not implemented.");
        if (context.Clip is { Elliptic: true }) throw new NotSupportedException("ScrollDC requires rectangular DC clipping.");
        PixelSurface surface = context.Surface;
        var surfaceBounds = new ScrollBounds(0, 0, surface.Width, surface.Height);
        ScrollBounds clipping = ToDeviceBounds(clip, context.WindowOrigin, surfaceBounds).Intersect(surfaceBounds);
        if (context.Clip is { } selectedClip)
            clipping = clipping.Intersect(ToDeviceBounds(selectedClip.Bounds, default, surfaceBounds));
        ScrollBounds source = ToDeviceBounds(scroll, context.WindowOrigin, surfaceBounds).Intersect(clipping);
        ScrollBounds moved = source.Offset(horizontal, vertical).Intersect(clipping);
        int changed = 0;
        if (!source.IsEmpty && !moved.IsEmpty)
        {
            changed = context.Draw(destination => destination.CopyRegion(destination,
                moved.Left, moved.Top, moved.Right - moved.Left, moved.Bottom - moved.Top,
                moved.Left - horizontal, moved.Top - vertical));
        }
        // ScrollDC does not erase newly exposed pixels. The guest uses this result
        // for its following FillRect. The memory-DC oracle returns bounds translated back by its window origin.
        update = needsUpdate && (horizontal != 0 || vertical != 0)
            ? ExposedBounds(source, moved).ToRectangle16(context.WindowOrigin) : default;
        RecordOperation("ScrollDC", hdc, scroll ?? default, changed);
        return true;
    }

    private static ScrollBounds ToDeviceBounds(Rectangle16? rectangle, Point16 origin, ScrollBounds fallback) =>
        rectangle is { } value ? new(value.Left - origin.X, value.Top - origin.Y,
            value.Right - origin.X, value.Bottom - origin.Y) : fallback;

    /// <summary>Bounding box of source minus moved coverage, built from at most four exposed strips.</summary>
    private static ScrollBounds ExposedBounds(ScrollBounds source, ScrollBounds moved)
    {
        if (source.IsEmpty) return default;
        ScrollBounds covered = source.Intersect(moved);
        if (covered.IsEmpty) return source;
        ScrollBounds exposed = default;
        Include(new(source.Left, source.Top, source.Right, covered.Top));
        Include(new(source.Left, covered.Bottom, source.Right, source.Bottom));
        Include(new(source.Left, covered.Top, covered.Left, covered.Bottom));
        Include(new(covered.Right, covered.Top, source.Right, covered.Bottom));
        return exposed;

        void Include(ScrollBounds strip)
        {
            if (strip.IsEmpty) return;
            exposed = exposed.IsEmpty ? strip : new(Math.Min(exposed.Left, strip.Left), Math.Min(exposed.Top, strip.Top),
                Math.Max(exposed.Right, strip.Right), Math.Max(exposed.Bottom, strip.Bottom));
        }
    }

    /// <summary>Wide intermediate device coordinates; shifting Win16 rectangles must not wrap at 32767.</summary>
    private readonly record struct ScrollBounds(int Left, int Top, int Right, int Bottom)
    {
        public bool IsEmpty => Left >= Right || Top >= Bottom;
        public ScrollBounds Intersect(ScrollBounds other) => new(Math.Max(Left, other.Left), Math.Max(Top, other.Top),
            Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));
        public ScrollBounds Offset(int horizontal, int vertical) => new(Left + horizontal, Top + vertical, Right + horizontal, Bottom + vertical);
        // Narrow to Win16 coordinates after restoring the logical origin.
        public Rectangle16 ToRectangle16(Point16 origin) => IsEmpty ? new(origin.X, origin.Y, origin.X, origin.Y) : new(unchecked((short)(Left + origin.X)),
            unchecked((short)(Top + origin.Y)), unchecked((short)(Right + origin.X)), unchecked((short)(Bottom + origin.Y)));
    }
}
