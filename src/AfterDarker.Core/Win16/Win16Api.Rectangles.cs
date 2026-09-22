using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Guest RECT operations; these change coordinates in memory without drawing.</summary>
public sealed partial class Win16Api
{
    /// <summary>TRUE when either signed extent is zero or negative; does not reorder or modify the RECT.</summary>
    public bool IsRectEmpty(FarPointer16 address)
    {
        Rectangle16 rectangle = ReadRectangle(address);
        return rectangle.Left >= rectangle.Right || rectangle.Top >= rectangle.Bottom;
    }

    /// <summary>Move all four edges by signed deltas. Win16 stores wrapping 16-bit results and returns void.</summary>
    public void OffsetRect(FarPointer16 address, short horizontal, short vertical)
    {
        Rectangle16 rectangle = ReadRectangle(address);
        WriteRectangle(address, new(unchecked((short)(rectangle.Left + horizontal)),
            unchecked((short)(rectangle.Top + vertical)), unchecked((short)(rectangle.Right + horizontal)),
            unchecked((short)(rectangle.Bottom + vertical))));
    }

    /// <summary>Expand opposite edges by signed deltas; negative amounts shrink, without normalizing inverted bounds.</summary>
    public void InflateRect(FarPointer16 address, short horizontal, short vertical)
    {
        Rectangle16 rectangle = ReadRectangle(address);
        WriteRectangle(address, new(unchecked((short)(rectangle.Left - horizontal)),
            unchecked((short)(rectangle.Top - vertical)), unchecked((short)(rectangle.Right + horizontal)),
            unchecked((short)(rectangle.Bottom + vertical))));
    }

    /// <summary>Write the positive-area intersection, or an all-zero RECT and FALSE. Input/output may alias.</summary>
    public bool IntersectRect(FarPointer16 destination, FarPointer16 firstAddress, FarPointer16 secondAddress)
    {
        // Read both inputs before writing: callers commonly reuse one input as the destination.
        Rectangle16 first = ReadRectangle(firstAddress), second = ReadRectangle(secondAddress);
        var intersection = new Rectangle16(Math.Max(first.Left, second.Left), Math.Max(first.Top, second.Top),
            Math.Min(first.Right, second.Right), Math.Min(first.Bottom, second.Bottom));
        bool overlaps = intersection.Left < intersection.Right && intersection.Top < intersection.Bottom;
        WriteRectangle(destination, overlaps ? intersection : new(0, 0, 0, 0));
        return overlaps;
    }

    /// <summary>Compare the four signed coordinates exactly; differently encoded empty rectangles are not equal.</summary>
    public bool EqualRect(FarPointer16 first, FarPointer16 second) => ReadRectangle(first) == ReadRectangle(second);

    /// <summary>Copy eight guest bytes, including inverted bounds. Read first so source and destination may overlap.</summary>
    /// <remarks>Win16 CopyRect returns void; importing Win32's BOOL signature would corrupt the guest convention.</remarks>
    public void CopyRect(FarPointer16 destination, FarPointer16 source) => WriteRectangle(destination, ReadRectangle(source));

    /// <summary>Write the bounding rectangle of nonempty inputs, or zero and FALSE if both are empty. Inputs may alias output.</summary>
    public bool UnionRect(FarPointer16 destination, FarPointer16 firstAddress, FarPointer16 secondAddress)
    {
        Rectangle16 first = ReadRectangle(firstAddress), second = ReadRectangle(secondAddress);
        bool firstEmpty = first.Left >= first.Right || first.Top >= first.Bottom;
        bool secondEmpty = second.Left >= second.Right || second.Top >= second.Bottom;
        Rectangle16 result;
        if (firstEmpty && secondEmpty) result = default;
        else if (firstEmpty) result = second;
        else if (secondEmpty) result = first;
        else result = new(Math.Min(first.Left, second.Left), Math.Min(first.Top, second.Top),
            Math.Max(first.Right, second.Right), Math.Max(first.Bottom, second.Bottom));
        WriteRectangle(destination, result);
        return !firstEmpty || !secondEmpty;
    }

    private Rectangle16 ReadRectangle(FarPointer16 address) =>
        Rectangle16.Decode(State.Memory.Read(address, Rectangle16.ByteCount));
    private void WriteRectangle(FarPointer16 address, Rectangle16 rectangle) => State.Memory.Write(address, rectangle.Encode());
}
