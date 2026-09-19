using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>
/// Import identities and ABI metadata, separate from Win16Api implementations.
/// This is the currently supported subset, not a complete Windows registry.
/// Drawing/error imports still resolve to addresses, but fail by name if reached.
/// </summary>
public static class Win16Imports
{
    public enum Handler { Unsupported, LocalInitReservation, GlobalLock, GlobalUnlock, Environment, Ticks }
    public sealed record ImportEntry(NeImport Import, string Name, FarPointer16 Address,
        Handler Implementation, int? ArgumentBytes, Win16ReturnLayout? ReturnLayout);
    public sealed record Reply(uint Value, ushort? Cx = null);

    public static IReadOnlyList<ImportEntry> BindImports(NeImage image, ushort gateway)
    {
        // Ordinals/signatures: Wine 10.0 krnl386.exe16.spec and user.exe16.spec.
        // Each synthetic address is OUR choice; its selector is a code gateway.
        // Unsupported entries deliberately have no guessed marshaling contract.
        var definitions = new (string Module, ushort Ordinal, string Name, Handler Handler, int? Bytes, Win16ReturnLayout? Return)[]
        {
            ("KERNEL", 4, "LocalInit", Handler.LocalInitReservation, 6, Win16ReturnLayout.WordInAx),
            ("KERNEL", 18, "GlobalLock", Handler.GlobalLock, 2, Win16ReturnLayout.DwordInDxAx),
            ("KERNEL", 19, "GlobalUnlock", Handler.GlobalUnlock, 2, Win16ReturnLayout.WordInAx),
            ("KERNEL", 131, "GetDOSEnvironment", Handler.Environment, 0, Win16ReturnLayout.DwordInDxAx),
            ("USER", 13, "GetTickCount", Handler.Ticks, 0, Win16ReturnLayout.DwordInDxAx),
            ("KERNEL", 1, "FatalExit", Handler.Unsupported, null, null),
            ("KERNEL", 115, "OutputDebugString", Handler.Unsupported, null, null),
            ("KERNEL", 137, "FatalAppExit", Handler.Unsupported, null, null),
            ("GDI", 1, "SetBkColor", Handler.Unsupported, null, null),
            ("GDI", 2, "SetBkMode", Handler.Unsupported, null, null),
            ("GDI", 9, "SetTextColor", Handler.Unsupported, null, null),
            ("GDI", 33, "TextOut", Handler.Unsupported, null, null),
            ("GDI", 87, "GetStockObject", Handler.Unsupported, null, null),
            ("GDI", 346, "SetTextAlign", Handler.Unsupported, null, null),
            ("USER", 72, "SetRect", Handler.Unsupported, null, null),
            ("USER", 81, "FillRect", Handler.Unsupported, null, null),
            ("USER", 82, "InvertRect", Handler.Unsupported, null, null),
        };
        const int firstGatewayOffset = 0x100, gatewaySpacing = 0x10;
        return Array.AsReadOnly(image.Imports.Select(import =>
        {
            int index = Array.FindIndex(definitions, d => import.Name is null && d.Ordinal == import.Ordinal &&
                string.Equals(d.Module, import.Module, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new NotSupportedException($"Unrecognized Win16 import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}.");
            var d = definitions[index];
            return new ImportEntry(import, $"{d.Module}!{d.Name} (#{d.Ordinal})",
                new(gateway, (ushort)(firstGatewayOffset + index * gatewaySpacing)), d.Handler, d.Bytes, d.Return);
        }).ToArray());
    }

    // ABI adapter only: turn decoded words into typed arguments, and typed
    // results into register-sized values. Service behavior lives in Win16Api.
    public static Reply Invoke(Win16Api api, ImportEntry entry, ushort[] arguments)
    {
        if (entry.Implementation == Handler.Unsupported)
            throw new NotSupportedException($"{entry.Name} reached outside the supported initialization path.");
        if (arguments.Length * 2 != entry.ArgumentBytes)
            throw new ArgumentException($"Wrong argument count for {entry.Name}.");
        switch (entry.Implementation)
        {
            case Handler.LocalInitReservation:
                return new(api.LocalInit(arguments[0], arguments[1], arguments[2]) ? 1u : 0u);
            case Handler.GlobalLock:
                FarPointer16 pointer = api.GlobalLock(arguments[0]);
                // Win16 GlobalLock also returns its selector in CX.
                return new(Pack(pointer), pointer.Selector);
            case Handler.GlobalUnlock: return new(api.GlobalUnlock(arguments[0]));
            case Handler.Environment: return new(Pack(api.GetDOSEnvironment()));
            case Handler.Ticks: return new(api.GetTickCount());
            default: throw new NotSupportedException(entry.Name);
        }
    }
    private static uint Pack(FarPointer16 pointer) => ((uint)pointer.Selector << 16) | pointer.Offset;
}
