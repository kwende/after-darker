using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

public sealed partial class Win16ImportGatewayTests
{
    [TestMethod]
    public void RectangleHelpersMarshalSignedWordsAndAliasedPointersWithWin16VoidReturns()
    {
        using var setup = new Probe();
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(-5, 2, 7, 10).Encode());
        setup.Guest.Write(new(Data, 0x360), new Rectangle16(0, 8, 6, 12).Encode());
        setup.Guest.Write(new(Data, 0x370), new Rectangle16(0, 8, 2, 12).Encode());
        var code = new List<byte> { 0xB8, 0x34, 0x12, 0xBA, 0x78, 0x56 }; // Known AX/DX survive void helpers.
        setup.EmitCall(code, "OffsetRect", Data, 0x350, 0xFFFD, 4);
        setup.EmitCall(code, "InflateRect", Data, 0x350, 0xFFFE, 1);
        code.AddRange([0xA3, 0x20, 0, 0x89, 0x16, 0x22, 0]);
        setup.EmitCall(code, "IntersectRect", Data, 0x350, Data, 0x350, Data, 0x360);
        code.AddRange([0xA3, 0x24, 0]);
        setup.EmitCall(code, "EqualRect", Data, 0x350, Data, 0x370);
        code.AddRange([0xA3, 0x26, 0]);
        var returned = setup.Run(code);
        Assert.AreEqual((ushort)0x1234, setup.Word(0x20)); Assert.AreEqual((ushort)0x5678, setup.Word(0x22));
        Assert.AreEqual((ushort)1, setup.Word(0x24)); Assert.AreEqual((ushort)1, setup.Word(0x26));
        Assert.AreEqual(new Rectangle16(0, 8, 2, 12), Rectangle16.Decode(setup.Guest.Read(new(Data, 0x350), 8)));
        Assert.AreEqual((ushort)0x1000, returned.Sp);
        foreach (var call in setup.Calls) AssertPascalReturn(call);
        CollectionAssert.AreEqual(new ushort[] { Data, 0x350, 0xFFFD, 4 }, setup.Calls[0].Arguments.ToArray());
        Assert.AreEqual(Win16ReturnLayout.Void, setup.Calls[0].Binding.ReturnLayout);
        Assert.AreEqual(Win16ReturnLayout.Void, setup.Calls[1].Binding.ReturnLayout);
    }

    [TestMethod]
    public void OriginPixelBrushAndCopyCallsReturnRealValuesToGuestAndPreservePascalStack()
    {
        using var setup = new Probe(drawing: true);
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(-2, 3, 3, 7).Encode());
        var code = new List<byte>();
        setup.EmitCall(code, "GetStockObject", 5); code.AddRange([0xA3, 0x20, 0]);
        setup.EmitCall(code, "SetWindowOrg", 0x103, 0xFFFE, 3);
        code.AddRange([0xA3, 0x22, 0, 0x89, 0x16, 0x24, 0]);
        setup.EmitCall(code, "GetWindowOrg", 0x103);
        code.AddRange([0xA3, 0x26, 0, 0x89, 0x16, 0x28, 0]);
        setup.EmitCall(code, "SetROP2", 0x103, 7); code.AddRange([0xA3, 0x2A, 0]);
        setup.EmitCall(code, "SetPixel", 0x103, 0xFFFF, 4, 0x0033, 0x2211);
        code.AddRange([0xA3, 0x2C, 0, 0x89, 0x16, 0x2E, 0]);
        setup.EmitCall(code, "FrameRect", 0x103, Data, 0x350, Win16Drawing.WhiteBrushHandle);
        code.AddRange([0xA3, 0x30, 0]);
        setup.EmitCall(code, "BitBlt", 0x103, 3, 4, 2, 1, 0x103, 0xFFFF, 4, 0x00CC, 0x0020);
        code.AddRange([0xA3, 0x32, 0]);
        setup.EmitCall(code, "SetPixel", 0x103, 0xFFFD, 4, 0, 0);
        code.AddRange([0xA3, 0x34, 0, 0x89, 0x16, 0x36, 0]);
        var returned = setup.Run(code);
        Assert.AreEqual(Win16Drawing.NullBrushHandle, setup.Word(0x20));
        Assert.AreEqual((ushort)0, setup.Word(0x22)); Assert.AreEqual((ushort)0, setup.Word(0x24));
        Assert.AreEqual((ushort)0xFFFE, setup.Word(0x26)); Assert.AreEqual((ushort)3, setup.Word(0x28));
        Assert.AreEqual((ushort)13, setup.Word(0x2A));
        Assert.AreEqual((ushort)0x2211, setup.Word(0x2C)); Assert.AreEqual((ushort)0x0033, setup.Word(0x2E));
        Assert.AreEqual((ushort)1, setup.Word(0x30)); Assert.AreEqual((ushort)1, setup.Word(0x32));
        Assert.AreEqual(ushort.MaxValue, setup.Word(0x34)); Assert.AreEqual(ushort.MaxValue, setup.Word(0x36));
        Assert.IsTrue(setup.Surface.CopyRgb().AsSpan((1 * 8 + 5) * 3, 3).SequenceEqual(new byte[] { 0x11, 0x22, 0x33 }));
        Assert.AreEqual((ushort)0x1000, returned.Sp);
        foreach (var call in setup.Calls) AssertPascalReturn(call);
        var copy = setup.Calls.Single(call => call.Binding.Implementation == Win16Imports.Handler.BitBlt);
        Assert.AreEqual(20, copy.Binding.ArgumentBytes);
        CollectionAssert.AreEqual(new ushort[] { 0x103, 3, 4, 2, 1, 0x103, 0xFFFF, 4, 0x00CC, 0x0020 }, copy.Arguments.ToArray());
    }

    [TestMethod]
    [DataRow("OffsetRect")]
    [DataRow("InflateRect")]
    public void RectangleMutationsRejectInvalidFarPointersWithoutResumingGuest(string name)
    {
        using var setup = new Probe();
        var code = new List<byte>(); setup.EmitCall(code, name, Data, 0xFFF, 1, 1);
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, name); Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
    }

    private static void AssertPascalReturn(AfterDarker.Runtime.Win16CallTrace call)
    {
        Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
        Assert.AreEqual(Code, call.After.Cs); Assert.AreEqual(Data, call.After.Ds); Assert.AreEqual(Stack, call.After.Ss);
        Assert.AreEqual(call.Before.Bp, call.After.Bp); Assert.AreEqual(call.Before.Es, call.After.Es);
    }
}
