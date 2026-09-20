using System.Buffers.Binary;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class DosInterruptTests
{
    [TestMethod]
    public void LargerServiceCeilingStillRejectsWorkBeyondTheChosenBudget()
    {
        // Zot! needs more service returns than earlier modules; this source-only
        // proof ensures widening the configurable ceiling did not remove the limit.
        byte[] code = Enumerable.Range(0, 1100).SelectMany(_ => new byte[] { 0xCD, 0x21 }).Append((byte)0x90).ToArray();
        using var guest = new SegmentedGuest(instructionLimit: 2_000_000, nativeSliceTimeout: TimeSpan.FromSeconds(3));
        guest.Map(8, 0x10000, code, true); guest.Install(16);
        int dispatched = 0;
        guest.DispatchInterrupt = _ => dispatched++;
        Assert.Throws<InvalidOperationException>(() => guest.RunUntil("bounded", new(8, 0), new(8, 2200), 1024));
        Assert.AreEqual(1024, dispatched);
        dispatched = 0;
        guest.RunUntil("explicit", new(8, 0), new(8, 2200), 4096);
        Assert.AreEqual(1100, dispatched);
        Assert.Throws<ArgumentOutOfRangeException>(() => guest.RunUntil("too large", new(8, 0), new(8, 2200), 4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedGuest(instructionLimit: 2_000_001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedGuest(nativeSliceTimeout: TimeSpan.FromSeconds(4)));
    }

    [TestMethod]
    public void LargerCleanupServiceBudgetIsExplicitAndStillBoundsDispatch()
    {
        // 200 independent INTs stand in for a cleanup loop's 200 host services.
        // This tests the generic execution budget without a copyrighted input.
        byte[] code = Enumerable.Range(0, 200).SelectMany(_ => new byte[] { 0xCD, 0x21 }).Append((byte)0x90).ToArray();
        foreach (int limit in new[] { 128, 200 })
        {
            using var guest = new SegmentedGuest();
            guest.Map(8, 0x10000, code, true); guest.Install(16);
            int dispatched = 0;
            guest.DispatchInterrupt = _ => dispatched++;
            if (limit == 128)
                Assert.Throws<InvalidOperationException>(() => guest.RunUntil("cleanup", new(8, 0), new(8, 400), limit));
            else guest.RunUntil("cleanup", new(8, 0), new(8, 400), limit);
            Assert.AreEqual(limit, dispatched);
        }
    }

    [TestMethod]
    public void ProtectedModeInterruptStopsAfterIntWithoutFrameAndResumesGuestStores()
    {
        using var guest = new SegmentedGuest();
        byte[] code = [
            0xB4, 0x2A, 0xCD, 0x21,             // MOV AH,2A; INT 21
            0xA3, 0x20, 0x00,                   // MOV [0020],AX
            0x89, 0x0E, 0x22, 0x00,             // MOV [0022],CX
            0x89, 0x16, 0x24, 0x00,             // MOV [0024],DX
            0xB4, 0x2C, 0xCD, 0x21,             // MOV AH,2C; INT 21
            0x89, 0x0E, 0x26, 0x00,             // MOV [0026],CX
            0x89, 0x16, 0x28, 0x00,             // MOV [0028],DX
            0xB8, 0xEF, 0xBE,                   // MOV AX,BEEF proves execution continued
            0x90];                             // end marker, not executed
        guest.Map(8, 0x10000, code, true);
        guest.Map(16, 0x20000, new byte[4096], false);
        guest.Map(24, 0x30000, new byte[4096], false);
        guest.Install(32);
        guest.Set(X86.UC_X86_REG_DS, 16);
        guest.Set(X86.UC_X86_REG_SS, 24);
        guest.Set(X86.UC_X86_REG_SP, 0x1000);
        guest.Set(X86.UC_X86_REG_BX, 0xABCD);
        var time = new DateTime(1993, 6, 15, 12, 34, 56, 780);
        guest.DispatchInterrupt = number =>
        {
            Assert.AreEqual(0x21, number);
            ushort ax = (ushort)guest.Get(X86.UC_X86_REG_AX);
            var reply = DosClock.Respond((byte)(ax >> 8), ax, time);
            guest.Set(X86.UC_X86_REG_AX, reply.Ax);
            guest.Set(X86.UC_X86_REG_CX, reply.Cx);
            guest.Set(X86.UC_X86_REG_DX, reply.Dx);
        };
        var result = guest.RunUntil("DOS probe", new(8, 0), new(8, (ushort)(code.Length - 1)));
        Assert.AreEqual((ushort)0xBEEF, result.Ax);
        Assert.AreEqual((ushort)0xABCD, result.Bx);
        Assert.AreEqual((ushort)0x1000, result.Sp);
        byte[] stored = guest.Read(new(16, 0x20), 10);
        Assert.AreEqual((ushort)2, (ushort)(Word(0) & 0xFF)); // Tuesday, independently specified
        Assert.AreEqual((ushort)1993, Word(2));
        Assert.AreEqual((ushort)0x060F, Word(4));
        Assert.AreEqual((ushort)0x0C22, Word(6));
        Assert.AreEqual((ushort)0x384E, Word(8));
        Assert.AreEqual(2, guest.Interrupts.Count);
        foreach (var visit in guest.Interrupts)
        {
            Assert.AreEqual(visit.BeforeInstruction.Ip + 2, (int)visit.AtHook.Ip);
            Assert.AreEqual(visit.BeforeInstruction.Sp, visit.AtHook.Sp);
            Assert.AreEqual(visit.BeforeInstruction.Flags, visit.AtHook.Flags);
            Assert.AreEqual(visit.AtHook.Pc, visit.AfterHandler.Pc);
            Assert.AreEqual(visit.AtHook.Sp, visit.AfterHandler.Sp);
        }
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(stored.AsSpan(offset));
    }

    [TestMethod]
    public void UnknownDosServiceFailsOutsideNativeCallback()
    {
        using var guest = new SegmentedGuest();
        guest.Map(8, 0x10000, [0xB4, 0x09, 0xCD, 0x21, 0x90], true);
        guest.Install(16);
        guest.DispatchInterrupt = _ => DosClock.Respond(9, 0x0900, new DateTime(1993, 6, 15));
        var error = Assert.Throws<NotSupportedException>(() => guest.RunUntil("unsupported", new(8, 0), new(8, 4)));
        StringAssert.Contains(error.Message, "AH=09");
    }

    [TestMethod]
    public void InstructionBudgetStopsGuestLoop()
    {
        using var guest = new SegmentedGuest(instructionLimit: 20);
        guest.Map(8, 0x10000, [0xEB, 0xFE, 0x90], true); // JMP to self
        guest.Install(16);
        Assert.Throws<InvalidOperationException>(() => guest.RunUntil("loop", new(8, 0), new(8, 2)));
    }

    [TestMethod]
    public void InterruptRetentionIsBoundedAndInstructionBudgetResetsIndependentlyOfLifetimeCount()
    {
        using var guest = new SegmentedGuest(instructionLimit: 10, diagnostics: new(2));
        // First entry: one serviced INT. Second entry: a loop that repeatedly
        // exits to the host, ensuring a trap does not reset the instruction budget.
        guest.Map(8, 0x10000, [0xCD, 0x21, 0x90, 0xCD, 0x21, 0xEB, 0xFC, 0x90], true);
        guest.Install(16);
        guest.DispatchInterrupt = _ => guest.Set(X86.UC_X86_REG_AX, guest.InterruptCount + 1);
        for (int i = 0; i < 100; i++) guest.RunUntil("repeated INT", new(8, 0), new(8, 2));
        Assert.AreEqual(100L, guest.Instructions);
        Assert.AreEqual(100L, guest.InterruptCount);
        var recent = guest.Interrupts;
        Assert.AreEqual(2, recent.Count);
        Assert.AreEqual((ushort)99, recent[0].AfterHandler.Ax);
        Assert.AreEqual((ushort)100, recent[1].AfterHandler.Ax);
        Assert.Throws<InvalidOperationException>(() => guest.RunUntil("INT loop", new(8, 3), new(8, 7)));
        Assert.AreEqual(2, guest.Interrupts.Count);
        Assert.AreEqual((ushort)100, recent[1].AfterHandler.Ax); // old snapshot remains detached
    }
}
