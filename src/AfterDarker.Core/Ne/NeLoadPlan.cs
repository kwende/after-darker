using System.Buffers.Binary;

namespace AfterDarker.Core.Ne;

/// <summary>A runtime address, unlike NeAddress's file-level segment number.</summary>
public readonly record struct FarPointer16(ushort Selector, ushort Offset)
{
    public override string ToString() => $"{Selector:X4}:{Offset:X4}";
}

public sealed record NeSegmentPlacement(ushort Number, ushort Selector, uint LinearBase);
public enum Win16ReturnLayout { WordInAx, DwordInDxAx }
public sealed record NeImportBinding(NeImport Import, string Name, FarPointer16 Address,
    int ArgumentBytes, Win16ReturnLayout ReturnLayout, string Handler);
public sealed record PreparedNeSegment(NeSegment Source, NeSegmentPlacement Placement, byte[] Bytes);
public sealed record NePatch(NeAddress Source, string Reason, byte[] Before, byte[] After,
    NeRelocation? Relocation = null, FarPointer16? Target = null, ushort? NextOffset = null);

/// <summary>
/// A CPU-independent loading step: copy segments, reconnect internal/imported
/// addresses, and fix single-data export prologues. Nothing executes here.
/// Unsupported relocation forms fail instead of being silently ignored.
/// </summary>
public sealed record NeLoadPlan(NeImage Image, IReadOnlyList<PreparedNeSegment> Segments,
    IReadOnlyList<NePatch> Patches)
{
    // x86 encoding: a GDT descriptor occupies eight bytes. A selector contains
    // its byte index plus three low bits for table/privilege selection. Our
    // current placements require those bits clear (GDT, privilege level zero).
    private const int GdtDescriptorBytes = 8;
    private const int SelectorTableAndPrivilegeMask = 0x07;
    private const int WordBytes = sizeof(ushort);
    private const int FarPointerBytes = 2 * WordBytes;
    private const int SegmentAddressSpaceBytes = 1 << 16;

    // Loader policy, not NE limits: use at most 16 separate 64 KiB slots.
    // Starting above zero leaves the null selector and low memory unused.
    private const int MaximumPreparedSegments = 16;
    private const ushort DefaultFirstSelector = GdtDescriptorBytes;
    private const uint DefaultFirstLinearBase = SegmentAddressSpaceBytes;
    private const uint SlotAlignmentMask = SegmentAddressSpaceBytes - 1;
    private const ushort UnsupportedHeaderFlags = NeFormat.HeaderFlags.Win32 |
        NeFormat.HeaderFlags.SelfLoading | NeFormat.HeaderFlags.LinkErrors;

    // Recognized compiler prologues are instructions, not NE format flags.
    private const int ExportPrologueBytes = 3;
    private const byte MovAxImmediateOpcode = 0xB8;
    private static ReadOnlySpan<byte> PushDsPopAxNop => [0x1E, 0x58, 0x90];
    private static ReadOnlySpan<byte> MovAxDsNop => [0x8C, 0xD8, 0x90];

    /// <summary>Choose a simple deterministic layout; this assigns addresses, not CPU memory.</summary>
    public static IReadOnlyList<NeSegmentPlacement> PlaceSegments(NeImage image,
        ushort firstSelector = DefaultFirstSelector, uint firstLinearBase = DefaultFirstLinearBase)
    {
        if (image.Segments.Count is < 1 or > MaximumPreparedSegments || firstSelector == 0 ||
            (firstSelector & SelectorTableAndPrivilegeMask) != 0 || (firstLinearBase & SlotAlignmentMask) != 0)
            throw new ArgumentException("Use 1-16 segments, an aligned non-null GDT selector, and a 64 KiB aligned base.");
        return Array.AsReadOnly(image.Segments.Select((s, i) => new NeSegmentPlacement(s.Number,
            checked((ushort)(firstSelector + i * GdtDescriptorBytes)),
            checked(firstLinearBase + (uint)i * SegmentAddressSpaceBytes))).ToArray());
    }

    public FarPointer16 ResolveCode(NeAddress address)
    {
        PreparedNeSegment segment = Segments.Single(s => s.Source.Number == address.SegmentNumber);
        if (segment.Source.IsData || address.Offset >= segment.Source.FileBytes)
            throw new InvalidDataException($"{address} is not stored code.");
        return new(segment.Placement.Selector, address.Offset);
    }

    public static NeLoadPlan Create(byte[] file, IReadOnlyList<NeSegmentPlacement> placements,
        IReadOnlyList<NeImportBinding> bindings) => CreateWithImportResolver(file, placements, import =>
            bindings.SingleOrDefault(b => SameImport(b.Import, import))?.Address
            ?? throw new NotSupportedException($"Unbound import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}."));

    // Address resolution is separate from ABI/handler implementation. Tutorial 06
    // supplies real host bindings; tutorial 07 supplies explicitly non-callable
    // demonstration addresses. An unresolved import must throw, not become zero.
    // onPatch observes each completed write to our private copy, useful for a
    // debugger or an interactive lesson. Failure returns no partial load plan.
    public static NeLoadPlan CreateWithImportResolver(byte[] file,
        IReadOnlyList<NeSegmentPlacement> placements, Func<NeImport, FarPointer16> resolveImport,
        Action<NePatch>? onPatch = null)
    {
        NeImage image = NeReader.Read(file);
        // This milestone uses one shared automatic data segment, not task-local
        // instances, self-loading DLLs, Win32, or executable startup conventions.
        if (!image.Header.IsLibrary ||
            (image.Header.Flags & NeFormat.HeaderFlags.DataModeMask) != NeFormat.HeaderFlags.SingleData ||
            (image.Header.Flags & UnsupportedHeaderFlags) != 0 || image.Header.AutomaticDataSegment == 0 ||
            image.Header.StackBytes != 0 || image.Header.StackSegment != 0)
            throw new NotSupportedException("Loader requires a single-data Win16 DLL without a private stack, self-loader, Win32, or link-error flags.");
        if (placements.Count != image.Segments.Count || placements.Count > MaximumPreparedSegments ||
            placements.Select(p => p.Number).Distinct().Count() != placements.Count ||
            placements.Select(p => p.Selector).Distinct().Count() != placements.Count ||
            placements.Any(p => p.Selector == 0 || (p.Selector & SelectorTableAndPrivilegeMask) != 0 ||
                (p.LinearBase & SlotAlignmentMask) != 0 || p.LinearBase > uint.MaxValue - SegmentAddressSpaceBytes) ||
            placements.Select(p => p.LinearBase).Distinct().Count() != placements.Count)
            throw new ArgumentException("Give each segment a unique GDT selector and nonoverlapping 64 KiB aligned slot.", nameof(placements));

        // Pass one: give every file segment its own writable copy and assigned
        // runtime address. Forward references work because we prepare ALL segments
        // before patching any reference. These arrays are not yet Unicorn memory.
        var segments = new List<PreparedNeSegment>();
        foreach (NeSegment source in image.Segments)
        {
            NeSegmentPlacement placement = placements.SingleOrDefault(p => p.Number == source.Number)
                ?? throw new ArgumentException($"Missing placement for S{source.Number}.", nameof(placements));
            int size = Math.Max(source.MinimumAllocationBytes, source.FileBytes);
            if (source.Number == image.Header.AutomaticDataSegment)
            {
                if (!source.IsData) throw new InvalidDataException("Automatic data segment must be data.");
                size += image.Header.HeapBytes; // Reserve the header's heap request after static data.
            }
            if (size is < 1 or > SegmentAddressSpaceBytes)
                throw new NotSupportedException($"S{source.Number} needs {size} bytes; segment limit is 64 KiB.");
            byte[] bytes = new byte[size]; // Zero-fill allocation not backed by bytes in the file.
            if (source.FileOffset is int start)
                file.AsSpan(start, source.FileBytes).CopyTo(bytes);
            segments.Add(new(source, placement, bytes));
        }

        // The file uses segment numbers (S1, S2, ...), not our selectors. This
        // lookup reconnects those identities to the placements chosen by the host.
        var byNumber = segments.ToDictionary(s => s.Source.Number);
        var entries = image.Entries.ToDictionary(e => e.Ordinal);
        var patches = new List<NePatch>();
        var written = new HashSet<(ushort Segment, int Offset)>();
        // Pass two: relocation records tell us WHERE an address field lives and
        // WHAT it must point to. We use the tables as instructions for editing
        // copied code/data; we do not update the original file's tables.
        foreach (NeRelocation relocation in image.Relocations)
        {
            if (relocation.Kind == NeFormat.RelocationFlags.OsFixup ||
                (relocation.Flags & ~NeFormat.RelocationFlags.KindMask) != 0 ||
                relocation.AddressType is not (NeFormat.AddressTypes.Selector16 or
                    NeFormat.AddressTypes.FarPointer16 or NeFormat.AddressTypes.Offset16))
                throw new NotSupportedException($"S{relocation.SegmentNumber}:{relocation.SourceOffset:X4}: " +
                    "only non-additive internal/imported selector, offset16, and far16:16 relocations are implemented.");
            (FarPointer16 target, string reason) = ResolveTarget(relocation);
            PreparedNeSegment segment = byNumber[relocation.SegmentNumber];
            int width = relocation.AddressType == NeFormat.AddressTypes.FarPointer16 ? FarPointerBytes : WordBytes;

            // Several fields can refer to the same destination. NE chains those
            // sites together using their first word as a temporary "next site"
            // link. Save that link BEFORE replacing it with the resolved address,
            // or we lose where to patch next. The last link is EndOfRelocationChain.
            ushort offset = relocation.SourceOffset;
            if (offset == NeFormat.EndOfRelocationChain) throw new InvalidDataException("NE relocation has no initial patch site.");
            var visited = new HashSet<ushort>();
            while (offset != NeFormat.EndOfRelocationChain)
            {
                if (!visited.Add(offset)) throw new InvalidDataException("Cyclic NE relocation chain.");
                if (offset > segment.Source.FileBytes - width)
                    throw new InvalidDataException("NE fixup lies outside stored segment bytes.");
                ushort next = BinaryPrimitives.ReadUInt16LittleEndian(segment.Bytes.AsSpan(offset, WordBytes));
                byte[] replacement = new byte[width];
                // AddressType says which PART of the resolved address this field
                // stores. Not every relocation replaces a complete far pointer.
                switch (relocation.AddressType)
                {
                    case NeFormat.AddressTypes.Selector16:
                        BinaryPrimitives.WriteUInt16LittleEndian(replacement, target.Selector);
                        break;
                    case NeFormat.AddressTypes.Offset16:
                        BinaryPrimitives.WriteUInt16LittleEndian(replacement, target.Offset);
                        break;
                    case NeFormat.AddressTypes.FarPointer16:
                        // We write selector:offset in prose, but x86 stores the
                        // OFFSET word first, then the SELECTOR word in memory.
                        BinaryPrimitives.WriteUInt16LittleEndian(replacement, target.Offset);
                        BinaryPrimitives.WriteUInt16LittleEndian(replacement.AsSpan(WordBytes), target.Selector);
                        break;
                }
                Patch(segment, offset, replacement, reason, relocation, target, next);
                offset = next;
            }
        }

        // Separate from relocation records: these recognized Win16 export
        // prologues need the DLL's data selector so they can establish DS even
        // when called from another module whose DS points somewhere else.
        ushort dataSelector = segments.Single(s => s.Source.Number == image.Header.AutomaticDataSegment).Placement.Selector;
        foreach (NeEntry entry in image.Entries.Where(e => e.Exported && e.SharedData && e.Address is not null))
        {
            NeAddress address = entry.Address!.Value;
            PreparedNeSegment segment = segments.Single(s => s.Source.Number == address.SegmentNumber);
            if (segment.Source.IsData || address.Offset > segment.Source.FileBytes - ExportPrologueBytes)
                throw new NotSupportedException("Shared-data export is not a supported code prologue.");
            ReadOnlySpan<byte> prefix = segment.Bytes.AsSpan(address.Offset, ExportPrologueBytes);
            if (!prefix.SequenceEqual(PushDsPopAxNop) && !prefix.SequenceEqual(MovAxDsNop))
                throw new NotSupportedException($"Export #{entry.Ordinal} has an unrecognized Win16 prologue.");
            // PUSH DS; POP AX; NOP (or MOV AX,DS; NOP) becomes MOV AX,dllSelector.
            // The existing prologue then saves caller DS and loads DLL DS from AX.
            byte[] replacement = new byte[ExportPrologueBytes];
            replacement[0] = MovAxImmediateOpcode;
            BinaryPrimitives.WriteUInt16LittleEndian(replacement.AsSpan(1), dataSelector);
            Patch(segment, address.Offset, replacement, $"export #{entry.Ordinal} establishes DLL DS");
        }
        return new(image, segments.AsReadOnly(), patches.AsReadOnly());

        (FarPointer16, string) ResolveTarget(NeRelocation relocation)
        {
            if (relocation.Kind is NeFormat.RelocationFlags.ImportByOrdinal or NeFormat.RelocationFlags.ImportByName)
            {
                NeImport import = relocation.Import!;
                FarPointer16 pointer = resolveImport(import);
                if (pointer.Selector == 0) throw new InvalidDataException("Import resolver returned a null selector.");
                return (pointer, $"import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}");
            }

            NeAddress address;
            string reason;
            if (relocation.Target1 == NeFormat.InternalEntryOrdinalMarker)
            {
                // A movable reference contains an ENTRY ORDINAL, not a segment
                // number. Internal entries need not carry the public export flag.
                // Here Target1 is the marker and Target2 selects an entry-table
                // row; that row supplies the actual segment number and offset.
                if (!entries.TryGetValue(relocation.Target2, out NeEntry? entry) || entry.Address is null)
                    throw new InvalidDataException($"Internal entry #{relocation.Target2} is missing or constant.");
                address = entry.Address.Value;
                reason = $"internal entry #{entry.Ordinal} -> {address}";
            }
            else
            {
                // Fixed references encode the segment in one byte; the high byte
                // is reserved. Selector-only references do not consume Target2.
                // Here Target1 is the segment number and Target2 is the offset.
                if (relocation.Target1 is 0 or > NeFormat.MaximumFixedSegmentNumber)
                    throw new InvalidDataException("Invalid internal segment number/reserved byte.");
                address = new(relocation.Target1,
                    relocation.AddressType == NeFormat.AddressTypes.Selector16 ? (ushort)0 : relocation.Target2);
                reason = $"internal fixed {address}";
            }
            if (!byNumber.TryGetValue(address.SegmentNumber, out PreparedNeSegment? destination) ||
                address.Offset >= destination.Bytes.Length)
                throw new InvalidDataException($"Internal target {address} is outside allocated segments.");
            // The offset stays relative to its segment. We replace the file's
            // segment identity with our selector, not with LinearBase + Offset:
            // the CPU will use the selector's descriptor to find that base later.
            return (new(destination.Placement.Selector, address.Offset), reason);
        }

        void Patch(PreparedNeSegment segment, ushort offset, byte[] replacement, string reason,
            NeRelocation? relocation = null, FarPointer16? target = null, ushort? next = null)
        {
            for (int i = 0; i < replacement.Length; i++)
                if (!written.Add((segment.Source.Number, offset + i)))
                    throw new InvalidDataException("Overlapping NE patches are unsupported.");
            byte[] before = segment.Bytes.AsSpan(offset, replacement.Length).ToArray();
            // This is the actual reconnection: overwrite an address field in our
            // copied segment. A field can be an instruction operand or a pointer
            // in data. Relocation does not move the method or rewrite CALL's opcode.
            replacement.CopyTo(segment.Bytes, offset);
            var patch = new NePatch(new(segment.Source.Number, offset), reason, before, replacement, relocation, target, next);
            patches.Add(patch);
            onPatch?.Invoke(patch);
        }
    }

    private static bool SameImport(NeImport a, NeImport b) =>
        string.Equals(a.Module, b.Module, StringComparison.OrdinalIgnoreCase) &&
        a.Ordinal == b.Ordinal && string.Equals(a.Name, b.Name, StringComparison.Ordinal);
}
