using System.Buffers.Binary;
using System.Text;

namespace AfterDarker.Core.Ne;

/// <summary>
/// Reads the Windows NE metadata needed by our next loader experiments.
/// Offsets in different tables have different origins; keep those origins explicit.
/// Iterated segments and non-Windows NE variants are deliberately unsupported.
/// </summary>
public static class NeReader
{
    public const int MaximumFileBytes = 64 * 1024 * 1024;

    public static NeImage ReadFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > MaximumFileBytes)
            throw new InvalidDataException("NE inspection is limited to 64 MiB inputs.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return new Parser(bytes).Parse();
    }

    public static NeImage Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumFileBytes)
            throw new InvalidDataException("NE inspection is limited to 64 MiB inputs.");
        return new Parser(bytes.ToArray()).Parse();
    }

    private sealed class Parser(byte[] data)
    {
        private int ne;
        private int metadataItems;
        private readonly List<NeSegment> segments = [];

        // Bound allocation even if malicious tables repeatedly reference the same bytes.
        private void CountItem()
        {
            if (++metadataItems > 100_000)
                throw new InvalidDataException("NE metadata exceeds the 100,000-item inspection limit.");
        }

        private static InvalidDataException Bad(string what, int offset) =>
            new($"Invalid NE {what} at file offset 0x{offset:X}.");

        private ReadOnlySpan<byte> Span(int offset, int length, int? limit = null)
        {
            int end = Math.Min(limit ?? data.Length, data.Length);
            if (offset < 0 || length < 0 || offset > end || length > end - offset)
                throw Bad("range (truncated or out of bounds)", offset);
            return data.AsSpan(offset, length);
        }

        private byte Byte(int offset, int? limit = null) => Span(offset, 1, limit)[0];
        private ushort Word(int offset, int? limit = null) =>
            BinaryPrimitives.ReadUInt16LittleEndian(Span(offset, 2, limit));
        private uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Span(offset, 4));

        private int FileOffset(uint value)
        {
            if (value > data.Length) throw Bad("file offset", data.Length);
            return (int)value;
        }

        private int Relative(int field) => ne + Word(ne + field);

        private string Name(int offset, int? limit = null)
        {
            int length = Byte(offset, limit);
            // Preserve all byte values; historical names need not be ASCII.
            return Encoding.Latin1.GetString(Span(offset + 1, length, limit));
        }

        private int Shift(ushort value, int shift, int at)
        {
            if (shift > 31) throw Bad("alignment shift", at);
            long result = (long)value << shift;
            if (result > data.Length) throw Bad("shifted file range", at);
            return (int)result;
        }

        public NeImage Parse()
        {
            if (Word(0) != 0x5A4D) throw Bad("signature: expected MZ", 0);
            ne = FileOffset(Dword(0x3C));
            if (ne < 0x40 || Word(ne) != 0x454E)
                throw Bad("signature: expected NE (PE and compressed installers are not supported)", ne);
            Span(ne, 0x40);
            if (Byte(ne + 0x36) != 2)
                throw new NotSupportedException("This inspector supports Windows NE files (target OS 2) only.");

            int segmentCount = Word(ne + 0x1C);
            int segmentTable = Relative(0x22);
            int alignment = Word(ne + 0x32);
            if (alignment > 31) throw Bad("segment alignment shift", ne + 0x32);
            Span(segmentTable, segmentCount * 8);
            for (int i = 0; i < segmentCount; i++)
            {
                CountItem();
                int at = segmentTable + i * 8;
                ushort sector = Word(at), size = Word(at + 2), flags = Word(at + 4);
                if ((flags & 8) != 0)
                    throw new NotSupportedException($"Segment {i + 1} is iterated; expansion is not implemented.");
                int? start = sector == 0 ? null : Shift(sector, alignment, at);
                int length = start is null ? 0 : size == 0 ? 65536 : size;
                if (start is int position) Span(position, length);
                if (start is null && (flags & 0x100) != 0)
                    throw Bad("relocations on a segment without file data", at);
                int minimum = Word(at + 6);
                segments.Add(new((ushort)(i + 1), start, length, minimum == 0 ? 65536 : minimum, flags));
            }

            NeAddress? startup = Word(ne + 0x16) == 0 ? null :
                Address(Word(ne + 0x16), Word(ne + 0x14), ne + 0x14);
            ushort autoData = Word(ne + 0x0E), stackSegment = Word(ne + 0x1A);
            if (autoData > segmentCount || stackSegment > segmentCount)
                throw Bad("automatic data or stack segment number", ne + 0x0E);
            NeHeader header = new(ne, Word(ne + 0x0C), Byte(ne + 0x36), Word(ne + 0x3E),
                autoData, Word(ne + 0x10), Word(ne + 0x12), startup, stackSegment,
                Word(ne + 0x18), (ushort)alignment);

            List<NeEntry> entries = ReadEntries();
            List<NeName> names = [];
            ReadNames(Relative(0x26), Relative(0x28), true, names);
            int nonResidentSize = Word(ne + 0x20);
            if (nonResidentSize != 0)
            {
                int start = FileOffset(Dword(ne + 0x2C));
                Span(start, nonResidentSize);
                ReadNames(start, start + nonResidentSize, false, names);
            }
            HashSet<ushort> ordinals = entries.Select(e => e.Ordinal).ToHashSet();
            foreach (NeName name in names)
                if (name.Ordinal != 0 && !ordinals.Contains(name.Ordinal))
                    throw Bad("name referencing an absent entry ordinal", ne);

            int moduleTable = Relative(0x28), importNames = Relative(0x2A);
            int moduleCount = Word(ne + 0x1E);
            Span(moduleTable, moduleCount * 2);
            List<string> modules = [];
            for (int i = 0; i < moduleCount; i++)
            {
                CountItem();
                modules.Add(Name(importNames + Word(moduleTable + i * 2)));
            }

            return new(header, segments.AsReadOnly(), entries.AsReadOnly(), names.AsReadOnly(),
                modules.AsReadOnly(), ReadRelocations(modules, importNames).AsReadOnly(),
                ReadResources().AsReadOnly());
        }

        private NeAddress Address(ushort segment, ushort offset, int at)
        {
            if (segment == 0 || segment > segments.Count) throw Bad("entry segment number", at);
            NeSegment target = segments[segment - 1];
            if (offset >= Math.Max(target.FileBytes, target.MinimumAllocationBytes))
                throw Bad("entry offset outside segment", at);
            return new(segment, offset);
        }

        private List<NeEntry> ReadEntries()
        {
            List<NeEntry> result = [];
            int at = Relative(4), size = Word(ne + 6), ordinal = 1;
            Span(at, size);
            int end = at + size;
            if (size == 0) return result;
            while (at < end)
            {
                int count = Byte(at++, end);
                if (count == 0) return result;
                byte kind = Byte(at++, end);
                if (ordinal + count > 65536) throw Bad("entry ordinal overflow", at);
                for (int i = 0; i < count; i++, ordinal++)
                {
                    if (kind == 0) continue; // An unused bundle still consumes ordinals.
                    CountItem();
                    byte flags = Byte(at++, end);
                    if (kind == 0xFF)
                    {
                        if (Word(at, end) != 0x3FCD) throw Bad("movable entry marker", at);
                        byte segment = Byte(at + 2, end);
                        ushort offset = Word(at + 3, end);
                        result.Add(new((ushort)ordinal, flags, true, Address(segment, offset, at), null));
                        at += 5;
                    }
                    else
                    {
                        ushort offset = Word(at, end);
                        result.Add(kind == 0xFE
                            ? new((ushort)ordinal, flags, false, null, offset)
                            : new((ushort)ordinal, flags, false, Address(kind, offset, at), null));
                        at += 2;
                    }
                }
            }
            throw Bad("unterminated entry table", at);
        }

        private void ReadNames(int at, int end, bool resident, List<NeName> result)
        {
            if (end < at) throw Bad("name table boundaries", at);
            while (at < end)
            {
                int length = Byte(at, end);
                if (length == 0) return;
                CountItem();
                result.Add(new(Name(at, end), Word(at + length + 1, end), resident));
                at += length + 3;
            }
            throw Bad("unterminated name table", at);
        }

        private List<NeRelocation> ReadRelocations(List<string> modules, int importNames)
        {
            List<NeRelocation> result = [];
            foreach (NeSegment segment in segments.Where(s => (s.Flags & 0x100) != 0))
            {
                int table = segment.FileOffset!.Value + segment.FileBytes;
                int count = Word(table);
                Span(table + 2, count * 8);
                for (int i = 0; i < count; i++)
                {
                    CountItem();
                    int at = table + 2 + i * 8;
                    byte addressType = Byte(at), flags = Byte(at + 1);
                    ushort source = Word(at + 2), target1 = Word(at + 4), target2 = Word(at + 6);
                    NeImport? import = null;
                    if ((flags & 3) is 1 or 2)
                    {
                        if (target1 == 0 || target1 > modules.Count) throw Bad("import module index", at);
                        import = (flags & 3) == 1
                            ? new(modules[target1 - 1], target2, null)
                            : new(modules[target1 - 1], null, Name(importNames + target2));
                    }
                    // Preserve raw fixup fields; applying/expanding their chains is loader work.
                    result.Add(new(segment.Number, at, addressType, flags, source, target1, target2, import));
                }
            }
            return result;
        }

        private List<NeResource> ReadResources()
        {
            List<NeResource> result = [];
            int table = Relative(0x24), end = Relative(0x26);
            if (Word(ne + 0x24) == 0 || table == end) return result;
            int shift = Word(table, end);
            if (shift > 31) throw Bad("resource alignment shift", table);
            int at = table + 2;
            while (at < end)
            {
                ushort type = Word(at, end);
                if (type == 0) return result;
                int count = Word(at + 2, end);
                Span(at, 8, end); // Type, count, and four reserved bytes.
                at += 8;
                Span(at, count * 12, end);
                NeResourceIdentifier typeId = ResourceId(type, table, end);
                for (int i = 0; i < count; i++, at += 12)
                {
                    CountItem();
                    int offset = Shift(Word(at), shift, at);
                    int length = Shift(Word(at + 2), shift, at + 2);
                    Span(offset, length);
                    result.Add(new(typeId, ResourceId(Word(at + 6), table, end),
                        offset, length, Word(at + 4)));
                }
            }
            throw Bad("unterminated resource table", at);
        }

        private NeResourceIdentifier ResourceId(ushort raw, int table, int end) =>
            (raw & 0x8000) != 0 ? new((ushort)(raw & 0x7FFF), null) : new(null, Name(table + raw, end));
    }
}
