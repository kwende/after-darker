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
    /// <summary>Per-guest Windows state; never shared across simultaneously loaded modules.</summary>
    public Win16ApiState State { get; } = state;

    /// <summary>Create a supported solid pen; color is a Win16 COLORREF, not an ARGB pixel.</summary>
    public ushort CreatePen(short style, short width, uint color) => Drawing.CreatePen(style, width, color);
    /// <summary>Select a pen or supported stock brush, returning the previous handle of the same kind.</summary>
    public ushort SelectObject(ushort hdc, ushort handle) => Drawing.SelectObject(hdc, handle);
    /// <summary>Draw with the selected pen and brush; all four coordinates are signed Win16 words.</summary>
    public bool Ellipse(ushort hdc, short left, short top, short right, short bottom) =>
        Drawing.Ellipse(hdc, new(left, top, right, bottom));
    /// <summary>Delete an unselected owned pen; stock objects remain host-owned.</summary>
    public bool DeleteObject(ushort handle) => Drawing.DeleteObject(handle);
    /// <summary>Move the HDC current point; return its previous coordinates packed as Y:X.</summary>
    public uint MoveTo(ushort hdc, short x, short y) => Drawing.MoveTo(hdc, x, y);
    /// <summary>Draw a line with the selected pen and advance the HDC current point.</summary>
    public bool LineTo(ushort hdc, short x, short y) => Drawing.LineTo(hdc, x, y);

    /// <summary>Return the Windows version configured by the host.</summary>
    public uint GetVersion() => State.WindowsVersion;

    /// <summary>Write the signed corners verbatim into guest memory; Win16's return type is void.</summary>
    public void SetRect(FarPointer16 destination, short left, short top, short right, short bottom)
        => State.Memory.Write(destination, new Rectangle16(left, top, right, bottom).Encode());

    /// <summary>Return a host-owned stock black brush or pen; these do not consume the created-pen pool.</summary>
    public ushort GetStockObject(short index) => index switch
    {
        Win16Drawing.BlackBrushIndex => Win16Drawing.BlackBrushHandle,
        Win16Drawing.BlackPenIndex => Win16Drawing.BlackPenHandle,
        _ => throw new NotSupportedException($"Unsupported stock object {index}.")
    };

    /// <summary>Test a signed point against a guest RECT, including left/top and excluding right/bottom.</summary>
    /// <remarks>Empty or inverted rectangles contain no point. This reads checked guest memory without changing it.</remarks>
    public bool PtInRect(FarPointer16 rectangleAddress, Point16 point)
    {
        Rectangle16 rectangle = Rectangle16.Decode(State.Memory.Read(rectangleAddress, Rectangle16.ByteCount));
        return point.X >= rectangle.Left && point.X < rectangle.Right &&
            point.Y >= rectangle.Top && point.Y < rectangle.Bottom;
    }

    /// <summary>Read a guest RECT, fill it using the supported black brush, and return success.</summary>
    public short FillRect(ushort hdc, FarPointer16 rectangle, ushort brush)
    {
        if (brush != Win16Drawing.BlackBrushHandle) throw new NotSupportedException($"Unknown brush {brush:X4}.");
        Drawing.Paint(hdc, Rectangle16.Decode(State.Memory.Read(rectangle, Rectangle16.ByteCount)), invert: false);
        return 1;
    }

    /// <summary>Invert RGB bits. Inverting the same pixels twice restores them; Win16 returns void.</summary>
    public void InvertRect(ushort hdc, FarPointer16 rectangle)
        => Drawing.Paint(hdc, Rectangle16.Decode(State.Memory.Read(rectangle, Rectangle16.ByteCount)), invert: true);

    private Win16Drawing Drawing => State.Drawing ?? throw new NotSupportedException("No drawing surface was supplied.");

    /// <summary>
    /// Validate the loader's reserved local heap and publish its per-guest allocator.
    /// Memory is already mapped; this call establishes ownership, not an OS allocation.
    /// </summary>
    public bool LocalInit(ushort dataSelector, ushort start, ushort bytes)
    {
        // Supported DLLs request LocalInit(DS, 0, heapBytes). The loader has
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
        State.LocalHeap = new Win16LocalHeap(State.Memory, reserved, State.LocalHeapCapacityBytes);
        State.InitializedHeap = reserved;
        return true;
    }

    /// <summary>Allocate from the heap selected by caller DS; return a Win16 handle or zero for exhaustion.</summary>
    public ushort LocalAlloc(ushort dataSelector, LocalMemoryFlags flags, ushort bytes)
        => GetLocalHeap(dataSelector).Allocate(flags, bytes);

    /// <summary>Resolve a local handle. AX is the near offset; DX carries the owning selector for Win16 compatibility.</summary>
    public FarPointer16 LocalLock(ushort dataSelector, ushort handle)
        => new(dataSelector, GetLocalHeap(dataSelector).Lock(handle));

    /// <summary>Release a movable lock; a zero result after the last lock is normal.</summary>
    public ushort LocalUnlock(ushort dataSelector, ushort handle) => GetLocalHeap(dataSelector).Unlock(handle);

    /// <summary>Return an allocation to the guest free list. The underlying mapped segment remains alive.</summary>
    public ushort LocalFree(ushort dataSelector, ushort handle) => GetLocalHeap(dataSelector).Free(handle);

    private Win16LocalHeap GetLocalHeap(ushort dataSelector)
    {
        Win16LocalHeap heap = State.LocalHeap ?? throw new InvalidOperationException("LocalInit has not established a local heap.");
        heap.RequireOwner(dataSelector);
        return heap;
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

    /// <summary>Return the configured guest clock, deterministic or live.</summary>
    public uint GetTickCount()
    {
        uint result = State.Clock.GetTickCount();
        State.LastReturnedTick = result; // Observe the actual reply without reading/advancing time again.
        return result;
    }
}
