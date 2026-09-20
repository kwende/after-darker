using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class BitmapOwnershipTests
{
    [TestMethod]
    public void BitmapPixelsOutliveTheirDcAndSelectionsRemainExclusive()
    {
        var drawing = new Win16Drawing(); var display = new PixelSurface(8, 8); drawing.Register(1, display);
        ushort bitmap = drawing.CreateCompatibleBitmap(1, 4, 3);
        ushort first = drawing.CreateCompatibleDC(1), second = drawing.CreateCompatibleDC(first);
        Assert.AreEqual(Win16Drawing.DefaultBitmapHandle, drawing.SelectObject(first, bitmap));
        Assert.AreEqual((ushort)0, drawing.SelectObject(second, bitmap));
        Assert.AreEqual((ushort)0, drawing.SelectObject(1, bitmap));
        Assert.IsFalse(drawing.DeleteObject(bitmap));
        drawing.SetPixel(first, 2, 1, 0x665544);
        Assert.IsTrue(drawing.DeleteObject(first)); // Original Gravity uses this GDI alias for DeleteDC.
        Assert.AreEqual(1, drawing.LiveBitmapCount); Assert.AreEqual(1, drawing.LiveMemoryDcCount);
        Assert.AreEqual(Win16Drawing.DefaultBitmapHandle, drawing.SelectObject(second, bitmap));
        drawing.BitBlt(1, 0, 0, 4, 3, second, 0, 0, Win16Drawing.SourceCopy);
        Assert.IsTrue(display.CopyRgb().AsSpan((1 * 8 + 2) * 3, 3).SequenceEqual(new byte[] { 0x44, 0x55, 0x66 }));
        Assert.AreEqual(bitmap, drawing.SelectObject(second, Win16Drawing.DefaultBitmapHandle));
        Assert.IsTrue(drawing.DeleteObject(bitmap)); Assert.IsTrue(drawing.DeleteDC(second));
        Assert.IsFalse(drawing.DeleteDC(1)); Assert.IsFalse(drawing.DeleteObject(1));
        Assert.AreEqual(0, drawing.LiveBitmapCount); Assert.AreEqual(0, drawing.LiveMemoryDcCount); Assert.AreEqual(0, drawing.BitmapBytes);
        Assert.IsFalse(drawing.DeleteDC(second)); Assert.IsFalse(drawing.DeleteObject(bitmap));
    }

    [TestMethod]
    public void CompatibleDcStartsWithDefaultAttributesAndMonochromeRequestsFailExplicitly()
    {
        var drawing = new Win16Drawing(); drawing.Register(1, new(8, 8));
        drawing.SetWindowOrg(1, 4, -6); drawing.SetROP2(1, 7); drawing.MoveTo(1, 3, 4);
        ushort memory = drawing.CreateCompatibleDC(1);
        Assert.AreEqual(0u, drawing.GetWindowOrg(memory));
        Assert.AreEqual((ushort)13, drawing.SetROP2(memory, 13)); Assert.AreEqual(0u, drawing.MoveTo(memory, 0, 0));
        Assert.Throws<NotSupportedException>(() => drawing.CreateCompatibleBitmap(memory, 1, 1));
        Assert.Throws<NotSupportedException>(() => drawing.SetPixel(memory, 0, 0, 0xFFFFFF));
        Assert.Throws<NotSupportedException>(() => drawing.CreateCompatibleBitmap(1, 0, 1));
        Assert.Throws<NotSupportedException>(() => drawing.CreateCompatibleDC(99));
        Assert.AreEqual((ushort)0, drawing.CreateCompatibleBitmap(1, ushort.MaxValue, 4));
        Assert.Throws<ArgumentException>(() => drawing.Register(0x500, new(8, 8)));
        Assert.AreEqual(0, drawing.LiveBitmapCount); Assert.AreEqual(0, drawing.BitmapBytes);
    }

    [TestMethod]
    public void BitmapBytesAndHandlePoolsAreBoundedAndReused()
    {
        var drawing = new Win16Drawing(); drawing.Register(1, new(1, 1));
        ushort large = drawing.CreateCompatibleBitmap(1, 2048, 2048);
        ushort another = drawing.CreateCompatibleBitmap(1, 2048, 2048);
        Assert.AreNotEqual((ushort)0, large); Assert.AreNotEqual((ushort)0, another);
        Assert.AreEqual((ushort)0, drawing.CreateCompatibleBitmap(1, 2048, 2048));
        Assert.IsTrue(drawing.DeleteObject(large)); Assert.AreEqual(large, drawing.CreateCompatibleBitmap(1, 2048, 2048));
        Assert.IsTrue(drawing.DeleteObject(large)); Assert.IsTrue(drawing.DeleteObject(another));
        var handles = Enumerable.Range(0, 256).Select(_ => drawing.CreateCompatibleBitmap(1, 1, 1)).ToArray();
        Assert.AreEqual(256, handles.Distinct().Count()); Assert.IsFalse(handles.Contains((ushort)0));
        Assert.AreEqual((ushort)0, drawing.CreateCompatibleBitmap(1, 1, 1));
        foreach (ushort handle in handles) Assert.IsTrue(drawing.DeleteObject(handle));
        var contexts = Enumerable.Range(0, 64).Select(_ => drawing.CreateCompatibleDC(0)).ToArray();
        Assert.AreEqual((ushort)0, drawing.CreateCompatibleDC(0));
        Assert.IsTrue(drawing.DeleteDC(contexts[0])); Assert.AreEqual(contexts[0], drawing.CreateCompatibleDC(0));
        foreach (ushort handle in contexts) Assert.IsTrue(drawing.DeleteDC(handle));
        Assert.AreEqual(0, drawing.BitmapBytes); Assert.AreEqual(0, drawing.LiveMemoryDcCount);
    }

    [TestMethod]
    public void PatBltIgnoresRop2AndDoesNotWrapLargeEdgesOntoTheSurface()
    {
        var drawing = new Win16Drawing(); var surface = new PixelSurface(8, 8); drawing.Register(1, surface);
        drawing.SetROP2(1, 7);
        drawing.PatBlt(1, short.MaxValue, 0, 10, 8, (uint)BitmapRasterOperation.PatternCopy);
        Assert.AreEqual(0L, surface.Revision);
        drawing.PatBlt(1, 6, 7, -3, -4, (uint)BitmapRasterOperation.PatternCopy);
        byte[] first = surface.CopyRgb();
        drawing.PatBlt(1, 6, 7, -3, -4, (uint)BitmapRasterOperation.PatternCopy);
        CollectionAssert.AreEqual(first, surface.CopyRgb());
        Assert.AreEqual(12 * 3, first.Count(value => value == 255));
        Assert.Throws<NotSupportedException>(() => drawing.PatBlt(1, 0, 0, 8, 8, 0));
        CollectionAssert.AreEqual(first, surface.CopyRgb());
    }
}
