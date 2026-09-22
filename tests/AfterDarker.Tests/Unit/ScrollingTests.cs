using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
public sealed class ScrollingTests
{
    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 1)]
    [DataRow(0, -1)]
    public void ScrollPreservesOverlappingSourcePixelsAndClipsWrites(int horizontal, int vertical)
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(8, 6);
        byte[] original = Enumerable.Range(1, surface.RgbByteCount).Select(value => (byte)value).ToArray();
        surface.LoadRgb(original); drawing.Register(1, surface);
        Rectangle16 clip = new(2, 1, 6, 5);
        Assert.IsTrue(drawing.ScrollDC(1, (short)horizontal, (short)vertical, new(0, 0, 8, 6), clip, 0, true, out var exposed));
        byte[] actual = surface.CopyRgb();
        for (int row = 0; row < 6; row++)
            for (int column = 0; column < 8; column++)
            {
                int sourceColumn = column - horizontal, sourceRow = row - vertical;
                bool inside = column >= clip.Left && column < clip.Right && row >= clip.Top && row < clip.Bottom;
                if (inside && (sourceColumn < clip.Left || sourceColumn >= clip.Right || sourceRow < clip.Top || sourceRow >= clip.Bottom)) continue;
                int source = inside ? sourceRow * 8 + sourceColumn : row * 8 + column;
                CollectionAssert.AreEqual(original[(source * 3)..(source * 3 + 3)], actual[((row * 8 + column) * 3)..((row * 8 + column) * 3 + 3)]);
            }
        Rectangle16 expected = horizontal switch
        {
            1 => new(2, 1, 3, 5), -1 => new(5, 1, 6, 5),
            _ => vertical == 1 ? new(2, 1, 6, 2) : new(2, 4, 6, 5)
        };
        Assert.AreEqual(expected, exposed);
    }

    [TestMethod]
    public void NullRectanglesUseWholeBitmapAndUnsupportedHandlesFailBeforeDrawing()
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(8, 6); drawing.Register(1, surface);
        drawing.SetPixel(1, 0, 0, 0x332211);
        Assert.IsTrue(drawing.ScrollDC(1, 1, 0, null, null, 0, false, out _));
        CollectionAssert.AreEqual(new byte[] { 0x11, 0x22, 0x33 }, surface.CopyRgb()[3..6]);
        long operations = drawing.OperationCount;
        Assert.Throws<NotSupportedException>(() => drawing.ScrollDC(1, 1, 0, null, null, 1, true, out _));
        Assert.Throws<NotSupportedException>(() => drawing.ScrollDC(999, 1, 0, null, null, 0, false, out _));
        Assert.AreEqual(operations, drawing.OperationCount);

    }

    [TestMethod]
    public void HandRolledScrollingUsesRectangularDcClippingAndRejectsEllipticClippingBeforeMutation()
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(8, 6);
        surface.LoadRgb(Enumerable.Range(1, surface.RgbByteCount).Select(value => (byte)value).ToArray());
        drawing.Register(1, surface);
        ushort rectangle = drawing.CreateRegion(new(2, 1, 6, 5), false);
        drawing.SelectClipRgn(1, rectangle);
        Assert.IsTrue(drawing.ScrollDC(1, 1, 0, null, null, 0, true, out var update));
        Assert.AreEqual(new Rectangle16(2, 1, 3, 5), update);
        ushort ellipse = drawing.CreateRegion(new(0, 0, 8, 6), true);
        drawing.SelectClipRgn(1, ellipse);
        byte[] before = surface.CopyRgb(); long operations = drawing.OperationCount;
        Assert.Throws<NotSupportedException>(() => drawing.ScrollDC(1, 1, 0, null, null, 0, true, out _));
        CollectionAssert.AreEqual(before, surface.CopyRgb());
        Assert.AreEqual(operations, drawing.OperationCount);
    }
}
