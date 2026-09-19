using System.Runtime.InteropServices;
using AfterDarker.Core.Rendering;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class LineRasterTests
{
    [TestMethod]
    public void SolidCosmeticLinesMatchWindowsAcrossOctantsTiesAndClipping()
    {
        var random = new Random(42);
        for (int i = 0; i < 1500; i++)
        {
            short x0 = (short)random.Next(-10, 18), y0 = (short)random.Next(-10, 16);
            short x1 = (short)random.Next(-10, 18), y1 = (short)random.Next(-10, 16);
            var surface = new PixelSurface(8, 6);
            surface.Line(x0, y0, x1, y1, 0x00332211);
            CollectionAssert.AreEqual(NativePixels(x0, y0, x1, y1), surface.CopyRgb(),
                $"Line ({x0},{y0})->({x1},{y1}) differs from GDI.");
        }
    }

    // Test-only modern Windows oracle. This checks raster boundaries, NOT the
    // appearance/timing of Mondrian on Windows 3.1. Production uses no native GDI.
    private static byte[] NativePixels(short x0, short y0, short x1, short y1)
    {
        nint dc = CreateCompatibleDC(0);
        Assert.AreNotEqual(nint.Zero, dc);
        var info = new BitmapInfo { Size = 40, Width = 8, Height = -6, Planes = 1, BitCount = 32 };
        nint bitmap = CreateDIBSection(dc, ref info, 0, out nint bits, 0, 0);
        Assert.AreNotEqual(nint.Zero, bitmap);
        nint previous = SelectObject(dc, bitmap);
        try
        {
            byte[] bgra = new byte[8 * 6 * 4];
            Marshal.Copy(bgra, 0, bits, bgra.Length);
            nint pen = CreatePen(0, 1, 0x00332211);
            nint oldPen = SelectObject(dc, pen);
            try
            {
                Assert.IsTrue(MoveToEx(dc, x0, y0, 0));
                Assert.IsTrue(LineTo(dc, x1, y1));
            }
            finally { SelectObject(dc, oldPen); DeleteObject(pen); }
            Assert.IsTrue(GdiFlush());
            Marshal.Copy(bits, bgra, 0, bgra.Length);
            byte[] rgb = new byte[8 * 6 * 3];
            for (int i = 0; i < 8 * 6; i++)
            {
                rgb[i * 3] = bgra[i * 4 + 2]; rgb[i * 3 + 1] = bgra[i * 4 + 1]; rgb[i * 3 + 2] = bgra[i * 4];
            }
            return rgb;
        }
        finally { SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); }
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
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern nint CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern bool MoveToEx(nint dc, int x, int y, nint previous);
    [DllImport("gdi32.dll")] private static extern bool LineTo(nint dc, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
