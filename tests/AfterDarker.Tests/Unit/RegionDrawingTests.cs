using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class RegionDrawingTests
{
    [TestMethod]
    public void SelectedRegionIsCopiedAndItsDeviceCoordinatesDoNotFollowWindowOrigin()
    {
        var drawing = new Win16Drawing(); var surface = new PixelSurface(8, 8);
        drawing.Register(1, surface); drawing.Register(2, surface);
        ushort region = drawing.CreateRegion(new(2, 2, 5, 5), false);
        Assert.AreEqual((ushort)2, drawing.SelectClipRgn(1, region)); Assert.IsTrue(drawing.DeleteObject(region));
        drawing.SetWindowOrg(1, -10, -10);
        drawing.FillRect(1, new(-10, -10, -2, -2), Win16Drawing.WhiteBrushHandle);
        Assert.AreEqual(27, surface.CopyRgb().Count(value => value == 255));
        Assert.AreEqual(uint.MaxValue, drawing.SetPixel(1, -10, -10, 0xFFFFFF));
        Assert.AreEqual((ushort)0, drawing.SelectClipRgn(1, 0x999)); // Keep old clip on failure.
        drawing.Paint(1, new(-10, -10, -2, -2), invert: true);
        Assert.IsTrue(surface.CopyRgb().All(value => value == 0));
        // The shared bitmap has no permanent clip: the second DC may paint everywhere.
        drawing.FillRect(2, new(0, 0, 8, 8), Win16Drawing.WhiteBrushHandle);
        Assert.IsTrue(surface.CopyRgb().All(value => value == 255));
        Assert.AreEqual((ushort)2, drawing.SelectClipRgn(1, 0));
        drawing.Paint(1, new(-10, -10, -2, -2), invert: false);
        Assert.IsTrue(surface.CopyRgb().All(value => value == 0));
    }

    [TestMethod]
    public void SourceClipDoesNotLimitBlitButDestinationClipDoes()
    {
        var drawing = new Win16Drawing(); var source = new PixelSurface(8, 8); var destination = new PixelSurface(8, 8);
        source.Fill(new(0, 0, 8, 8), 0xFFFFFF); drawing.Register(1, source); drawing.Register(2, destination);
        drawing.SelectClipRgn(1, drawing.CreateRegion(new(0, 0, 0, 0), false));
        drawing.SelectClipRgn(2, drawing.CreateRegion(new(2, 2, 5, 5), false));
        drawing.BitBlt(2, 0, 0, 8, 8, 1, 0, 0, Win16Drawing.SourceCopy);
        Assert.AreEqual(27, destination.CopyRgb().Count(value => value == 255));
    }

    [TestMethod]
    public void RegionPoolIsBoundedAndDeletingASelectedHandleDoesNotClearTheClip()
    {
        var drawing = new Win16Drawing(); drawing.Register(1, new(8, 8));
        var regions = Enumerable.Range(0, 256).Select(_ => drawing.CreateRegion(new(0, 0, 8, 8), true)).ToArray();
        Assert.Throws<InvalidOperationException>(() => drawing.CreateRegion(new(0, 0, 1, 1), false));
        Assert.Throws<ArgumentException>(() => drawing.Register(regions[0], new(1, 1)));
        drawing.SelectClipRgn(1, regions[0]); Assert.IsTrue(drawing.DeleteObject(regions[0]));
        Assert.AreEqual(uint.MaxValue, drawing.SetPixel(1, 0, 0, 0xFFFFFF));
        Assert.AreEqual(regions[0], drawing.CreateRegion(new(0, 0, 1, 1), false));
        foreach (ushort region in regions) Assert.IsTrue(drawing.DeleteObject(region));
        Assert.AreEqual(0, drawing.LiveRegionCount); Assert.AreEqual(256, drawing.PeakRegionCount);
    }

    [TestMethod]
    public void ClipScopeIsRestoredAfterAnExceptionAndExtremeCoordinatesStayBounded()
    {
        var surface = new PixelSurface(4, 4);
        Assert.Throws<InvalidOperationException>(() => surface.WithClip<int>(new(new(0, 0, 0, 0), false), _ => throw new InvalidOperationException()));
        Assert.AreEqual(0xFFFFFFu, surface.SetPixel(0, 0, 0xFFFFFF));
        var huge = new RasterClipRegion(new(short.MinValue, short.MinValue, short.MaxValue, short.MaxValue), true);
        Assert.IsTrue(huge.Contains(0, 0)); Assert.IsFalse(huge.Contains(short.MinValue, short.MinValue));
    }

    [TestMethod]
    public void OneBitResourceExpandsThroughDestinationColorsAndBackgroundModeDoesNotMakeBlitsTransparent()
    {
        var drawing = new Win16Drawing(); var display = new PixelSurface(3, 2); drawing.Register(1, display);
        ushort bitmap = drawing.CreateBitmap(DibBitmapDecoder.Decode(BitmapResourceFixture.Dib()));
        ushort memory = drawing.CreateCompatibleDC(1); drawing.SelectObject(memory, bitmap);
        Assert.AreEqual(0u, drawing.SetTextColor(1, 0x332211));
        Assert.AreEqual(0xFFFFFFu, drawing.SetBkColor(1, 0x665544));
        drawing.SetBkMode(1, 1);
        drawing.BitBlt(1, 0, 0, 3, 2, memory, 0, 0, Win16Drawing.SourceCopy);
        CollectionAssert.AreEqual(new byte[] { 0x44, 0x55, 0x66, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 }, display.CopyRgb()[..9]);
        Assert.Throws<NotSupportedException>(() => drawing.SetPixel(memory, 0, 0, 0xFF));
        Assert.Throws<NotSupportedException>(() => drawing.CreateCompatibleBitmap(memory, 1, 1));
        Assert.IsTrue(drawing.DeleteDC(memory)); Assert.IsTrue(drawing.DeleteObject(bitmap));
    }

    [TestMethod]
    public void DefaultMemoryDcAcceptsOnlyTheObservedBlackWhiteRectangleSubset()
    {
        var drawing = new Win16Drawing(); drawing.Register(1, new(8, 8));
        ushort memory = drawing.CreateCompatibleDC(1);
        Assert.IsTrue(drawing.Rectangle(memory, new(0, 0, 320, 240)));
        ushort brush = drawing.CreateSolidBrush(0xFF); drawing.SelectObject(memory, brush);
        Assert.Throws<NotSupportedException>(() => drawing.Rectangle(memory, new(0, 0, 320, 240)));
        drawing.DeleteDC(memory); Assert.IsTrue(drawing.DeleteObject(brush));
    }

    [TestMethod]
    public void PolygonXorTouchesSharedVerticesAndOutlineFillOverlapOnlyOnce()
    {
        var surface = new PixelSurface(12, 10); Point16[] points = [new(-2, 1), new(8, 2), new(4, 8)];
        surface.Polygon(points, 0xFFFFFF, 0xFFFFFF, RasterMix.XorPen);
        Assert.IsTrue(surface.CopyRgb().Any(value => value != 0));
        surface.Polygon(points, 0xFFFFFF, 0xFFFFFF, RasterMix.XorPen);
        Assert.IsTrue(surface.CopyRgb().All(value => value == 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.Polygon(new Point16[257], 0, 0));
    }
}
