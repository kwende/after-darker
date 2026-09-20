namespace AfterDarker.Core.Win16;

/// <summary>A guest pen's solid RGB color and device-pixel width under our identity mapping.</summary>
/// <param name="Color">COLORREF channel order (0x00BBGGRR), with palette-request bits already resolved.</param>
/// <param name="Width">One or two pixels; CreatePen width zero is normalized to one.</param>
internal sealed record Win16Pen(uint Color, int Width);
