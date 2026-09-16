using System.Buffers.Binary;

namespace AfterDarker.Core.Win16;

/// <summary>
/// A same-privilege 16-bit far return address followed by signed word arguments.
/// This is tutorial 04's layout, not a decoder for arbitrary Win16 signatures.
/// The caller checks guest memory bounds before supplying these bytes.
/// </summary>
public readonly ref struct FarPascalWordFrame
{
    private readonly ReadOnlySpan<byte> bytes;

    public FarPascalWordFrame(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || (bytes.Length & 1) != 0)
            throw new ArgumentException("A far frame needs four return bytes followed by whole word arguments.", nameof(bytes));
        this.bytes = bytes;
    }

    public ushort ReturnIp => BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    public ushort ReturnCs => BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
    public int ArgumentCount => (bytes.Length - 4) / 2;

    // Pascal pushes left to right: the last argument sits closest to the return.
    // Indices here follow the source-language order: 0 is the LEFT argument.
    public short ReadArgument(int index)
    {
        if (index < 0 || index >= ArgumentCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        int offset = 4 + (ArgumentCount - 1 - index) * 2;
        return BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, 2));
    }

    // Pop IP + CS + all arguments. Our supported host path rejects wrapping;
    // a general x86 stack implementation would also need to model wrap semantics.
    public ushort StackPointerAfterReturn(ushort stackPointer) =>
        checked((ushort)(stackPointer + bytes.Length));
}
