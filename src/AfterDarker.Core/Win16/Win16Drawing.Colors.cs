namespace AfterDarker.Core.Win16;

public sealed partial class Win16Drawing
{
    /// <summary>Set text/monochrome-zero color and return the previous COLORREF.</summary>
    public uint SetTextColor(ushort hdc, uint color)
    {
        uint resolved = Win16Color.ResolveSolidRgb(color);
        Win16DeviceContext context = RequireDeviceContext(hdc);
        uint previous = context.TextColor;
        context.TextColor = resolved;
        return previous;
    }
    /// <summary>Set background/monochrome-one color and return the previous COLORREF.</summary>
    public uint SetBkColor(ushort hdc, uint color)
    {
        uint resolved = Win16Color.ResolveSolidRgb(color);
        Win16DeviceContext context = RequireDeviceContext(hdc);
        uint previous = context.BackgroundColor;
        context.BackgroundColor = resolved;
        return previous;
    }
    /// <summary>Store TRANSPARENT (1) or OPAQUE (2); invalid values return zero without mutation.</summary>
    /// <remarks>Background mode does not make BitBlt transparent. Text and patterned pens remain unsupported.</remarks>
    public ushort SetBkMode(ushort hdc, ushort mode)
    {
        Win16DeviceContext context = RequireDeviceContext(hdc);
        if (mode is not (1 or 2)) return 0;
        ushort previous = context.BackgroundMode;
        context.BackgroundMode = mode;
        return previous;
    }
}
