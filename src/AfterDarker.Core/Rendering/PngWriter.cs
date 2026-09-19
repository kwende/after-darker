using System.Buffers.Binary;
using System.IO.Compression;

namespace AfterDarker.Core.Rendering;

/// <summary>Small RGB8 PNG encoder using .NET's zlib; no rendering or image-library dependency.</summary>
public static class PngWriter
{
    public static void Write(Stream destination, int width, int height, byte[] rgb)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048 || rgb.Length != checked(width * height * 3))
            throw new ArgumentException("Expected bounded, tightly packed RGB pixels.");
        destination.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 2; // Eight bits per channel, truecolor RGB, no interlace.
        Chunk(destination, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // PNG filter None: every row is self-contained and inspectable.
                zlib.Write(rgb, y * width * 3, width * 3);
            }
        }
        Chunk(destination, "IDAT"u8, compressed.ToArray());
        Chunk(destination, "IEND"u8, []);
    }

    private static void Chunk(Stream destination, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        destination.Write(word); destination.Write(type); destination.Write(data);
        // PNG uses the reflected IEEE CRC-32 over type + payload (not length).
        uint crc = uint.MaxValue;
        foreach (byte value in type) Accumulate(value);
        foreach (byte value in data) Accumulate(value);
        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
        destination.Write(word);
        void Accumulate(byte value)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
        }
    }
}
