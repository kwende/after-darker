using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class BitmapFamilyRasterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RegionClippedSourcePaintMatchesNativeInteriorAndBounds(bool elliptic)
    {
        const int width = 64, height = 64;
        byte[] initial = Enumerable.Repeat((byte)0x55, width * height * 3).ToArray();
        byte[] sourceBytes = Enumerable.Repeat((byte)0xAA, width * height * 3).ToArray();
        using var native = new WindowsDrawingOracle(width, height, initial);
        using var nativeSource = new WindowsDrawingOracle(width, height, sourceBytes);
        nint region = elliptic ? WindowsDrawingOracle.CreateEllipticRgn(10, 10, 50, 50) : WindowsDrawingOracle.CreateRectRgn(10, 10, 50, 50);
        Assert.AreEqual(elliptic ? 3 : 2, WindowsDrawingOracle.SelectClipRgn(native.Context, region));
        Assert.IsTrue(WindowsDrawingOracle.DeleteObject(region)); // Selected clip survives handle deletion.
        Assert.IsTrue(WindowsDrawingOracle.BitBlt(native.Context, 0, 0, width, height, nativeSource.Context, 0, 0, (uint)BitmapRasterOperation.SourcePaint));
        var drawing = new Win16Drawing(); var surface = new PixelSurface(width, height); surface.LoadRgb(initial);
        var source = new PixelSurface(width, height); source.LoadRgb(sourceBytes); drawing.Register(1, surface); drawing.Register(2, source);
        drawing.SelectClipRgn(1, drawing.CreateRegion(new(10, 10, 50, 50), elliptic));
        drawing.BitBlt(1, 0, 0, width, height, 2, 0, 0, (uint)BitmapRasterOperation.SourcePaint);
        byte[] expected = native.CopyRgb(), actual = surface.CopyRgb();
        int differences = Enumerable.Range(0, width * height).Count(pixel => expected[pixel * 3] != actual[pixel * 3]);
        Console.WriteLine($"{(elliptic ? "Ellipse" : "Rectangle")} clipping: {differences} differing pixels.");
        if (!elliptic) CollectionAssert.AreEqual(expected, actual);
        else
        {
            // Elliptic edges use our stated pixel-center approximation; interior/exterior must agree.
            Assert.IsLessThanOrEqualTo(160, differences); // Perimeter-sized edge budget (4 * diameter).
            for (int row = 0; row < height; row++) for (int column = 0; column < width; column++)
            {
                double radius = Math.Sqrt(Math.Pow(column + 0.5 - 30, 2) + Math.Pow(row + 0.5 - 30, 2));
                if (Math.Abs(radius - 20) > 1.5) Assert.AreEqual(expected[(row * width + column) * 3], actual[(row * width + column) * 3]);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FilledPolygonAgreesWithNativeForConvexAndConcaveSamples(bool concave)
    {
        Point16[] points = concave ? [new(2, 2), new(20, 2), new(10, 10), new(20, 20), new(2, 20)] : [new(-4, 2), new(20, 6), new(10, 22)];
        using var native = new WindowsDrawingOracle(32, 32); native.Pen(1, 0xFF); native.Brush(0x00FF00);
        Assert.IsTrue(WindowsDrawingOracle.Polygon(native.Context, points.Select(p => new WindowsDrawingOracle.NativePoint(p.X, p.Y)).ToArray(), points.Length));
        var surface = new PixelSurface(32, 32); surface.Polygon(points, 0xFF, 0x00FF00);
        byte[] expected = native.CopyRgb(), actual = surface.CopyRgb();
        int differences = Enumerable.Range(0, 32 * 32).Count(pixel => !expected.AsSpan(pixel * 3, 3).SequenceEqual(actual.AsSpan(pixel * 3, 3)));
        Console.WriteLine($"Polygon concave={concave}: {differences} differing pixels.");
        // These two samples match exactly. This does not generalize to every
        // polygon or establish historical Win3.1 pixel fidelity.
        CollectionAssert.AreEqual(expected, actual);
    }
}
