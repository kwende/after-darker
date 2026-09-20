using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using static AfterDarker.Tests.Conformance.WindowsDrawingOracle;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class StainedGlassRasterTests
{
    [TestMethod]
    public void AllBinaryMixModesAndOriginsMatchNativeForLinesAndFilledRectangles()
    {
        var random = new Random(581);
        byte[] initial = new byte[24 * 24 * 3]; random.NextBytes(initial);
        for (ushort mode = 1; mode <= 16; mode++)
        {
            var surface = new PixelSurface(24, 24); surface.LoadRgb(initial);
            var drawing = new Win16Drawing(); drawing.Register(1, surface);
            drawing.SelectObject(1, drawing.CreatePen(0, 1, 0x996611));
            drawing.SelectObject(1, drawing.CreateSolidBrush(0x337799));
            using var native = new WindowsDrawingOracle(24, 24, initial);
            native.Pen(1, 0x996611); native.Brush(0x337799);
            Assert.AreEqual((ushort)13, drawing.SetROP2(1, mode)); SetROP2(native.Context, mode);
            Assert.AreEqual(0u, drawing.SetWindowOrg(1, -3, 4)); SetWindowOrgEx(native.Context, -3, 4, 0);
            drawing.MoveTo(1, -5, 6); drawing.LineTo(1, 17, 23);
            MoveToEx(native.Context, -5, 6, 0); LineTo(native.Context, 17, 23);
            drawing.Rectangle(1, new(2, 7, 15, 20)); Rectangle(native.Context, 2, 7, 15, 20);
            CollectionAssert.AreEqual(native.CopyRgb(), surface.CopyRgb(), $"ROP2 {mode}");
        }
    }

    [TestMethod]
    public void ExplicitBrushFrameAndSetPixelIgnoreRop2AndPreserveClipping()
    {
        Rectangle16[] rectangles = [new(1, 1, 7, 6), new(-4, 2, 4, 10), new(4, 2, 5, 3),
            new(5, 5, 2, 2), new(2, 2, 2, 8), new(2, 3, 8, 3), new(2, 2, 2, 2),
            new(short.MinValue, 2, short.MaxValue, 6)];
        foreach (var rectangle in rectangles)
        {
            var surface = new PixelSurface(12, 12); var drawing = new Win16Drawing(); drawing.Register(1, surface);
            ushort brush = drawing.CreateSolidBrush(0x553399);
            drawing.SetWindowOrg(1, -2, 1); drawing.SetROP2(1, 7);
            using var native = new WindowsDrawingOracle(12, 12);
            nint nativeBrush = native.Brush(0x553399);
            SetWindowOrgEx(native.Context, -2, 1, 0); SetROP2(native.Context, 7);
            var nativeRect = new NativeRect(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom);
            Assert.AreEqual(FrameRect(native.Context, ref nativeRect, nativeBrush) != 0, drawing.FrameRect(1, rectangle, brush) != 0, rectangle.ToString());
            foreach (var point in new Point16[] { new(-2, 1), new(-3, 1), new(7, 9), new(11, 11) })
                Assert.AreEqual(SetPixel(native.Context, point.X, point.Y, 0xABCDEF), drawing.SetPixel(1, point.X, point.Y, 0xABCDEF));
            CollectionAssert.AreEqual(native.CopyRgb(), surface.CopyRgb(), rectangle.ToString());
        }
    }

    [TestMethod]
    public void SelfCopiesMatchNativeInBothOverlapDirectionsAndAtClippedDestinations()
    {
        byte[] initial = new byte[24 * 24 * 3]; new Random(27).NextBytes(initial);
        foreach (var destination in new Point16[] { new(3, 4), new(0, 0), new(-4, -2), new(18, 20) })
        {
            var surface = new PixelSurface(24, 24); surface.LoadRgb(initial);
            var drawing = new Win16Drawing(); drawing.Register(1, surface);
            drawing.SetWindowOrg(1, 1, -2); drawing.SetROP2(1, 7);
            using var native = new WindowsDrawingOracle(24, 24, initial);
            SetWindowOrgEx(native.Context, 1, -2, 0); SetROP2(native.Context, 7);
            drawing.BitBlt(1, destination.X, destination.Y, 12, 10, 1, 2, 1, Win16Drawing.SourceCopy);
            Assert.IsTrue(BitBlt(native.Context, destination.X, destination.Y, 12, 10, native.Context, 2, 1, Win16Drawing.SourceCopy));
            CollectionAssert.AreEqual(native.CopyRgb(), surface.CopyRgb(), destination.ToString());
        }
    }

    [TestMethod]
    public void ThreePixelStrokesStayWithinOnePixelOfNativeAcrossOctantsBeforeClipping()
    {
        var random = new Random(741);
        for (int sample = 0; sample < 200; sample++)
        {
            // Compare edges with surrounding pixels available. Clipping can remove
            // the only nearby matching edge entirely; it cannot preserve this metric.
            short startX = (short)random.Next(4, 28), startY = (short)random.Next(4, 28);
            short endX = (short)random.Next(4, 28), endY = (short)random.Next(4, 28);
            if (sample == 0) { endX = startX; endY = startY; } // Include a zero-length wide stroke.
            var surface = new PixelSurface(32, 32);
            surface.WideLine(startX, startY, endX, endY, 0xFFFFFF, RasterMix.XorPen);
            using var native = new WindowsDrawingOracle(32, 32);
            native.Pen(3, 0xFFFFFF); SetROP2(native.Context, 7);
            MoveToEx(native.Context, startX, startY, 0); LineTo(native.Context, endX, endY);
            byte[] expected = native.CopyRgb(), actual = surface.CopyRgb();
            for (int row = 0; row < 32; row++)
            for (int column = 0; column < 32; column++)
            {
                byte actualColor = actual[(row * 32 + column) * 3], expectedColor = expected[(row * 32 + column) * 3];
                Assert.IsTrue(Nearby(expected, actualColor, column, row) && Nearby(actual, expectedColor, column, row),
                    $"Stroke ({startX},{startY})->({endX},{endY}) at ({column},{row}) exceeds the one-pixel comparison bound.");
            }
            surface.WideLine(startX, startY, endX, endY, 0xFFFFFF, RasterMix.XorPen);
            Assert.IsTrue(surface.CopyRgb().All(component => component == 0));
        }
    }

    private static bool Nearby(byte[] pixels, byte color, int column, int row)
    {
        for (int nearbyY = Math.Max(0, row - 1); nearbyY <= Math.Min(31, row + 1); nearbyY++)
        for (int nearbyX = Math.Max(0, column - 1); nearbyX <= Math.Min(31, column + 1); nearbyX++)
            if (pixels[(nearbyY * 32 + nearbyX) * 3] == color) return true;
        return false;
    }
}
