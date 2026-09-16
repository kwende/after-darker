# Local After Dark import census

Scan date: 2026-09-16. **Artifact facts, not observed execution.**

The 29 local modules do not directly import thread creation, mutex/semaphore
operations, Win16 task-event waits, or task-yield APIs. They do require services
beyond drawing. Memory handles, resource loading, timing, files/settings, modal
dialogs, helper libraries, and one process-launch import are visible.

This supports starting with one guest execution context and a narrow host
service layer. It does **not** prove that all modules and their dependencies
can run without scheduling or asynchronous behavior. The missing helper DLLs
are the largest unresolved part of this scan.

## Scope and reproducible evidence

- 28 `.ad` files and `Aquatic Realm.ad.dll`: all 29 are MZ/NE, target OS 2
  (Windows). The `.dll` suffix does not make Aquatic Realm a Win32 module.
- Two non-executable supporting files were excluded. No guest code was run.
- [Complete API CSV](ad-imports.csv): one row per distinct library/ordinal or
  library/name, with every consuming module. Counts are distinct import targets,
  **not runtime call counts or instruction counts**.
- [Artifact manifest](ad-import-manifest.json): each input's SHA-256, size,
  dependencies, and imported identities; hashes of the ordinal reference files.
  No original binary bytes or absolute private paths are included.
- [Read-only inspector](../../tools/inspect-ne-imports.py): a standalone Python
  standard-library research utility, independent of the C# tutorials/runtime.
  It reads NE module references and each segment's relocation records, including
  ordinal and named imports. It does not load or relocate executable code.

| Imported library | Distinct targets | Modules using it | Resolution |
| --- | ---: | ---: | --- |
| GDI | 51 | 29 | All ordinal names resolved |
| USER | 35 | 29 | All ordinal names resolved |
| KERNEL | 43 | 29 | 42 functions plus `__AHSHIFT`, an imported constant |
| AD_RSRC | 59 | 5 | Ordinals known; names and contracts unresolved |
| AD_SND | 10 | 9 | Names embedded in the callers; contracts not recovered |
| WIN87EM | 1 | 10 | Ordinal 1 maps to `_fpMath` |
| **Total** | **199** | **29 distinct modules** | **129 Windows targets + 70 helper targets** |

`AD_RSRC`, `AD_SND`, and `WIN87EM` are not supplied as helper files in this local
folder. Their transitive imports have **not** been scanned. Wine's export tables
resolve the Windows and WIN87EM ordinals; these are reference mappings, not
export tables read from the original Windows installation.

## Synchronization, tasks, and re-entrancy

No direct imports match `CreateThread`, mutexes, semaphores, critical sections,
or `WaitFor*` APIs. More relevant to Win16, there are also no direct imports of
`Yield`, `OldYield`, `DirectedYield`, `WaitEvent`, `PostEvent`, `LockCurrentTask`,
or `SetPriority`. No direct `GetMessage`, `PeekMessage`, `DispatchMessage`,
`SetTimer`, or `KillTimer` imports were found.

Do not mistake `GlobalLock`, `LocalLock`, or `LockSegment` for mutual-exclusion
primitives. They concern movable memory/segments and the validity of pointers.
We need guest memory handles and their lifetime/locking semantics, not a mutex
for each such call. Microsoft's [history of GlobalLock](https://devblogs.microsoft.com/oldnewthing/20041104-00/?p=37393)
explains the distinction in its original memory-management context.

The relevant complications actually present are:

| Evidence in the callers | Implication / remaining uncertainty |
| --- | --- |
| `DialogBox`, `MakeProcInstance`, `FreeProcInstance` in Globe and Sounder | Settings paths can require host-to-guest dialog callbacks and nested execution. A callback can run on one thread; it still needs a distinct re-entrancy mechanism. Imports alone do not prove which paths invoke it. |
| `ADWSOUNDASYNCCAP` in nine modules, with other AD_SND functions | The sound interface exposes an asynchronous-capability query. This does not establish guest threads, callback behavior, or the helper's internal synchronization. Inspect the helper/contract before selecting a sound design. |
| `WinExec` in GraphStat | A potential program-launch path must be inspected. Do not infer what program is launched or whether this runs during drawing from the import alone. |
| `PostQuitMessage` in Mountains | There is a quit-notification dependency, despite no direct message-loop imports. |
| `LoadLibrary` in Aquatic Realm, Clocks, Globe, Marbles, Swan Lake | Dependencies can extend beyond static module-reference tables. There is no direct `GetProcAddress` import, but that does not close the dynamic-dependency boundary. |

**Reasoned inference:** these direct imports do not justify building a general
guest thread scheduler before the first drawing module. Begin with serialized
guest execution; add callbacks or scheduling only when a selected execution
path and its helper contracts require them. Do not replace an unimplemented
stateful API with unconditional success to make a module appear to work.

## Windows services beyond drawing

These are the complete KERNEL targets, grouped by responsibility:

| Responsibility | Imported targets |
| --- | --- |
| Local heap | `LocalInit`, `LocalAlloc`, `LocalReAlloc`, `LocalFree`, `LocalLock`, `LocalUnlock` |
| Global memory and segments | `GlobalAlloc`, `GlobalReAlloc`, `GlobalFree`, `GlobalLock`, `GlobalUnlock`, `GlobalHandle`, `GlobalCompact`, `GlobalWire`, `GlobalUnWire`, `LockSegment`, `UnlockSegment` |
| Files and locations | `OpenFile`, `_lopen`, `_lclose`, `_lread`, `_llseek`, `_lwrite`, `GetModuleFileName`, `GetWindowsDirectory` |
| INI settings | `GetPrivateProfileInt`, `GetPrivateProfileString`, `WritePrivateProfileString` |
| Strings | `lstrcpy`, `lstrcat`, `lstrlen` |
| Environment and capabilities | `GetVersion`, `GetDOSEnvironment`, `GetWinFlags` |
| Diagnostics/termination | `FatalExit`, `FatalAppExit`, `OutputDebugString` |
| Callback instances | `MakeProcInstance`, `FreeProcInstance` |
| Library/program loading | `LoadLibrary`, `WinExec` |
| Resource lifetime | `FreeResource` |
| Absolute imported value | `__AHSHIFT` (Globe only) |

`__AHSHIFT` is especially relevant to loader design: not every imported ordinal
can become a callable gateway. This one represents a value used in segmented
pointer arithmetic. Wine declares it as an `equate`; exact legacy relocation
semantics still need to be implemented and tested.

These are the complete USER targets:

| Responsibility | Imported targets |
| --- | --- |
| Clocks | `GetTickCount`, `GetCurrentTime` |
| Rectangle helpers | `SetRect`, `CopyRect`, `IsRectEmpty`, `PtInRect`, `OffsetRect`, `InflateRect`, `IntersectRect`, `EqualRect` |
| Drawing/palette | `FillRect`, `InvertRect`, `FrameRect`, `SelectPalette`, `RealizePalette` |
| Bitmap resource loading | `LoadBitmap` |
| Windows | `GetWindowRect`, `EnableWindow`, `GetWindowTextLength`, `IsWindow`, `MoveWindow` |
| Dialogs/controls | `DialogBox`, `EndDialog`, `GetDlgItem`, `SetDlgItemText`, `GetDlgItemText`, `DlgDirSelect`, `DlgDirList`, `SendDlgItemMessage` |
| Notification/input | `MessageBox`, `PostQuitMessage`, `MessageBeep`, `GetAsyncKeyState` |
| Strings/formatting | `wvsprintf`, `lstrcmp` |

The 51 GDI targets are listed individually in the CSV. They cover pens/brushes,
object selection/lifetime, lines/shapes, pixels, text/fonts, regions/clipping,
compatible DCs/bitmaps, raster blits, DIBs, palettes, coordinate conversion, and
device capabilities. Drawing-related functions also live in USER, so library
name alone is not an implementation category. Historical export names in the
CSV follow the reference table, e.g. `GDI!181 = RectInRegionOld`.

The ten named AD_SND imports are `ADWOPENSOUND`, `ADWCLOSESOUND`,
`ADWLOADSOUNDRESOURCE`, `ADWLOADSOUNDFILE`, `ADWFREESOUND`, `ADWPLAYSOUND`,
`ADWSTOPSOUND`, `ADWSETSOUNDMODE`, `ADWGETSOUNDINFO`, and `ADWSOUNDASYNCCAP`.
The 59 AD_RSRC ordinals are retained explicitly in the CSV rather than assigned
guessed names. WIN87EM's `_fpMath` is a floating-point runtime dependency;
Wine marks its entry as register-based, so our two-word Pascal tutorial ABI
must not be assumed to cover it.

## Per-module surface

| Module | KERNEL | USER | GDI | AD_RSRC | AD_SND | WIN87EM | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Aquatic Realm.ad.dll | 16 | 7 | 16 | 34 | 7 | 1 | 81 |
| Can of Worms.ad | 7 | 7 | 15 | 0 | 7 | 0 | 36 |
| Clocks.ad | 15 | 6 | 18 | 39 | 9 | 1 | 88 |
| Down the Drain.ad | 7 | 3 | 17 | 0 | 0 | 1 | 28 |
| Fade Away.ad | 7 | 2 | 14 | 0 | 0 | 0 | 23 |
| GeoBounce.ad | 8 | 3 | 14 | 0 | 7 | 0 | 32 |
| Globe.ad | 29 | 19 | 19 | 31 | 0 | 1 | 99 |
| GraphStat.ad | 16 | 3 | 14 | 0 | 0 | 0 | 33 |
| Gravity.ad | 7 | 4 | 15 | 0 | 7 | 0 | 33 |
| Hall of Mirrors.ad | 11 | 2 | 12 | 0 | 0 | 0 | 25 |
| Hard Rain.ad | 7 | 2 | 10 | 0 | 0 | 0 | 19 |
| Lasers.ad | 11 | 2 | 11 | 0 | 0 | 0 | 24 |
| Magic.ad | 11 | 2 | 11 | 0 | 0 | 0 | 24 |
| Marbles.ad | 14 | 6 | 15 | 49 | 7 | 1 | 92 |
| Mondrian.ad | 7 | 4 | 6 | 0 | 0 | 0 | 17 |
| Mountains.ad | 11 | 6 | 16 | 0 | 0 | 1 | 34 |
| Nocturnes.ad | 7 | 4 | 12 | 0 | 7 | 0 | 30 |
| Penrose.ad | 13 | 4 | 21 | 0 | 0 | 1 | 39 |
| Punch Out.ad | 7 | 1 | 17 | 0 | 7 | 0 | 32 |
| Rainstorm.ad | 7 | 4 | 11 | 0 | 0 | 0 | 22 |
| Shapes.ad | 7 | 2 | 11 | 0 | 0 | 0 | 20 |
| Sounder.ad | 24 | 14 | 6 | 0 | 9 | 0 | 53 |
| Spiral Gyra.ad | 7 | 3 | 11 | 0 | 0 | 0 | 21 |
| Stained Glass.ad | 7 | 8 | 19 | 0 | 0 | 0 | 34 |
| String Theory.ad | 11 | 2 | 11 | 0 | 0 | 0 | 24 |
| Swan Lake.ad | 14 | 7 | 14 | 37 | 0 | 1 | 73 |
| Vertigo.ad | 11 | 7 | 15 | 0 | 0 | 1 | 34 |
| Wrap Around.ad | 7 | 3 | 11 | 0 | 0 | 1 | 22 |
| Zot!.ad | 11 | 3 | 11 | 0 | 0 | 0 | 25 |

Thirteen modules depend directly only on KERNEL/USER/GDI. The smallest import
surfaces are Mondrian (17), Hard Rain (19), Shapes (20), and Spiral Gyra (21).
Import count is a planning hint, not a complexity measurement: one blit or a
callback can require more machinery than several simple helper functions.

Spiral Gyra's non-GDI surface is particularly small:

- KERNEL: `LocalInit`, `GlobalLock`, `GlobalUnlock`, `GetDOSEnvironment`,
  `FatalExit`, `FatalAppExit`, `OutputDebugString`.
- USER: `GetTickCount`, `SetRect`, `FillRect`.

Its remaining eleven targets are GDI operations. It has no direct helper-DLL,
dialog, file, or task-management import. This supports retaining it as an early
target while we establish the required startup, memory, and host structures.

## Limits of the result

1. A static import can belong to drawing, configuration, startup, a linked
   runtime, an error path, or unreachable code. We have not established which
   imports execute on the selected After Dark lifecycle path.
2. Dynamically loaded modules, calls through host-supplied function pointers,
   and missing helper DLLs can add dependencies. This is not the transitive
   closure of the full original After Dark installation.
3. The inspector counts import relocation records without following their
   source chains; multiple sites can share a record. This is sufficient to
   identify their target, not to count calls or implement a loader.
4. Non-import relocations are counted separately in the manifest. There are
   2,662 OS-fixup records across the collection; they are not Windows API
   imports and their patch behavior has not been interpreted here.
5. Bounds checks and a generated fixture exercised ordinal/name imports,
   internal-fixup exclusion, truncation, invalid module indices, and preserving
   unresolved ordinals. Layout was checked against Wine's NE dump code. This is
   a research inspector for this collection, not a hardened general NE parser;
   iterated segments are explicitly rejected rather than silently misread.

## Re-run the scan

From the repository root with Python 3 installed, this reads the local files
and preserves unresolved ordinal numbers even without reference tables:

```powershell
New-Item -ItemType Directory -Force artifacts/import-census | Out-Null
python tools/inspect-ne-imports.py ad > artifacts/import-census/census.json
```

For the same name resolution used in this report, download the small upstream
export tables to the ignored reference directory and scan again:

```powershell
$specModules = @('krnl386.exe16', 'user.exe16', 'gdi.exe16', 'win87em.dll16',
                 'mmsystem.dll16', 'sound.drv16', 'shell.dll16', 'system.drv16')
foreach ($specModule in $specModules) {
    $url = "https://raw.githubusercontent.com/wine-mirror/wine/wine-10.0/dlls/$specModule/$specModule.spec"
    Invoke-WebRequest $url -OutFile "artifacts/import-census/$specModule.spec"
}
python tools/inspect-ne-imports.py ad --spec-dir artifacts/import-census > artifacts/import-census/census.json
```

These downloads are reference metadata for offline analysis, not new runtime
dependencies. Their hashes are recorded in the manifest. `ReferenceExportKind`
in the CSV reflects Wine's declaration (`pascal`, `stub`, `equate`), not our
implementation status or a complete ABI definition. In particular, Wine's
`stub` designation does not mean a function can safely be ignored here.

References: Wine 10.0's [NE inspector](https://github.com/wine-mirror/wine/blob/wine-10.0/tools/winedump/ne.c),
[KERNEL exports](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/krnl386.exe16.spec),
[USER exports](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.exe16.spec),
[GDI exports](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/gdi.exe16/gdi.exe16.spec),
and [WIN87EM exports](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/win87em.dll16/win87em.dll16.spec).
