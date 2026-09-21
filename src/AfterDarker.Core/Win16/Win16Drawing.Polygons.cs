namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>Close and fill a polygon without changing the DC's current drawing position.</summary>
    public bool Polygon(ushort hdc, IReadOnlyList<Point16> points)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        Win16Pen? pen = SelectedPen(context);
        if (pen is { Width: not 1 }) throw new NotSupportedException("Polygon supports null or cosmetic one-pixel pens.");
        int changed = context.Draw(surface => surface.Polygon(points, pen?.Color, SelectedBrushColor(context), context.Mix, context.WindowOrigin));
        RecordOperation("Polygon", hdc, new(points.Min(point => point.X), points.Min(point => point.Y),
            points.Max(point => point.X), points.Max(point => point.Y)), changed);
        return true;
    }
}
