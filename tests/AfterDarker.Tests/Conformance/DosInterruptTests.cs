using System.Buffers.Binary;
using AfterDarker.Core.Win16;
using AfterDarker.Tutorials.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class DosInterruptTests
{
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
}
