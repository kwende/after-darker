using System.Security.Cryptography;
using AfterDarker.Core.Ne;
using AfterDarker.Tutorials.Fixtures;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Two passes, visible in the debugger: assign all destinations, then patch
/// their references. Prepared segments are ordinary C# arrays. No CPU executes.
/// </summary>
public sealed class Tutorial07Relocations(string? inputPath = null, bool step = false,
    TextReader? input = null, TextWriter? output = null) : ITutorial
{
    private const int GdtDescriptorBytes = 8;
    // Arbitrary demonstration layout: reserve the first 256 bytes, then give
    // each import a distinct address 16 bytes apart. NE does not mandate this.
    private const int FirstImportOffset = 0x100;
    private const int ImportSlotBytes = 16;
    private const int SegmentAddressSpaceBytes = 1 << 16;
    private const int MaximumImportSlots = (SegmentAddressSpaceBytes - FirstImportOffset) / ImportSlotBytes;

    public string Id => "07";
    public string Title => "Reconnect NE references: segment map -> target lookup -> patched bytes";

    public void Run()
    {
        TextWriter writer = output ?? Console.Out;
        TextReader reader = input ?? Console.In;
        string? path = inputPath;
        if (path == "--file")
        {
            writer.Write("Path to a local Windows NE library (.AD / .DLL): ");
            path = reader.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A local module path is required.");
        }
        byte[] bytes;
        if (path is null)
        {
            writer.WriteLine("Using the original built-in metadata demo; its bytes are not executable code.");
            bytes = RelocationDemo.Create();
        }
        else
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("NE input exceeds 64 MiB.");
            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
        }

        // Put a breakpoint in Execute, then in NeLoadPlan.ResolveTarget/Patch.
        // --step pauses AFTER each patch has been written to the private copy.
        Execute(bytes, writer, step ? Pause : null);
        void Pause(NePatch patch)
        {
            writer.Write("Enter: next patch; q: stop: ");
            string? answer = reader.ReadLine();
            if (answer is null || answer.Equals("q", StringComparison.OrdinalIgnoreCase))
                throw new OperationCanceledException("Relocation walkthrough stopped; source file is unchanged.");
        }
    }

    public static Result Execute(byte[] file, TextWriter? output = null, Action<NePatch>? afterPatch = null)
    {
        TextWriter writer = output ?? TextWriter.Null;
        NeImage image = NeReader.Read(file);
        string hash = Convert.ToHexString(SHA256.HashData(file));
        writer.WriteLine($"Input SHA-256: {hash}");
        writer.WriteLine("PASS 1: assign ALL segment destinations before resolving any reference");
        IReadOnlyList<NeSegmentPlacement> placements = NeLoadPlan.PlaceSegments(image);
        foreach (NeSegmentPlacement p in placements)
            writer.WriteLine($"  S{p.Number} -> selector {p.Selector:X4}, linear base 0x{p.LinearBase:X5}");

        // A load-only demonstration can assign addresses without claiming to
        // implement the imported methods. No dummy argument count/ABI is invented.
        // A future executor must replace this resolver with actual host bindings.
        // PlaceSegments uses consecutive GDT entries starting after the null
        // entry. The next entry after the module segments is our import segment.
        ushort importSelector = checked((ushort)((placements.Count + 1) * GdtDescriptorBytes));
        NeImport[] imports = image.Imports.ToArray();
        if (imports.Length > MaximumImportSlots) throw new NotSupportedException("Import demonstration slots exceed one 64 KiB segment.");
        var addresses = imports.Select((import, i) => new ImportAddress(import,
            new(importSelector, checked((ushort)(FirstImportOffset + i * ImportSlotBytes))))).ToArray();
        var byImport = addresses.ToDictionary(a => a.Import, a => a.Address);
        writer.WriteLine("IMPORT ADDRESSES ONLY: these slots have NO implementation, ABI, or executable memory");
        foreach (ImportAddress a in addresses)
            writer.WriteLine($"  {Escape(a.Import.Module)}!{Escape(a.Import.Name ?? $"#{a.Import.Ordinal}")} -> {a.Address}");

        writer.WriteLine("PASS 2: resolve each target, read the chain link, then write the replacement");
        int patchCount = 0;
        NeLoadPlan plan = NeLoadPlan.CreateWithImportResolver(file, placements,
            import => byImport.TryGetValue(import, out FarPointer16 address) ? address
                : throw new NotSupportedException($"Unresolved import {import}."),
            patch =>
            {
                string form = patch.Relocation?.AddressType switch
                {
                    NeFormat.AddressTypes.Selector16 => "selector16",
                    NeFormat.AddressTypes.FarPointer16 => "far16:16",
                    NeFormat.AddressTypes.Offset16 => "offset16",
                    _ => "export prologue",
                };
                writer.WriteLine($"  Patch {++patchCount}: {patch.Source} [{form}]");
                NeSegmentPlacement sourcePlacement = placements.Single(p => p.Number == patch.Source.SegmentNumber);
                writer.WriteLine($"    Write destination: {sourcePlacement.Selector:X4}:{patch.Source.Offset:X4}, " +
                    $"planned linear 0x{sourcePlacement.LinearBase + patch.Source.Offset:X5}");
                writer.WriteLine($"    Target lookup: {Escape(patch.Reason)}");
                if (patch.Target is FarPointer16 target)
                    writer.WriteLine($"    Resolved address: {target} (write only the part required by {form})");
                if (patch.NextOffset is ushort next)
                    writer.WriteLine($"    Saved chain link BEFORE overwrite: {(next == NeFormat.EndOfRelocationChain ? "FFFF = end" : $"{next:X4} = next source offset")}");
                writer.WriteLine($"    Before: {Hex(patch.Before)} -> After: {Hex(patch.After)}");
                afterPatch?.Invoke(patch);
            });

        int internalRecords = image.Relocations.Count(r => r.Kind == NeFormat.RelocationFlags.Internal);
        int importedRecords = image.Relocations.Count(r => r.Kind is
            NeFormat.RelocationFlags.ImportByOrdinal or NeFormat.RelocationFlags.ImportByName);
        int relocationWrites = plan.Patches.Count(p => p.Relocation is not null);
        writer.WriteLine($"Prepared {plan.Segments.Count} segments: {internalRecords} internal + " +
            $"{importedRecords} imported records -> {relocationWrites} relocation writes; " +
            $"{plan.Patches.Count - relocationWrites} export-prologue writes.");
        writer.WriteLine("PASS: references patched in private C# arrays; source unchanged. No CPU, initialization, or API execution.");
        return new(plan, Array.AsReadOnly(addresses), hash);
    }

    public sealed record ImportAddress(NeImport Import, FarPointer16 Address);
    public sealed record Result(NeLoadPlan Plan, IReadOnlyList<ImportAddress> Imports, string SourceSha256);
    private static string Hex(byte[] bytes) => string.Join(" ", bytes.Select(b => b.ToString("X2")));
    private static string Escape(string text) => string.Concat(text.Select(c => char.IsControl(c) ? $"\\x{(int)c:X2}" : c.ToString()));
}
