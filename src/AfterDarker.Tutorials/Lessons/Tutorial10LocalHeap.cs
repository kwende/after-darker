using System.Buffers.Binary;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>A source-authored x86 caller makes real Win16 heap imports and writes through the returned guest offset.</summary>
/// <remarks>No AD file, Watcom binary, or native host pointer is needed. See docs/win16-local-heap.md.</remarks>
public sealed class Tutorial10LocalHeap : ITutorial
{
    public string Id => "10";
    public string Title => "A local heap: backing memory, handles, guest writes, and reuse";

    /// <summary>Actual observations from emulated instructions and the shared allocator.</summary>
    /// <param name="Handle">Movable identity returned through AX.</param>
    /// <param name="Address">Address derived from LocalLock, distinct from the handle.</param>
    /// <param name="GuestStoredValue">Word written and then read by the original guest instructions.</param>
    /// <param name="HostObservedValue">Independent read of the allocation before it is freed.</param>
    /// <param name="ReusedValue">Old bytes observed by the guest after a fixed allocation reused the range.</param>
    /// <param name="ZeroInitializedValue">Guest read after LocalAlloc with LMEM_ZEROINIT reused it again.</param>
    /// <param name="WhileLocked">Live ownership snapshot while the guest still holds its movable lock.</param>
    /// <param name="AfterFree">Final heap snapshot; no allocations remain.</param>
    /// <param name="Calls">Real far-call ABI observations, including registers and Pascal stack cleanup.</param>
    /// <param name="FinalRegisters">Guest state after all allocations were released.</param>
    public sealed record Result(ushort Handle, FarPointer16 Address, ushort GuestStoredValue,
        ushort HostObservedValue, ushort ReusedValue, ushort ZeroInitializedValue,
        LocalHeapSnapshot WhileLocked, LocalHeapSnapshot AfterFree,
        IReadOnlyList<Win16CallTrace> Calls, SegmentedGuest.CpuState FinalRegisters);

    public void Run()
    {
        Result result = Execute(Console.Out);
        if (result.Handle == result.Address.Offset || result.GuestStoredValue != 0xBEEF ||
            result.HostObservedValue != 0xBEEF || result.ReusedValue != 0xBEEF ||
            result.ZeroInitializedValue != 0 || result.AfterFree.Allocations.Count != 0 || result.FinalRegisters.Sp != 0x1000)
            throw new InvalidOperationException("Local heap observations did not match the lesson.");
        Console.WriteLine("PASS: x86 allocated, locked, wrote, unlocked, freed, reused and zeroed guest memory.");
    }

    /// <summary>Run the same bounded mechanism used by the player, with visible source-authored guest instructions.</summary>
    public Result Execute(TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        const ushort codeSelector = 0x08, gatewaySelector = 0x10, dataSelector = 0x18, stackSelector = 0x20;
        const ushort heapStart = 0x400, initialHeapBytes = 16, heapCapacityBytes = 128;
        const ushort requestedBytes = 32;
        var imports = new ushort[] { 4, 5, 7, 8, 9 }.Select(ordinal => new NeImport("KERNEL", ordinal, null));
        var bindings = Win16Imports.BindImports(imports, gatewaySelector);
        var instructions = new List<byte>();

        // Phase 1: the handle (SI) and the pointer offset (BX) are deliberately separate.
        CallWin16("LocalInit", dataSelector, 0, initialHeapBytes);
        CallWin16("LocalAlloc", (ushort)(LocalMemoryFlags.Moveable | LocalMemoryFlags.ZeroInit), requestedBytes);
        instructions.AddRange([0xA3, 0x20, 0x00]);             // MOV [0020],AX: keep the returned handle.
        instructions.AddRange([0x89, 0xC6, 0x56]);            // MOV SI,AX; PUSH SI: pass that handle to Lock.
        CallWin16("LocalLock");
        instructions.AddRange([0xA3, 0x22, 0x00]);             // MOV [0022],AX: near pointer returned by Lock.
        instructions.AddRange([0x89, 0x16, 0x24, 0x00]);       // MOV [0024],DX: observe the selector too.
        instructions.AddRange([0x89, 0xC3]);                  // MOV BX,AX: use the returned offset.
        instructions.AddRange([0xC7, 0x07, 0xEF, 0xBE]);       // MOV word [DS:BX],BEEF: a real guest heap WRITE.
        instructions.AddRange([0x8B, 0x07, 0xA3, 0x26, 0x00]); // MOV AX,[DS:BX]; MOV [0026],AX: guest READ and store.
        ushort afterWrite = checked((ushort)instructions.Count);

        // Phase 2: release the pin, then ownership. Free does not erase or unmap bytes.
        instructions.Add(0x56); CallWin16("LocalUnlock");      // PUSH SI: still the HANDLE, not BX's pointer.
        instructions.Add(0x56); CallWin16("LocalFree");
        CallWin16("LocalAlloc", (ushort)LocalMemoryFlags.Fixed, requestedBytes);
        instructions.AddRange([0x89, 0xC3, 0x50]);            // MOV BX,AX; PUSH AX: fixed handle equals offset.
        instructions.AddRange([0x8B, 0x07, 0xA3, 0x28, 0x00]); // Observe stale bytes in the newly owned block.
        CallWin16("LocalFree");                              // Argument already pushed above.
        CallWin16("LocalAlloc", (ushort)LocalMemoryFlags.ZeroInit, requestedBytes);
        instructions.AddRange([0x89, 0xC3, 0x50]);
        instructions.AddRange([0x8B, 0x07, 0xA3, 0x2A, 0x00]); // Same storage, explicitly zeroed by the API.
        CallWin16("LocalFree");
        ushort end = checked((ushort)instructions.Count);

        using var guest = new SegmentedGuest();
        byte[] code = new byte[4096]; instructions.CopyTo(code);
        byte[] gateways = new byte[4096];
        foreach (var binding in bindings)
        {
            gateways[binding.Address.Offset] = 0x0F;
            gateways[binding.Address.Offset + 1] = 0x0B; // UD2 guard; hook stops before execution.
        }
        guest.Map(codeSelector, 0x10000, code, code: true);
        guest.Map(gatewaySelector, 0x20000, gateways, code: true);
        // THIS creates Unicorn's backing storage. LocalAlloc later divides a portion of it.
        guest.Map(dataSelector, 0x30000, new byte[4096], code: false);
        guest.Map(stackSelector, 0x40000, new byte[4096], code: false);
        guest.Install(gatewaySelector);
        var reservation = new LocalHeapReservation(new(dataSelector, heapStart), initialHeapBytes);
        var services = new Win16Api(new Win16ApiState(guest, reservation, localHeapCapacityBytes: heapCapacityBytes));
        var dispatcher = new Win16ImportGateway(guest, bindings, services, new(guest, stackSelector, 0x1000));
        var calls = new List<Win16CallTrace>();
        guest.DispatchGateway = () => calls.Add(dispatcher.Dispatch());
        Win16RegisterConvention.SetUpPrologRegisters(guest, new(dataSelector, initialHeapBytes, stackSelector, 0x1000));

        output.WriteLine("1. Map backing once; reserve DS:0400 with 16 initial bytes and 128 bytes of capacity.");
        guest.RunUntil("allocate and write", new(codeSelector, 0), new(codeSelector, afterWrite));
        ushort handle = ReadWord(0x20);
        var address = new FarPointer16(ReadWord(0x24), ReadWord(0x22));
        LocalHeapSnapshot locked = services.State.LocalHeap!.Snapshot();
        ushort stored = ReadWord(0x26);
        ushort hostRead = BinaryPrimitives.ReadUInt16LittleEndian(guest.Read(address, 2));
        output.WriteLine($"2. LocalAlloc returns handle {handle:X4}; LocalLock resolves it to {address}.");
        output.WriteLine($"   Enabled heap grows {locked.InitialBytes} -> {locked.EnabledBytes} bytes without another MemMap.");
        output.WriteLine($"3. MOV [DS:BX],BEEF writes the allocation. Guest reads {stored:X4}; independent host read sees {hostRead:X4}.");
        var final = guest.RunUntil("free and reuse", new(codeSelector, afterWrite), new(codeSelector, end));
        output.WriteLine($"4. Free returns the range to the heap. Reallocation sees {ReadWord(0x28):X4}; ZEROINIT reallocation sees {ReadWord(0x2A):X4}.");
        output.WriteLine("5. Final Free leaves no allocations. The using scope now disposes Unicorn and releases backing storage.");
        return new(handle, address, stored, hostRead, ReadWord(0x28), ReadWord(0x2A), locked,
            services.State.LocalHeap.Snapshot(), calls.AsReadOnly(), final);

        ushort ReadWord(ushort offset) => BinaryPrimitives.ReadUInt16LittleEndian(guest.Read(new(dataSelector, offset), 2));

        // Small encoding helpers keep the interesting instructions above visible.
        void CallWin16(string name, params ushort[] arguments)
        {
            foreach (ushort argument in arguments) { instructions.Add(0x68); AppendWord(argument); }
            FarPointer16 target = bindings.Single(binding => binding.Name.Contains("!" + name + " ")).Address;
            instructions.Add(0x9A); AppendWord(target.Offset); AppendWord(target.Selector);
        }
        void AppendWord(ushort value) { instructions.Add((byte)value); instructions.Add((byte)(value >> 8)); }
    }
}
