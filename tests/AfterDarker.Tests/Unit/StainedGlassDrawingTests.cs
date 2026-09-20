using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class StainedGlassDrawingTests
{
    [TestMethod]
    public void HollowBrushPreservesInteriorAndOwnedObjectAccounting()
    {
        var surface = new PixelSurface(12, 12); surface.LoadRgb(Enumerable.Repeat((byte)0x55, 12 * 12 * 3).ToArray());
        var drawing = new Win16Drawing(); drawing.Register(1, surface);
        Assert.AreEqual(Win16Drawing.WhiteBrushHandle, drawing.SelectObject(1, Win16Drawing.NullBrushHandle));
        drawing.Rectangle(1, new(1, 1, 11, 11));
        drawing.Ellipse(1, new(2, 2, 10, 10));
        Assert.IsTrue(surface.CopyRgb().AsSpan((6 * 12 + 6) * 3, 3).SequenceEqual(new byte[] { 0x55, 0x55, 0x55 }));
        byte[] before = surface.CopyRgb();
        drawing.SelectObject(1, Win16Drawing.NullPenHandle);
        drawing.Ellipse(1, new(0, 0, 12, 12)); drawing.Rectangle(1, new(0, 0, 12, 12));
        drawing.FillRect(1, new(0, 0, 12, 12), Win16Drawing.NullBrushHandle);
        CollectionAssert.AreEqual(before, surface.CopyRgb());
        Assert.IsTrue(drawing.DeleteObject(Win16Drawing.NullBrushHandle));
        Assert.AreEqual(0, drawing.LiveBrushCount); Assert.AreEqual(0, drawing.LivePenCount);
    }

    [TestMethod]
    public void OriginAndMixArePerDcAndExtremeTranslationsDoNotWrapIntoTheSurface()
    {
        var surface = new PixelSurface(8, 8); var drawing = new Win16Drawing();
        drawing.Register(1, surface); drawing.Register(2, new PixelSurface(8, 8));
        drawing.SetWindowOrg(1, short.MinValue, short.MinValue);
        Assert.AreEqual(0x80008000u, drawing.GetWindowOrg(1)); Assert.AreEqual(0u, drawing.GetWindowOrg(2));
        Assert.AreEqual(uint.MaxValue, drawing.SetPixel(1, short.MaxValue, short.MaxValue, 0xFFFFFF));
        Assert.AreEqual(0x112233u, drawing.SetPixel(1, short.MinValue, short.MinValue, 0x112233));
        Assert.AreEqual((ushort)13, drawing.SetROP2(1, 7));
        Assert.AreEqual((ushort)0, drawing.SetROP2(1, 17));
        Assert.AreEqual((ushort)7, drawing.SetROP2(1, 13)); Assert.AreEqual((ushort)13, drawing.SetROP2(2, 13));
        Assert.Throws<NotSupportedException>(() => drawing.GetWindowOrg(99));
        Assert.Throws<NotSupportedException>(() => drawing.SetWindowOrg(99, 0, 0));
    }

    [TestMethod]
    public void XorStrokesRestoreTheEntireSurfaceAndClippingDoesNotWrapSignedCoordinates()
    {
        var surface = new PixelSurface(16, 16);
        byte[] initial = new byte[surface.RgbByteCount]; new Random(61).NextBytes(initial); surface.LoadRgb(initial);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            surface.WideLine(short.MinValue, short.MinValue, short.MaxValue, short.MaxValue, 0x335599, RasterMix.XorPen);
            surface.Ellipse(new(-6, -6, 12, 12), 0x005533, 3, null, RasterMix.XorPen);
            surface.Rectangle(new(-2, -2, 8, 10), 0x332211, true, 0xFFEEDD, RasterMix.XorPen);
        }
        CollectionAssert.AreEqual(initial, surface.CopyRgb());
    }

    [TestMethod]
    public void CopyGuardsLeaveTheDestinationUntouchedAndDistinctSourceRemainsUnmodified()
    {
        var source = new PixelSurface(8, 8); source.Fill(new(0, 0, 8, 8), 0xABCDEF);
        var destination = new PixelSurface(8, 8); var drawing = new Win16Drawing();
        drawing.Register(1, source); drawing.Register(2, destination);
        byte[] original = source.CopyRgb();
        Assert.Throws<NotSupportedException>(() => drawing.BitBlt(2, 0, 0, 8, 8, 1, 0, 0, 0));
        Assert.Throws<NotSupportedException>(() => drawing.BitBlt(2, 0, 0, -1, 8, 1, 0, 0, Win16Drawing.SourceCopy));
        Assert.Throws<NotSupportedException>(() => drawing.BitBlt(2, 0, 0, 8, 8, 99, 0, 0, Win16Drawing.SourceCopy));
        Assert.AreEqual(0L, destination.Revision);
        drawing.BitBlt(2, 1, 1, 4, 4, 1, 2, 2, Win16Drawing.SourceCopy);
        CollectionAssert.AreEqual(original, source.CopyRgb());
        Assert.IsTrue(destination.CopyRgb().AsSpan((1 * 8 + 1) * 3, 3).SequenceEqual(new byte[] { 0xEF, 0xCD, 0xAB }));
    }

    [TestMethod]
    public void RectangleHelpersWrapWordsAndClearEmptyAliasedIntersections()
    {
        var memory = new RectangleMemory(); var api = new Win16Api(new Win16ApiState(memory, new(new(0x48, 128), 128)));
        var first = new FarPointer16(0x48, 0);
        var second = new FarPointer16(0x48, 8);
        api.SetRect(first, short.MinValue, 2, short.MaxValue, 10);
        api.OffsetRect(first, -1, 2);
        Assert.AreEqual(new Rectangle16(short.MaxValue, 4, 32766, 12), Rectangle16.Decode(memory.Read(first, 8)));
        api.SetRect(first, -3, -2, 7, 8); api.InflateRect(first, -2, -3);
        Assert.AreEqual(new Rectangle16(-1, 1, 5, 5), Rectangle16.Decode(memory.Read(first, 8)));
        api.SetRect(second, 5, 1, 10, 8);
        Assert.IsFalse(api.IntersectRect(first, first, second));
        Assert.AreEqual(new Rectangle16(0, 0, 0, 0), Rectangle16.Decode(memory.Read(first, 8)));
        Assert.IsFalse(api.EqualRect(first, second)); Assert.IsTrue(api.EqualRect(second, second));
    }

    private sealed class RectangleMemory : IGuestMemory16
    {
        private readonly byte[] bytes = new byte[256];
        public byte[] Read(FarPointer16 address, int count)
        {
            if (address.Selector != 0x48 || count < 0 || address.Offset > bytes.Length - count) throw new InvalidOperationException();
            return bytes.AsSpan(address.Offset, count).ToArray();
        }
        public void Write(FarPointer16 address, byte[] value) { _ = Read(address, value.Length); value.CopyTo(bytes, address.Offset); }
    }
}
