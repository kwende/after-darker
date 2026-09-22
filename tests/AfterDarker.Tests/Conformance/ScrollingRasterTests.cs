using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using static AfterDarker.Tests.Conformance.WindowsDrawingOracle;

namespace AfterDarker.Tests.Conformance;

[TestClass]
public sealed class ScrollingRasterTests
{
    [TestMethod]
    [DataRow(1, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 1)]
    [DataRow(0, -1)]
    [DataRow(2, -2)]
    [DataRow(-2, 2)]
    [DataRow(0, 0)]
    [DataRow(20, 20)]
    [DataRow(-32768, 32767)]
    public void HandRolledMatchesNativePixelsAndExposedBoundsAcrossRectangleRelationships(int horizontal, int vertical)
    {
        Rectangle16?[] rectangles = [null, new(0, 0, 8, 6), new(2, 1, 6, 5), new(-2, -1, 4, 4),
            new(7, 5, 10, 9), new(3, 3, 3, 4), new(5, 4, 2, 1)];
        foreach (var scroll in rectangles)
            foreach (var clip in rectangles)
                Compare(scroll, clip, (short)horizontal, (short)vertical, default);
    }

    [TestMethod]
    public void HandRolledAndNativeOriginsTranslateInputsAndExposedBounds()
    {
        foreach (Point16 origin in new Point16[] { new(-3, -2), new(3, 2), new(32767, -32768) })
            foreach (Rectangle16? scroll in new Rectangle16?[] { null, new(-3, -2, 6, 5) })
                foreach (Point16 delta in new Point16[] { new(1, -1), new(-1, 0), new(0, 0) })
                    Compare(scroll, null, delta.X, delta.Y, origin);
    }

    private static NativeRect[]? ToNative(Rectangle16? rectangle) => rectangle is { } value
        ? [new(value.Left, value.Top, value.Right, value.Bottom)] : null;

    private static void Compare(Rectangle16? scroll, Rectangle16? clip, short horizontal, short vertical, Point16 origin)
    {
        var handRolled = new Win16Drawing();
        var pixels = new PixelSurface(8, 6);
        byte[] original = Enumerable.Range(1, pixels.RgbByteCount).Select(value => (byte)value).ToArray();
        pixels.LoadRgb(original);
        handRolled.Register(1, pixels);
        handRolled.SetWindowOrg(1, origin.X, origin.Y);
        using var native = new WindowsDrawingOracle(8, 6, original);
        Assert.IsTrue(SetWindowOrgEx(native.Context, origin.X, origin.Y, 0));
        NativeRect[]? nativeScroll = ToNative(scroll), nativeClip = ToNative(clip);
        NativeRect[] nativeUpdate = new NativeRect[1];
        bool nativeResult = ScrollDC(native.Context, horizontal, vertical, nativeScroll, nativeClip, 0, nativeUpdate);
        var nativeExposed = new Rectangle16(unchecked((short)nativeUpdate[0].Left), unchecked((short)nativeUpdate[0].Top),
            unchecked((short)nativeUpdate[0].Right), unchecked((short)nativeUpdate[0].Bottom));
        bool handRolledResult = handRolled.ScrollDC(1, horizontal, vertical, scroll, clip, 0, true, out var handRolledExposed);
        string scenario = $"scroll={scroll}, clip={clip}, delta={horizontal},{vertical}, origin={origin}";
        Assert.AreEqual(nativeResult, handRolledResult, scenario);
        Assert.AreEqual(nativeExposed, handRolledExposed, scenario);
        CollectionAssert.AreEqual(native.CopyRgb(), pixels.CopyRgb(), scenario);
    }
}
