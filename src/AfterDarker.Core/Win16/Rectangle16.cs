using System.Buffers.Binary;

namespace AfterDarker.Core.Win16;

/// <summary>The eight guest bytes of RECT16. Coordinates are signed; preserve their order.</summary>
public readonly record struct Rectangle16(short Left, short Top, short Right, short Bottom)
{
    public const int ByteCount = 8;
    public byte[] Encode()
    {
        byte[] bytes = new byte[ByteCount];
        short[] values = [Left, Top, Right, Bottom];
        for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), values[i]);
        return bytes;
    }
    public static Rectangle16 Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteCount) throw new ArgumentException("RECT16 requires eight bytes.");
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
    }
}
