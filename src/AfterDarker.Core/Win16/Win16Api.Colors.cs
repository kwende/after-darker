namespace AfterDarker.Core.Win16;

public sealed partial class Win16Api
{
    /// <summary>Set the DC's text and monochrome-zero expansion color.</summary>
    public uint SetTextColor(ushort hdc, uint color) => Drawing.SetTextColor(hdc, color);
    /// <summary>Set the DC's background and monochrome-one expansion color.</summary>
    public uint SetBkColor(ushort hdc, uint color) => Drawing.SetBkColor(hdc, color);
    /// <summary>Set TRANSPARENT/OPAQUE background mode without changing BitBlt semantics.</summary>
    public ushort SetBkMode(ushort hdc, ushort mode) => Drawing.SetBkMode(hdc, mode);
}
