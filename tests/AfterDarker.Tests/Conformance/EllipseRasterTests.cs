using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class EllipseRasterTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void HardRainRingsStayWithinOnePixelOfWindowsButDoNotClaimExactRasterEquality(int penWidth)
    {
        // Square-pixel Hard Rain generates circles; radius 27 gives one pixel of
        // headroom beyond its largest erasure. Compare each color in both directions, so disappearing interiors
        // and thin outlines cannot pass merely by covering the same area.
        int differences = 0;
        for (short radius = 4; radius <= 27; radius++)
        {
            var rectangle = new Rectangle16((short)(32 - radius), (short)(32 - radius), (short)(32 + radius), (short)(32 + radius));
            var surface = new PixelSurface(64, 64);
            surface.Ellipse(rectangle, 0x00332211, penWidth, 0x00996644);
            byte[] actual = surface.CopyRgb();
            byte[] expected = WindowsShapeOracle.Render(64, 64, rectangle, penWidth);
            for (int row = 0; row < 64; row++)
            for (int column = 0; column < 64; column++)
            {
                int offset = (row * 64 + column) * 3;
                if (actual.AsSpan(offset, 3).SequenceEqual(expected.AsSpan(offset, 3))) continue;
                differences++;
                Assert.IsTrue(HasColorNearby(expected, actual.AsSpan(offset, 3), column, row), $"Software radius {radius}: ({column},{row}) exceeds one-pixel tolerance.");
                Assert.IsTrue(HasColorNearby(actual, expected.AsSpan(offset, 3), column, row), $"Windows radius {radius}: ({column},{row}) exceeds one-pixel tolerance.");
            }
        }
        Assert.IsGreaterThan(0, differences); // Keep the known non-equivalence visible.
        Console.WriteLine($"Width-{penWidth} circles: {differences} differing pixels across 24 radii, each within one pixel of the matching native color.");
    }

    private static bool HasColorNearby(byte[] pixels, ReadOnlySpan<byte> color, int column, int row)
    {
        for (int nearbyRow = Math.Max(0, row - 1); nearbyRow <= Math.Min(63, row + 1); nearbyRow++)
        for (int nearbyColumn = Math.Max(0, column - 1); nearbyColumn <= Math.Min(63, column + 1); nearbyColumn++)
            if (pixels.AsSpan((nearbyRow * 64 + nearbyColumn) * 3, 3).SequenceEqual(color)) return true;
        return false;
    }
}
