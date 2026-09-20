using System.Runtime.InteropServices;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

/// <summary>Test-only modern Windows raster reference. No native GDI objects enter guest code or production rendering.</summary>
internal static class WindowsEllipseOracle
{
    public static byte[] Render(int width, int height, Rectangle16 rectangle, int penWidth,
        uint penColor = 0x00332211, uint brushColor = 0x00996644)
    {
        nint context = CreateCompatibleDC(0);
        Assert.AreNotEqual(nint.Zero, context);
        nint bitmap = 0, pen = 0, brush = 0, previousBitmap = 0, previousPen = 0, previousBrush = 0;
        try
        {
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(context, ref info, 0, out nint bits, 0, 0);
            Assert.AreNotEqual(nint.Zero, bitmap);
            previousBitmap = SelectObject(context, bitmap);
            pen = CreatePen(0, penWidth, penColor);
            brush = CreateSolidBrush(brushColor);
            Assert.AreNotEqual(nint.Zero, pen); Assert.AreNotEqual(nint.Zero, brush);
            previousPen = SelectObject(context, pen); previousBrush = SelectObject(context, brush);
            byte[] bgra = new byte[width * height * 4];
            Marshal.Copy(bgra, 0, bits, bgra.Length);
            Assert.IsTrue(Ellipse(context, rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom));
            Assert.IsTrue(GdiFlush());
            Marshal.Copy(bits, bgra, 0, bgra.Length);
            byte[] rgb = new byte[width * height * 3];
            for (int pixel = 0; pixel < width * height; pixel++)
            {
                rgb[pixel * 3] = bgra[pixel * 4 + 2];
                rgb[pixel * 3 + 1] = bgra[pixel * 4 + 1];
                rgb[pixel * 3 + 2] = bgra[pixel * 4];
            }
            return rgb;
        }
        finally
        {
            if (previousPen != 0) SelectObject(context, previousPen);
            if (previousBrush != 0) SelectObject(context, previousBrush);
            if (previousBitmap != 0) SelectObject(context, previousBitmap);
            if (pen != 0) DeleteObject(pen);
            if (brush != 0) DeleteObject(brush);
            if (bitmap != 0) DeleteObject(bitmap);
            DeleteDC(context);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public uint Size;
        public int Width, Height;
        public ushort Planes, BitCount;
        public uint Compression, ImageSize;
        public int XPixelsPerMeter, YPixelsPerMeter;
        public uint UsedColors, ImportantColors, Color;
    }
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint context);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint context, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint context, nint item);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool Ellipse(nint context, int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint context);
}
