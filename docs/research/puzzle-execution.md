# Puzzle: original code with hand rolled scrolling

The seventeenth supported artifact uses our C# Win16 services and pixel surface.
The original code chooses and animates the tiles. The retired native experiment
and the reason for this choice are preserved in [the decision record](native-gdi-spike-decision.md).

## Run and inspect

Select WPF profile **Puzzle - hand rolled**, or console profile
**Tutorial 12 - Puzzle with hand rolled scrolling**. Supply your own supported file:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 12 path/to/PUZZLE.AD
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- path/to/PUZZLE.AD
```

Lesson 12 produces 1,800 draws, an initial PNG, thirty animation PNGs and a typed
JSON report in a new ignored `artifacts/puzzle/` directory. An optional third
argument names another new directory. The default input points to the owner's
private extracted collection; no module or capture is committed.

## Artifact facts and host choices

- SHA-256: `1E25FBC9567DA400A09A7056D90178BD6D329BA7B262F44573904C8922F53F5D`.
- Five NE segments; automatic data is segment 5; initial local heap is 1,024 bytes.
- DLL startup: S4:0000; MODULE: S1:003E; WEP: S1:0027. The existing loader and
  relocation implementation already handled this file; no loader changes.
- Resource controls identify Small/Medium/Large size, Slow/Medium/Fast speed,
  Sound, and Invert Screen. The profile supplies `[0, 0, 0, 0]`: Small, Slow,
  Sound off, no inversion. Its original audio calls still reach the shared
  unavailable-audio implementation and receive failure; no fake HSOUND exists.
- S1's dispatcher stores HDC at DGROUP:03F4, the AD_SYSTEM pointer at 03FA,
  AD_MODULE at 041C, startup instance at 0414, and compatibility at 0022.
  The profile checks those guest-written values against the supplied inputs.
- **Modern adaptation:** the initial image is a generated RGB gradient/checker
  pattern. A uniform white background would conceal image rearrangement. This
  replaces desktop capture; it does not replace Puzzle's animation. Fixed
  controls and 128..2048-pixel dimensions define the supported profile.

## The mechanism worth retaining

```text
original Puzzle chooses adjacent tiles and signed movement
  -> UnionRect computes their bounding rectangle
  -> original CALL FAR USER!ScrollDC
  -> Win16ApiDispatcher decodes 20 bytes of Pascal arguments
  -> Win16Api.ScrollDC validates guest pointers and decodes RECT values
  -> Win16Drawing.ScrollDC resolves the guest HDC and clips the geometry
  -> existing PixelSurface.CopyRegion moves overlapping pixels
  -> our geometry calculates exposed bounds
  -> our bridge stores that rectangle in guest memory
  -> existing far return/stack cleanup resumes the original module
  -> Puzzle calls FillRect on the returned strip, using a black stock brush
```

At S2:05CE and S2:0614, the original code passes DGROUP:0400 as both scroll and
clip rectangles, a zero update-region handle, and DGROUP:0386 for output. It
then passes that output to FillRect. Returning TRUE without writing the exposed
rectangle would be an incorrect implementation even if the pixels moved.

Files to read:

- [Win16Api.Scrolling](../../src/AfterDarker.Core/Win16/Win16Api.Scrolling.cs):
  checked guest input/output conversion, without native addresses.
- [Win16Drawing.Scrolling](../../src/AfterDarker.Core/Win16/Win16Drawing.Scrolling.cs):
  hand rolled geometry and exposed bounds, reusing the overlap-safe bitmap copier.
- [Win16Api.Rectangles](../../src/AfterDarker.Core/Win16/Win16Api.Rectangles.cs):
  CopyRect and UnionRect alongside the existing small geometry helpers.
- [PuzzleProfile](../../src/AfterDarker.Runtime/Modules/PuzzleProfile.cs):
  identity, settings, observed globals and the starting image.
- [Tutorial12Puzzle](../../src/AfterDarker.Tutorials/Lessons/Tutorial12Puzzle.cs):
  bounded lifecycle, image captures, import counts and cleanup checks.

## ABI and supported service boundaries

| Import | Win16 argument bytes | Return | Implementation |
| --- | ---: | --- | --- |
| USER #74 CopyRect | 8 | void | Checked eight-byte copy; overlapping buffers allowed |
| USER #80 UnionRect | 12 | BOOL in AX | Small shared coordinate helper; empty/inverted inputs ignored |
| USER #221 ScrollDC | 20 | BOOL in AX | Hand rolled scrolling through existing CopyRegion |

CopyRect's Win16 return is **void**, unlike Win32's BOOL. The pinned Watcom
`h/win/win16.h` declares that signature; it also declares BOOL UnionRect and
ScrollDC. Ordinals/argument types agree with the
[Wine 10.0 USER export specification](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.exe16.spec).
No implementation source was copied. See Microsoft's contracts for
[ScrollDC](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-scrolldc)
and [UnionRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unionrect).

Scroll/clip/output far pointers may be zero. Nonzero update-region handles are
explicitly unsupported: no update-region result is implemented. Required
output bytes are checked for range **and write permission before scrolling**;
`IGuestMemory16.ValidateWrite` defaults to rejection for memory providers which
have not implemented the check. SegmentedGuest validates its existing map without
mutating memory. Invalid inputs do not become native pointers or fake success.

## Observed execution and verification

The 320x240 console capture completed **1,800 draws**, **1,639 changed frames**,
**1,721 ScrollDC calls**, **83 UnionRect calls**, and **388,768 instructions**,
then CLOSE/WEP. Guest resources are released after shutdown. CopyRect is not
needed at this divisible size; the 321x239 test reaches it during initialization.
Scrolls succeeded, and tests observe movement in both directions.

The private suite runs 600 draws at 128x128, 321x239 and 640x480 with different
seeds, verifies guest stack/data registers, actual import replies and output
pointers, and checks clean guest ownership. A 2048x2048 bounded lifecycle
also passes. The public suite executes synthetic x86 calls through all three
new ABIs; it checks overlapping pointer aliases, signed deltas, void/BOOL register
behavior, Pascal cleanup, invalid/read-only output, overlapping copies,
clipping, exposed bounds, omitted rectangles and unsupported region handles.

Public tests and four opt-in Puzzle lifecycle cases cover this retained path.
The direct Windows ScrollDC oracle compares 459 rectangle/delta/origin combinations;
it is test code, not a native playback backend. Historical dual-backend results
are in [the decision record](native-gdi-spike-decision.md). Current verification
is recorded in [current status](../current-status.md).

```powershell
$env:AFTER_DARKER_PUZZLE = 'path/to/PUZZLE.AD'
dotnet test --project tests/AfterDarker.Tests -p:TestLocalPuzzle=true
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke path/to/PUZZLE.AD artifacts/puzzle/new-wpf-run
```

## Hand rolled algorithm boundary

ScrollDC clips source and destination rectangles, then delegates pixel movement
to the existing overlap-safe CopyRegion. The bounding box of the source area
not covered by its moved copy is returned as the exposed rectangle. Coordinates
use 32-bit intermediates and narrow only after restoring the logical origin.
Selected rectangular DC clipping is supported; selected elliptic clipping and
nonzero output-region handles fail explicitly before mutation. This does not
implement general mapping modes or window invalidation. Modern native oracle
agreement is not proof of historical Windows 3.1 pixel fidelity.
