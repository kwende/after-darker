using System.Buffers.Binary;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Lessons;

namespace AfterDarker.Tests.Toolchain;

[TestClass]
[TestCategory("Toolchain")]
public sealed class LoadedLibraryTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "hello42.dll");
    private static byte[] Fixture() => File.ReadAllBytes(FixturePath);

    [TestMethod]
    public void CompiledStartupExportAndExitReturnThroughRealFarFrames()
    {
        var result = new Tutorial06LoadLibrary().Execute(Fixture());
        Assert.IsTrue(result.Initialized);
        Assert.IsTrue(result.ProtectedMode);
        Assert.AreEqual((ushort)1, result.StoredInitialization);
        Assert.AreEqual((ushort)42, result.Hello!.Ax);
        Assert.AreEqual((ushort)42, result.StoredHello);
        Assert.AreEqual((ushort)1, result.Exit!.Ax);
        Assert.AreEqual((ushort)1, result.StoredExit);
        Assert.AreEqual((ushort)0x1000, result.Hello.Sp);
        Assert.AreEqual((ushort)0x1000, result.Exit.Sp);
        Assert.AreEqual((ushort)0x30, result.Hello.Ds); // restored caller DS, distinct from DLL DS
        Assert.AreEqual((ushort)0x30, result.Exit.Ds);
        Assert.AreEqual(3, result.Entries.Count);
        CollectionAssert.AreEqual(new ushort[] { 0xFFC, 0xFFC, 0xFFA }, result.Entries.Select(e => e.Registers.Sp).ToArray());
        CollectionAssert.AreEqual(new ushort[] { 5, 0x105, 0x208 }, result.Entries.Select(e => e.ReturnIp).ToArray());
        Assert.IsTrue(result.Entries.All(e => e.ReturnCs == 0x18 && e.Address.Selector == 8));
        Assert.AreEqual((ushort)0x400, result.Entries[0].Registers.Cx);
        Assert.AreEqual((ushort)0x10, result.Entries[0].Registers.Di);
        Assert.AreEqual((ushort)0, result.Entries[0].Registers.Es);
        Assert.AreEqual((ushort)0, result.Entries[0].Registers.Si);
        Assert.AreEqual((ushort)0xCCCC, result.Entries[1].Registers.Ax); // host did not pre-seed 42
        Assert.AreEqual((ushort)0x30, result.Entries[1].Registers.Ds);
    }

    [TestMethod]
    public void ImportedCallsDecodeUnsignedWordsAndReturnWithCorrectCleanup()
    {
        var result = new Tutorial06LoadLibrary().Execute(Fixture(), version: 0x12340B03);
        Assert.AreEqual(2, result.Calls.Count);
        var heap = result.Calls[0];
        Assert.AreEqual("CheckedHeapTestDouble", heap.Binding.Handler);
        CollectionAssert.AreEqual(new ushort[] { 0x10, 0, 0x400 }, heap.Arguments.ToArray());
        Assert.AreEqual(heap.Before.Sp + 10, (int)heap.After.Sp); // IP + CS + three words
        Assert.AreEqual((ushort)1, heap.After.Ax);
        var version = result.Calls[1];
        Assert.AreEqual(0, version.Arguments.Count);
        Assert.AreEqual((ushort)0x0B03, version.After.Ax);
        Assert.AreEqual((ushort)0x1234, version.After.Dx);
        Assert.AreEqual(version.Before.Sp + 4, (int)version.After.Sp);
        foreach (var call in result.Calls)
        {
            Assert.AreEqual(call.Before.Ds, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es);
            Assert.AreEqual(call.Before.Ss, call.After.Ss);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
            Assert.AreEqual(call.ReturnAddress.Selector, call.After.Cs);
            Assert.AreEqual(call.ReturnAddress.Offset, call.After.Ip);
        }
        // Pinned libentry layout: __winmajor/__winminor/__winver at 18h/19h/1Ah.
        // These writes are performed by compiled startup, after the host returns.
        Assert.AreEqual((byte)3, result.DllDataAfterExecution[0x18]);
        Assert.AreEqual((byte)11, result.DllDataAfterExecution[0x19]);
        Assert.AreEqual((ushort)0x030B, BinaryPrimitives.ReadUInt16LittleEndian(result.DllDataAfterExecution.AsSpan(0x1A)));
    }

    [TestMethod]
    public void InitializationFailurePreventsExportAndUnloadCalls()
    {
        var result = new Tutorial06LoadLibrary().Execute(Fixture(), localInitSucceeds: false);
        Assert.IsFalse(result.Initialized);
        Assert.AreEqual((ushort)0, result.StoredInitialization);
        Assert.AreEqual((ushort)0x1000, result.Initialization.Sp);
        Assert.IsNull(result.Hello);
        Assert.IsNull(result.Exit);
        Assert.AreEqual((ushort)0xCCCC, result.StoredHello);
        Assert.AreEqual((ushort)0xCCCC, result.StoredExit);
        Assert.AreEqual(1, result.Calls.Count);
        Assert.AreEqual(1, result.Entries.Count);
    }

    [TestMethod]
    public void ChangedCompiledConstantChangesBothRegisterAndGuestStore()
    {
        byte[] file = Fixture();
        NeImage image = NeReader.Read(file);
        NeAddress start = image.FindExport("HELLOWORLD")!.Address!.Value;
        NeAddress end = image.FindExport("WEP")!.Address!.Value;
        int segmentFile = image.Segments.Single(s => s.Number == start.SegmentNumber).FileOffset!.Value;
        Span<byte> function = file.AsSpan(segmentFile + start.Offset, end.Offset - start.Offset);
        int instruction = function.IndexOf(new byte[] { 0xB8, 0x2A, 0 }); // MOV AX,42
        Assert.IsTrue(instruction >= 0, "Pinned fixture must contain MOV AX,42.");
        function[instruction + 1] = 77; // change the actual guest instruction, not a C# handler
        var result = new Tutorial06LoadLibrary().Execute(file);
        Assert.AreEqual((ushort)77, result.Hello!.Ax);
        Assert.AreEqual((ushort)77, result.StoredHello);
    }

    [TestMethod]
    public void InstructionBudgetIsFailureRatherThanFalseCompletion()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            new Tutorial06LoadLibrary().Execute(Fixture(), instructionLimit: 3));
        StringAssert.Contains(error.Message, "budget");
    }

    [TestMethod]
    public void InstructionTraceShowsFetchedBytesAndRegisterContext()
    {
        using var output = new StringWriter();
        var result = new Tutorial06LoadLibrary().Execute(Fixture(), output, traceInstructions: true);
        StringAssert.Contains(output.ToString(), "trace hello");
        StringAssert.Contains(output.ToString(), "bytes=B82A00");
        // At export+10 the pinned compiler's MOV DS,AX has executed. Inspect
        // THAT line, not an unrelated DS=0010 from DLL startup.
        ushort afterPrologue = (ushort)(result.Entries[1].Address.Offset + 10);
        string insideFunction = output.ToString().Split('\n').Single(line =>
            line.Contains($"trace hello   0008:{afterPrologue:X4}"));
        StringAssert.Contains(insideFunction, "DS=0010");
    }

    [TestMethod]
    public void LocalInitRejectsWrongGuestSelector()
    {
        byte[] file = Fixture();
        NeImage image = NeReader.Read(file);
        NeRelocation heapCall = image.Relocations.Single(r => r.Import == new NeImport("KERNEL", 4, null));
        int code = image.Segments.Single(s => s.Number == heapCall.SegmentNumber).FileOffset!.Value;
        int pushDataSelector = code + heapCall.SourceOffset - 4;
        Assert.AreEqual((byte)0x1E, file[pushDataSelector]); // PUSH DS before LocalInit arguments
        file[pushDataSelector] = 0x06; // PUSH ES instead; ES is NULL during startup
        var error = Assert.Throws<InvalidOperationException>(() => new Tutorial06LoadLibrary().Execute(file));
        StringAssert.Contains(error.Message, "LocalInit test double");
    }

    [TestMethod]
    public void ErrorPathImportFailsByNameInsteadOfShowingADialog()
    {
        byte[] file = Fixture();
        NeImage image = NeReader.Read(file);
        NeRelocation heapCall = image.Relocations.Single(r => r.Import == new NeImport("KERNEL", 4, null));
        // Point startup's first imported call to USER!1, already in this DLL's
        // module table. It must trap and fail, never execute the gateway guard.
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(heapCall.RecordFileOffset + 4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(heapCall.RecordFileOffset + 6), 1);
        var error = Assert.Throws<NotSupportedException>(() => new Tutorial06LoadLibrary().Execute(file));
        StringAssert.Contains(error.Message, "USER!MessageBox");
    }

    [TestMethod]
    public void ConsoleLessonRunsItsSuccessChecks()
    {
        new Tutorial06LoadLibrary(FixturePath).Run();
    }
}
