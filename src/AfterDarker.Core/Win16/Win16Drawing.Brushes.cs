namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>Use the explicitly supplied brush, preserving the DC's selected brush and ROP2.</summary>
    public short FillRect(ushort hdc, Rectangle16 rectangle, ushort brush)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        int changed = brush == NullBrushHandle ? 0 : context.Draw(surface => surface.Fill(rectangle, ResolveBrushColor(brush), context.WindowOrigin));
        RecordOperation("FillRect", hdc, rectangle, changed);
        return 1;
    }

    /// <summary>Draw the inside border with the explicit brush without using the selected pen or ROP2.</summary>
    public short FrameRect(ushort hdc, Rectangle16 rectangle, ushort brush)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        int changed = brush == NullBrushHandle ? 0 : context.Draw(surface => surface.Frame(rectangle, ResolveBrushColor(brush), context.WindowOrigin));
        RecordOperation("FrameRect", hdc, rectangle, changed);
        // Native FrameRect accepts zero extents but rejects reversed edges.
        return (short)(rectangle.Left <= rectangle.Right && rectangle.Top <= rectangle.Bottom ? 1 : 0);
    }

    /// <summary>Translate a logical pixel and write its color directly, returning RGB or CLR_INVALID.</summary>
    public uint SetPixel(ushort hdc, short horizontal, short vertical, uint color)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        long previousRevision = context.Surface.Revision;
        uint result = context.Draw(surface => surface.SetPixel(horizontal - context.WindowOrigin.X, vertical - context.WindowOrigin.Y,
            Win16Color.ResolveSolidRgb(color)));
        RecordOperation("SetPixel", hdc, new(horizontal, vertical, horizontal, vertical),
            context.Surface.Revision == previousRevision ? 0 : 1);
        return result;
    }

    private void RecordOperation(string name, ushort hdc, Rectangle16 rectangle, int changedPixels)
    {
        LastOperation = new(name, hdc, rectangle, changedPixels);
        if (OperationCount < long.MaxValue) OperationCount++;
    }
}
