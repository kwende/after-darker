# Reusing Win16 and GDI implementations

Assessment date: 2026-09-21. This is source research and a proposed experiment,
not a renderer migration or a claim that another runtime executes our modules.
The owner has paused module expansion to reassess reusable foundations.

## Decision in brief

There is substantial prior work. The strongest immediate reuse opportunity is
modern Windows GDI itself, called from our C# gateway. Win3mu demonstrates this
in C#; WineVDM demonstrates both forwarding and the historical compatibility
adjustments that forwarding sometimes needs. Retain our working runtime while
testing this seam before writing more raster algorithms.

Interpret the no-emulator requirement as no booted legacy OS or external VM:
our existing embedded Unicorn CPU remains acceptable. A separate native worker
is an alternative to evaluate, not an accepted change to the product boundary.

The [collection audit](windows98-readiness-audit.md) also changes priorities:
32 of 36 additional modules stop during loading (24 additive relocations, five
OS fixups, two export-prologue cases and one segment-count limit). GDI reuse
does not fix these barriers. Shared loader work could help many modules, but
removing a first blocker does not prove the rest of their dependencies work.

## Options and integration cost

| Option | Useful existing work | Fit and remaining work |
| --- | --- | --- |
| Host Windows GDI through C# P/Invoke | Raster operations, geometry, clipping, DC state, fonts, bitmap copying/stretching | Best incremental in-process x64 candidate. Keep Win16 marshaling, checked guest memory, handle mapping and compatibility policy. Needs a production backend and regression proof. |
| Win3mu | Managed NE/module loading, local/global heaps, handle maps, USER/KERNEL/GDI bridges | Closest C# architectural reference. Original project is unsupported/incomplete and targets .NET Framework 4.6.1 with Sharp86 dependencies; integration into .NET 10/Unicorn is adaptation work. |
| WineVDM / OTVDM | Broad Win16 environment on modern Windows, Wine-derived APIs, compatibility fixes | Strong whole-runtime candidate and reference. Inspected GDI build is Win32/x86 and depends on its KERNEL/USER/runtime. Not a drop-in x64 DLL. A 32-bit worker could communicate pixels/control with .NET; AD hosting, off-screen output and shutdown still need proof. |
| Wine | Win16 API implementations and broad Windows compatibility source | Valuable semantic reference. Its GDI16 target imports other Wine runtime components; extracting a library is a porting project. WineVDM is the more directly relevant Windows adaptation. |
| ReactOS | Windows NT-style USER/GDI and system implementation | Reference for API behavior. Its documented NT/user/kernel architecture is a poor basis for assuming a standalone Win16 renderer can be dropped into our host. |
| Original AD helper DLLs | Proprietary shared AD behavior already compiled | Execute them as guest modules when practical. Requires multi-module loading, identity/resource ownership, per-module data and initialization/teardown, plus their reached Windows imports. |

These choices act at different layers. Reusing Windows GDI and executing AD
helpers can coexist with our CPU and loader; replacing the whole runtime is a
separate decision.

## The useful mechanism: translate, then let Windows draw

```text
original AD instructions in Unicorn
    -> existing Win16 import gateway / Pascal stack decoder
    -> checked guest pointer and 16-bit handle translation
    -> C# P/Invoke into host gdi32.dll / applicable user32.dll helpers
    -> memory HDC with an off-screen DIB section
    -> GdiFlush, then copied pixels
    -> existing frame transport and WPF WriteableBitmap
```

For Polygon, for example, read the guest POINT16 array through checked segmented
memory, widen signed coordinates into native POINT structures, resolve the guest
HDC through our host table, call Polygon, then return through our existing ABI.
The guest never receives the native HDC or an address in host memory. We still
own argument validation, guest-visible errors and object lifetime.

This does not require a visible GDI window. Microsoft's
[CreateDIBSection contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection)
provides direct access to bitmap memory and requires synchronization before
accessing it after GDI drawing. Our existing
[WindowsDrawingOracle](../../tests/AfterDarker.Tests/Conformance/WindowsDrawingOracle.cs)
already creates a memory DC and 32-bit DIB, draws with P/Invoke, flushes and
copies RGB pixels. It is test-only and is not a production rendering backend.

Modern GDI is not an exact Windows 3.1 display implementation. Palette animation,
indexed bitmap interpretation, historical fonts and monochrome mask conversion
need explicit comparisons. WineVDM's StretchBlt16 and palette-update paths
contain adaptations beyond forwarding. The project's color-only policy avoids
optional grayscale modes; it does not remove palettes used to animate color.

Keep the software renderer available for deterministic tests and as a reference.
A native backend should own a complete coherent set of DCs and objects per
session; independently switching individual calls between unrelated software
and native state would lose selected objects, clipping and bitmap contents.

## What the source examination established

Source revisions discovered during this assessment:

- WineVDM: `9d34766ca79482ca6b704ec3157ea87ec825b61c`.
  [GDI implementation](https://github.com/otya128/winevdm/blob/9d34766ca79482ca6b704ec3157ea87ec825b61c/gdi/gdi.c)
  forwards Rectangle16 and TextOut16 after handle conversion, widens Polygon16
  points, and adds palette handling around StretchBlt16/AnimatePalette16.
  Its [GDI project](https://github.com/otya128/winevdm/blob/9d34766ca79482ca6b704ec3157ea87ec825b61c/gdi/gdi.vcxproj)
  builds Win32 and links its own krnl386, user and Wine support libraries.
- Win3mu mirror: `d041473c0e8f7f650a80b8f6c43f17c9375ce351`.
  [Gdi.cs](https://github.com/skochinsky/win3mu/blob/d041473c0e8f7f650a80b8f6c43f17c9375ce351/Win3muCore/EmulatedModules/Gdi.cs)
  combines ordinal declarations, P/Invoke and guest-memory conversion.
  [ModuleManager](https://github.com/skochinsky/win3mu/blob/d041473c0e8f7f650a80b8f6c43f17c9375ce351/Win3muCore/Core/ModuleManager.cs)
  handles dependency loading and tracks pending linking; heap and handle-map
  classes are separate responsibilities. The
  [author's page](https://www.toptensoftware.com/win3mu/) explicitly describes
  incomplete compatibility and unsupported source distribution. The upstream
  Bitbucket browser page failed to load in this examination; code was inspected
  through the identified mirror.
- Wine: `7b3fff76fa5178f6ce0141b2c776afa2a822f101`.
  [GDI16 sources](https://github.com/wine-mirror/wine/blob/7b3fff76fa5178f6ce0141b2c776afa2a822f101/dlls/gdi.exe16/gdi.c)
  and its [build dependencies](https://github.com/wine-mirror/wine/blob/7b3fff76fa5178f6ce0141b2c776afa2a822f101/dlls/gdi.exe16/Makefile.in)
  establish that the layer exists, but not that it is independently embeddable.
- [ReactOS architecture](https://reactos.org/architecture/) describes its NT
  kernel and Win32 environment. No ReactOS component was built or embedded.

Microsoft documents that a
[64-bit process cannot load a 32-bit DLL](https://learn.microsoft.com/en-us/windows/win32/winprog64/process-interoperability).
This rules out simply P/Invoking the inspected WineVDM build from our x64
runtime. A worker process is technically a different possibility from a VM;
its lifetime and image transport would still be part of our implementation.

Source reuse also has distribution implications: Win3mu declares GPL-3.0-or-later;
Wine's [license file](https://github.com/wine-mirror/wine/blob/7b3fff76fa5178f6ce0141b2c776afa2a822f101/LICENSE)
declares LGPL-2.1-or-later with file exceptions; WineVDM's root license is GPL-2.0
while the inspected GDI file declares LGPL-2.1-or-later. Review the exact files
and dependencies before copying or distributing them. No third-party source was
vendored or dependency adopted during this assessment.

## Original Windows DLLs as a targeted reference

Reading a Windows 98 implementation one or two calls deep can clarify a specific
contract when documentation and open implementations disagree. Record the exact
binary hash, entry ordinal, inferred behavior and a small reproducing test.
Distinguish application-visible behavior from internal device/OS mechanisms.
This assessment did not extract, disassemble or execute Windows 98 GDI/USER.

AD helper code is a different reuse opportunity: execute original guest exports
where their own imports can be satisfied. Matching a helper filename is not
enough; preserve versions and test initialization/resource ownership. Neither
native GDI nor an alternative Windows runtime supplies the AD lifecycle for us.

## Proposed next discriminating experiment

Before continuing module-specific raster expansion, prototype one optional
native GDI backend for a supported bitmap module such as Gravity. Use the same
guest file, controls, seed, clock, surface size and execution bounds. Verify
original-code execution, copied color frames, intermediate-frame behavior where
applicable, CLOSE/WEP, and zero outstanding handles/DCs/bitmaps after disposal.
Compare existing software output and explain differences; do not assume modern
GDI proves historical pixel fidelity.

Then exercise one unsupported operation such as StretchBlt with a source-built
fixture through the actual guest ABI. This distinguishes a useful backend from
a demonstration that C# can call GDI. Palette animation needs a separate indexed
color proof before claiming Satori support.

In parallel planning, rank loader barriers and helper dependencies by affected
module families. Preserve per-module regression checks as the evidence that
shared infrastructure actually enables playback. No prototype is implemented
or selected as the new architecture by this assessment.

## Verification and consumer boundary

Re-ran the existing StainedGlassRasterTests, GravityRasterTests and
BitmapFamilyRasterTests: **13 passed, zero failed/skipped**. These demonstrate
the current native-oracle path and selected comparisons, not a new backend.
No external compatibility project was compiled or benchmarked.

For Ocuvera, keep native resources on the sequential guest worker, publish only
copied pixels/typed metadata, retain bounded shutdown and dispose all native
objects before completion. Put any Windows-specific rendering dependency behind
the runtime boundary without adding WPF to Core. No public lifecycle or pixel
contract changed; packaging and single-file publication remain unproven as
recorded in the [consumer assessment](../ocuvera-compatibility.md).
