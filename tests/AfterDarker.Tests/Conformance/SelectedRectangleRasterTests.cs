using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class SelectedRectangleRasterTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void RectangleMatchesNativeGdiWithNullOrOnePixelPenIncludingClippingAndReversedBounds(int penWidth)
    {
        var rectangles = new List<Rectangle16>
        {
            new(1, 2, 18, 17), new(18, 17, 1, 2), new(-5, -4, 12, 8),
            new(3, 3, 4, 4), new(3, 3, 4, 10), new(3, 3, 10, 4),
            new(1, 1, 1, 9), new(1, 1, 9, 1), new(-20, -20, -1, -1),
            new(short.MinValue, short.MinValue, short.MaxValue, short.MaxValue)
        };
        var random = new Random(741);
        for (int index = 0; index < 100; index++)
            rectangles.Add(new((short)random.Next(-20, 45), (short)random.Next(-20, 45),
                (short)random.Next(-20, 45), (short)random.Next(-20, 45)));
        foreach (var rectangle in rectangles)
        {
            var surface = new PixelSurface(24, 24);
            int changed = surface.Rectangle(rectangle, 0x00332211, penWidth != 0, 0x00996644);
            byte[] actual = surface.CopyRgb();
            byte[] expected = WindowsShapeOracle.Render(24, 24, rectangle, penWidth, drawRectangle: true);
            Assert.IsTrue(actual.AsSpan().SequenceEqual(expected), $"Rectangle {rectangle}, pen width {penWidth}.");
            Assert.AreEqual(Enumerable.Range(0, 24 * 24).Count(pixel => actual[pixel * 3] != 0), changed);
            Assert.AreEqual(0, surface.Rectangle(rectangle, 0x00332211, penWidth != 0, 0x00996644));
        }
    }
}
