using AfterDarker.Core.Win16;
using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianInitializationTests
{
    private static byte[] ReadModule()
    {
        string path = Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")
            ?? throw new InvalidOperationException("Set AFTER_DARKER_MONDRIAN to your local analyzed module before enabling TestLocalMondrian.");
        return File.ReadAllBytes(path);
    }

    [TestMethod]
    [DataRow(0, 140, false)]
    [DataRow(25, 70, true)]
    [DataRow(50, 30, true)]
    [DataRow(75, 0, false)]
    [DataRow(100, 0, true)]
    public void OriginalCodeConsumesOptionsAndReturnsWithBalancedState(int speed, int threshold, bool clear)
    {
        byte[] file = ReadModule();
        byte[] originalHash = SHA256.HashData(file);
        var result = Tutorial08MondrianInitialize.Execute(file, options: new(320, 200, (ushort)speed, clear));
        CollectionAssert.AreEqual(originalHash, SHA256.HashData(file));
        Assert.AreEqual(3, result.Phases.Count);
        Assert.AreEqual((ushort)1, result.Phases[0].StoredAx);
        Assert.AreEqual((ushort)1, result.Phases[1].State.Compatibility);
        var state = result.Phases[2].State;
        Assert.AreEqual((ushort)threshold, state.Threshold);
        Assert.AreEqual(clear ? (ushort)1 : (ushort)0, state.Clear);
        Assert.AreEqual((ushort)0, state.Counter);
        Assert.AreEqual((ushort)0, state.Rectangles);
        Assert.AreEqual((uint)0x12345678, state.Tick);
        Assert.AreEqual((uint)0x2C1E2460, state.Time); // observed fixed civil time + guest timezone conversion
        Assert.AreEqual((uint)0x2460, state.Seed);
        Assert.AreNotEqual(result.BeforeExecution.Seed, state.Seed);
        Assert.AreEqual(new FarPointer16(0x48, 0x100), state.System);
        Assert.AreEqual(new FarPointer16(0x48, 0x200), state.Module);
        Assert.AreEqual((ushort)0x28, state.InstanceHandle);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.IsTrue(result.ProtectedMode);
        Assert.IsTrue(result.Phases.All(p => p.Registers.Sp == 0x1000 && p.Registers.Ss == 0x40 && p.Registers.Bp == 0));
        Assert.AreEqual(11, result.Calls.Count);
        Assert.IsTrue(result.Calls.All(c => c.Binding.Implementation != Win16Imports.Handler.Unsupported));
        CollectionAssert.AreEqual(new[] { 0x2A, 0x2C, 0x2A }, result.Interrupts.Select(i => i.BeforeInstruction.Ax >> 8).ToArray());
        Assert.AreEqual(1024, result.Heap.Length);
    }

    [TestMethod]
    public void ConsolePromptAndInstructionTraceCompleteTheSameLesson()
    {
        string path = Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")!;
        using var input = new StringReader(path + Environment.NewLine);
        using var output = new StringWriter();
        new Tutorial08MondrianInitialize(trace: true, input: input, output: output).Run();
        StringAssert.Contains(output.ToString(), "Path to your local Mondrian.ad");
        StringAssert.Contains(output.ToString(), "INT 21h AH=2A");
        StringAssert.Contains(output.ToString(), "PASS: original Mondrian");
    }

    [TestMethod]
    public void OriginalModuleInstructionBudgetFailsWithoutReportingSuccess() =>
        Assert.Throws<InvalidOperationException>(() => Tutorial08MondrianInitialize.Execute(ReadModule(), instructionLimit: 10));
}
