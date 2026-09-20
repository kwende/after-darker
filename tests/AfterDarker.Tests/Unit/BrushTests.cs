using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class BrushTests
{
    [TestMethod]
    public void BrushesHaveBoundedReusableHandlesAndRemainAliveWhileSelectedInAnyContext()
    {
        var drawing = new Win16Drawing();
        drawing.Register(1, new(8, 8)); drawing.Register(2, new(8, 8));
        ushort first = drawing.CreateSolidBrush(0x02332211);
        ushort pen = drawing.CreatePen(0, 1, 0xFF);
        Assert.AreNotEqual(first, pen);
        Assert.AreEqual(Win16Drawing.WhiteBrushHandle, drawing.SelectObject(1, first));
        drawing.SelectObject(2, first);
        drawing.SelectObject(1, Win16Drawing.NullPenHandle);
        Assert.IsFalse(drawing.DeleteObject(first)); // Pen selection did not displace the brush.
        drawing.SelectObject(1, Win16Drawing.WhiteBrushHandle);
        Assert.IsFalse(drawing.DeleteObject(first)); // Still selected into HDC 2.
        drawing.SelectObject(2, Win16Drawing.BlackBrushHandle);
        Assert.IsTrue(drawing.DeleteObject(first));
        Assert.IsFalse(drawing.DeleteObject(first));
        Assert.Throws<NotSupportedException>(() => drawing.SelectObject(1, first));
        Assert.AreEqual(first, drawing.CreateSolidBrush(0x00332211));
        for (int index = 1; index < 256; index++) drawing.CreateSolidBrush((uint)index);
        Assert.Throws<InvalidOperationException>(() => drawing.CreateSolidBrush(0));
        Assert.AreEqual(256, drawing.LiveBrushCount);
        Assert.AreEqual(256, drawing.PeakBrushCount);
        Assert.AreEqual(1, drawing.LivePenCount);
        var independent = new Win16Drawing();
        Assert.AreEqual(0, independent.LiveBrushCount);
        Assert.AreEqual(first, independent.CreateSolidBrush(0));
    }

    [TestMethod]
    public void PaletteRelativeBrushUsesItsRgbChannelsAndNullPenSuppressesShapeOutlines()
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(12, 8);
        drawing.Register(1, surface);
        Assert.Throws<NotSupportedException>(() => drawing.CreateSolidBrush(0x01000017));
        Assert.Throws<NotSupportedException>(() => drawing.CreateSolidBrush(0xFF332211));
        Assert.AreEqual(0, drawing.LiveBrushCount);
        ushort brush = drawing.CreateSolidBrush(0x02332211);
        drawing.SelectObject(1, brush);
        Assert.AreEqual(Win16Drawing.BlackPenHandle, drawing.SelectObject(1, Win16Drawing.NullPenHandle));
        drawing.Rectangle(1, new(1, 1, 5, 5));
        AssertPixel(surface, 1, 1, [0x11, 0x22, 0x33]);
        AssertPixel(surface, 3, 3, [0x11, 0x22, 0x33]);
        AssertPixel(surface, 4, 4, [0, 0, 0]); // NULL_PEN contracts right/bottom once more.
        drawing.Ellipse(1, new(6, 1, 12, 7));
        AssertPixel(surface, 8, 1, [0x11, 0x22, 0x33]);
        AssertPixel(surface, 9, 4, [0x11, 0x22, 0x33]);
        AssertPixel(surface, 6, 1, [0, 0, 0]);
        long revision = surface.Revision;
        Assert.Throws<NotSupportedException>(() => drawing.Rectangle(99, new(0, 0, 8, 8)));
        Assert.AreEqual(revision, surface.Revision);
    }

    [TestMethod]
    public void NullLinesAdvanceTheCurrentPointWhileShapesPreserveItAndWideRectanglesFailBeforeMutation()
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(8, 8);
        drawing.Register(1, surface);
        drawing.SelectObject(1, Win16Drawing.NullPenHandle);
        Assert.IsTrue(drawing.DeleteObject(Win16Drawing.NullPenHandle));
        drawing.LineTo(1, -3, -4);
        Assert.AreEqual(0L, surface.Revision);
        drawing.Rectangle(1, new(1, 1, 7, 7));
        drawing.Ellipse(1, new(1, 1, 7, 7));
        Assert.AreEqual(0xFFFCFFFDu, drawing.MoveTo(1, 0, 0));
        drawing.SelectObject(1, drawing.CreatePen(0, 2, 0xFF));
        byte[] before = surface.CopyRgb();
        Assert.Throws<NotSupportedException>(() => drawing.Rectangle(1, new(0, 0, 8, 8)));
        CollectionAssert.AreEqual(before, surface.CopyRgb());
    }

    private static void AssertPixel(PixelSurface surface, int column, int row, byte[] expected) =>
        Assert.IsTrue(surface.CopyRgb().AsSpan((row * surface.Width + column) * 3, 3).SequenceEqual(expected));
}
