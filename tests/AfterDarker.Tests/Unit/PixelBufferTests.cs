using AfterDarker.Core.Rendering;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class PixelBufferTests
{
    [TestMethod]
    public void ReusableDestinationMatchesSnapshotWithoutExposingSurfaceStorage()
    {
        var surface = new PixelSurface(7, 3);
        byte[] destination = new byte[surface.RgbByteCount];
        surface.Paint(new(1, 0, 4, 3), true);
        surface.CopyRgbTo(destination);
        CollectionAssert.AreEqual(surface.CopyRgb(), destination);
        Array.Fill(destination, (byte)123);
        surface.CopyRgbTo(destination);
        CollectionAssert.AreEqual(surface.CopyRgb(), destination);
        surface.Paint(new(0, 0, 7, 3), false);
        surface.CopyRgbTo(destination);
        CollectionAssert.AreEqual(new byte[7 * 3 * 3], destination);
    }

    [TestMethod]
    public void WrongSizeIsRejectedBeforeTouchingDestination()
    {
        var surface = new PixelSurface(2, 2);
        foreach (int length in new[] { 11, 13 })
        {
            byte[] destination = Enumerable.Repeat((byte)0xCC, length).ToArray();
            Assert.Throws<ArgumentException>(() => surface.CopyRgbTo(destination));
            Assert.IsTrue(destination.All(b => b == 0xCC));
        }
    }

    [TestMethod]
    public void RepeatedCopyIntoExistingBufferAllocatesNoFrameArrays()
    {
        var surface = new PixelSurface(640, 480);
        byte[] destination = new byte[surface.RgbByteCount];
        for (int i = 0; i < 10; i++) surface.CopyRgbTo(destination);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) surface.CopyRgbTo(destination);
        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
