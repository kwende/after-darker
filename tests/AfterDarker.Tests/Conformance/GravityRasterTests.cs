using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using static AfterDarker.Tests.Conformance.WindowsDrawingOracle;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class GravityRasterTests
{
    [TestMethod]
    [DataRow(BitmapRasterOperation.SourceCopy)]
    [DataRow(BitmapRasterOperation.SourceAnd)]
    [DataRow(BitmapRasterOperation.BrushThroughSourceMask)]
    public void TernaryBlitsMatchNativeWithOriginsClippingAndOverlappingPixels(BitmapRasterOperation operation)
    {
        byte[] sourceBytes = new byte[24 * 24 * 3], destinationBytes = new byte[sourceBytes.Length];
        var random = new Random(592); random.NextBytes(sourceBytes); random.NextBytes(destinationBytes);
        foreach (bool sameSurface in new[] { false, true })
        foreach (var destination in new Point16[] { new(-3, -2), new(4, 5), new(1, 1), new(20, 21) })
        {
            var target = new PixelSurface(24, 24); target.LoadRgb(destinationBytes);
            var source = sameSurface ? target : new PixelSurface(24, 24);
            if (!sameSurface) source.LoadRgb(sourceBytes);
            var drawing = new Win16Drawing(); drawing.Register(1, target); drawing.Register(2, source);
            drawing.SelectObject(1, drawing.CreateSolidBrush(0x3311AA)); drawing.SetWindowOrg(1, 1, -2); drawing.SetWindowOrg(2, -1, 1);
            drawing.SetROP2(1, 7); // Explicit ROP3 must override the DC's ROP2.
            using var nativeTarget = new WindowsDrawingOracle(24, 24, destinationBytes);
            using var nativeSource = new WindowsDrawingOracle(24, 24, sourceBytes);
            nativeTarget.Brush(0x3311AA); SetWindowOrgEx(nativeTarget.Context, 1, -2, 0); SetROP2(nativeTarget.Context, 7);
            SetWindowOrgEx(nativeSource.Context, -1, 1, 0);
            // A true self-copy uses one HDC/origin, matching Stained Glass's path.
            ushort sourceHandle = sameSurface ? (ushort)1 : (ushort)2;
            nint nativeSourceHandle = sameSurface ? nativeTarget.Context : nativeSource.Context;
            drawing.BitBlt(1, destination.X, destination.Y, 12, 10, sourceHandle, 2, 2, (uint)operation);
            Assert.IsTrue(BitBlt(nativeTarget.Context, destination.X, destination.Y, 12, 10, nativeSourceHandle, 2, 2, (uint)operation));
            CollectionAssert.AreEqual(nativeTarget.CopyRgb(), target.CopyRgb(), $"{operation}: {destination}, same={sameSurface}");
        }
    }

    [TestMethod]
    public void PatternCopyMatchesNativeForSignedExtentsAndIgnoresMixMode()
    {
        foreach (var extent in new (short X, short Y, short Width, short Height)[] {
            (1, 2, 7, 8), (6, 9, -4, -6), (-4, -3, 8, 9), (4, 3, 0, 5), (32767, 2, 10, 8), (-32768, 0, 32767, 2) })
        {
            var surface = new PixelSurface(12, 12); var drawing = new Win16Drawing(); drawing.Register(1, surface);
            drawing.SelectObject(1, drawing.CreateSolidBrush(0x3311AA)); drawing.SetWindowOrg(1, -2, 1); drawing.SetROP2(1, 7);
            using var native = new WindowsDrawingOracle(12, 12); native.Brush(0x3311AA);
            SetWindowOrgEx(native.Context, -2, 1, 0); SetROP2(native.Context, 7);
            uint operation = (uint)BitmapRasterOperation.PatternCopy;
            drawing.PatBlt(1, extent.X, extent.Y, extent.Width, extent.Height, operation);
            Assert.IsTrue(PatBlt(native.Context, extent.X, extent.Y, extent.Width, extent.Height, operation));
            CollectionAssert.AreEqual(native.CopyRgb(), surface.CopyRgb(), extent.ToString());
        }
    }

    [TestMethod]
    public void NativeDeleteObjectAcceptsMemoryDcAndRetainsItsBitmapForReselection()
    {
        using var destination = new WindowsDrawingOracle(8, 8);
        nint bitmap = CreateCompatibleBitmap(destination.Context, 4, 3);
        nint first = CreateCompatibleDC(destination.Context), second = 0;
        try
        {
            Assert.AreNotEqual(nint.Zero, bitmap); Assert.AreNotEqual(nint.Zero, first);
            Assert.AreNotEqual(nint.Zero, SelectObject(first, bitmap));
            Assert.AreEqual(0x665544u, SetPixel(first, 1, 1, 0x665544));
            Assert.IsTrue(DeleteObject(first)); first = 0;
            second = CreateCompatibleDC(destination.Context);
            Assert.AreNotEqual(nint.Zero, SelectObject(second, bitmap));
            Assert.IsTrue(BitBlt(destination.Context, 0, 0, 4, 3, second, 0, 0, Win16Drawing.SourceCopy));
            Assert.IsTrue(destination.CopyRgb().AsSpan((1 * 8 + 1) * 3, 3).SequenceEqual(new byte[] { 0x44, 0x55, 0x66 }));
        }
        finally { if (first != 0) DeleteDC(first); if (second != 0) DeleteDC(second); if (bitmap != 0) DeleteObject(bitmap); }
    }
}
