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
        LineTo,
        Ellipse,
        Rectangle,
        CreateSolidBrush,
        GetWindowOrg,
        OffsetRect,
        InflateRect,
        IntersectRect,
        EqualRect,
        SetROP2,
        SetWindowOrg,
        FrameRect,
        SetPixel,
        BitBlt,
        CreateCompatibleDC, CreateCompatibleBitmap, DeleteDC, PatBlt,
        SoundOpen, SoundClose, SoundAsyncCapability, SoundLoadResource, SoundSetMode, SoundPlay, SoundFree
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
        // Ordinals/signatures: Wine 10.0 krnl386.exe16.spec, user.exe16.spec and gdi.exe16.spec.
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
            // Hard Rain reaches Ellipse. Fade Away's Radar path still does not.
            ("GDI", 24, "Ellipse", enableDrawing ? Handler.Ellipse : Handler.Unsupported, 10, Win16ReturnLayout.WordInAx),
            ("GDI", 27, "Rectangle", enableDrawing ? Handler.Rectangle : Handler.Unsupported, 10, Win16ReturnLayout.WordInAx),
            // Gravity constructs its masks with PATCOPY; other PatBlt operations remain guarded.
            ("GDI", 29, "PatBlt", enableDrawing ? Handler.PatBlt : Handler.Unsupported, 14, Win16ReturnLayout.WordInAx),
            ("KERNEL", 5, "LocalAlloc", Handler.LocalAlloc, 4, Win16ReturnLayout.WordInAx),
            ("KERNEL", 7, "LocalFree", Handler.LocalFree, 2, Win16ReturnLayout.WordInAx),
            ("KERNEL", 8, "LocalLock", Handler.LocalLock, 2, Win16ReturnLayout.DwordInDxAx),
            ("KERNEL", 9, "LocalUnlock", Handler.LocalUnlock, 2, Win16ReturnLayout.WordInAx),
            // Wine 10.0 user.exe16.spec maps both clock ordinals to GetTickCount.
            ("USER", 15, "GetCurrentTime", Handler.Ticks, 0, Win16ReturnLayout.DwordInDxAx),
            ("GDI", 66, "CreateSolidBrush", enableDrawing ? Handler.CreateSolidBrush : Handler.Unsupported, 4, Win16ReturnLayout.WordInAx),
            ("GDI", 97, "GetWindowOrg", enableDrawing ? Handler.GetWindowOrg : Handler.Unsupported, 2, Win16ReturnLayout.DwordInDxAx),
            ("GDI", 11, "SetWindowOrg", enableDrawing ? Handler.SetWindowOrg : Handler.Unsupported, 6, Win16ReturnLayout.DwordInDxAx),
            ("GDI", 4, "SetROP2", enableDrawing ? Handler.SetROP2 : Handler.Unsupported, 4, Win16ReturnLayout.WordInAx),
            ("USER", 83, "FrameRect", enableDrawing ? Handler.FrameRect : Handler.Unsupported, 8, Win16ReturnLayout.WordInAx),
            ("GDI", 31, "SetPixel", enableDrawing ? Handler.SetPixel : Handler.Unsupported, 10, Win16ReturnLayout.DwordInDxAx),
            ("GDI", 34, "BitBlt", enableDrawing ? Handler.BitBlt : Handler.Unsupported, 20, Win16ReturnLayout.WordInAx),
            ("GDI", 35, "StretchBlt", Handler.Unsupported, null, null),
            // Win16 OffsetRect/InflateRect return void (unlike the Win32 BOOL declarations).
            ("USER", 77, "OffsetRect", Handler.OffsetRect, 8, Win16ReturnLayout.Void),
            ("USER", 78, "InflateRect", Handler.InflateRect, 8, Win16ReturnLayout.Void),
            ("USER", 79, "IntersectRect", Handler.IntersectRect, 12, Win16ReturnLayout.WordInAx),
            ("USER", 244, "EqualRect", Handler.EqualRect, 8, Win16ReturnLayout.WordInAx),
            ("GDI", 52, "CreateCompatibleDC", enableDrawing ? Handler.CreateCompatibleDC : Handler.Unsupported, 2, Win16ReturnLayout.WordInAx),
            ("GDI", 51, "CreateCompatibleBitmap", enableDrawing ? Handler.CreateCompatibleBitmap : Handler.Unsupported, 6, Win16ReturnLayout.WordInAx),
            ("GDI", 68, "DeleteDC", enableDrawing ? Handler.DeleteDC : Handler.Unsupported, 2, Win16ReturnLayout.WordInAx),
        };
        // Recovered AD_SND.H uses named FAR PASCAL exports. Every BOOL/HSOUND
        // return is a WORD; a resource name is a far pointer, not a host string.
        var soundDefinitions = new (string Name, Handler Handler, int Bytes)[]
        {
            ("ADWOPENSOUND", Handler.SoundOpen, 0), ("ADWCLOSESOUND", Handler.SoundClose, 2),
            ("ADWSOUNDASYNCCAP", Handler.SoundAsyncCapability, 0),
            ("ADWLOADSOUNDRESOURCE", Handler.SoundLoadResource, 6),
            ("ADWSETSOUNDMODE", Handler.SoundSetMode, 4), ("ADWPLAYSOUND", Handler.SoundPlay, 2),
            ("ADWFREESOUND", Handler.SoundFree, 2)
        };
        const int firstGatewayOffset = 0x100, gatewaySpacing = 0x10;
        return Array.AsReadOnly(imports.Distinct().Select(import =>
        {
            if (string.Equals(import.Module, "AD_SND", StringComparison.OrdinalIgnoreCase))
            {
                int soundIndex = Array.FindIndex(soundDefinitions, definition => import.Ordinal is null &&
                    string.Equals(definition.Name, import.Name, StringComparison.OrdinalIgnoreCase));
                if (soundIndex < 0) throw new NotSupportedException($"Unrecognized AD_SND import {import.Name ?? $"#{import.Ordinal}"}.");
                var sound = soundDefinitions[soundIndex];
                return new ImportEntry(import, $"AD_SND!{sound.Name}",
                    new(gateway, (ushort)(firstGatewayOffset + (definitions.Length + soundIndex) * gatewaySpacing)),
                    sound.Handler, sound.Bytes, Win16ReturnLayout.WordInAx);
            }
            int index = Array.FindIndex(definitions, definition => import.Name is null && definition.Ordinal == import.Ordinal &&
                string.Equals(definition.Module, import.Module, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new NotSupportedException($"Unrecognized Win16 import {import.Module}!{import.Name ?? $"#{import.Ordinal}"}.");
            var definition = definitions[index];
            return new ImportEntry(import, $"{definition.Module}!{definition.Name} (#{definition.Ordinal})",
                new(gateway, (ushort)(firstGatewayOffset + index * gatewaySpacing)), definition.Handler, definition.Bytes, definition.Return);
        }).ToArray());
    }

}
