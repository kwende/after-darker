using System.Buffers.Binary;
using System.Text;

namespace AfterDarker.Tutorials.Fixtures;

/// <summary>
/// Original, deliberately small NE metadata example for lesson 07. The stored
/// segment bytes are pointer fields, NOT an executable program. No Watcom or
/// private modules are needed. Real-file parsing uses exactly the same loader.
/// </summary>
public static class RelocationDemo
{
    public static byte[] Create()
    {
        byte[] file = new byte[0x540];
        Word(0, 0x5A4D); Word(0x3C, 0x80); // MZ; e_lfanew low word (rest zero)
        Word(0x80, 0x454E); Word(0x84, 0x100); Word(0x86, 16); // NE, entry table
        Word(0x8C, 0x8001); Word(0x8E, 3); // library, single automatic data S3
        Word(0x94, 0); Word(0x96, 1); // a metadata startup address; never executed
        Word(0x9C, 3); Word(0x9E, 1); // three segments, one import module
        Word(0xA2, 0x40); Word(0xA4, 0x60); Word(0xA6, 0x60); // no resources
        Word(0xA8, 0xA0); Word(0xAA, 0xB0); Word(0xB2, 4);
        file[0xB6] = 2; Word(0xBE, 0x030A); // Windows target
        Segment(0xC0, 0x30, 0x100, 0x40); // S1 stored at 0x300, with fixups
        Segment(0xC8, 0x40, 0, 0x40);     // S2 stored at 0x400
        Segment(0xD0, 0x50, 1, 0x80);    // S3 stored at 0x500 + zero-filled tail
        Name(0xE0, "RELOCDEMO"); // ordinal zero module name, followed by terminator
        Word(0x120, 0); Name(0x130, "KERNEL");
        // Ordinal 1: fixed S1:0000. Ordinal 2: movable S2:0020, NOT exported.
        byte[] entries = [1, 1, 0, 0, 0, 1, 0xFF, 0, 0xCD, 0x3F, 2, 0x20, 0, 0];
        entries.CopyTo(file, 0x180);

        Word(0x304, 0x10); Word(0x310, 0xFFFF); // linked sites 0004 -> 0010 -> end
        Word(0x318, 0xFFFF); Word(0x320, 0xFFFF); Word(0x328, 0xFFFF);
        Word(0x340, 4);
        Relocation(0x342, 3, 0, 0x04, 2, 0x12);   // far pointer to fixed S2:0012
        Relocation(0x34A, 2, 0, 0x18, 3, 0);      // selector of data segment S3
        Relocation(0x352, 5, 0, 0x20, 0xFF, 2);   // offset of entry #2 -> S2:0020
        Relocation(0x35A, 3, 1, 0x28, 1, 3);      // KERNEL ordinal 3
        return file;

        void Word(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(at), value);
        void Name(int at, string name) { file[at] = (byte)name.Length; Encoding.ASCII.GetBytes(name).CopyTo(file, at + 1); }
        void Segment(int at, ushort sector, ushort flags, ushort allocation)
        { Word(at, sector); Word(at + 2, 0x40); Word(at + 4, flags); Word(at + 6, allocation); }
        void Relocation(int at, byte type, byte flags, ushort source, ushort target1, ushort target2)
        { file[at] = type; file[at + 1] = flags; Word(at + 2, source); Word(at + 4, target1); Word(at + 6, target2); }
    }
}
