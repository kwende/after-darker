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
public sealed partial class Win16ImportGatewayTests
{
    [TestMethod]
    [DataRow(0x12345678u, 0x12345688u)]
    [DataRow(0xFFFFFFF8u, 8u)]
    public void CurrentTimeAndTickCountShareOneDwordClockAndPreserveTheFarReturn(uint initial, uint next)
    {
        using var setup = new Probe(initialTick: initial);
        var code = new List<byte>();
        setup.EmitCall(code, "GetCurrentTime");
        code.AddRange([0xA3, 0x20, 0, 0x89, 0x16, 0x22, 0]); // Store AX and DX, no host-written result.
        setup.EmitCall(code, "GetTickCount");
        code.AddRange([0xA3, 0x24, 0, 0x89, 0x16, 0x26, 0]);
        var final = setup.Run(code);
        Assert.AreEqual(initial, ((uint)setup.Word(0x22) << 16) | setup.Word(0x20));
        Assert.AreEqual(next, ((uint)setup.Word(0x26) << 16) | setup.Word(0x24));
        Assert.AreEqual((ushort)0x1000, final.Sp);
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(0, call.Arguments.Count);
            Assert.AreEqual(call.Before.Sp + 4, (int)call.After.Sp);
            Assert.AreEqual(Code, call.After.Cs);
            Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(Stack, call.After.Ss);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
            Assert.AreEqual(call.Before.Es, call.After.Es);
        }
    }

    private const ushort Code = MondrianSession.Caller, Gateway = MondrianSession.Gateway;
    private const ushort Data = MondrianSession.HostData, Stack = MondrianSession.Stack, DllData = 0x28;

    [TestMethod]
    [DataRow("LocalAlloc")]
    [DataRow("LocalLock")]
    [DataRow("LocalUnlock")]
    [DataRow("LocalFree")]
    public void LocalHeapImportsUseCallerDsAndRejectAnotherSegmentsHandleNamespace(string name)
    {
        using var setup = new Probe();
        setup.Services.LocalInit(DllData, 0, 1024);
        ushort handle = setup.Services.LocalAlloc(DllData, LocalMemoryFlags.Moveable, 8);
        var code = new List<byte>();
        setup.EmitCall(code, name, name == "LocalAlloc" ? new ushort[] { 0x42, 8 } : new ushort[] { handle });
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code)); // DS is host Data, not DLL Data.
        StringAssert.Contains(error.Message, "!" + name);
        StringAssert.Contains(error.Message, "caller DS");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(1, setup.Services.State.LocalHeap!.Snapshot().Allocations.Count);
        Assert.AreEqual(0, setup.Services.State.LocalHeap.Snapshot().OutstandingLocks);
    }

    [TestMethod]
    public void UnsignedOversizeAllocationReturnsNullToGuestWithoutWrappingOrLosingTheStack()
    {
        using var setup = new Probe();
        setup.Services.LocalInit(DllData, 0, 1024);
        var code = new List<byte> { 0x1E, 0xB8, (byte)DllData, 0, 0x8E, 0xD8, 0xBA, 0x78, 0x56 };
        // PUSH DS; MOV AX,DllData; MOV DS,AX; MOV DX,5678. LocalAlloc's size is UNSIGNED.
        setup.EmitCall(code, "LocalAlloc", 0, 0xFFFF);
        code.AddRange([0x1F, 0xA3, 0x20, 0]); // POP DS; store returned AX in caller data.
        var final = setup.Run(code);
        Assert.AreEqual((ushort)0, setup.Word(0x20));
        Assert.AreEqual((ushort)0, final.Cx);
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(Data, final.Ds);
        Assert.AreEqual(0, setup.Services.State.LocalHeap!.Snapshot().Allocations.Count);
    }

    [TestMethod]
    [DataRow("LocalAlloc", 0x100, 8)]
    [DataRow("LocalLock", 42, 0)]
    [DataRow("LocalUnlock", 42, 0)]
    [DataRow("LocalFree", 42, 0)]
    public void UnsupportedHeapInputsFailByImportBeforeReturningAUsablePointer(string name, int first, int second)
    {
        using var setup = new Probe();
        setup.Services.LocalInit(DllData, 0, 1024);
        var code = new List<byte> { 0x1E, 0xB8, (byte)DllData, 0, 0x8E, 0xD8 };
        setup.EmitCall(code, name, name == "LocalAlloc" ? new ushort[] { (ushort)first, (ushort)second } : new ushort[] { (ushort)first });
        code.AddRange([0x1F, 0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "!" + name);
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(0, setup.Services.State.LocalHeap!.Snapshot().Allocations.Count);
    }

    [TestMethod]
    [DataRow("StretchBlt")]
    public void UnsupportedDrawingImportsStopByNameBeforeReadingArguments(string name)
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        // No argument layout is guessed for an unimplemented service.
        setup.EmitCall(code, name);
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<NotSupportedException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GDI!" + name);
        StringAssert.Contains(error.Message, "service is not enabled");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(0L, setup.Surface.Revision);
    }

    [TestMethod]
    [DataRow(-8, 6, 1)]
    [DataRow(6, -8, 0)]
    [DataRow(2, 0, 0)]
    [DataRow(0, 9, 0)]
    public void PointByValueRoundTripsSignedCoordinatesBoolAndPascalCleanup(int x, int y, int expected)
    {
        using var setup = new Probe(); // Geometry works without a drawing surface.
        byte[] rectangle = new Rectangle16(-10, -5, 2, 9).Encode();
        setup.Guest.Write(new(Data, 0x350), rectangle);
        var code = new List<byte> { 0xBA, 0x78, 0x56 }; // MOV DX,5678: BOOL must preserve DX.
        // The RECT is a far pointer. POINT is one value, pushed Y then X.
        setup.EmitCall(code, "PtInRect", Data, 0x350, unchecked((ushort)y), unchecked((ushort)x));
        code.AddRange([0xA3, 0x20, 0]); // Guest stores the returned AX itself.
        var final = setup.Run(code);
        Assert.AreEqual((ushort)expected, setup.Word(0x20));
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        var call = setup.Calls.Single();
        Assert.AreEqual(call.Before.Sp + 12, (int)call.After.Sp); // Four return bytes + eight argument bytes.
        Assert.AreEqual(Code, call.After.Cs);
        Assert.AreEqual(Data, call.After.Ds);
        Assert.AreEqual(Stack, call.After.Ss);
        Assert.AreEqual(call.Before.Bp, call.After.Bp);
        Assert.AreEqual(call.Before.Es, call.After.Es);
        CollectionAssert.AreEqual(rectangle, setup.Guest.Read(new(Data, 0x350), Rectangle16.ByteCount));
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(Data, 0xFFC)]
    public void PointInRectRejectsInvalidRectangleBeforeReturningToGuest(int selector, int offset)
    {
        using var setup = new Probe();
        var code = new List<byte>();
        setup.EmitCall(code, "PtInRect", (ushort)selector, (ushort)offset, 0, 0);
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "PtInRect");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
    }

    [TestMethod]
    public void GuestCanSelectTheStockBlackPenReturnedThroughAx()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte> { 0xBA, 0x78, 0x56 };
        setup.EmitCall(code, "GetStockObject", (ushort)Win16Drawing.BlackPenIndex);
        code.AddRange([0xA3, 0x20, 0, 0x68, 0x03, 0x01, 0x50]); // Store AX; PUSH HDC; PUSH returned AX.
        setup.EmitCall(code, "SelectObject");
        var final = setup.Run(code);
        Assert.AreEqual(Win16Drawing.BlackPenHandle, setup.Word(0x20));
        Assert.AreEqual(Win16Drawing.BlackPenHandle, final.Ax);
        Assert.AreEqual((ushort)0x5678, final.Dx);
        Assert.AreEqual((ushort)0x1000, final.Sp);
        Assert.AreEqual(0, setup.Services.State.Drawing!.LivePenCount);
    }

    [TestMethod]
    public void PenAndLineImportsRoundTripColorsSignedCoordinatesHandlesAndReturnRegisters()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        setup.EmitCall(code, "CreatePen", 0, 1, 0x0200, 0x3322); // PALETTERGB: red=22 green=33 blue=00
        code.AddRange([0x89, 0xC3]); // MOV BX,AX; use the guest-returned pen handle
        code.AddRange([0x68, 0x03, 0x01, 0x53]); // PUSH HDC; PUSH BX
        setup.EmitCall(code, "SelectObject");
        setup.EmitCall(code, "MoveTo", 0x103, 0xFFFE, 0xFFFF);
        setup.EmitCall(code, "MoveTo", 0x103, 1, 1);
        code.AddRange([0xA3, 0x20, 0, 0x89, 0x16, 0x22, 0]); // guest stores signed previous point
        setup.EmitCall(code, "LineTo", 0x103, 5, 1);
        code.Add(0x53); setup.EmitCall(code, "DeleteObject"); // selected object: FALSE
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.BlackPenHandle);
        code.Add(0x53); setup.EmitCall(code, "DeleteObject"); // deselected object: TRUE
        var final = setup.Run(code);
        Assert.AreEqual((ushort)0xFFFE, setup.Word(0x20));
        Assert.AreEqual((ushort)0xFFFF, setup.Word(0x22));
        Assert.AreEqual(Win16Drawing.BlackPenHandle, setup.Calls[1].After.Ax);
        Assert.AreEqual((ushort)1, setup.Calls[4].After.Ax);
        Assert.AreEqual((ushort)0, setup.Calls[5].After.Ax);
        Assert.AreEqual((ushort)1, final.Ax);
        Assert.AreEqual(0, setup.Services.State.Drawing!.LivePenCount);
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 0x0200, 0x3322 }, setup.Calls[0].Arguments.ToArray());
        byte[] pixels = setup.Surface.CopyRgb();
        for (int x = 0; x < 8; x++)
        {
            int offset = (8 + x) * 3;
            Assert.AreEqual(x is >= 1 and < 5 ? (byte)0x22 : (byte)0, pixels[offset]);
            Assert.AreEqual(x is >= 1 and < 5 ? (byte)0x33 : (byte)0, pixels[offset + 1]);
            Assert.AreEqual((byte)0, pixels[offset + 2]);
        }
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
            Assert.AreEqual(Code, call.After.Cs);
            Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es);
            Assert.AreEqual(call.Before.Bp, call.After.Bp);
        }
        Assert.AreEqual((ushort)0x1000, final.Sp);
    }

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
        StringAssert.Contains(error.Message, "service is not enabled");
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
    [DataRow("GetStockObject", 1, 0, 0, 0)]
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

    [TestMethod]
    public void GuestEllipseUsesPascalCoordinatesSelectedObjectsAndBalancedFarReturn()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte> { 0xBA, 0x78, 0x56 }; // Sentinel DX survives word results.
        setup.EmitCall(code, "CreatePen", 0, 2, 0x0200, 0x3322);
        code.AddRange([0x89, 0xC3, 0x68, 0x03, 0x01, 0x53]); // BX=returned pen; PUSH HDC; PUSH BX.
        setup.EmitCall(code, "SelectObject");
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.BlackBrushHandle);
        code.AddRange([0xA3, 0x20, 0]); // Save the previous brush in guest memory.
        setup.EmitCall(code, "MoveTo", 0x103, 0xFFFC, 0xFFFD);
        code.AddRange([0xBA, 0x78, 0x56]); // MoveTo returned DX:AX; install a fresh nonzero word-result sentinel.
        setup.EmitCall(code, "Ellipse", 0x103, 0xFFFE, 0xFFFF, 6, 5);
        code.AddRange([0xA3, 0x22, 0]);
        setup.EmitCall(code, "MoveTo", 0x103, 0, 0);
        code.AddRange([0xA3, 0x24, 0, 0x89, 0x16, 0x26, 0]); // Previous point: DX:AX.
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.WhiteBrushHandle);
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.BlackPenHandle);
        code.Add(0x53); setup.EmitCall(code, "DeleteObject");
        var final = setup.Run(code);
        Assert.AreEqual(Win16Drawing.WhiteBrushHandle, setup.Word(0x20));
        Assert.AreEqual((ushort)1, setup.Word(0x22));
        Assert.AreEqual((ushort)0xFFFC, setup.Word(0x24));
        Assert.AreEqual((ushort)0xFFFD, setup.Word(0x26));
        var ellipse = setup.Calls.Single(call => call.Binding.Implementation == Win16Imports.Handler.Ellipse);
        CollectionAssert.AreEqual(new ushort[] { 0x103, 0xFFFE, 0xFFFF, 6, 5 }, ellipse.Arguments.ToArray());
        Assert.AreEqual(ellipse.Before.Dx, ellipse.After.Dx);
        Assert.AreEqual((ushort)0x5678, ellipse.After.Dx);
        Assert.AreEqual((ushort)1, ellipse.After.Ax);
        Assert.AreEqual(14, ellipse.After.Sp - ellipse.Before.Sp); // Ten argument bytes plus far return.
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual(Code, call.After.Cs); Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es); Assert.AreEqual(call.Before.Bp, call.After.Bp);
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
        }
        byte[] pixels = setup.Surface.CopyRgb();
        Assert.IsTrue(pixels.Contains((byte)0x22) && pixels.Contains((byte)0x33));
        Assert.IsTrue(pixels.AsSpan((2 * 8 + 2) * 3, 3).SequenceEqual(new byte[3])); // Black selected brush.
        Assert.AreEqual(0, setup.Services.State.Drawing!.LivePenCount);
        Assert.AreEqual((ushort)0x1000, final.Sp);
    }

    [TestMethod]
    public void EllipseRejectsUnknownHdcBeforeReturningSuccessOrChangingPixels()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        setup.EmitCall(code, "Ellipse", 0xFFFF, 0, 0, 8, 6);
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GDI!Ellipse");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(0L, setup.Surface.Revision);
    }

    [TestMethod]
    public void EllipseRemainsGuardedWhenDrawingIsDisabled()
    {
        using var setup = new Probe(drawing: false);
        var code = new List<byte>();
        setup.EmitCall(code, "Ellipse"); // Disabled handler must fail before reading a guessed stack frame.
        var error = Assert.Throws<NotSupportedException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GDI!Ellipse");
        Assert.AreEqual(0L, setup.Surface.Revision);
    }

    [TestMethod]
    [DataRow("Ellipse")]
    [DataRow("Rectangle")]
    public void GuestCreatesPaletteRelativeBrushDrawsWithNullPenAndDeletesItsRestoredBrush(string shape)
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte> { 0xBA, 0x78, 0x56 }; // Nonzero DX must survive word results.
        setup.EmitCall(code, "CreateSolidBrush", 0x0233, 0x2211); // DWORD in Pascal high-word/low-word order.
        code.AddRange([0x89, 0xC3, 0x68, 0x03, 0x01, 0x53]); // Save brush in BX; PUSH HDC; PUSH BX.
        setup.EmitCall(code, "SelectObject");
        code.AddRange([0xA3, 0x20, 0]);
        setup.EmitCall(code, "GetStockObject", (ushort)Win16Drawing.NullPenIndex);
        code.AddRange([0x68, 0x03, 0x01, 0x50]); // PUSH HDC; PUSH returned NULL_PEN.
        setup.EmitCall(code, "SelectObject");
        setup.EmitCall(code, shape, 0x103, 0xFFFE, 0xFFFF, 6, 5);
        code.AddRange([0xA3, 0x22, 0]);
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.BlackPenHandle);
        setup.EmitCall(code, "SelectObject", 0x103, Win16Drawing.WhiteBrushHandle);
        code.Add(0x50); setup.EmitCall(code, "DeleteObject"); // AX is the brush just deselected.
        code.AddRange([0xA3, 0x24, 0]);
        var final = setup.Run(code);
        Assert.AreEqual(Win16Drawing.WhiteBrushHandle, setup.Word(0x20));
        Assert.AreEqual((ushort)1, setup.Word(0x22));
        Assert.AreEqual((ushort)1, setup.Word(0x24));
        var brushCall = setup.Calls.Single(call => call.Binding.Implementation == Win16Imports.Handler.CreateSolidBrush);
        CollectionAssert.AreEqual(new ushort[] { 0x0233, 0x2211 }, brushCall.Arguments.ToArray());
        Assert.AreEqual(8, brushCall.After.Sp - brushCall.Before.Sp);
        var shapeCall = setup.Calls.Single(call => call.Binding.Name.Contains("!" + shape + " "));
        CollectionAssert.AreEqual(new ushort[] { 0x103, 0xFFFE, 0xFFFF, 6, 5 }, shapeCall.Arguments.ToArray());
        Assert.AreEqual(14, shapeCall.After.Sp - shapeCall.Before.Sp);
        foreach (var call in setup.Calls)
        {
            Assert.AreEqual((ushort)0x5678, call.After.Dx);
            Assert.AreEqual(Code, call.After.Cs); Assert.AreEqual(Data, call.After.Ds);
            Assert.AreEqual(call.Before.Es, call.After.Es); Assert.AreEqual(call.Before.Bp, call.After.Bp);
            Assert.AreEqual(call.Before.Sp + 4 + call.Binding.ArgumentBytes, (int?)call.After.Sp);
        }
        Assert.IsTrue(setup.Surface.CopyRgb().AsSpan((2 * 8 + 2) * 3, 3).SequenceEqual(new byte[] { 0x11, 0x22, 0x33 }));
        Assert.AreEqual(0, setup.Services.State.Drawing!.LiveBrushCount);
        Assert.AreEqual(1, setup.Services.State.Drawing.PeakBrushCount);
        Assert.AreEqual((ushort)0x1000, final.Sp);
    }

    [TestMethod]
    [DataRow("Rectangle")]
    [DataRow("CreateSolidBrush")]
    public void ShapesImportsFailSymbolicallyWithoutReadingArgumentsWhenDrawingIsDisabled(string name)
    {
        using var setup = new Probe(drawing: false);
        var code = new List<byte>();
        setup.EmitCall(code, name);
        var error = Assert.Throws<NotSupportedException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GDI!" + name);
    }

    [TestMethod]
    public void IndexedBrushColorStopsBeforeAllocatingAnObjectOrReturningToTheGuest()
    {
        using var setup = new Probe(drawing: true);
        var code = new List<byte>();
        setup.EmitCall(code, "CreateSolidBrush", 0x0100, 17);
        code.AddRange([0xA3, 0x20, 0]);
        var error = Assert.Throws<InvalidOperationException>(() => setup.Run(code));
        StringAssert.Contains(error.Message, "GDI!CreateSolidBrush");
        Assert.AreEqual((ushort)0xCCCC, setup.Word(0x20));
        Assert.AreEqual(0, setup.Services.State.Drawing!.LiveBrushCount);
    }

    // Entirely original tiny guest programs: no private file or Watcom required.
    private sealed class Probe : IDisposable
    {
        public SegmentedGuest Guest { get; } = new();
        public Win16Api Services { get; }
        public IReadOnlyList<Win16Imports.ImportEntry> Bindings { get; }
        public List<Win16CallTrace> Calls { get; } = [];
        public PixelSurface Surface { get; } = new(8, 6);
        public Probe(bool drawing = false, uint initialTick = Win16ApiState.DefaultInitialTick, Win16ModuleResources? resources = null)
        {
            var imports = new[] { new NeImport("KERNEL", 4, null), new("KERNEL", 18, null), new("KERNEL", 19, null),
                new("USER", 75, null), new("USER", 175, null), new("GDI", 36, null), new("GDI", 44, null),
                new("GDI", 55, null), new("GDI", 65, null), new("GDI", 1, null), new("GDI", 2, null), new("GDI", 9, null),
                new("KERNEL", 131, null), new("USER", 13, null), new("USER", 82, null), new("USER", 72, null), new("USER", 81, null), new("GDI", 87, null),
                new("GDI", 61, null), new("GDI", 45, null), new("GDI", 69, null), new("GDI", 20, null), new("GDI", 19, null), new("USER", 76, null),
                new("GDI", 24, null), new("GDI", 27, null), new("GDI", 29, null), new("GDI", 66, null),
                new("GDI", 51, null), new("GDI", 52, null), new("GDI", 68, null), new("GDI", 35, null),
                new("AD_SND", null, "ADWOPENSOUND"), new("AD_SND", null, "ADWCLOSESOUND"),
                new("AD_SND", null, "ADWSOUNDASYNCCAP"), new("AD_SND", null, "ADWLOADSOUNDRESOURCE"),
                new("AD_SND", null, "ADWSETSOUNDMODE"), new("AD_SND", null, "ADWPLAYSOUND"), new("AD_SND", null, "ADWFREESOUND"),
                new("GDI", 97, null), new("GDI", 11, null), new("GDI", 4, null), new("GDI", 31, null), new("GDI", 34, null),
                new("USER", 77, null), new("USER", 78, null), new("USER", 79, null), new("USER", 244, null), new("USER", 83, null),
                new("KERNEL", 5, null), new("KERNEL", 7, null), new("KERNEL", 8, null), new("KERNEL", 9, null), new("USER", 15, null) };
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
            Services = new(new Win16ApiState(Guest, new(new(DllData, 64), 1024), new(Data, 0x300), initialTick: initialTick)
            { Drawing = drawing ? contexts : null, ModuleResources = resources });
            Services.State.Blocks.Register(0x102, new(Data, 0x200), records.Module.Length);
        }
        public void EmitCall(List<byte> code, string name, params ushort[] args)
        {
            foreach (ushort arg in args) code.AddRange([0x68, (byte)arg, (byte)(arg >> 8)]);
            var entry = Bindings.Single(b => b.Name.Split('!')[1].Split(' ')[0] == name);
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
            var stack = new Win16Stack(Guest, MondrianSession.Stack, MondrianSession.InitialSp);
            var gateway = new Win16ImportGateway(Guest, Bindings, Services, stack);
            Guest.DispatchGateway = () => Calls.Add(gateway.Dispatch());
            return Guest.RunUntil("synthetic import probe", new(Code, 0), new(Code, end));
        }
        public ushort Word(ushort offset) => BinaryPrimitives.ReadUInt16LittleEndian(Guest.Read(new(Data, offset), 2));
        public void Dispose() => Guest.Dispose();
    }
}
