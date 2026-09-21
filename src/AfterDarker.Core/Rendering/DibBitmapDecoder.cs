using System.Buffers.Binary;

namespace AfterDarker.Core.Rendering;

/// <summary>Decode bounded uncompressed Windows/OS2 DIB payloads, without a BMP file wrapper.</summary>
/// <remarks>Original implementation from the documented format, not vendored Wine code.
/// Supports CORE/INFO headers and indexed or direct RGB pixels. See docs/ne-bitmap-resources.md.</remarks>
public static class DibBitmapDecoder
{
    private const int CoreHeaderBytes = 12, InformationHeaderBytes = 40;
    /// <summary>Validate all lengths before allocation and produce top-to-bottom RGB pixels.</summary>
    public static DecodedBitmap Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < CoreHeaderBytes) throw new InvalidDataException("Truncated DIB header.");
        uint headerBytes = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        bool coreHeader = headerBytes == CoreHeaderBytes;
        if (!coreHeader && headerBytes != InformationHeaderBytes)
            throw new NotSupportedException($"DIB header size {headerBytes} is unsupported; expected CORE (12) or INFO (40).");
        if (payload.Length < headerBytes) throw new InvalidDataException("Truncated DIB information header.");
        int width = coreHeader ? BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]) : BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        int signedHeight = coreHeader ? BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]) : BinaryPrimitives.ReadInt32LittleEndian(payload[8..]);
        int height = signedHeight == int.MinValue ? 0 : Math.Abs(signedHeight);
        ushort planes = BinaryPrimitives.ReadUInt16LittleEndian(payload[(coreHeader ? 8 : 12)..]);
        ushort bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(payload[(coreHeader ? 10 : 14)..]);
        if (width is < 1 or > 2048 || height is < 1 or > 2048 || planes != 1)
            throw new InvalidDataException("DIB requires one plane and dimensions within 1..2048.");
        if (bitsPerPixel is not (1 or 4 or 8 or 24 or 32)) throw new NotSupportedException($"Unsupported DIB bit depth {bitsPerPixel}.");
        if (coreHeader && bitsPerPixel == 32) throw new NotSupportedException("CORE headers do not define 32-bit RGB pixels.");
        if (!coreHeader && BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]) != 0)
            throw new NotSupportedException("Only uncompressed BI_RGB resource bitmaps are supported.");
        uint usedColors = coreHeader ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(payload[32..]);
        int maximumColors = bitsPerPixel <= 8 ? 1 << bitsPerPixel : 0;
        if (usedColors > (uint)maximumColors) throw new InvalidDataException("DIB palette size exceeds its supported bit depth.");
        int paletteCount = usedColors == 0 ? maximumColors : (int)usedColors;
        int paletteEntryBytes = coreHeader ? 3 : 4;
        int pixelOffset = (int)headerBytes + paletteCount * paletteEntryBytes;
        int rowStride = ((width * bitsPerPixel + 31) / 32) * 4;
        if ((long)pixelOffset + (long)rowStride * height > payload.Length)
            throw new InvalidDataException("Truncated DIB color table or padded pixel rows.");
        byte[] rgb = new byte[width * height * 3];
        for (int row = 0; row < height; row++)
        {
            int storedRow = signedHeight > 0 ? height - 1 - row : row;
            ReadOnlySpan<byte> source = payload.Slice(pixelOffset + storedRow * rowStride, rowStride);
            for (int column = 0; column < width; column++)
            {
                int colorOffset;
                ReadOnlySpan<byte> colorBytes;
                if (bitsPerPixel <= 8)
                {
                    int paletteIndex = bitsPerPixel switch
                    {
                        1 => source[column / 8] >> (7 - column % 8) & 1,
                        4 => source[column / 2] >> (column % 2 == 0 ? 4 : 0) & 15,
                        _ => source[column]
                    };
                    if (paletteIndex >= paletteCount) throw new InvalidDataException("DIB pixel references a missing palette entry.");
                    colorOffset = (int)headerBytes + paletteIndex * paletteEntryBytes;
                    colorBytes = payload.Slice(colorOffset, 3);
                }
                else
                {
                    colorOffset = column * (bitsPerPixel / 8);
                    colorBytes = source.Slice(colorOffset, 3); // BI_RGB's fourth byte is not alpha.
                }
                int destination = (row * width + column) * 3;
                rgb[destination] = colorBytes[2]; rgb[destination + 1] = colorBytes[1]; rgb[destination + 2] = colorBytes[0];
            }
        }
        bool monochrome = bitsPerPixel == 1 && paletteCount == 2 &&
            payload.Slice((int)headerBytes, 3).SequenceEqual(new byte[] { 0, 0, 0 }) &&
            payload.Slice((int)headerBytes + paletteEntryBytes, 3).SequenceEqual(new byte[] { 255, 255, 255 });
        return new(width, height, bitsPerPixel, rgb) { IsMonochrome = monochrome };
    }
}
