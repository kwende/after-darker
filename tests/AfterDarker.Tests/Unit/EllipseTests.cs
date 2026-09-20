using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class EllipseTests
{
    [TestMethod]
    public void PenAndBrushSelectionsUseIndependentSlotsAndReturnThePreviousObjectOfThatKind()
    {
        var drawing = new Win16Drawing();
        var first = new PixelSurface(8, 8); var second = new PixelSurface(8, 8);
        drawing.Register(1, first); drawing.Register(2, second);
        ushort pen = drawing.CreatePen(0, 1, 0x00332211);
        Assert.AreEqual(Win16Drawing.WhiteBrushHandle, drawing.SelectObject(1, Win16Drawing.BlackBrushHandle));
        Assert.AreEqual(Win16Drawing.BlackPenHandle, drawing.SelectObject(1, pen));
        drawing.SelectObject(2, pen);
        drawing.Ellipse(1, new(1, 1, 7, 7)); drawing.Ellipse(2, new(1, 1, 7, 7));
        AssertPixel(first, 4, 4, [0, 0, 0]);
        AssertPixel(second, 4, 4, [255, 255, 255]);
        AssertPixel(first, 3, 1, [0x11, 0x22, 0x33]);
        Assert.AreEqual(Win16Drawing.BlackBrushHandle, drawing.SelectObject(1, Win16Drawing.WhiteBrushHandle));
        Assert.AreEqual(pen, drawing.SelectObject(1, Win16Drawing.BlackPenHandle));
        Assert.IsFalse(drawing.DeleteObject(pen)); // Still selected in the second DC.
        drawing.SelectObject(2, Win16Drawing.BlackPenHandle);
        Assert.IsTrue(drawing.DeleteObject(pen));
        Assert.IsTrue(drawing.DeleteObject(Win16Drawing.WhiteBrushHandle));
        drawing.Ellipse(2, new(0, 0, 8, 8)); // Deleting a stock object never destroys it.
        AssertPixel(second, 4, 4, [255, 255, 255]);
    }

    [TestMethod]
    public void EllipseUsesTheStoredPenWidthAndPreservesTheCurrentPointWhileWideLinesFailBeforeMutation()
    {
        var drawing = new Win16Drawing();
        var surface = new PixelSurface(16, 16);
        drawing.Register(1, surface);
        ushort pen = drawing.CreatePen(0, 2, 0x020000FF);
        drawing.SelectObject(1, pen);
        drawing.MoveTo(1, -5, -6);
        Assert.Throws<NotSupportedException>(() => drawing.LineTo(1, 10, 12));
        Assert.AreEqual(0L, surface.Revision);
        Assert.IsTrue(drawing.Ellipse(1, new(4, 4, 12, 12)));
        Assert.AreEqual(0xFFFAFFFBu, drawing.MoveTo(1, 0, 0));
        var narrow = new PixelSurface(16, 16);
        narrow.Ellipse(new(4, 4, 12, 12), 0xFF, 1, 0xFFFFFF);
        int redPixels = CountColor(surface.CopyRgb(), [255, 0, 0]);
        Assert.IsGreaterThan(CountColor(narrow.CopyRgb(), [255, 0, 0]), redPixels);
        AssertPixel(surface, 7, 7, [255, 255, 255]);
        Assert.IsFalse(drawing.DeleteObject(pen));
        drawing.SelectObject(1, Win16Drawing.BlackPenHandle);
        Assert.IsTrue(drawing.DeleteObject(pen));
        Assert.Throws<NotSupportedException>(() => drawing.Ellipse(99, new(0, 0, 8, 8)));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void EllipseClipsTheOriginalCurveAndHandlesReversedAndEmptyBounds(int penWidth)
    {
        var small = new PixelSurface(10, 8);
        var large = new PixelSurface(30, 24);
        int changed = small.Ellipse(new(-5, -4, 18, 8), 0x00332211, penWidth, 0x00996644);
        Assert.IsGreaterThan(0, changed);
        Assert.AreEqual(CountNonblack(small.CopyRgb()), changed);
        large.Ellipse(new(3, 4, 26, 16), 0x00332211, penWidth, 0x00996644);
        byte[] cropped = small.CopyRgb(), full = large.CopyRgb();
        for (int row = 0; row < 8; row++)
            Assert.IsTrue(cropped.AsSpan(row * 10 * 3, 30).SequenceEqual(full.AsSpan(((row + 8) * 30 + 8) * 3, 30)));
        long revision = small.Revision;
        Assert.AreEqual(0, small.Ellipse(new(18, 8, -5, -4), 0x00332211, penWidth, 0x00996644));
        Assert.AreEqual(revision, small.Revision);
        Assert.AreEqual(0, small.Ellipse(new(1, 1, 1, 7), 0xFF, penWidth, 0xFF));
        Assert.AreEqual(0, small.Ellipse(new(1, 1, 7, 1), 0xFF, penWidth, 0xFF));
        Assert.AreEqual(revision, small.Revision);
    }

    [TestMethod]
    public void FullSignedCoordinateRangeDoesNotOverflowAndUnsupportedWidthsLeavePixelsAlone()
    {
        var surface = new PixelSurface(2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Ellipse(new(0, 0, 2, 2), 0, 3, 0));
        Assert.AreEqual(0L, surface.Revision);
        Assert.AreEqual(4, surface.Ellipse(new(short.MinValue, short.MinValue, short.MaxValue, short.MaxValue), 0, 2, 0xFFFFFF));
        Assert.IsTrue(surface.CopyRgb().All(component => component == 255));
    }

    private static void AssertPixel(PixelSurface surface, int column, int row, byte[] expected) =>
        Assert.IsTrue(surface.CopyRgb().AsSpan((row * surface.Width + column) * 3, 3).SequenceEqual(expected));
    private static int CountColor(byte[] pixels, byte[] color) => Enumerable.Range(0, pixels.Length / 3)
        .Count(pixel => pixels.AsSpan(pixel * 3, 3).SequenceEqual(color));
    private static int CountNonblack(byte[] pixels) => Enumerable.Range(0, pixels.Length / 3)
        .Count(pixel => pixels[pixel * 3] != 0 || pixels[pixel * 3 + 1] != 0 || pixels[pixel * 3 + 2] != 0);
}
