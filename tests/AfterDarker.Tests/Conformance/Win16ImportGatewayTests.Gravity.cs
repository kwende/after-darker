using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tests.Conformance;

public sealed partial class Win16ImportGatewayTests
{
    [TestMethod]
    public void NamedSoundImportsFailConsistentlyAndReturnThroughTheRealGuestStack()
    {
        using var setup = new Probe();
        var code = new List<byte> { 0xBA, 0x78, 0x56 }; // Known DX survives every WORD reply.
        string[] names = ["ADWOPENSOUND", "ADWLOADSOUNDRESOURCE", "ADWSOUNDASYNCCAP", "ADWSETSOUNDMODE", "ADWPLAYSOUND", "ADWCLOSESOUND", "ADWFREESOUND"];
        ushort[][] arguments = [[], [DllData, Data, 0xFFF], [], [0, 0x20], [0], [2], [0]];
        // No device means Load fails without dereferencing even this invalid
        // resource string. No phantom HSOUND is created for the later calls.
        for (int index = 0; index < names.Length; index++)
        {
            setup.EmitCall(code, names[index], arguments[index]);
            code.AddRange([0xA3, (byte)(0x20 + index * 2), 0]);
        }
        var returned = setup.Run(code);
        for (int index = 0; index < names.Length; index++)
        {
            var call = setup.Calls[index];
            Assert.AreEqual((ushort)0, setup.Word((ushort)(0x20 + index * 2)));
            Assert.AreEqual(Win16ReturnLayout.WordInAx, call.Binding.ReturnLayout);
            Assert.AreEqual((ushort)0x5678, call.After.Dx);
            CollectionAssert.AreEqual(arguments[index], call.Arguments.ToArray());
            AssertPascalReturn(call);
        }
        Assert.AreEqual((ushort)0x1000, returned.Sp);
        var binding = Win16Imports.BindImports([new NeImport("ad_snd", null, "adwOpenSound")], Gateway).Single();
        Assert.AreEqual(Win16Imports.Handler.SoundOpen, binding.Implementation);
        Assert.Throws<NotSupportedException>(() => Win16Imports.BindImports([new NeImport("AD_SND", null, "UNKNOWN")], Gateway));
    }

    [TestMethod]
    public void GuestCreatesPaintsCopiesAndReleasesBitmapAndDcHandles()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, "CreateCompatibleDC", 0x103); code.AddRange([0xA3, 0x20, 0]);
        setup.EmitCall(code, "CreateCompatibleBitmap", 0x103, 4, 3); code.AddRange([0xA3, 0x22, 0]);
        setup.EmitCall(code, "SelectObject", 0x600, 0x500); code.AddRange([0xA3, 0x24, 0]);
        setup.EmitCall(code, "PatBlt", 0x600, 3, 3, 0xFFFE, 0xFFFE, 0x00F0, 0x0021);
        setup.EmitCall(code, "BitBlt", 0x103, 2, 1, 4, 3, 0x600, 0, 0, 0x00CC, 0x0020);
        setup.EmitCall(code, "DeleteObject", 0x500); code.AddRange([0xA3, 0x26, 0]); // Selected: fails.
        setup.EmitCall(code, "DeleteDC", 0x600); code.AddRange([0xA3, 0x28, 0]);
        setup.EmitCall(code, "DeleteObject", 0x500); code.AddRange([0xA3, 0x2A, 0]);
        setup.EmitCall(code, "CreateCompatibleDC", 0);
        setup.EmitCall(code, "DeleteObject", 0x600); code.AddRange([0xA3, 0x2C, 0]); // DC alias.
        var returned = setup.Run(code);
        Assert.AreEqual((ushort)0x600, setup.Word(0x20)); Assert.AreEqual((ushort)0x500, setup.Word(0x22));
        Assert.AreEqual(Win16Drawing.DefaultBitmapHandle, setup.Word(0x24));
        Assert.AreEqual((ushort)0, setup.Word(0x26));
        foreach (ushort offset in new ushort[] { 0x28, 0x2A, 0x2C }) Assert.AreEqual((ushort)1, setup.Word(offset));
        Assert.AreEqual(12, setup.Surface.CopyRgb().Count(value => value == 255));
        Assert.AreEqual(0, setup.Services.State.Drawing!.LiveBitmapCount);
        Assert.AreEqual(0, setup.Services.State.Drawing.LiveMemoryDcCount); Assert.AreEqual(0, setup.Services.State.Drawing.BitmapBytes);
        Assert.AreEqual((ushort)0x1000, returned.Sp);
        foreach (var call in setup.Calls) { AssertPascalReturn(call); Assert.AreEqual((ushort)0x5678, call.After.Dx); }
        CollectionAssert.AreEqual(new ushort[] { 0x600, 3, 3, 0xFFFE, 0xFFFE, 0x00F0, 0x0021 }, setup.Calls[3].Arguments.ToArray());
        Assert.AreEqual(14, setup.Calls[3].Binding.ArgumentBytes);
    }

    [TestMethod]
    public void OversizedBitmapReturnsUnsignedNullAndUnsupportedPatternStopsBeforeGuestStore()
    {
        using (var setup = new Probe(drawing: true))
        {
            var code = new List<byte>(); setup.EmitCall(code, "CreateCompatibleBitmap", 0x103, 65535, 1);
            code.AddRange([0xA3, 0x20, 0]); setup.Run(code);
            Assert.AreEqual((ushort)0, setup.Word(0x20)); AssertPascalReturn(setup.Calls.Single());
        }
        using (var setup = new Probe(drawing: true))
        {
            var code = new List<byte>(); setup.EmitCall(code, "PatBlt", 0x103, 0, 0, 1, 1, 0, 0);
            code.AddRange([0xA3, 0x20, 0]);
            StringAssert.Contains(Assert.Throws<InvalidOperationException>(() => setup.Run(code)).Message, "PatBlt");
            Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20)); Assert.AreEqual(0L, setup.Surface.Revision);
        }
    }
}
