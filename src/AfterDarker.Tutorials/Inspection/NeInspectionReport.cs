using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Tutorials.Inspection;

/// <summary>Presentation and After Dark context layered over generic, typed NE metadata.</summary>
public static class NeInspectionReport
{
    public static void Write(NeImage image, TextWriter output)
    {
        NeHeader h = image.Header;
        output.WriteLine("FILE FACTS (no guest execution)");
        output.WriteLine($"Windows NE {(h.IsLibrary ? "library" : "executable")}; header at 0x{h.FileOffset:X}; flags 0x{h.Flags:X4}");
        output.WriteLine($"Expected Windows version: {h.ExpectedWindowsVersion >> 8}.{h.ExpectedWindowsVersion & 255:D2}");
        output.WriteLine($"Header startup: {h.Startup?.ToString() ?? "none"}");
        output.WriteLine($"Automatic data segment: {h.AutomaticDataSegment}; initial heap: {h.HeapBytes} bytes; stack request: {h.StackBytes} bytes");
        output.WriteLine($"Header stack: segment {h.StackSegment}, SP=0x{h.StackPointer:X4} (zero segment does not allocate a host stack)");
        foreach (NeName name in image.Names.Where(n => n.Ordinal == 0))
            output.WriteLine($"Module {(name.Resident ? "name" : "description")}: {Escape(name.Text)}");

        output.WriteLine("\nSEGMENTS (numbers in the file, not runtime selectors)");
        foreach (NeSegment segment in image.Segments)
            output.WriteLine($"  S{segment.Number}: {(segment.IsData ? "data" : "code")}; file={Offset(segment.FileOffset)}; " +
                $"stored={segment.FileBytes}; minimum allocation={segment.MinimumAllocationBytes}; flags=0x{segment.Flags:X4}");

        output.WriteLine("\nENTRY TABLE (export flag distinguishes public entries from internal entries)");
        ILookup<ushort, NeName> namesByOrdinal = image.Names.ToLookup(n => n.Ordinal);
        foreach (NeEntry entry in image.Entries)
        {
            string names = string.Join(", ", namesByOrdinal[entry.Ordinal].Select(n => Escape(n.Text)).Distinct());
            string target = entry.Address?.ToString() ?? $"constant 0x{entry.ConstantValue:X4}";
            output.WriteLine($"  #{entry.Ordinal} {(entry.Exported ? "export" : "internal")} " +
                $"{(names.Length == 0 ? "(unnamed)" : names)} -> {target}; " +
                $"{(entry.ConstantValue is not null ? "constant" : entry.Movable ? "movable" : "fixed")}; flags=0x{entry.Flags:X2}; " +
                $"shared-data={entry.SharedData}; parameter-word hint={entry.ParameterWordCount}");
        }
        output.WriteLine("  Entry flags do not supply a complete function signature or prove an entry is callable code.");

        output.WriteLine("\nIMPORTS (identities come from relocation records; these may reference code, data, or constants)");
        output.WriteLine($"  Module references: {string.Join(", ", image.ModuleReferences.Select(Escape))}");
        ILookup<NeImport, NeRelocation> fixupsByImport = image.Relocations
            .Where(r => r.Import is not null).ToLookup(r => r.Import!);
        foreach (NeImport import in image.Imports)
        {
            string identity = import.Ordinal is ushort ordinal
                ? $"#{ordinal}" + Win16OrdinalReference.Describe(import.Module, ordinal)
                : Escape(import.Name!);
            output.WriteLine($"  {Escape(import.Module)}!{identity}");
            foreach (NeRelocation fixup in fixupsByImport[import])
                output.WriteLine($"    fixup head S{fixup.SegmentNumber}:{fixup.SourceOffset:X4}; " +
                    $"record=0x{fixup.RecordFileOffset:X}; address type=0x{fixup.AddressType:X2}; " +
                    $"{(fixup.Additive ? "additive" : "chain")}; flags=0x{fixup.Flags:X2}");
        }
        output.WriteLine($"  {image.Imports.Count} distinct imports; {image.Relocations.Count} total relocation records " +
            $"({image.Relocations.Count(r => r.Kind == 0)} internal, {image.Relocations.Count(r => r.Kind == 3)} OS fixups).");
        output.WriteLine("  Fixup offsets are patch locations, not method entry points or call counts. Chains are not expanded here.");
        output.WriteLine("  Imported method addresses require the dependency's exports or a host gateway binding.");
        output.WriteLine("  Ordinal annotations: existing Wine 10.0 census subset; absent annotations stay unknown. ABI support is not implied.");
        output.WriteLine("  Reference kinds describe Wine declarations: 'stub' does not mean the guest may skip that service.");

        output.WriteLine("\nRESOURCES (identifiers and stored ranges; payloads are not decoded)");
        if (image.Resources.Count == 0) output.WriteLine("  None.");
        foreach (NeResource resource in image.Resources)
            output.WriteLine($"  type={ResourceType(resource.Type)}, id={Id(resource.Id)}; " +
                $"file=0x{resource.FileOffset:X}; stored length={resource.Length}; flags=0x{resource.Flags:X4}");
        output.WriteLine("  Numeric STRING identifiers designate string-table blocks, not individual strings.");
        output.WriteLine("  A bitmap/icon/custom resource's presence does not prove when or how the module uses it.");

        WriteAfterDarkContract(image, output);
        output.WriteLine("\nPASS: NE metadata read and reported. No segments loaded, relocations applied, or original code executed.");
    }

    private static void WriteAfterDarkContract(NeImage image, TextWriter output)
    {
        output.WriteLine("\nCALLING REQUIREMENTS (external Win16 / After Dark contract, not NE signatures)");
        output.WriteLine("  Loader: allocate segments/selectors, initialize data/heap/stack, and resolve imports and relocations first.");
        output.WriteLine("  Runtime address: CS = selector assigned to S<n>; IP = the reported offset. Do not put the segment number into CS.");
        output.WriteLine("  Calls also need DS, SS:SP, arguments, and a valid far return address; setting IP alone is insufficient.");
        if (image.Header.IsLibrary && image.Header.Startup is NeAddress startup)
            output.WriteLine($"  DLL initialization: enter {startup} using the Win16 DLL startup contract (not the MODULE arguments).");
        else
            output.WriteLine("  No DLL startup address identified; do not substitute the screensaver's INITIALIZE message.");

        AfterDarkCallPlan? plan = AfterDarkCallPlan.FromImage(image);
        if (plan is null)
        {
            output.WriteLine("  No code export named MODULE in a library: no After Dark frame entry identified. No ordinal-1 guess is made.");
            return;
        }
        output.WriteLine($"  Candidate After Dark dispatcher: MODULE, ordinal {plan.DispatcherOrdinal}, {plan.Dispatcher}.");
        output.WriteLine("  Assumed SDK contract: int FAR PASCAL Module(int message, HDC surface, HANDLE systemRecord).");
        output.WriteLine("  Three 16-bit values, pushed left-to-right; return AX; callee removes 6 argument bytes (RETF 6).");
        output.WriteLine("  At entry: [SS:SP]=return IP, +2=return CS, +4=system handle, +6=HDC, +8=message.");
        output.WriteLine("  Supply valid guest system/module records and handles. Their full layouts are not defined by NE metadata.");
        foreach (AfterDarkInvocation call in plan.Invocations)
            output.WriteLine($"    {call.Message.ToString().ToUpperInvariant()}{(call.Repeat ? " (repeat)" : "")}: " +
                $"enter {call.EntryPoint}, message={(ushort)call.Message}");
        output.WriteLine("  This is a contract-based plan, not disassembly or execution proof. Return meanings vary by message/module.");
        output.WriteLine("  A frame call can return without drawing; preserve guest state and surface pixels between calls.");
    }

    // Never let artifact strings emit terminal control sequences.
    private static string Escape(string text) => string.Concat(text.Select(c => char.IsControl(c) ? $"\\x{(int)c:X2}" : c.ToString()));
    private static string Offset(int? offset) => offset is int value ? $"0x{value:X}" : "none (zero-filled)";
    private static string Id(NeResourceIdentifier id) => id.Number is ushort number ? $"#{number}" : $"\"{Escape(id.Name!)}\"";
    private static string ResourceType(NeResourceIdentifier type)
    {
        string? known = type.Number switch
        {
            1 => "CURSOR", 2 => "BITMAP", 3 => "ICON", 4 => "MENU", 5 => "DIALOG", 6 => "STRING",
            7 => "FONTDIR", 8 => "FONT", 9 => "ACCELERATOR", 10 => "RCDATA", 12 => "GROUP_CURSOR",
            14 => "GROUP_ICON", 16 => "VERSION", _ => null,
        };
        return Id(type) + (known is null ? "" : $" ({known})");
    }
}
