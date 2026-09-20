using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>
/// Import identities and ABI metadata, separate from Win16Api implementations.
/// This is the currently supported subset, not a complete Windows registry.
/// Drawing is opt-in; unsupported imports fail by name if reached.
/// </summary>
public static class Win16Imports
{
    /// <summary>Typed implementation identity; unsupported services retain a symbolic name without a guessed ABI.</summary>
    public enum Handler
    {
        Unsupported,
        LocalInit,
        LocalAlloc,
        LocalFree,
        LocalLock,
        LocalUnlock,
        GlobalLock,
        GlobalUnlock,
        Environment,
        Ticks,
        SetRect,
        PtInRect,
        GetStockObject,
        FillRect,
        InvertRect,
        CreatePen,
        SelectObject,
        DeleteObject,
        MoveTo,
        LineTo
    }
    /// <summary>One NE import bound to our synthetic code address and its known ABI.</summary>
    /// <param name="Import">Original module/ordinal or module/name identity.</param>
    /// <param name="Name">Human-readable symbol used in diagnostics.</param>
    /// <param name="Address">Host-chosen guest gateway address patched into the loaded DLL.</param>
    /// <param name="Implementation">Which supported C# service should run.</param>
    /// <param name="ArgumentBytes">Bytes removed by Pascal callee cleanup; null when unknown.</param>
    /// <param name="ReturnLayout">Register representation of the result; null when unknown.</param>
    public sealed record ImportEntry(NeImport Import, string Name, FarPointer16 Address,
        Handler Implementation, int? ArgumentBytes, Win16ReturnLayout? ReturnLayout);
    /// <summary>A service result before it is written to guest registers.</summary>
    /// <param name="Value">Word, DWORD or packed far pointer; ignored for void signatures.</param>
    /// <param name="Cx">Optional extra result: GlobalLock's selector or LocalAlloc's handle.</param>
    public sealed record Reply(uint Value, ushort? Cx = null);

    /// <summary>Resolve known imports to distinct gateway entries; no guest code or services execute here.</summary>
    /// <remarks>See docs/win16-implementations.md for adding a signature and its implementation.</remarks>
    public static IReadOnlyList<ImportEntry> BindImports(NeImage image, ushort gateway, bool enableDrawing = false)
        => BindImports(image.Imports, gateway, enableDrawing);

    /// <summary>Bind explicit import identities, also used by source-authored guest conformance programs.</summary>
    public static IReadOnlyList<ImportEntry> BindImports(IEnumerable<NeImport> imports, ushort gateway, bool enableDrawing = false)
    {
        // Ordinals/signatures: Wine 10.0 krnl386.exe16.spec and user.exe16.spec.
        // Each synthetic address is OUR choice; its selector is a code gateway.
        // Unsupported entries deliberately have no guessed marshaling contract.
        var definitions = new (string Module, ushort Ordinal, string Name, Handler Handler, int? Bytes, Win16ReturnLayout? Return)[]
        {
            ("KERNEL", 4, "LocalInit", Handler.LocalInit, 6, Win16ReturnLayout.WordInAx),
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
            ("GDI", 87, "GetStockObject", enableDrawing ? Handler.GetStockObject : Handler.Unsupported, 2, Win16ReturnLayout.WordInAx),
            ("GDI", 346, "SetTextAlign", Handler.Unsupported, null, null),
            ("USER", 72, "SetRect", enableDrawing ? Handler.SetRect : Handler.Unsupported, 12, Win16ReturnLayout.Void),
            ("USER", 81, "FillRect", enableDrawing ? Handler.FillRect : Handler.Unsupported, 8, Win16ReturnLayout.WordInAx),
            ("USER", 82, "InvertRect", enableDrawing ? Handler.InvertRect : Handler.Unsupported, 6, Win16ReturnLayout.Void),
            ("GDI", 61, "CreatePen", enableDrawing ? Handler.CreatePen : Handler.Unsupported, 8, Win16ReturnLayout.WordInAx),
            ("GDI", 45, "SelectObject", enableDrawing ? Handler.SelectObject : Handler.Unsupported, 4, Win16ReturnLayout.WordInAx),
            ("GDI", 69, "DeleteObject", enableDrawing ? Handler.DeleteObject : Handler.Unsupported, 2, Win16ReturnLayout.WordInAx),
            ("GDI", 20, "MoveTo", enableDrawing ? Handler.MoveTo : Handler.Unsupported, 6, Win16ReturnLayout.DwordInDxAx),
            ("GDI", 19, "LineTo", enableDrawing ? Handler.LineTo : Handler.Unsupported, 6, Win16ReturnLayout.WordInAx),
            // Geometry needs checked memory, but does not require a drawing surface.
            ("USER", 76, "PtInRect", Handler.PtInRect, 8, Win16ReturnLayout.WordInAx),
            // Fade Away imports these for other styles. Radar does not call them.
            // Bind their identities for relocation, but fail symbolically if a guest reaches them.
            ("GDI", 24, "Ellipse", Handler.Unsupported, null, null),
            ("GDI", 27, "Rectangle", Handler.Unsupported, null, null),
            ("GDI", 29, "PatBlt", Handler.Unsupported, null, null),
            ("KERNEL", 5, "LocalAlloc", Handler.LocalAlloc, 4, Win16ReturnLayout.WordInAx),
            ("KERNEL", 7, "LocalFree", Handler.LocalFree, 2, Win16ReturnLayout.WordInAx),
            ("KERNEL", 8, "LocalLock", Handler.LocalLock, 2, Win16ReturnLayout.DwordInDxAx),
            ("KERNEL", 9, "LocalUnlock", Handler.LocalUnlock, 2, Win16ReturnLayout.WordInAx),
        };
        const int firstGatewayOffset = 0x100, gatewaySpacing = 0x10;
        return Array.AsReadOnly(imports.Distinct().Select(import =>
        {
            int index = Array.FindIndex(definitions, definition => import.Name is null && definition.Ordinal == import.Ordinal &&
                string.Equals(definition.Module, import.Module, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new NotSupportedException($"Unrecognized Win16 import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}.");
            var definition = definitions[index];
            return new ImportEntry(import, $"{definition.Module}!{definition.Name} (#{definition.Ordinal})",
                new(gateway, (ushort)(firstGatewayOffset + index * gatewaySpacing)), definition.Handler, definition.Bytes, definition.Return);
        }).ToArray());
    }

}
