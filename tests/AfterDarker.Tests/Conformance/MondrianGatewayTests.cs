using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Fixtures;
using AfterDarker.Tutorials.Lessons;
using AfterDarker.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tests.Conformance;

[TestClass]
[TestCategory("Conformance")]
public sealed class MondrianGatewayTests
{
    private const ushort Code = MondrianSession.Caller, Gateway = MondrianSession.Gateway;
    private const ushort Data = MondrianSession.HostData, Stack = MondrianSession.Stack, DllData = 0x28;

    [TestMethod]
    public void EverySupportedImportReturnsThroughRealFarCallsAndGuestDereferencesLockedBlock()
    {
        using var setup = new Probe();
        var code = new List<byte>();
        Call("LocalInit", DllData, 0, 1024); StoreAx(0x20);
        Call("GlobalLock", 0x102); StoreAx(0x22); StoreDx(0x24);
        code.AddRange([0x89, 0x0E, 0x26, 0]); // MOV [0026],CX; GlobalLock's extra selector result
        // Use the returned pointer, not a host read: MOV ES,DX; MOV BX,AX;
        // MOV AX,ES:[BX+6] reads the module's speed through guest segmentation.
        code.AddRange([0x8E, 0xC2, 0x89, 0xC3, 0x26, 0x8B, 0x47, 0x06]); StoreAx(0x28);
        Call("GlobalUnlock", 0x102); StoreAx(0x2A);
        Call("GetDOSEnvironment"); StoreAx(0x2C); StoreDx(0x2E);
        code.AddRange([0x8E, 0xC2, 0x89, 0xC3, 0x26, 0x8B, 0x07]); StoreAx(0x38); // read double NUL
        Call("GetTickCount"); StoreAx(0x30); StoreDx(0x32);
        Call("GetTickCount"); StoreAx(0x34); StoreDx(0x36);
        var final = setup.Run(code);
        Assert.AreEqual((ushort)1, setup.Word(0x20));
        Assert.AreEqual((ushort)0x200, setup.Word(0x22));
        Assert.AreEqual(Data, setup.Word(0x24));
        Assert.AreEqual(Data, setup.Word(0x26));
        Assert.AreEqual((ushort)50, setup.Word(0x28)); // guest dereferenced the returned pointer
        Assert.AreEqual((ushort)0, setup.Word(0x2A)); // final unlock is FALSE, not failure
        Assert.AreEqual((ushort)0x300, setup.Word(0x2C));
        Assert.AreEqual(Data, setup.Word(0x2E));
        Assert.AreEqual((ushort)0, setup.Word(0x38));
        Assert.AreEqual((ushort)0x5678, setup.Word(0x30));
        Assert.AreEqual((ushort)0x1234, setup.Word(0x32));
        Assert.AreEqual((ushort)0x5688, setup.Word(0x34));
        Assert.AreEqual((ushort)0x1234, setup.Word(0x36));
        Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(0, setup.Services.State.Blocks.OutstandingLocks);
        Assert.AreEqual(new FarPointer16(DllData, 64), setup.Services.State.InitializedHeap!.Start);
        Assert.AreEqual(1024, setup.Services.State.InitializedHeap.Length);
        Assert.AreEqual(6, setup.Calls.Count);
        CollectionAssert.AreEqual(new ushort[] { DllData, 0, 1024 }, setup.Calls[0].Arguments.ToArray());
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
            Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
            Assert.AreEqual(Stack, call.After.Ss);
            Assert.AreEqual(Code, call.After.Cs);
        }
        void Call(string name, params ushort[] args) => setup.EmitCall(code, name, args);
        void StoreAx(byte offset) => code.AddRange([0xA3, offset, 0]);
        void StoreDx(byte offset) => code.AddRange([0x89, 0x16, offset, 0]);
    }

    [TestMethod]
    [DataRow(0x28, 1, 1024)]
    [DataRow(0x28, 0, 512)]
    [DataRow(0x48, 0, 1024)]
    public void LocalInitRejectsRequestsOutsideTheReservedGuestHeap(int selector, int start, int length)
    {
        using var setup = new Probe();
        var code = new List<byte>();
        setup.EmitCall(code, "LocalInit", (ushort)selector, (ushort)start, (ushort)length);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "LocalInit");
        Assert.IsNull(setup.Services.State.InitializedHeap);
    }

    [TestMethod]
    public void UnknownHandleStopsBeforeReturningNullToGuest()
    {
        using var setup = new Probe();
        var code = new List<byte>();
        setup.EmitCall(code, "GlobalLock", 0xDEAD);
        code.AddRange([0xA3, 0x20, 0]); // must never execute
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GlobalLock");
        StringAssert.Contains(error.Message, "DEAD");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
    }

    [TestMethod]
    public void DrawingCallFailsByNameBeforeAttemptingToDecodeAnUnsupportedAbi()
    {
        using var setup = new Probe();
        var code = new List<byte>();
        setup.EmitCall(code, "InvertRect");
        var error = Assert.Throws<NotSupportedException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "USER!InvertRect");
        StringAssert.Contains(error.Message, "initialization-only");
    }

    [TestMethod]
    public void HostTranslationRejectsNullOutOfRangeAndCodeWrites()
    {
        using var guest = new SegmentedGuest();
        guest.Map(8, 0x10000, [0x90], true);
        guest.Map(16, 0x20000, new byte[16], false);
        Assert.Throws<InvalidOperationException>(() => guest.Read(new(0, 0), 1));
        Assert.Throws<InvalidOperationException>(() => guest.Read(new(16, 15), 2));
        Assert.Throws<InvalidOperationException>(() => guest.Read(new(16, 0xFFFF), 2));
        Assert.Throws<InvalidOperationException>(() => guest.Write(new(8, 0), [0]));
    }

    [TestMethod]
    public void DrawingImportsDecodeSignedCoordinatesFarPointersAndVoidReturnsThroughRealGuestCalls()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte> { 0xB8, 0x34, 0x12, 0xBA, 0x78, 0x56 }; // AX=1234 DX=5678
        setup.EmitCall(code, "SetRect", Data, 0x350, unchecked((ushort)-2), unchecked((ushort)-1), 4, 3);
        setup.EmitCall(code, "GetStockObject", (ushort)Win16Drawing.BlackBrushIndex);
        // Use the returned guest brush handle as FillRect's last parameter.
        foreach (ushort word in new ushort[] { 0x103, Data, 0x350 }) code.AddRange([0x68, (byte)word, (byte)(word >> 8)]);
        code.Add(0x50); // PUSH AX
        setup.EmitCall(code, "FillRect");
        setup.EmitCall(code, "InvertRect", 0x103, Data, 0x350);
        var final = setup.Run(code);
        Assert.AreEqual(new Rectangle16(-2, -1, 4, 3), Rectangle16.Decode(setup.Guest.Read(new(Data, 0x350), 8)));
        Assert.AreEqual((ushort)0x1234, setup.Calls[0].After.Ax); // void SetRect
        Assert.AreEqual((ushort)0x5678, setup.Calls[0].After.Dx);
        Assert.AreEqual(Win16Drawing.BlackBrushHandle, setup.Calls[1].After.Ax);
        Assert.AreEqual((ushort)1, setup.Calls[2].After.Ax); // successful FillRect
        Assert.AreEqual((ushort)1, setup.Calls[3].After.Ax); // void InvertRect preserved AX
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(12, setup.Surface.CopyRgb().Count(b => b != 0) / 3);
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
            Assert.AreEqual(Code, call.After.Cs);
            Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
        }
    }

    [TestMethod]
    [DataRow("GetStockObject", 0, 0, 0, 0)]
    [DataRow("GetStockObject", 65535, 0, 0, 0)]
    [DataRow("FillRect", 0x103, Data, 0x350, 0xFFFF)]
    [DataRow("FillRect", 0xFFFF, Data, 0x350, Win16Drawing.BlackBrushHandle)]
    [DataRow("InvertRect", 0xFFFF, Data, 0x350, 0)]
    [DataRow("InvertRect", 0x103, 0, 0, 0)]
    [DataRow("InvertRect", 0x103, Data, 0xFFF, 0)]
    public void UnsupportedDrawingInputsFailSymbolicallyBeforeReturningToGuest(string name, int a, int b, int c, int d)
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        ushort[] words = name == "GetStockObject" ? [(ushort)a] : name == "InvertRect"
            ? [(ushort)a, (ushort)b, (ushort)c] : [(ushort)a, (ushort)b, (ushort)c, (ushort)d];
        setup.EmitCall(code, name, words);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "!" + name);
        Assert.AreEqual(0L, setup.Surface.Revision);
    }

    // Entirely original tiny guest programs: no private file or Watcom required.
    private sealed class Probe : IDisposable
    {
        public SegmentedGuest Guest { get; } = new();
        public Win16Api Services { get; }
        public IReadOnlyList<Win16Imports.ImportEntry> Bindings { get; }
        public List<MondrianSession.HostCall> Calls { get; } = [];
        public PixelSurface Surface { get; } = new(8, 6);
        public Probe(bool drawing = false)
        {
            var imports = new[] { new NeImport("KERNEL", 4, null), new("KERNEL", 18, null), new("KERNEL", 19, null),
                new("KERNEL", 131, null), new("USER", 13, null), new("USER", 82, null), new("USER", 72, null), new("USER", 81, null), new("GDI", 87, null) };
            var image = NeReader.Read(RelocationDemo.Create()) with
            {
                Relocations = imports.Select(i => new NeRelocation(1, 0, 3, 1, 0, 0, 0, i)).ToArray()
            };
            Bindings = Win16Imports.BindImports(image, Gateway, drawing);
            byte[] data = new byte[4096];
            var records = MondrianInitialization.CreateRecords(new());
            records.Module.CopyTo(data, 0x200);
            for (int i = 0x20; i < 0x40; i++) data[i] = 0xCC;
            Guest.Map(Data, 0x90000, data, false);
            Guest.Map(Stack, 0x80000, new byte[4096], false);
            var prepared = new PreparedNeSegment(new(5, 0, 64, 64, 1), new(5, DllData, 0x50000), new byte[1088]);
            Guest.Map(DllData, 0x50000, prepared.Bytes, false);
            Guest.Map(Gateway, 0x70000, new byte[4096], true);
            var contexts = new Win16Drawing();
            contexts.Register(0x103, Surface);
            Services = new(new Win16ApiState(Guest, new(new(DllData, 64), 1024), new(Data, 0x300))
                { Drawing = drawing ? contexts : null });
            Services.State.Blocks.Register(0x102, new(Data, 0x200), records.Module.Length);
        }
        public void EmitCall(List<byte> code, string name, params ushort[] args)
        {
            foreach (ushort arg in args) code.AddRange([0x68, (byte)arg, (byte)(arg >> 8)]);
            var entry = Bindings.Single(b => b.Name.Contains("!" + name + " ", StringComparison.Ordinal));
            code.AddRange([0x9A, (byte)entry.Address.Offset, (byte)(entry.Address.Offset >> 8), (byte)Gateway, 0]);
        }
        public SegmentedGuest.CpuState Run(List<byte> code)
        {
            ushort end = (ushort)code.Count;
            code.Add(0x90);
            Guest.Map(Code, 0x60000, code.ToArray(), true);
            Guest.Install(Gateway);
            Guest.Set(X86.UC_X86_REG_DS, Data);
            Guest.Set(X86.UC_X86_REG_SS, Stack);
            Guest.Set(X86.UC_X86_REG_SP, 0x1000);
            Guest.DispatchGateway = () => Calls.Add(MondrianSession.DispatchImport(Guest, Bindings, Services));
            return Guest.RunUntil("synthetic import probe", new(Code, 0), new(Code, end));
        }
        public ushort Word(ushort offset) => BinaryPrimitives.ReadUInt16LittleEndian(Guest.Read(new(Data, offset), 2));
        public void Dispose() => Guest.Dispose();
    }
}
