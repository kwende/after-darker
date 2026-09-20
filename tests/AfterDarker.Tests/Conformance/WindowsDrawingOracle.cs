using System.Runtime.InteropServices;

namespace AfterDarker.Tests.Conformance;

/// <summary>Disposable test-only RGB DIB/DC used to compare translation, ROP2, brushes and copying with modern GDI.</summary>
internal sealed class WindowsDrawingOracle : IDisposable
{
    private readonly int width, height;
    private readonly nint bitmap, previousBitmap, bits;
    private readonly List<nint> objects = [];
    public nint Context { get; }
    public WindowsDrawingOracle(int width, int height, byte[]? initial = null)
    {
        this.width = width; this.height = height;
        Context = CreateCompatibleDC(0);
        Assert.AreNotEqual(nint.Zero, Context);
        var information = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
        bitmap = CreateDIBSection(Context, ref information, 0, out bits, 0, 0);
        Assert.AreNotEqual(nint.Zero, bitmap);
        previousBitmap = SelectObject(Context, bitmap);
        var bgra = new byte[width * height * 4];
        if (initial is not null)
            for (int pixel = 0; pixel < width * height; pixel++)
            {
                bgra[pixel * 4] = initial[pixel * 3 + 2]; bgra[pixel * 4 + 1] = initial[pixel * 3 + 1];
                bgra[pixel * 4 + 2] = initial[pixel * 3];
            }
        Marshal.Copy(bgra, 0, bits, bgra.Length);
    }
    public void Pen(int width, uint color) => SelectOwned(CreatePen(0, width, color));
    public nint Brush(uint color)
    {
        nint brush = CreateSolidBrush(color); SelectOwned(brush); return brush;
    }
    public void HollowBrush() => SelectObject(Context, GetStockObject(5));
    private void SelectOwned(nint value) { Assert.AreNotEqual(nint.Zero, value); objects.Add(value); SelectObject(Context, value); }
    public byte[] CopyRgb()
    {
        Assert.IsTrue(GdiFlush());
        byte[] bgra = new byte[width * height * 4], rgb = new byte[width * height * 3];
        Marshal.Copy(bits, bgra, 0, bgra.Length);
        for (int pixel = 0; pixel < width * height; pixel++)
        {
            rgb[pixel * 3] = bgra[pixel * 4 + 2]; rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1]; rgb[pixel * 3 + 2] = bgra[pixel * 4];
        }
        return rgb;
    }
    public void Dispose()
    {
        SelectObject(Context, GetStockObject(7)); SelectObject(Context, GetStockObject(0));
        SelectObject(Context, previousBitmap);
        foreach (nint value in objects) DeleteObject(value);
        DeleteObject(bitmap); DeleteDC(Context);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect(int left, int top, int right, int bottom) { public int Left = left, Top = top, Right = right, Bottom = bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, ImageSize; public int XPixelsPerMeter, YPixelsPerMeter; public uint UsedColors, ImportantColors, Color;
    }
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint context);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint context, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint context, nint value);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint context);
    [DllImport("gdi32.dll")] internal static extern int SetROP2(nint context, int mode);
    [DllImport("gdi32.dll")] internal static extern bool SetWindowOrgEx(nint context, int horizontal, int vertical, nint previous);
    [DllImport("gdi32.dll")] internal static extern bool MoveToEx(nint context, int horizontal, int vertical, nint previous);
    [DllImport("gdi32.dll")] internal static extern bool LineTo(nint context, int horizontal, int vertical);
    [DllImport("gdi32.dll")] internal static extern bool Rectangle(nint context, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] internal static extern uint SetPixel(nint context, int horizontal, int vertical, uint color);
    [DllImport("user32.dll")] internal static extern int FrameRect(nint context, ref NativeRect rectangle, nint brush);
    [DllImport("gdi32.dll")] internal static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);
}
