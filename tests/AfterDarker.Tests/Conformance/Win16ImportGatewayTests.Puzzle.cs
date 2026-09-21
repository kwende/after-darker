using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

public sealed partial class Win16ImportGatewayTests
{
    [TestMethod]
    public void CopyRectPreservesVoidRegistersAndUnionRectSupportsAliasedSignedInputs()
    {
        using var setup = new Probe(); // Rectangle helpers require no drawing backend.
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(-7, 2, 4, 9).Encode());
        setup.Guest.Write(new(Data, 0x370), new Rectangle16(-2, -3, 12, 7).Encode());
        var code = new List<byte> { 0xB8, 0x34, 0x12, 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, "CopyRect", Data, 0x351, Data, 0x350); // Partially overlapping eight-byte buffers.
        code.AddRange([0xA3, 0x20, 0]);
        setup.EmitCall(code, "UnionRect", Data, 0x351, Data, 0x351, Data, 0x370);
        code.AddRange([0xA3, 0x22, 0]);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)0x1234, setup.Word(0x20));
        Assert.AreEqual(Win16ReturnLayout.Void, setup.Calls[0].Binding.ReturnLayout);
        Assert.AreEqual((ushort)1, setup.Word(0x22));
        Assert.AreEqual(new Rectangle16(-7, -3, 12, 9), Rectangle16.Decode(setup.Guest.Read(new(Data, 0x351), 8)));
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        CollectionAssert.AreEqual(new ushort[] { Data, 0x351, Data, 0x350 }, setup.Calls[0].Arguments.ToArray());
        CollectionAssert.AreEqual(new ushort[] { Data, 0x351, Data, 0x351, Data, 0x370 }, setup.Calls[1].Arguments.ToArray());
        foreach (var call in setup.Calls) AssertPascalReturn(call);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void UnionRectIgnoresEmptyAndInvertedInputs(bool bothEmpty)
    {
        using var setup = new Probe();
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(9, 9, -9, -9).Encode());
        Rectangle16 second = bothEmpty ? new(-3, 4, -3, 8) : new(-3, 4, 7, 8);
        setup.Guest.Write(new(Data, 0x370), second.Encode());
        var code = new List<byte>();
        setup.EmitCall(code, "UnionRect", Data, 0x370, Data, 0x350, Data, 0x370);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)(bothEmpty ? 0 : 1), final.Ax);
        Assert.AreEqual(bothEmpty ? default : second, Rectangle16.Decode(setup.Guest.Read(new(Data, 0x370), 8)));
        AssertPascalReturn(setup.Calls.Single());
    }

    [TestMethod]
    public void ScrollDCTranslatesSignedDeltasAndWritesExposedBoundsBackToTheGuest()
    {
        using var setup = new Probe(drawing: true);
        for (short row = 0; row < 6; row++)
            for (short column = 0; column < 8; column++)
                setup.Services.SetPixel(0x103, column, row, (uint)(row * 8 + column + 1));
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(1, 1, 7, 5).Encode());
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        ushort[] arguments = [0x103, 0xFFFF, 0, Data, 0x350, Data, 0x350, 0, Data, 0x370];
        setup.EmitCall(code, "ScrollDC", arguments);
        code.AddRange([0xA3, 0x20, 0]);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)1, setup.Word(0x20));
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(new Rectangle16(6, 1, 7, 5), Rectangle16.Decode(setup.Guest.Read(new(Data, 0x370), 8)));
        byte[] pixels = setup.Surface.CopyRgb();
        for (int row = 1; row < 5; row++)
            for (int column = 1; column < 6; column++)
                Assert.AreEqual((byte)(row * 8 + column + 2), pixels[(row * 8 + column) * 3]);
        var call = setup.Calls.Single();
        CollectionAssert.AreEqual(arguments, call.Arguments.ToArray());
        Assert.AreEqual(20, call.Binding.ArgumentBytes);
        AssertPascalReturn(call);
    }

    [TestMethod]
    public void ScrollDCRejectsReadOnlyGuestCodeAsOutputBeforeChangingPixels()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        setup.EmitCall(code, "ScrollDC", 0x103, 1, 0, 0, 0, 0, 0, 0, Code, 0);
        StringAssert.Contains(Assert.Throws<InvalidOperationException>(() => setup.Run(code)).Message, "write");
        Assert.AreEqual(0L, setup.Services.State.Drawing!.OperationCount);
    }

    [TestMethod]
    [DataRow("CopyRect")]
    [DataRow("UnionRect")]
    [DataRow("ScrollDC")]
    public void RectangleOutputsAreRangeCheckedBeforeReturningOrDrawing(string name)
    {
        using var setup = new Probe(drawing: true);
        setup.Guest.Write(new(Data, 0x350), new Rectangle16(0, 0, 8, 6).Encode());
        var code = new List<byte>();
        ushort[] arguments = name switch
        {
            "CopyRect" => [Data, 0xFFC, Data, 0x350],
            "UnionRect" => [Data, 0xFFC, Data, 0x350, Data, 0x350],
            _ => [0x103, 1, 0, Data, 0x350, 0, 0, 0, Data, 0xFFC]
        };
        setup.EmitCall(code, name, arguments);
        code.AddRange([0xA3, 0x20, 0]);
        StringAssert.Contains(Assert.Throws<InvalidOperationException>(() => setup.Run(code)).Message, name);
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(0L, setup.Services.State.Drawing!.OperationCount);
    }
}
