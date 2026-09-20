namespace AfterDarker.Core.Win16;

public sealed partial class Win16Api
{
    /// <summary>Create a guest memory DC; it needs a selected bitmap before rendering.</summary>
    public ushort CreateCompatibleDC(ushort sourceHdc) => Drawing.CreateCompatibleDC(sourceHdc);
    /// <summary>Create a color bitmap independently of DC ownership; dimensions are Win16 WORDs.</summary>
    public ushort CreateCompatibleBitmap(ushort sourceHdc, ushort width, ushort height) => Drawing.CreateCompatibleBitmap(sourceHdc, width, height);
    /// <summary>Delete a guest-created DC without deleting its selected bitmap.</summary>
    public bool DeleteDC(ushort hdc) => Drawing.DeleteDC(hdc);
    /// <summary>Paint a rectangle with the selected brush and an explicit raster operation.</summary>
    public bool PatBlt(ushort hdc, short left, short top, short width, short height, uint operation) => Drawing.PatBlt(hdc, left, top, width, height, operation);
}
