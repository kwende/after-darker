using System.Buffers.Binary;
using System.Text;

namespace AfterDarker.Tests.Fixtures;

/// <summary>
/// A tiny, original metadata fixture. No executable program or proprietary bytes.
/// Tables deliberately use distinct origins to expose wrong-base offset arithmetic.
/// </summary>
internal static class NeFixture
{
    public static byte[] Create()
    {
        byte[] b = new byte[0x540];
        Word(b, 0, 0x5A4D);
        Dword(b, 0x3C, 0x80);
        Word(b, 0x80, 0x454E);
        Word(b, 0x84, 0x100); // Entries at file 0x180, relative to NE at 0x80.
        Word(b, 0x86, 21);
        Word(b, 0x8C, 0x8001); // DLL, single automatic data segment.
        Word(b, 0x8E, 2);
        Word(b, 0x90, 0x400);
        Word(b, 0x94, 8); Word(b, 0x96, 1); // Startup S1:0008.
        Word(b, 0x9C, 2); Word(b, 0x9E, 1); // Two segments, one module reference.
        Word(b, 0xA0, 15); // Nonresident table bytes.
        Word(b, 0xA2, 0x40); // Segment table at 0xC0.
        Word(b, 0xA4, 0x50); // Resource table at 0xD0.
        Word(b, 0xA6, 0xA0); // Resident names at 0x120.
        Word(b, 0xA8, 0xD0); // Module reference table at 0x150.
        Word(b, 0xAA, 0xD2); // Import names at 0x152.
        Dword(b, 0xAC, 0x200); // Nonresident offset is absolute, NOT NE-relative.
        Word(b, 0xB0, 1); Word(b, 0xB2, 4);
        b[0xB6] = 2; Word(b, 0xBE, 0x030A);

        Word(b, 0xC0, 0x40); Word(b, 0xC2, 0x40); // Stored code at 0x400, 64 bytes.
        Word(b, 0xC4, 0x100); Word(b, 0xC6, 0x40); // Relocations follow it.
        Word(b, 0xCC, 1); Word(b, 0xCE, 0x100); // Zero-filled data, 256 bytes.

        Word(b, 0xD0, 4); // Resource offsets/sizes use a separate alignment shift.
        Word(b, 0xD2, 0x8002); Word(b, 0xD4, 1); // Numeric type BITMAP.
        Word(b, 0xDA, 0x50); Word(b, 0xDC, 2); // File 0x500, 32 bytes.
        Word(b, 0xDE, 0x30); Word(b, 0xE0, 0x8007); // Numeric resource ID 7.
        Word(b, 0xE6, 0x34); Word(b, 0xE8, 1); // Named type at resource-table + 0x34.
        Word(b, 0xEE, 0x52); Word(b, 0xF0, 1); // File 0x520, 16 bytes.
        Word(b, 0xF2, 0x10); Word(b, 0xF4, 0x3C); // Named ID at table + 0x3C.
        Name(b, 0x104, "CUSTOM"); Name(b, 0x10C, "FRAME0");

        int next = ExportName(b, 0x120, "TESTLIB", 0);
        ExportName(b, next, "MODULE", 1);
        Word(b, 0x150, 0); Name(b, 0x152, "KERNEL"); Name(b, 0x159, "NamedProc");
        // Fixed ordinal 1; unused ordinal 2; movable ordinal 3; constant ordinal 4.
        byte[] entries = [1, 1, 3, 0x10, 0, 1, 0, 1, 0xFF, 1, 0xCD, 0x3F, 1, 0x20, 0, 1, 0xFE, 1, 0xEF, 0xBE, 0];
        entries.CopyTo(b, 0x180);
        ExportName(b, 0x200, "FRAME_ALIAS", 3);

        Word(b, 0x440, 3);
        Fixup(b, 0x442, 1, 4, 18);
        Fixup(b, 0x44A, 1, 0x10, 18); // Same import, another fixup record.
        Fixup(b, 0x452, 2, 0x18, 7); // Name offset relative to import names.
        return b;
    }

    private static void Fixup(byte[] b, int at, byte flags, ushort source, ushort target)
    {
        b[at] = 3; b[at + 1] = flags;
        Word(b, at + 2, source); Word(b, at + 4, 1); Word(b, at + 6, target);
    }

    private static int Name(byte[] b, int at, string text)
    {
        b[at] = (byte)text.Length;
        Encoding.ASCII.GetBytes(text).CopyTo(b, at + 1);
        return at + 1 + text.Length;
    }

    private static int ExportName(byte[] b, int at, string text, ushort ordinal)
    {
        int end = Name(b, at, text); Word(b, end, ordinal); return end + 2;
    }

    public static void Word(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    public static void Dword(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
