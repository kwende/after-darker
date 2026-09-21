# Where to look in the runtime

The host owns a guest machine. Original x86 code owns its animation state.
When that code calls a Windows import, our gateway temporarily takes control,
reads the arguments, calls an ordinary C# method, and completes the return
that Windows would have performed. Execution then continues in the DLL.

For lifecycle ordering, see [session lifetime](mondrian-session.md). For the
larger boundaries and proof requirements, see [architecture](architecture.md).

## Where do I look?

| Question | Start here |
| --- | --- |
| Where is a mocked call coordinated? | [Win16ImportGateway.Dispatch](../src/AfterDarker.Runtime/Calls/Win16ImportGateway.cs) |
| Who reads arguments and removes the return frame? | [Win16Stack](../src/AfterDarker.Runtime/Calls/Win16Stack.cs) |
| What is the stack's byte layout? | [FarPascalWordFrame](../src/AfterDarker.Core/Win16/FarPascalWordFrame.cs) |
| Why are DS, DI, CX, or AX assigned? | [Win16RegisterConvention](../src/AfterDarker.Runtime/Calls/Win16RegisterConvention.cs) and [LibraryStartupContext](../src/AfterDarker.Runtime/Calls/LibraryStartupContext.cs) |
| Which import corresponds to this ordinal and signature? | [Win16Imports](../src/AfterDarker.Core/Win16/Win16Imports.cs) |
| How do words become handles, signed coordinates, or pointers? | [Win16ApiDispatcher](../src/AfterDarker.Core/Win16/Win16ApiDispatcher.cs) and [Win16ArgumentReader](../src/AfterDarker.Core/Win16/Win16ArgumentReader.cs) |
| What does the Windows method actually do? | [Win16Api](../src/AfterDarker.Core/Win16/Win16Api.cs) and [its implementation guide](win16-implementations.md) |
| How do allocation, handles, locks, freeing and reuse work? | [Win16LocalHeap](../src/AfterDarker.Core/Win16/Win16LocalHeap.cs) and the [heap walkthrough](win16-local-heap.md) |
| Can I watch x86 write to an allocated block? | [Tutorial10LocalHeap](../src/AfterDarker.Tutorials/Lessons/Tutorial10LocalHeap.cs), F5 profile **Tutorial 10 - local heap and guest writes** |
| Which heap does a local handle belong to? | [Win16CallContext](../src/AfterDarker.Core/Win16/Win16CallContext.cs) carries caller DS from the gateway; `Win16LocalHeap.RequireOwner` verifies it |
| Where can I inspect live allocations? | [LocalHeapSnapshot](../src/AfterDarker.Core/Win16/LocalHeapSnapshot.cs), exposed by session and playback results |
| What makes Lasers different? | [LasersProfile](../src/AfterDarker.Runtime/Modules/LasersProfile.cs), [LasersState](../src/AfterDarker.Runtime/Modules/LasersState.cs) and [execution evidence](research/lasers-execution.md) |
| Where does Magic supply its settings and expose its circular line history? | [MagicProfile](../src/AfterDarker.Runtime/Modules/MagicProfile.cs), [MagicState](../src/AfterDarker.Runtime/Modules/MagicState.cs) and [execution evidence](research/magic-execution.md); the guest updates that history itself |
| Where are String Theory's groups, history and colors observed? | [StringTheoryProfile](../src/AfterDarker.Runtime/Modules/StringTheoryProfile.cs), [StringTheoryState](../src/AfterDarker.Runtime/Modules/StringTheoryState.cs) and [evidence](research/string-theory-execution.md) |
| Where are Zot!'s fixed blocks, clock gate and flash points defined? | [ZotProfile](../src/AfterDarker.Runtime/Modules/ZotProfile.cs), [ZotState](../src/AfterDarker.Runtime/Modules/ZotState.cs) and [evidence](research/zot-execution.md) |
| Where are Hard Rain's drop records and square-pixel inputs? | [HardRainProfile](../src/AfterDarker.Runtime/Modules/HardRainProfile.cs), [HardRainState](../src/AfterDarker.Runtime/Modules/HardRainState.cs) and [evidence](research/hard-rain-execution.md) |
| Where does Shapes accept its palette request and expose its original random state? | [ShapesProfile](../src/AfterDarker.Runtime/Modules/ShapesProfile.cs), [ShapesState](../src/AfterDarker.Runtime/Modules/ShapesState.cs) and [evidence](research/shapes-execution.md) |
| Where are Stained Glass's settings and original globals? | [StainedGlassProfile](../src/AfterDarker.Runtime/Modules/StainedGlassProfile.cs), [StainedGlassState](../src/AfterDarker.Runtime/Modules/StainedGlassState.cs) and [evidence](research/stained-glass-execution.md) |
| Where are coordinate origins and raster mixing stored? | [Win16DeviceContext](../src/AfterDarker.Core/Win16/Win16DeviceContext.cs) owns per-DC state; [RasterMix](../src/AfterDarker.Core/Rendering/RasterMix.cs) names the 16 bitwise operations |
| Where are guest RECTs moved, inflated and intersected? | [Win16Api.Rectangles](../src/AfterDarker.Core/Win16/Win16Api.Rectangles.cs) uses checked guest memory, signed words and alias-safe reads |
| Where do explicit brushes and SetPixel paint? | [Win16Drawing.Brushes](../src/AfterDarker.Core/Win16/Win16Drawing.Brushes.cs) separates API behavior from [PixelSurface.Brushes](../src/AfterDarker.Core/Rendering/PixelSurface.Brushes.cs) |
| Where does BitBlt preserve overlapping source pixels? | [Win16Drawing.Blits](../src/AfterDarker.Core/Win16/Win16Drawing.Blits.cs) validates DCs/ROP; [PixelSurface.Blit](../src/AfterDarker.Core/Rendering/PixelSurface.Blit.cs) snapshots and clips the copy |
| Who owns off-screen bitmaps and temporary memory DCs? | [Win16Drawing.Bitmaps](../src/AfterDarker.Core/Win16/Win16Drawing.Bitmaps.cs) separates pixel storage from DC selection and enforces ownership/capacity; [Gravity's guide](research/gravity-execution.md) follows their lifetimes |
| How do masks combine bitmap, brush and destination pixels? | [BitmapRasterOperation](../src/AfterDarker.Core/Rendering/BitmapRasterOperation.cs) names the ROP3 encodings; [PixelSurface.Blit](../src/AfterDarker.Core/Rendering/PixelSurface.Blit.cs) combines pixels and [PixelSurface.Pattern](../src/AfterDarker.Core/Rendering/PixelSurface.Pattern.cs) implements PATCOPY |
| What happens when a module calls the sound helper? | [UnavailableAfterDarkSound](../src/AfterDarker.Core/AfterDark/UnavailableAfterDarkSound.cs) returns consistent unavailable-device/null-handle responses; the ordinary import registry and gateway marshal its named AD_SND calls |
| Where are Gravity's controls and original ball positions? | [GravityProfile](../src/AfterDarker.Runtime/Modules/GravityProfile.cs) and [GravityState](../src/AfterDarker.Runtime/Modules/GravityState.cs); the host observes, rather than computes, the animation |
| Where are three-pixel lines approximated? | [PixelSurface.WideLine](../src/AfterDarker.Core/Rendering/PixelSurface.WideLine.cs) covers a round-ended stroke, mixing each pixel once even at overlapping end caps |
| Where are RGB and PALETTERGB interpreted? | [Win16Color](../src/AfterDarker.Core/Win16/Win16Color.cs); pens and brushes use the same explicit true-color policy |
| Where are brushes allocated, selected and deleted? | [Win16Drawing](../src/AfterDarker.Core/Win16/Win16Drawing.cs) owns the bounded brush pool; [PlaybackResult](../src/AfterDarker.Runtime/PlaybackResult.cs) exposes live/peak brush counts |
| Who writes the shared SDK record layout for newer modules? | [StandardModuleRecords](../src/AfterDarker.Runtime/Modules/StandardModuleRecords.cs); profiles supply the meanings of its four control words |
| Who runs and resumes machine code? | [SegmentedGuest.RunUntil](../src/AfterDarker.Runtime/SegmentedGuest.cs) |
| What do the register snapshots mean? | [SegmentedGuest diagnostics](../src/AfterDarker.Runtime/SegmentedGuest.Diagnostics.cs) |
| How do we make the initial call into the DLL? | [GuestCallerBuilder](../src/AfterDarker.Runtime/Calls/GuestCallerBuilder.cs) |
| How does a DOS interrupt differ from an imported call? | [DosInterruptDispatcher](../src/AfterDarker.Runtime/Calls/DosInterruptDispatcher.cs) and [DOS source reference](research/dos-source-reference.md) |
| Who chooses host-record and stack addresses? | [SessionMemoryLayout](../src/AfterDarker.Runtime/SessionMemoryLayout.cs) |
| Who sequences startup, frames, and shutdown? | [AfterDarkSession](../src/AfterDarker.Runtime/AfterDarkSession.cs) and [its diagnostic types](../src/AfterDarker.Runtime/AfterDarkSession.Diagnostics.cs) |
| Which module versions can run? | [SupportedModules](../src/AfterDarker.Runtime/SupportedModules.cs) |
| What differs between supported modules? | [ModuleProfile](../src/AfterDarker.Runtime/Modules/ModuleProfile.cs), [MondrianProfile](../src/AfterDarker.Runtime/Modules/MondrianProfile.cs), [SpiralGyraProfile](../src/AfterDarker.Runtime/Modules/SpiralGyraProfile.cs), [RainstormProfile](../src/AfterDarker.Runtime/Modules/RainstormProfile.cs), [FadeAwayProfile](../src/AfterDarker.Runtime/Modules/FadeAwayProfile.cs) |
| Who supplies an image for a module to erase? | `ModuleProfile.CreateInitialPixels` and [PixelSurface.LoadRgb](../src/AfterDarker.Core/Rendering/PixelSurface.cs); [Fade Away's input and completion](research/fade-away-execution.md) |
| How is a POINT passed by value? | [Point16](../src/AfterDarker.Core/Win16/Point16.cs), `Win16ArgumentReader.ReadPoint`, and the [Rainstorm notes](research/rainstorm-execution.md) |
| What may a UI call? | [IAnimationSession](../src/AfterDarker.Runtime/IAnimationSession.cs), [PlaybackOptions](../src/AfterDarker.Runtime/PlaybackOptions.cs), [PlaybackResult](../src/AfterDarker.Runtime/PlaybackResult.cs) |
| Who paces frames and owns the background guest? | [AfterDarkPlayback](../src/AfterDarker.Runtime/AfterDarkPlayback.cs) and [WPF guide](wpf-player.md) |
| How can an image be presented before DRAWFRAME returns? | [ImportFrameCheckpoint](../src/AfterDarker.Runtime/Presentation/ImportFrameCheckpoint.cs) describes a profile's image boundary; [ImportFrameCapture](../src/AfterDarker.Runtime/Presentation/ImportFrameCapture.cs) matches it after gateway dispatch and emits `IntermediateFrameReady` |
| Where is Rainstorm's brief inverted image made visible? | [RainstormProfile.FrameCheckpoints](../src/AfterDarker.Runtime/Modules/RainstormProfile.cs) identifies the first InvertRect return; [the evidence](research/rainstorm-execution.md) explains its 80-ms hold |
| Can a presenter identify an intermediate image? | [FrameInfo.IsIntermediate](../src/AfterDarker.Runtime/LatestFrameMailbox.cs) is copied atomically with the pixels; WPF's `--smoke-intermediate` mode requires that marker |
| Where are selected pens, brushes and the current point stored? | [Win16Drawing](../src/AfterDarker.Core/Win16/Win16Drawing.cs), [Win16Pen](../src/AfterDarker.Core/Win16/Win16Pen.cs) and [Win16DeviceContext](../src/AfterDarker.Core/Win16/Win16DeviceContext.cs) |
| Where are ellipse outlines, fills and the raster approximation explained? | [EllipseRasterizer](../src/AfterDarker.Core/Rendering/EllipseRasterizer.cs) builds row spans; [Hard Rain's notes](research/hard-rain-execution.md) record the native comparison |
| Where do operations become pixels? | [PixelSurface](../src/AfterDarker.Core/Rendering/PixelSurface.cs) and [CosmeticLineRasterizer](../src/AfterDarker.Core/Rendering/CosmeticLineRasterizer.cs) |
| Which tests explain the different Rectangle/FillRect bounds? | [SelectedRectangleRasterTests](../tests/AfterDarker.Tests/Conformance/SelectedRectangleRasterTests.cs) and the existing [RectangleRasterTests](../tests/AfterDarker.Tests/Conformance/RectangleRasterTests.cs); [WindowsShapeOracle](../tests/AfterDarker.Tests/Conformance/WindowsShapeOracle.cs) supplies native shape pixels only in tests |

Folders group responsibilities; new runtime files retain the `AfterDarker.Runtime`
namespace. The `.Diagnostics.cs` files hold nested types on partial classes so
existing tutorial type names remain usable. Import traces now use the independent
`Win16CallTrace` type. The former session-level `DispatchImport` helper has moved
to `Win16ImportGateway.Dispatch`.

## Following one mocked call

```mermaid
flowchart TD
    A[Guest CALL FAR] --> B[CPU hook stops at gateway]
    B --> C[RunUntil regains managed control]
    C --> D[Win16ImportGateway resolves binding]
    D --> E[Win16Stack.ReadCallFrame]
    E --> F[Win16ApiDispatcher reads typed arguments]
    F --> G[Win16Api performs service]
    G --> H[Win16RegisterConvention.WriteReturnRegisters]
    H --> I[Win16Stack.ReturnToCaller]
    I --> J[RunUntil resumes at restored CS:IP]
```

`EmuStop` ends the current blocking engine run. It neither destroys the CPU nor
returns from the guest procedure. The gateway completes that guest return
after managed execution regains control.

For profiles with intermediate-image checkpoints, the session observes the
completed import return between `ReturnToCaller` and native resume. It matches
the import and returned PC, copies pixels and calls the presenter on the same
worker. This is a host callback with no guest reentry, not an additional Win16
import or a modification of the guest's stack. See [Zot!'s flow](research/zot-execution.md).

For a three-word Pascal call, the stack at the gateway is:

```text
Increasing addresses within SS:
SP + 0   saved IP              <- next instruction after CALL FAR
SP + 2   saved CS
SP + 4   third argument        <- pushed last
SP + 6   second argument
SP + 8   first argument        <- pushed first

after return: SP = old SP + 4 + 6; IP = saved IP; CS = saved CS
```

The CPU made this frame. `ReadCallFrame` validates SS, bounds, whole-word
arguments and an executable return address before any service side effect.
It leaves registers unchanged and returns words in source order.
`ReturnToCaller` models `RETF 6`: restore CS:IP and advance SP past the frame
and arguments. It does not erase memory. AX/DX results are a separate concern,
handled first by `WriteReturnRegisters`. Void functions preserve those registers.

The source-ordered words still need interpretation: a far pointer is selector
then offset; a DWORD is high word then low word. `Win16ArgumentReader` groups
each pair as one parameter and interprets signed coordinates without changing
their bits.

## Register assignments have different reasons

`SetUpPrologRegisters` means host preparation before NE startup. It does not
replace the DLL's compiler-generated function prologue, nor run before every
mocked API. The DLL's own instructions still execute.

| Assignment | Purpose |
| --- | --- |
| Startup DS = automatic-data selector | Address the DLL's globals. |
| Startup DI = instance token | Our narrow loader uses the same selector as the instance identity. |
| Startup CX = NE heap size | Report the local heap reservation to the compiler runtime. |
| Startup ES:SI = 0:0 | Supply a null optional command-line pointer. |
| Startup SS:SP = stack selector and empty offset | Supply storage for CALL, PUSH and stack frames. |
| Startup BP = 0 | Terminate the initial frame chain. |
| Before MODULE, DS = host-data selector | Exercise the DLL export prologue's DS setup and restoration. |
| API word result: AX | Return a word value to the guest. |
| API DWORD/far pointer result: DX:AX | Return high and low words in their prescribed registers. |
| GlobalLock also sets CX | Supply its additional selector result. |
| Far return: CS, IP, SP | Restore control flow and remove arguments. |

AX is not assigned fabricated success before DLL startup. The original DLL
returns it. `GuestCallerBuilder` emits a real CALL FAR and memory store of the
returned AX; the host later compares the register and stored values.

DOS INT 21h is different. Unicorn advances IP without pushing an interrupt
frame; `SegmentedGuest` verifies that observed contract. `DosInterruptDispatcher`
updates date/time result registers without RETF or IRET. Separate conformance
tests preserve this distinction.

## Drawing state and pixels

For the bitmap/resource family, start with these purpose-named files:

| Question | Implementation |
| --- | --- |
| Where does a resource name become a file range? | `Core/Ne/NeResourceCatalog.cs`, `NeResourceAlias.cs` |
| Where are packed image bytes turned into RGB? | `Core/Rendering/DibBitmapDecoder.cs`, `DecodedBitmap.cs` |
| Where does a guest LoadBitmap call create an HBITMAP? | `Core/Win16/Win16Api.Resources.cs`, `Win16Drawing.Bitmaps.cs` |
| Where do selected regions constrain pixels? | `Win16Drawing.Regions.cs`, `Win16DeviceContext.Draw`, `PixelSurface.Clipping.cs` |
| Why do text colors matter to sprites? | `Win16Drawing.Colors.cs`, `Win16Drawing.Blits.cs` |
| Where is GeoBounce's polygon filled? | `Win16Api.Polygons.cs`, `Rendering/PixelSurface.Polygon.cs` |

The [resource walkthrough](ne-bitmap-resources.md) connects these responsibilities;
Tutorial 11 uses the same catalog and decoder without CPU execution. Module
profiles supply only artifact identity, controls and checked global observations.

An HDC identifies `Win16DeviceContext`, retaining separate pen/brush slots, current
position, window origin and ROP2. `Win16Drawing` owns the HDC/object registries and lifetimes.
`PixelSurface` owns bytes, clipping and change counts. `CosmeticLineRasterizer`
names the longer-axis step and shorter-axis rounding separately. Clipping
follows pixel generation so an off-screen start cannot change rounding phase.

The raster is compared with modern native GDI in 1,500 line cases. This is a
measured compatibility result, not complete historical Win3.1 fidelity.
Ellipse uses a separately documented software policy: its tested Hard Rain ring
sizes stay within one pixel of native GDI colors, but are not pixel-exact.
The pen carries its width; a brush selection never replaces the selected pen.
Stained Glass's [execution guide](research/stained-glass-execution.md) explains
which calls use ROP2, why XOR requires single coverage, and why overlapping
BitBlt reads a source snapshot. These drawing services remain independent of
module profiles and WPF.

Gravity extends the same model with selected color bitmaps. A bitmap owns pixels;
a memory DC owns attributes and a selection. Deleting a DC releases its selection
without deleting its bitmap. Explicit ROP3 mask operations combine bitmap, brush
and destination independently of ROP2. The [Gravity guide](research/gravity-execution.md)
connects this lifetime to the original module's construction and drawing calls.

## Tutorials and contribution style

Lessons 01–04 keep tiny CPU experiments local. Lesson 06 also retains an
explicit loader/caller/gateway walkthrough. That duplication is intentional
teaching material, now identified in comments. Extend the shared runtime for
new player behavior; use those lessons to understand its foundations. Lessons
08/09 already use the shared session.

Prefer purpose-named types, descriptive variables and ordinary control flow.
A method such as preparing startup inputs or returning to a caller should
locate the mechanism and keep its register writes visible. Avoid generic
frameworks that make readers chase indirection to discover intent.

XML summaries, parameter descriptions and `see` references supply IntelliSense.
Core and Runtime emit XML documentation beside their assemblies for library
consumers. Older undocumented APIs remain incremental work: missing-comment
warnings are suppressed, while malformed XML and unresolved references remain
compiler diagnostics. Local variables use descriptive names and ordinary
comments; C# does not attach XML member documentation to local-variable tooltips.

The recovered SDK supplies real structure names, but this pass does not expand
the narrow guest allocations or explain the two extra compatibility words.
See [SDK findings](research/after-dark-sdk.md).

## Verification

[Win16CallingConventionTests](../tests/AfterDarker.Tests/Conformance/Win16CallingConventionTests.cs)
check startup inputs, non-mutating frame reads, cleanup and invalid-frame
rejection. Import-gateway tests execute real tiny far calls through the shared
gateway, checking signedness, pointer translation, results and stack cleanup.
Original-module tests cover lifecycle, state, deterministic pixels, budgets
and shutdown. See [testing](testing.md) for the default and opt-in commands.
