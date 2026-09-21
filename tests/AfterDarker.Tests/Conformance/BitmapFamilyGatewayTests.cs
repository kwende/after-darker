using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Conformance;

public sealed partial class Win16ImportGatewayTests
{
    [TestMethod]
    [DataRow("CreateRectRgnIndirect", 2)]
    [DataRow("CreateEllipticRgnIndirect", 3)]
    public void RegionFarPointerHandleAndBoolTravelThroughTheGuestStack(string create, int complexity)
    {
        using var setup = new Probe(drawing: true);
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(-2, -1, 7, 5).Encode());
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, create, Data, 0x350);
        code.AddRange([0x89, 0xC3, 0x68, 3, 1, 0x53]); // Save AX in BX; push HDC, returned region.
        setup.EmitCall(code, "SelectClipRgn"); code.AddRange([0xA3, 0x20, 0]);
        code.Add(0x53); setup.EmitCall(code, "DeleteObject");
        var final = setup.Run(code);
        Assert.AreEqual((ushort)complexity, setup.Word(0x20)); Assert.AreEqual((ushort)1, final.Ax);
        Assert.AreEqual((ushort)0x5678, final.Dx); Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(0, setup.Services.State.Drawing!.LiveRegionCount);
        AssertFamilyReturns(setup);
    }

    [TestMethod]
    [DataRow(-1, 4, 0)]
    [DataRow(4, -1, 1)]
    [DataRow(2, 2, 1)]
    public void EmptyRectangleUsesSignedBoundsWithoutChangingGuestMemory(int left, int right, int empty)
    {
        using var setup = new Probe(); byte[] rectangle = new Rectangle16((short)left, -2, (short)right, 3).Encode();
        setup.Guest.Write(new(Data, 0x350), rectangle);
        var code = new List<byte> { 0xBA, 0x78, 0x56 }; setup.EmitCall(code, "IsRectEmpty", Data, 0x350);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)empty, final.Ax); Assert.AreEqual((ushort)0x5678, final.Dx);
        CollectionAssert.AreEqual(rectangle, setup.Guest.Read(new(Data, 0x350), 8)); AssertFamilyReturns(setup);
    }

    [TestMethod]
    public void PolygonReadsSignedPointArrayAndWhiteStockPenWithoutMovingCurrentPoint()
    {
        using var setup = new Probe(drawing: true);
        setup.Guest.Write(new(Data, 0x350), new byte[] { 0xFE, 0xFF, 1, 0, 6, 0, 1, 0, 3, 0, 5, 0 });
        setup.Services.MoveTo(0x103, 7, 4);
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, "GetStockObject", 6); code.AddRange([0x68, 3, 1, 0x50]); setup.EmitCall(code, "SelectObject");
        setup.EmitCall(code, "Polygon", 0x103, Data, 0x350, 3);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)1, final.Ax); Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.IsTrue(setup.Surface.CopyRgb().Any(value => value != 0));
        Assert.AreEqual(0x00040007u, setup.Services.MoveTo(0x103, 0, 0)); AssertFamilyReturns(setup);
    }

    [TestMethod]
    [DataRow("IsRectEmpty")]
    [DataRow("CreateRectRgnIndirect")]
    [DataRow("CreateEllipticRgnIndirect")]
    [DataRow("Polygon")]
    public void InvalidStructurePointerFailsBeforeObjectAllocationOrRasterMutation(string name)
    {
        using var setup = new Probe(drawing: true); var code = new List<byte>();
        setup.EmitCall(code, name, name == "Polygon" ? new ushort[] { 0x103, Data, 0xFFE, 3 } : new ushort[] { Data, 0xFFE });
        var failure = Assert.Throws<InvalidOperationException>(() => setup.Run(code)); StringAssert.Contains(failure.Message, name);
        Assert.AreEqual(0L, setup.Surface.Revision); Assert.AreEqual(0, setup.Services.State.Drawing!.LiveRegionCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadBitmapResolvesNumericOrNamedIdentityAndGuestDeletesReturnedObject(bool named)
    {
        using var setup = new Probe(drawing: true, resources: new(DllData, BitmapResourceFixture.Catalog()));
        setup.Guest.Write(new(Data, 0x350), "sprite\0"u8.ToArray());
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, "LoadBitmap", DllData, named ? Data : (ushort)0, named ? (ushort)0x350 : (ushort)1);
        code.AddRange([0xA3, 0x20, 0, 0x50]); setup.EmitCall(code, "DeleteObject");
        var final = setup.Run(code);
        Assert.AreNotEqual((ushort)0, setup.Word(0x20)); Assert.AreEqual((ushort)1, final.Ax);
        Assert.AreEqual((ushort)0x5678, final.Dx); Assert.AreEqual(0, setup.Services.State.Drawing!.LiveBitmapCount);
        AssertFamilyReturns(setup);
    }

    [TestMethod]
    public void MissingResourceReturnsNullAndBadNamePointerFailsBeforeAllocation()
    {
        using var setup = new Probe(drawing: true, resources: new(DllData, BitmapResourceFixture.Catalog()));
        Assert.AreEqual((ushort)0, setup.Services.LoadBitmap(DllData, new(0, 99)));
        Assert.AreEqual((ushort)0, setup.Services.LoadBitmap(999, new(0, 1)));
        var code = new List<byte>(); setup.EmitCall(code, "LoadBitmap", DllData, 0xFFFF, 10);
        Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        Assert.AreEqual(0, setup.Services.State.Drawing!.LiveBitmapCount);
    }

    [TestMethod]
    public void DcColorsReturnDwordValuesAndBackgroundModeReturnsAWord()
    {
        using var setup = new Probe(drawing: true); var code = new List<byte>();
        setup.EmitCall(code, "SetTextColor", 0x103, 0x0233, 0x2211);
        setup.EmitCall(code, "SetTextColor", 0x103, 0, 0); code.AddRange([0xA3, 0x20, 0, 0x89, 0x16, 0x22, 0]);
        setup.EmitCall(code, "SetBkColor", 0x103, 0x0266, 0x5544);
        setup.EmitCall(code, "SetBkColor", 0x103, 0, 0); code.AddRange([0xA3, 0x24, 0, 0x89, 0x16, 0x26, 0]);
        setup.EmitCall(code, "SetBkMode", 0x103, 1); code.AddRange([0xA3, 0x28, 0]);
        setup.EmitCall(code, "SetBkMode", 0x103, 0xFFFF);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)0x2211, setup.Word(0x20)); Assert.AreEqual((ushort)0x33, setup.Word(0x22));
        Assert.AreEqual((ushort)0x5544, setup.Word(0x24)); Assert.AreEqual((ushort)0x66, setup.Word(0x26));
        Assert.AreEqual((ushort)2, setup.Word(0x28)); Assert.AreEqual((ushort)0, final.Ax);
        Assert.AreEqual((ushort)1, setup.Services.SetBkMode(0x103, 2)); AssertFamilyReturns(setup);
    }

    private static void AssertFamilyReturns(Probe setup)
    {
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
            Assert.AreEqual(Code, call.After.Cs); Assert.AreEqual(Data, call.After.Ds); Assert.AreEqual(Stack, call.After.Ss);
            Assert.AreEqual(call.Before.Es, call.After.Es); Assert.AreEqual(call.Before.Bp, call.After.Bp);
        }
        Assert.AreEqual((ushort)0x1000, setup.Calls[^1].After.Sp);
    }
}
