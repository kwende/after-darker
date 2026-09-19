# Spiral Gyra: second original-module execution proof

Analyzed artifact SHA-256:
`8098AC464BA204E81719828ECDDB9DC78058231EF105F8B512072C21E3167529`.
Original bytes, disassembly and rendered images stay in ignored local storage.

## Artifact facts and selected path

The NE image has six segments, automatic data segment 6, a 4,096-byte local
heap reservation, 35 internal relocation records, and 21 imported records.
There are no OS-fixup records or helper-DLL imports in this artifact. Startup
is S5:0000; MODULE is S1:003E and WEP is S1:0027. MODULE uses the same three
WORD far Pascal ABI as Mondrian; WEP returns AX=1 with RETF 2.

The dispatcher at S1:003E locks the shared system and module handles. Its
PREINITIALIZE branch checks system +14h, +2Ch and +34h against the same values
as Mondrian. The normal path avoids its unsupported text/error branch.

| Phase | Guest routine | Relevant behavior |
| --- | --- | --- |
| INITIALIZE | S2:0012 | Reads dimensions, builds integer animation state, creates black and white pens, selects white and saves the previous pen, reads ticks |
| BLANK | S2:0ADB | SetRect, stock black brush, FillRect |
| DRAWFRAME | S2:047B | Tick-based calibration/gating, original integer geometry and color calculation, pen selection, MoveTo and LineTo |
| CLOSE | S2:0B22 | Optionally blanks, restores saved pen and deletes both allocated pens |
| WEP | S1:0027 | Returns one without another imported service |

DRAWFRAME's inner loop caps itself at 30 iterations (S2:08BB). Each iteration
can erase four old lines and draw four new ones, plus color/pen changes. This
explains why Mondrian's 128-service budget does not describe this workload.
The Spiral profile uses a bounded 768-service and 200,000-instruction limit per
invocation. Native time limits remain in force. We do not reset budgets at traps.

## Host record and adaptation

System fields are shared through `AfterDarkHostContract`. Spiral additionally
reads system +0Ah at S2:05C7; values at least four select its color branch. We
supply 24 for our RGB surface. This is a host capability choice, not recovered
evidence of the original user's display mode.

Spiral's module record is 12 bytes: width at +2, height at +4, and controls at
+6/+8/+0Ah. The chosen fixed controls are 270, 40, and the UI speed percentage.
Their uses at S2:056A..05C0 correspond to angle/length/timing calculations;
these names describe the observed calculations, not a complete recovered UI
schema. In particular, Mondrian's speed/clear record must not be reused here.

The profile observes compatibility at DS:0066, width/height at 0030/0034,
instance at 0B1C, tick at 0476, random seed at 0192, converted time at 0492,
system/module pointers at 04D6/0B28, and pen handles at 0B20/07FA. The shared
runner verifies startup success, host pointers, dimensions, balanced stack/DS/BP,
locks, and shutdown; it does not write these observed fields to force success.

## Shared implementation

`AfterDarkSession<TState>` contains the common NE preparation, segmented guest,
gateway dispatch and lifecycle. `ModuleProfile<TState>` supplies artifact
identity, host records, typed observations and checks. `MondrianSession` is a
small typed facade so the existing console lessons retain their observations.
Host selectors follow the actual segment count: Spiral gets caller 0038,
gateway 0040, stack 0048 and host-data 0050. Mondrian retains its previous map.

`AfterDarkPlayback` and the WPF mailbox/presenter operate on `IAnimationSession`.
The window never branches into module-specific drawing. `SupportedModules`
identifies the two supported versions by SHA-256 before any engine is created.

Five shared imports were added to `Win16Api`/`Win16Imports`:

| Import | Argument bytes | Result |
| --- | ---: | --- |
| GDI!CreatePen (#61) | 8 | WORD handle in AX |
| GDI!SelectObject (#45) | 4 | Previous WORD handle in AX |
| GDI!DeleteObject (#69) | 2 | WORD Boolean in AX |
| GDI!MoveTo (#20) | 6 | Previous signed x/y packed in DX:AX |
| GDI!LineTo (#19) | 6 | WORD Boolean in AX |

The Win16 signatures are grounded in Wine's
[GDI export declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/gdi.exe16/gdi.exe16.spec)
and [16-bit wrappers](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/gdi.exe16/gdi.c).
Each HDC retains its selected pen and current point. Created pens use bounded,
reusable guest handles, never native pointers. Deleting a selected pen fails;
restoring the old pen permits deletion. The pool is capped at 256 objects.

The renderer supports solid cosmetic pens (width zero/one), copy-pen drawing,
and identity coordinates. Lines exclude the endpoint, advance the current
position and clip to the surface, following the
[LineTo contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-lineto).
Spiral also creates PALETTERGB colors; the true-color surface uses the requested
RGB directly. This avoids a palette implementation and is explicitly a modern
display adaptation. Wider/dashed pens, palette indices and other raster modes
remain unsupported.

## Verification and boundaries

- A public synthetic guest makes real far calls through all five new imports,
  stores returned coordinates, and checks colors, argument order, signedness,
  selected-object deletion failure, return registers and Pascal stack cleanup.
- A deterministic Windows GDI oracle compares 1,500 line cases across octants,
  pixel ties and clipping. Production rendering remains managed software.
- Public state tests cover separate HDCs, pen selection/position, handle reuse,
  exhaustion and unsupported styles/colors.
- Seven optional original-module cases cover all five exposed speeds, 100 draws
  with pen reuse, identical independent deterministic runs, and CLOSE/WEP with
  zero remaining pens and global locks.
- Actual WPF acceptance presents 30 changed/initial images, verifies pixel
  readback, rejects unsupported content without stopping the active guest,
  stops/restarts, switches modules through the menu's load path, and closes
  during playback. The window's rendered content was inspected visually.

This is original-code execution with a narrowly supported GDI subset. It does
not establish Windows 3.1 pixel/palette/timing fidelity, support for other
versions, or compatibility with arbitrary AD modules. No settings dialogs,
sound, generic heap allocation or new guest threading machinery were needed.
