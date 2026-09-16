using System.Buffers.Binary;

namespace AfterDarker.Core.X86;

/// <summary>The descriptor encoding introduced in tutorial 03, independent of Unicorn.</summary>
public static class SegmentDescriptor16
{
    // Byte-granularity limits, 16-bit defaults, privilege 0. This deliberately
    // supports only the descriptor subset we have exercised, not a full GDT API.
    public static byte[] Encode(uint baseAddress, ushort inclusiveLimit, bool executable)
    {
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0, 2), inclusiveLimit);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2, 2), (ushort)baseAddress);
        descriptor[4] = (byte)(baseAddress >> 16);
        // Present, privilege 0, code/data, readable code or writable data, accessed.
        descriptor[5] = executable ? (byte)0x9B : (byte)0x93;
        descriptor[6] = 0; // G=0, D/B=0, upper limit bits=0.
        descriptor[7] = (byte)(baseAddress >> 24);
        return descriptor;
    }
}
