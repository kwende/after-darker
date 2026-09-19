# Tutorial 09: original Mondrian code becomes pixels

The original `Mondrian.ad` now produces 30 changed images in our Windows x64
console host. Its x86 instructions choose the rectangle coordinates, advance
its RNG, and maintain its rectangle history. Our C# implementations supply
the requested Windows operations and save the resulting surface as PNGs.

## Run and inspect

Select **Tutorial 09 - capture Mondrian PNG frames** in Visual Studio, press
F5, and supply your local module path. Or run:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 09 ad/Mondrian.ad
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 09 ad/Mondrian.ad 10
```

The default is 30 changed frames; the allowed range is 1..300. Each run creates
a unique directory under ignored `artifacts/mondrian-frames/`, containing:

- `frame-0001.png` onward: full 640×480 RGB snapshots, without scaling.
- `index.html`: local play/step/slider viewer. Open it in a browser.
- `report.json`: artifact hash, options, clock policy, import counts,
  instruction total, pixel hashes, rectangle arguments, and observed guest state.

If execution fails, `failure.json` marks the capture incomplete. Existing files
are preserved; incomplete output is not a successful capture. No private module
or generated image is added to Git. No new dependency or Watcom build is needed.
Only the hash-qualified Mondrian revision from [lesson 08](tutorial-08-mondrian-initialize.md)
is accepted. This is not a generic runner for all `.AD` modules.

## The mechanism to follow

Start in [Tutorial09MondrianFrames.Capture](../src/AfterDarker.Tutorials/Lessons/Tutorial09MondrianFrames.cs).
The loop is deliberately small:

```text
create one surface and register guest HDC 0103
    → load/initialize one guest with MondrianSession
    → MODULE(BLANK): fill the surface black
    → MODULE(DRAWFRAME): execute original x86
        → guest SetRect writes coordinates into its own RECT16 storage
        → guest InvertRect passes a far pointer to that rectangle
        → Win16Api reads those eight bytes and inverts the requested pixels
        → simulated far return resumes the original code
        → original MODULE returns to our caller with a balanced stack
    → compare complete RGB image with previous captured image
    → save it if changed; repeat until the requested count
```

The **same** guest and surface survive every call. Reinitializing between frames
would discard the DLL's animation state. `MondrianSession` is the shared
initialization/execution path shared with tutorial 08; that earlier lesson
still stops after INITIALIZE. The host owns the session with `using` and calls
`Initialize`, `Blank`, and `DrawFrame` explicitly. See the
[session lifetime walkthrough](mondrian-session.md). The small machine-code callers are prepared
before execution and perform real `CALL FAR` instructions into MODULE.

Useful breakpoints, in order:

1. `Capture`: `session.DrawFrame()`.
2. [MondrianSession.DispatchImport](../src/AfterDarker.Runtime/MondrianSession.cs): the actual guest frame and decoded words.
3. [Win16Api.SetRect / InvertRect](../src/AfterDarker.Core/Win16/Win16Api.cs): ordinary typed API bodies.
4. [PixelSurface.Paint](../src/AfterDarker.Core/Rendering/PixelSurface.cs): clipping and RGB changes.
5. `Capture`: the returned guest state and comparison before PNG encoding.

No RNG or rectangle-generation algorithm is implemented in C#. `PngWriter`
encodes a completed RGB image; it never decides where Mondrian draws.

The loop allocates two RGB buffers once and calls `session.CopyPixelsTo(current)`.
After capturing a change it swaps current/previous, reusing the old previous
array as the next destination. The save callback receives a `ReadOnlySpan<byte>`
valid only during that call; copy explicitly if you need to retain it. The PNG
sink consumes it synchronously. Tutorial 09 explicitly requests full diagnostics
for its bounded run; ordinary sessions default to recent history with lifetime
totals. See [retention and buffer ownership](mondrian-session.md).

## The four additional imports

Win16 return types matter here. Do not substitute similarly named Win32
declarations: SetRect16 and InvertRect16 are **void** in the compatibility
source. The gateway leaves AX/DX alone for these calls; a caller cannot rely
on an invented Boolean return.

| Import | Argument bytes | Implemented behavior | Return |
| --- | ---: | --- | --- |
| USER #72 SetRect | 12 | Write four signed 16-bit corners through a checked guest far pointer; preserve corner order | void |
| GDI #87 GetStockObject | 2 | Index 4 returns our guest stock-black-brush identity `0201` | WORD in AX |
| USER #81 FillRect | 8 | Resolve HDC and black brush; read RECT16; fill its clipped raster area black | INT16 1 in AX |
| USER #82 InvertRect | 6 | Resolve HDC; read RECT16; invert RGB bits in its clipped raster area | void |

For example, SetRect's decoded source-order words are
`selector, offset, left, top, right, bottom`. Its first two words form **one**
far pointer. Signed conversion applies to coordinates. Imported calls still
leave the native hook before dispatch; the dispatcher simulates RETF with the
correct argument cleanup, then resumes guest execution.

[Win16Drawing](../src/AfterDarker.Core/Win16/Win16Drawing.cs) holds a registry
from guest HDC to persistent software surface. The supported context has
identity coordinates, a full-surface clip, and a stock black brush. Unknown
handles, other stock objects, invalid pointers, and unsupported APIs fail
symbolically. Pens, palettes, arbitrary clip regions, mappings, and object
selection/lifetime APIs remain future work when a module requires them.

## Reversed rectangles: a useful experiment

The first observed rectangle is `(367,430)-(307,284)`: both axes run backwards.
Ignoring it as an empty Win32-style rectangle would lose actual guest output.

Wine 10.0's [Win16 USER implementation](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.c)
implements FillRect16 and InvertRect16 through `PatBlt`, passing signed width
`right-left` and height `bottom-top`. This establishes useful compatibility
prior art; it does not by itself prove which pixels our backend should touch.

We tested native Windows `PatBlt` into an independent memory DIB for positive,
negative, zero, clipped, and wholly outside rectangles. In identity coordinates,
the measured coverage is the sorted, half-open interval: the first rectangle
covers `x=[307,367), y=[284,430)`, or **8,760 pixels**. The RECT in guest memory
is unchanged; sorting happens only when deriving raster bounds.

An initial inference from a Wine bounding-box helper suggested a one-pixel shift
for negative extents. The native oracle rejected it in five cases. The software
rasterizer now agrees with the measured Windows behavior in all ten cases.
[RectangleRasterTests](../tests/AfterDarker.Tests/Conformance/RectangleRasterTests.cs)
preserves that evidence. This is a modern Windows compatibility check, **not**
a pixel-fidelity comparison against original Windows 3.1.

## Determinism and bounds

The capture sets speed 100, clear enabled, 640×480. The original initialization
code translates that speed to threshold zero. Civil time is fixed at
1993-06-15 12:34:56; initialization produces guest RNG seed `00002460`.
GetTickCount starts at `12345678` and advances by 16 **per request**. These are
host experiment inputs, not historical wall-clock pacing. The local viewer's
150 ms slideshow cadence is a separate presentation choice.

We count changed images, not invocations. The guest may return without drawing
because of its own timing gate. A slower-option test proves that more DRAWFRAME
calls can be needed than captured images. Image comparison also avoids counting
a sequence of operations whose final pixels cancel out.

Each entry invocation has a 50,000-instruction, 128-service-exit, five-second
emulated-execution budget, with a one-second limit per native EmuStart.
Capture allows at most 10,000 DRAWFRAME calls and 300 images. Surfaces are
limited to 2048×2048. Instruction totals remain cumulative for reporting, but
the safety budget resets at each **completed host invocation**, not each import.
Returning through many normal frames must not exhaust an initialization-era
lifetime instruction cap. Native Unicorn is not an adversarial-code sandbox.

## What is proven

Observed on the Windows x64 development host:

- Startup, PREINITIALIZE, INITIALIZE, BLANK, then 30 DRAWFRAME calls completed.
- 30 distinct 640×480 PNGs came from original code; total 11,859 instructions.
- 31 SetRect, one GetStockObject, one FillRect, and 30 InvertRect calls crossed
  the gateway. All nine imports predicted for this path were reached.
- Every MODULE return restored caller DS, SS:SP and BP and released global locks.
- Two independent guest runs produced identical per-frame hashes and state.
- A 180-image test crossed the original rectangle-removal branch and exceeded
  50,000 lifetime instructions successfully. A deliberately insufficient draw
  budget failed without reporting success.
- All 30 PNGs passed independent Python CRC/zlib checks and Pillow decoding;
  decoded RGB hashes matched the report. The final image was visually inspected.

Final RGB SHA-256 for the default 30-image experiment:
`66D8954D3F8D6BD5BA311662C2D958E91CC614BEA8199C22A689A9791950EE59`.

The lesson disposes the bounded guest after capture; it does **not** yet invoke
Mondrian CLOSE or WEP. General Windows compatibility, other modules/revisions,
historical pacing, and Windows 3.1 pixel fidelity remain outside this proof.
No historical-first claim follows from this experiment.

The durable model: **the guest owns animation state; the host owns the HDC's
pixels; imported calls connect them.**
