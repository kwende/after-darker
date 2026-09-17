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
public sealed record NePatch(NeAddress Source, string Reason, byte[] Before, byte[] After);

/// <summary>
/// A small, CPU-independent loading step: copy segment bytes, resolve imported
/// far pointers, and fix single-data export prologues. Nothing executes here.
/// Unsupported relocation forms fail instead of being silently ignored.
/// </summary>
public sealed record NeLoadPlan(NeImage Image, IReadOnlyList<PreparedNeSegment> Segments,
    IReadOnlyList<NePatch> Patches)
{
    public FarPointer16 ResolveCode(NeAddress address)
    {
        PreparedNeSegment segment = Segments.Single(s => s.Source.Number == address.SegmentNumber);
        if (segment.Source.IsData || address.Offset >= segment.Source.FileBytes)
            throw new InvalidDataException($"{address} is not stored code.");
        return new(segment.Placement.Selector, address.Offset);
    }

    public static NeLoadPlan Create(byte[] file, IReadOnlyList<NeSegmentPlacement> placements,
        IReadOnlyList<NeImportBinding> bindings)
    {
        NeImage image = NeReader.Read(file);
        // This milestone uses one shared automatic data segment, not task-local
        // instances, self-loading DLLs, Win32, or executable startup conventions.
        // Flags: 0001 single data; 0010 Win32; 0800 self-loader; 2000 link errors.
        if (!image.Header.IsLibrary || (image.Header.Flags & 3) != 1 ||
            (image.Header.Flags & 0x2810) != 0 || image.Header.AutomaticDataSegment == 0 ||
            image.Header.StackBytes != 0 || image.Header.StackSegment != 0)
            throw new NotSupportedException("Loader requires a single-data Win16 DLL without a private stack, self-loader, Win32, or link-error flags.");
        if (placements.Count != image.Segments.Count || placements.Count > 16 ||
            placements.Select(p => p.Number).Distinct().Count() != placements.Count ||
            placements.Select(p => p.Selector).Distinct().Count() != placements.Count ||
            placements.Any(p => p.Selector == 0 || (p.Selector & 7) != 0 ||
                (p.LinearBase & 0xFFFF) != 0 || p.LinearBase > uint.MaxValue - 65536) ||
            placements.Select(p => p.LinearBase).Distinct().Count() != placements.Count)
            throw new ArgumentException("Give each segment a unique GDT selector and nonoverlapping 64 KiB aligned slot.", nameof(placements));

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
            if (size is < 1 or > 65536)
                throw new NotSupportedException($"S{source.Number} needs {size} bytes; segment limit is 64 KiB.");
            byte[] bytes = new byte[size]; // Zero-fill allocation not backed by bytes in the file.
            if (source.FileOffset is int start)
                file.AsSpan(start, source.FileBytes).CopyTo(bytes);
            segments.Add(new(source, placement, bytes));
        }

        var patches = new List<NePatch>();
        var written = new HashSet<(ushort Segment, int Offset)>();
        foreach (NeRelocation relocation in image.Relocations)
        {
            if (relocation.Kind is not (1 or 2) || relocation.AddressType != 3 || relocation.Additive)
                throw new NotSupportedException($"S{relocation.SegmentNumber}:{relocation.SourceOffset:X4}: " +
                    "only non-additive imported 16:16 far-pointer relocations are implemented.");
            NeImport import = relocation.Import!;
            NeImportBinding binding = bindings.SingleOrDefault(b => SameImport(b.Import, import))
                ?? throw new NotSupportedException($"Unbound import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}.");
            PreparedNeSegment segment = segments.Single(s => s.Source.Number == relocation.SegmentNumber);

            // The first WORD at each patch site is a LINK to the next patch site,
            // not yet an instruction operand. Read it BEFORE overwriting it.
            ushort offset = relocation.SourceOffset;
            var visited = new HashSet<ushort>();
            while (offset != 0xFFFF)
            {
                if (!visited.Add(offset)) throw new InvalidDataException("Cyclic NE relocation chain.");
                if (offset > segment.Source.FileBytes - 4)
                    throw new InvalidDataException("NE far-pointer fixup lies outside stored segment bytes.");
                ushort next = BinaryPrimitives.ReadUInt16LittleEndian(segment.Bytes.AsSpan(offset, 2));
                byte[] replacement = new byte[4];
                BinaryPrimitives.WriteUInt16LittleEndian(replacement, binding.Address.Offset);
                BinaryPrimitives.WriteUInt16LittleEndian(replacement.AsSpan(2), binding.Address.Selector);
                Patch(segment, offset, replacement, $"import {binding.Name}");
                offset = next;
            }
        }

        ushort dataSelector = segments.Single(s => s.Source.Number == image.Header.AutomaticDataSegment).Placement.Selector;
        foreach (NeEntry entry in image.Entries.Where(e => e.Exported && e.SharedData && e.Address is not null))
        {
            NeAddress address = entry.Address!.Value;
            PreparedNeSegment segment = segments.Single(s => s.Source.Number == address.SegmentNumber);
            if (segment.Source.IsData || address.Offset > segment.Source.FileBytes - 3)
                throw new NotSupportedException("Shared-data export is not a supported code prologue.");
            ReadOnlySpan<byte> prefix = segment.Bytes.AsSpan(address.Offset, 3);
            if (!prefix.SequenceEqual(new byte[] { 0x1E, 0x58, 0x90 }) &&
                !prefix.SequenceEqual(new byte[] { 0x8C, 0xD8, 0x90 }))
                throw new NotSupportedException($"Export #{entry.Ordinal} has an unrecognized Win16 prologue.");
            // PUSH DS; POP AX; NOP (or MOV AX,DS; NOP) becomes MOV AX,dllSelector.
            // The existing prologue then saves caller DS and loads DLL DS from AX.
            byte[] replacement = [0xB8, (byte)dataSelector, (byte)(dataSelector >> 8)];
            Patch(segment, address.Offset, replacement, $"export #{entry.Ordinal} establishes DLL DS");
        }
        return new(image, segments.AsReadOnly(), patches.AsReadOnly());

        void Patch(PreparedNeSegment segment, ushort offset, byte[] replacement, string reason)
        {
            for (int i = 0; i < replacement.Length; i++)
                if (!written.Add((segment.Source.Number, offset + i)))
                    throw new InvalidDataException("Overlapping NE patches are unsupported.");
            byte[] before = segment.Bytes.AsSpan(offset, replacement.Length).ToArray();
            replacement.CopyTo(segment.Bytes, offset);
            patches.Add(new(new(segment.Source.Number, offset), reason, before, replacement));
        }
    }

    private static bool SameImport(NeImport a, NeImport b) =>
        string.Equals(a.Module, b.Module, StringComparison.OrdinalIgnoreCase) &&
        a.Ordinal == b.Ordinal && string.Equals(a.Name, b.Name, StringComparison.Ordinal);
}
