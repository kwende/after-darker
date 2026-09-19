using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>
/// Start here to read the Windows implementations. Each method receives ordinary
/// C# arguments and returns an ordinary value. No opcodes, stack frames, import
/// ordinals, register writes, NE parsing, or tutorial logic belong in this class.
///
/// One instance per emulated process: heaps, handles, and clocks must not leak
/// between guests. Win16ApiState holds that state and the host's chosen inputs.
/// These methods implement only the documented subset, not all of Windows.
/// </summary>
public sealed class Win16Api(Win16ApiState state)
{
    public Win16ApiState State { get; } = state;

    /// <summary>Return the Windows version configured by the host.</summary>
    public uint GetVersion() => State.WindowsVersion;

    /// <summary>
    /// Accept the loader's reserved local heap. This is a checked test double:
    /// no Windows heap metadata, LocalAlloc, or LocalFree exists yet.
    /// </summary>
    public bool LocalInit(ushort dataSelector, ushort start, ushort bytes)
    {
        // Both current DLLs request LocalInit(DS, 0, heapBytes). The loader has
        // already allocated a zero-filled tail after the data segment's static
        // storage. A successful reply is justified only for that exact request.
        LocalHeapReservation reserved = State.ReservedHeap;
        if (State.InitializedHeap is not null || dataSelector != reserved.Start.Selector || start != 0 ||
            bytes == 0 || bytes != reserved.Length)
            throw new NotSupportedException("LocalInit request does not match the prepared data-segment heap reservation.");
        if (State.Memory.Read(reserved.Start, bytes).Any(b => b != 0))
            throw new InvalidOperationException("LocalInit reservation is not zero-filled.");

        // Tutorial 06 can exercise the DLL's own failure path without changing
        // guest machine code. A failed initialization publishes no usable heap.
        if (!State.LocalInitSucceeds) return false;
        State.InitializedHeap = reserved;
        return true;
    }

    /// <summary>Resolve a handle to its resident guest block and acquire a lock.</summary>
    public FarPointer16 GlobalLock(ushort handle)
    {
        // The handle is a key, not an address. The registry verifies backing
        // memory and increments its lock count before giving out the pointer.
        // Unknown handles fail here; no unusable pointer reaches the guest.
        return State.Blocks.Lock(handle);
    }

    /// <summary>Release one lock and return the remaining lock count.</summary>
    public ushort GlobalUnlock(ushort handle)
    {
        // Zero after the last unlock is normal: the block is now unlocked.
        // Its memory remains allocated for the lifetime of this guest session.
        return State.Blocks.Unlock(handle);
    }

    /// <summary>Return the guest address of our explicitly supplied empty environment.</summary>
    public FarPointer16 GetDOSEnvironment()
    {
        FarPointer16 address = State.Environment
            ?? throw new NotSupportedException("No guest DOS environment was supplied.");
        // Two NUL bytes describe an empty list of environment strings. This
        // address must be backed by memory: the guest reads through it directly.
        if (!State.Memory.Read(address, 2).SequenceEqual(new byte[2]))
            throw new NotSupportedException("Only a double-NUL empty DOS environment is supported.");
        return address;
    }

    /// <summary>Return and advance the host's deterministic millisecond clock.</summary>
    public uint GetTickCount()
    {
        // A constant clock can prevent a guest's timing gates from opening.
        // Advancing once per request is our test policy, not real elapsed time.
        uint result = State.NextTick;
        State.NextTick = unchecked(result + State.TickStep);
        return result;
    }
}
