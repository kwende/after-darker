using System.Runtime.InteropServices;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class RectangleRasterTests
{
    [TestMethod]
    [DataRow(1, 1, 4, 3)]
    [DataRow(4, 1, 1, 3)]
    [DataRow(1, 3, 4, 1)]
    [DataRow(4, 3, 1, 1)]
    [DataRow(1, 1, 1, 3)]
    [DataRow(1, 1, 4, 1)]
    [DataRow(-3, -2, 4, 3)]
    [DataRow(4, 3, -3, -2)]
    [DataRow(8, 6, 0, 0)]
    [DataRow(20, 20, 30, 30)]
    public void InversionMatchesWindowsPatBltIncludingBackwardsExtents(int l, int t, int r, int b)
    {
        var rectangle = new Rectangle16((short)l, (short)t, (short)r, (short)b);
        var surface = new PixelSurface(8, 6);
        surface.Paint(rectangle, invert: true);
        CollectionAssert.AreEqual(NativePixels(rectangle), surface.CopyRgb());
        surface.Paint(rectangle, invert: true);
        CollectionAssert.AreEqual(new byte[8 * 6 * 3], surface.CopyRgb(), "Inverting twice must restore all pixels.");
    }

    [TestMethod]
    public void BlackFillOnlyClearsItsClippedPixels()
    {
        var surface = new PixelSurface(8, 6);
        surface.Paint(new(0, 0, 8, 6), true);
        Assert.AreEqual(6, surface.Paint(new(4, 3, 1, 1), false));
        byte[] pixels = surface.CopyRgb();
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 8; x++)
            Assert.AreEqual(x is >= 1 and < 4 && y is >= 1 and < 3 ? (byte)0 : (byte)255, pixels[(y * 8 + x) * 3]);
        Assert.AreEqual(0, surface.Paint(new(4, 3, 1, 1), false));
    }

    // Test-only modern Windows oracle. This checks raster boundaries, NOT the
    // appearance/timing of Mondrian on Windows 3.1. Production uses no native GDI.
    private static byte[] NativePixels(Rectangle16 rectangle)
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
            Assert.IsTrue(PatBlt(dc, rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left,
                rectangle.Bottom - rectangle.Top, 0x00550009)); // DSTINVERT
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
    [DllImport("gdi32.dll")] private static extern bool PatBlt(nint dc, int x, int y, int width, int height, uint rop);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
}
