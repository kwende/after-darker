namespace AfterDarker.Core.Ne;

/// <summary>A file-level address. SegmentNumber is NOT a runtime selector.</summary>
public readonly record struct NeAddress(ushort SegmentNumber, ushort Offset)
{
    public override string ToString() => $"S{SegmentNumber}:{Offset:X4}";
}

public sealed record NeHeader(int FileOffset, ushort Flags, byte TargetOs,
    ushort ExpectedWindowsVersion, ushort AutomaticDataSegment, ushort HeapBytes,
    ushort StackBytes, NeAddress? Startup, ushort StackSegment, ushort StackPointer,
    ushort SegmentAlignmentShift)
{
    public bool IsLibrary => (Flags & NeFormat.HeaderFlags.Library) != 0;
}

public sealed record NeSegment(ushort Number, int? FileOffset, int FileBytes,
    int MinimumAllocationBytes, ushort Flags)
{
    public bool IsData => (Flags & 1) != 0;
}

// Ordinal zero names describe the module; they are not exports.
public sealed record NeName(string Text, ushort Ordinal, bool Resident);

public sealed record NeEntry(ushort Ordinal, byte Flags, bool Movable,
    NeAddress? Address, ushort? ConstantValue)
{
    public bool Exported => (Flags & 1) != 0;
    public bool SharedData => (Flags & 2) != 0;
    public int ParameterWordCount => Flags >> 3;
}

public sealed record NeImport(string Module, ushort? Ordinal, string? Name);

/// <summary>One fixup record, possibly the head of a chain; not an instruction/call count.</summary>
public sealed record NeRelocation(ushort SegmentNumber, int RecordFileOffset,
    byte AddressType, byte Flags, ushort SourceOffset, ushort Target1, ushort Target2,
    NeImport? Import)
{
    public int Kind => Flags & NeFormat.RelocationFlags.KindMask;
    public bool Additive => (Flags & NeFormat.RelocationFlags.Additive) != 0;
}

public sealed record NeResourceIdentifier(ushort? Number, string? Name);
public sealed record NeResource(NeResourceIdentifier Type, NeResourceIdentifier Id,
    int FileOffset, int Length, ushort Flags);

/// <summary>Parsed metadata only: no loaded guest memory, selector allocation, or CPU state.</summary>
public sealed record NeImage(NeHeader Header, IReadOnlyList<NeSegment> Segments,
    IReadOnlyList<NeEntry> Entries, IReadOnlyList<NeName> Names,
    IReadOnlyList<string> ModuleReferences, IReadOnlyList<NeRelocation> Relocations,
    IReadOnlyList<NeResource> Resources)
{
    public IReadOnlyList<NeImport> Imports => Array.AsReadOnly(Relocations
        .Where(r => r.Import is not null).Select(r => r.Import!).Distinct().ToArray());

    public NeEntry? FindExport(string name)
    {
        NeName? match = Names.FirstOrDefault(n => n.Ordinal != 0 &&
            string.Equals(n.Text, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : Entries.FirstOrDefault(e => e.Ordinal == match.Ordinal && e.Exported);
    }
}
