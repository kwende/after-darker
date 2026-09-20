using AfterDarker.Core.Ne;
using AfterDarker.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class Win16CallingConventionTests
{
    private const ushort CodeSelector = 8;
    private const ushort DataSelector = 16;
    private const ushort StackSelector = 24;
    private const ushort EmptyStackPointer = 0x1000;

    [TestMethod]
    public void StartupInputsAreAvailableToGuestCodeWithoutInventingAnAxResult()
    {
        using var guest = CreateGuest();
        guest.Set(X86.UC_X86_REG_AX, 0xBEEF);
        var context = new LibraryStartupContext(DataSelector, 0x0234, StackSelector, EmptyStackPointer);
        Win16RegisterConvention.SetUpPrologRegisters(guest, context);

        var prepared = guest.Snapshot();
        Assert.AreEqual((ushort)0xBEEF, prepared.Ax);
        Assert.AreEqual(DataSelector, prepared.Ds);
        Assert.AreEqual(DataSelector, prepared.Di);
        Assert.AreEqual((ushort)0x0234, prepared.Cx);
        Assert.AreEqual((ushort)0, prepared.Es);
        Assert.AreEqual((ushort)0, prepared.Si);
        Assert.AreEqual(StackSelector, prepared.Ss);
        Assert.AreEqual(EmptyStackPointer, prepared.Sp);
        Assert.AreEqual((ushort)0, prepared.Bp);

        // MOV AX,CX reads the actual startup input supplied above.
        var completed = guest.RunUntil("startup inputs", new(CodeSelector, 0), new(CodeSelector, 2));
        Assert.AreEqual((ushort)0x0234, completed.Ax);
    }

    [TestMethod]
    public void FrameReadDoesNotPopAndReturnConsumesOnlyTheFarFrameAndArguments()
    {
        using var guest = CreateGuest();
        Win16RegisterConvention.SetUpPrologRegisters(guest,
            new(DataSelector, 0, StackSelector, EmptyStackPointer));
        guest.Set(X86.UC_X86_REG_CS, CodeSelector);
        guest.Set(X86.UC_X86_REG_SP, 0x0FF6);
        guest.Set(X86.UC_X86_REG_AX, 0xCAFE);
        // Increasing addresses: saved IP, saved CS, last argument, middle, first.
        byte[] frameBytes = [2, 0, 8, 0, 0x33, 0x33, 0x22, 0x22, 0x11, 0x11];
        guest.Write(new(StackSelector, 0x0FF6), frameBytes);
        var before = guest.Snapshot();
        var stack = new Win16Stack(guest, StackSelector, EmptyStackPointer);

        Win16CallFrame frame = stack.ReadCallFrame(6, "three arguments");
        Assert.AreEqual(before, guest.Snapshot());
        CollectionAssert.AreEqual(new ushort[] { 0x1111, 0x2222, 0x3333 }, frame.Arguments.ToArray());
        Assert.AreEqual(new FarPointer16(CodeSelector, 2), frame.ReturnAddress);

        stack.ReturnToCaller(frame);
        var after = guest.Snapshot();
        Assert.AreEqual(new FarPointer16(CodeSelector, 2), after.Pc);
        Assert.AreEqual(EmptyStackPointer, after.Sp);
        Assert.AreEqual(before.Ss, after.Ss);
        Assert.AreEqual(before.Ax, after.Ax); // Stack return does not overwrite a service result.
        Assert.AreEqual(before.Ds, after.Ds);
        Assert.AreEqual(before.Bp, after.Bp);
        CollectionAssert.AreEqual(frameBytes, guest.Read(new(StackSelector, 0x0FF6), frameBytes.Length));
    }

    [TestMethod]
    public void InvalidFramesAreRejectedBeforeChangingRegisters()
    {
        using var guest = CreateGuest();
        Win16RegisterConvention.SetUpPrologRegisters(guest,
            new(DataSelector, 0, StackSelector, EmptyStackPointer));
        var stack = new Win16Stack(guest, StackSelector, EmptyStackPointer);
        var emptyRegisters = guest.Snapshot();
        Assert.Throws<InvalidOperationException>(() => stack.ReadCallFrame(0, "missing return"));
        Assert.AreEqual(emptyRegisters, guest.Snapshot());

        guest.Set(X86.UC_X86_REG_SP, 0x0FFC);
        guest.Write(new(StackSelector, 0x0FFC), [0, 0, 16, 0]); // Return points into data, not code.
        var invalidReturnRegisters = guest.Snapshot();
        Assert.Throws<InvalidOperationException>(() => stack.ReadCallFrame(0, "data return"));
        Assert.Throws<InvalidOperationException>(() => stack.ReadCallFrame(1, "partial word"));
        Assert.AreEqual(invalidReturnRegisters, guest.Snapshot());

        guest.Set(X86.UC_X86_REG_SS, DataSelector);
        var wrongStackRegisters = guest.Snapshot();
        Assert.Throws<InvalidOperationException>(() => stack.ReadCallFrame(0, "wrong stack"));
        Assert.AreEqual(wrongStackRegisters, guest.Snapshot());
    }

    private static SegmentedGuest CreateGuest()
    {
        var guest = new SegmentedGuest();
        guest.Map(CodeSelector, 0x10000, [0x89, 0xC8, 0x90], code: true); // MOV AX,CX; NOP end marker.
        guest.Map(DataSelector, 0x20000, new byte[4096], code: false);
        guest.Map(StackSelector, 0x30000, new byte[4096], code: false);
        guest.Install(32);
        return guest;
    }
}
