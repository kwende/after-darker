using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class Win16ApiTests
{
    [TestMethod]
    [DataRow(-10, -5, 2, 9, -10, -5, true)]
    [DataRow(-10, -5, 2, 9, 1, 8, true)]
    [DataRow(-10, -5, 2, 9, 2, 0, false)]
    [DataRow(-10, -5, 2, 9, 0, 9, false)]
    [DataRow(-10, -5, 2, 9, -11, 0, false)]
    [DataRow(-10, -5, 2, 9, 0, -6, false)]
    [DataRow(2, -5, 2, 9, 2, 0, false)]
    [DataRow(-10, 9, 2, 9, 0, 9, false)]
    [DataRow(2, -5, -10, 9, 0, 0, false)]
    [DataRow(-10, 9, 2, -5, 0, 0, false)]
    [DataRow(-32768, -32768, 32767, 32767, -32768, 32766, true)]
    public void PointInRectanglePreservesSignedHalfOpenGeometry(int left, int top, int right, int bottom,
        int x, int y, bool expected)
    {
        var api = Create();
        var rectangleAddress = new FarPointer16(0x48, 0x350);
        byte[] rectangle = new Rectangle16((short)left, (short)top, (short)right, (short)bottom).Encode();
        api.State.Memory.Write(rectangleAddress, rectangle);
        Assert.AreEqual(expected, api.PtInRect(rectangleAddress, new((short)x, (short)y)));
        CollectionAssert.AreEqual(rectangle, api.State.Memory.Read(rectangleAddress, Rectangle16.ByteCount));
    }

    [TestMethod]
    public void StockBlackPenReusesTheDeviceContextsDefaultAndHasNoOwnedLifetime()
    {
        var drawing = new Win16Drawing();
        var api = new Win16Api(new Win16ApiState(new Memory(), new(new(0x48, 0x400), 512)) { Drawing = drawing });
        drawing.Register(0x103, new AfterDarker.Core.Rendering.PixelSurface(8, 8));
        ushort pen = api.GetStockObject(Win16Drawing.BlackPenIndex);
        Assert.AreEqual(Win16Drawing.BlackPenHandle, pen);
        Assert.AreEqual(pen, api.GetStockObject(Win16Drawing.BlackPenIndex));
        Assert.AreEqual(pen, api.SelectObject(0x103, pen));
        Assert.IsTrue(api.DeleteObject(pen));
        Assert.AreEqual(pen, api.SelectObject(0x103, pen));
        Assert.AreEqual(0, drawing.LivePenCount);
        Assert.AreEqual(0, drawing.PeakPenCount);
    }

    [TestMethod]
    public void SeparateGuestsHaveIndependentHandlesHeapsVersionsAndClocks()
    {
        var first = Create(version: 0x12340A03, tick: uint.MaxValue - 7);
        var second = Create(version: 0x56780B03, tick: 100);
        first.State.Blocks.Register(0x101, new(0x48, 0x100), 16);
        Assert.AreEqual(new FarPointer16(0x48, 0x100), first.GlobalLock(0x101));
        Assert.Throws<InvalidOperationException>(() => second.GlobalLock(0x101));
        Assert.AreEqual(1, first.State.Blocks.OutstandingLocks);
        Assert.AreEqual(0, second.State.Blocks.OutstandingLocks);
        Assert.AreEqual((ushort)0, first.GlobalUnlock(0x101));
        Assert.AreEqual(0x12340A03u, first.GetVersion());
        Assert.AreEqual(0x56780B03u, second.GetVersion());
        Assert.AreEqual(uint.MaxValue - 7, first.GetTickCount());
        Assert.AreEqual(8u, first.GetTickCount()); // DWORD wrap, step 16
        Assert.AreEqual(100u, second.GetTickCount());
        Assert.IsTrue(first.LocalInit(0x48, 0, 512));
        Assert.IsNotNull(first.State.InitializedHeap);
        Assert.IsNull(second.State.InitializedHeap);
    }

    [TestMethod]
    public void InjectedLocalInitFailureDoesNotPublishHeapAndInvalidRequestStillFails()
    {
        var api = Create(succeeds: false);
        Assert.IsFalse(api.LocalInit(0x48, 0, 512));
        Assert.IsNull(api.State.InitializedHeap);
        Assert.Throws<NotSupportedException>(() => api.LocalInit(0x50, 0, 512));
        api.State.Memory.Write(new(0x48, 0x400), [1]);
        Assert.Throws<InvalidOperationException>(() => api.LocalInit(0x48, 0, 512));
    }

    [TestMethod]
    public void EnvironmentReturnsGuestPointerAndRejectsChangedUnsupportedContents()
    {
        var api = Create();
        Assert.AreEqual(new FarPointer16(0x48, 0x300), api.GetDOSEnvironment());
        api.State.Memory.Write(new(0x48, 0x300), [65, 0]);
        Assert.Throws<NotSupportedException>(() => api.GetDOSEnvironment());
        var noEnvironment = new Win16Api(new Win16ApiState(new Memory(), new(new(0x48, 0x400), 512)));
        Assert.Throws<NotSupportedException>(() => noEnvironment.GetDOSEnvironment());
    }

    private static Win16Api Create(uint version = 0x00000A03, uint tick = 100, bool succeeds = true) =>
        new(new Win16ApiState(new Memory(), new(new(0x48, 0x400), 512), new(0x48, 0x300),
            windowsVersion: version, localInitSucceeds: succeeds, initialTick: tick));

    private sealed class Memory : IGuestMemory16
    {
        private readonly byte[] bytes = new byte[4096];
        public byte[] Read(FarPointer16 address, int count)
        {
            if (address.Selector != 0x48 || count < 0 || address.Offset > bytes.Length - count)
                throw new InvalidOperationException("Invalid test guest memory.");
            return bytes.AsSpan(address.Offset, count).ToArray();
        }
        public void Write(FarPointer16 address, byte[] value)
        {
            _ = Read(address, value.Length);
            value.CopyTo(bytes, address.Offset);
        }
    }
}
