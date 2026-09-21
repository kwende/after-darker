using System.Buffers.Binary;
using System.Text;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Fixtures;

namespace AfterDarker.Tests.Fixtures;

/// <summary>Source-authored two-color image and legacy alias; no proprietary pixels or file bytes.</summary>
internal static class BitmapResourceFixture
{
    public static byte[] Dib(int depth = 1, bool topDown = false, bool core = false)
    {
        int header = core ? 12 : 40, entries = depth <= 8 ? 1 << depth : 0;
        int paletteBytes = entries * (core ? 3 : 4), stride = ((3 * depth + 31) / 32) * 4;
        byte[] bytes = new byte[header + paletteBytes + 2 * stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, header);
        if (core)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 3);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), topDown ? -2 : 2);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(core ? 8 : 12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(core ? 10 : 14), (ushort)depth);
        if (entries > 0) bytes.AsSpan(header + (core ? 3 : 4), 3).Fill(255);
        // Logical rows are white/black/white, then black/white/black.
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
            {
                bool white = (row + column) % 2 == 0;
                int offset = header + paletteBytes + (topDown ? row : 1 - row) * stride;
                if (depth == 1 && white) bytes[offset] |= (byte)(0x80 >> column);
                else if (depth == 4 && white) bytes[offset + column / 2] |= (byte)(column % 2 == 0 ? 0x10 : 1);
                else if (depth == 8) bytes[offset + column] = white ? (byte)1 : (byte)0;
                else if (depth >= 24)
                {
                    bytes.AsSpan(offset + column * (depth / 8), 3).Fill(white ? (byte)255 : (byte)0);
                    if (depth == 32) bytes[offset + column * 4 + 3] = 17; // Not alpha.
                }
            }
        return bytes;
    }

    public static (byte[] File, NeImage Image) ResourceFile()
    {
        byte[] bitmap = Dib(), alias = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(alias, 14);
        BinaryPrimitives.WriteUInt16LittleEndian(alias.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(alias.AsSpan(4), 0x8001);
        Encoding.ASCII.GetBytes("SPRITE").CopyTo(alias, 7); // Empty type string, then name and NUL.
        byte[] file = [.. bitmap, .. alias];
        NeImage image = NeReader.Read(RelocationDemo.Create()) with
        {
            Resources = new NeResource[]
        {
            new(new(2, null), new(1, null), 0, bitmap.Length, 0),
            new(new(15, null), new(1, null), bitmap.Length, alias.Length, 0)
        }
        };
        return (file, image);
    }
    public static NeResourceCatalog Catalog()
    {
        var fixture = ResourceFile();
        return new(fixture.File, fixture.Image);
    }
}
