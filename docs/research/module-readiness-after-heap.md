# Next modules after completing the local-heap group

Reassessment date: 2026-09-19. Runtime baseline: reviewed main `2a276f0`
(String Theory and Zot! merged in PR #18). This is planning work; no new
screensaver, API implementation or loader behavior is enabled here.

**Recommendation: Hard Rain, then Shapes, then a constrained Stained Glass
configuration.** These extend the drawing surface without first requiring
bitmap resources, a sound helper, a global allocator or floating-point startup.
The next reusable capability is selected brushes and filled/outlined geometry.

The [original sweep](module-readiness-sweep.md) remains historical evidence.
The [new machine-readable census and probe results](module-readiness-after-heap.json)
record the present registry hash and fresh first failures.

**Follow-up, 2026-09-20:** [Rainstorm's lightning presentation](rainstorm-execution.md)
is now implemented using the shared checkpoint mechanism. The next-module
ranking and historical probe results below are unchanged.
The subsequent [Hard Rain increment](hard-rain-execution.md) now implements
playback with pen widths, selected stock brushes and a documented software ellipse
raster. The subsequent [Shapes increment](shapes-execution.md) adds owned
brushes, null pens and rectangles, accepting its palette request with the owner's
approved direct-RGB policy. Constrained Stained Glass is next. The original
probe results below remain historical evidence.

## What was checked again

- **Artifact facts:** the folder still contains 29 NE modules, all matching the
  earlier hashes. Eight are supported, leaving 21. Starry Night is still absent.
- **Observed runtime behavior:** all 21 unsupported modules were probed against
  the current Core/Runtime services. Ten pass the current load plan; eleven
  stop in loader checks. None completed the research playback sequence.
- The shared local heap was available, including bounded growth in a mapped
  64-KiB data segment. The production clock alias, stock black pen and PtInRect
  implementations were used. Unknown imports received distinct guarded gateway
  addresses and failed on use; they did not return fabricated success.
- Extra probes tested minimum numeric controls, Shapes' Color switch and
  Gravity's unavailable-sound path. Static inspection followed relevant
  relocation-annotated code in Hard Rain, Shapes, Stained Glass, Hall of Mirrors
  and Wrap Around. Static observations below are not executed-path proofs.

The baseline supplied a 320x240 true-color surface, SDK-sized host records,
resource-derived diagnostic controls and a clock advancing 2,500 ms per read.
Each invocation allowed 2,000,000 instructions, 4,096 service exits and 3-second
native slices, retaining the runtime's cumulative native-time check. Each
module ran in a separate process with a 25-second watchdog. All returned a
symbolic first failure; none exhausted that process watchdog.

The intended sequence was startup, PREINITIALIZE, INITIALIZE, BLANK, ten draws,
CLOSE and WEP. Reaching a blocker means later stages remain untested. Control
values are diagnostic inputs, not certified original defaults or all-mode tests.
The local probe used the existing test apphost so its normal native-engine setup
applied. Its temporary compilation source was removed afterward; the default
241-test suite passes. Local harness, disassembly and detailed reports remain
under ignored `artifacts/module-reassessment/`.

## Recommended implementation order

| Priority | Module | Current evidence | Work to establish playback |
| --- | --- | --- | --- |
| 1 | **Hard Rain** | Startup, initialization and BLANK complete. First draw requests a solid width-2 pen; minimum drop controls do too. | Width-2 outlines, selection/restoration of a brush, and Ellipse rendering with the selected pen/brush. |
| 2 | **Shapes** | BLANK requests RGB_PAL (11), or GREY_PAL (12) with Color off. Explicit research continuation reaches NULL_PEN (8) on the first draw. | Null pen, owned solid brushes, selected-brush lifetimes, filled Ellipse/Rectangle, and a documented true-color palette-request policy. |
| 3 | **Stained Glass** | INITIALIZE reaches CreateSolidBrush even at minimum numeric controls. | Reuse geometry/brush work, then implement the reached coordinate-origin, raster-operation, pixel, rectangle-helper and blit behavior for a constrained configuration. Larger than the first two. |
| 4, provisional | **Gravity** | The new clock alias works; initialization now reaches ADWOPENSOUND even with Sound off. | Credible muted sound/resource behavior plus off-screen DC/bitmap ownership, BitBlt and filled geometry. A shared capability step, not another heap-sized patch. |
| 5, provisional | **GeoBounce** | INITIALIZE reaches CreateCompatibleDC. | Reuse bitmap/DC and sound work; add Polygon filling for its selected polyhedron. |
| Following that family | **Nocturnes**, **Can of Worms**, **Punch Out** | First blockers are compatible DC, OffsetRect and compatible DC respectively. | Nocturnes additionally imports LoadBitmap; Can of Worms needs rectangle helpers/bitmap composition; Punch Out adds clipping regions. All also reference AD_SND. Their relative order needs another probe after the common services exist. |

These priorities reflect the cost of the next shared mechanism, not just the
number of absent imports. A registered import can still reject a new argument;
Hard Rain has no unbound import identities but cannot yet draw its width-2 pen.

### Hard Rain: an actual missing rendering behavior

**Observed:** its first CreatePen arguments decode as solid style, width 2 and
a COLORREF. Reducing both numeric controls leaves the same request.

**Static evidence:** `S2:02DD` calls CreatePen, `S2:02F3` obtains BLACK_BRUSH,
and `S2:02F9` selects that brush into the HDC before Ellipse at `S2:0358`.
The caller restores its selected objects afterward. Another ellipse call at
`S2:0420` belongs to the erasure code. This requires a device context which
tracks a selected brush as well as its pen. Mapping width 2 to the existing
one-pixel pen would conceal the missing behavior and weaken the proof.

### Shapes: palette request plus straightforward filled geometry

The recovered SDK's MODULE.H defines BLANK replies 11 and 12 as RGB_PAL and
GREY_PAL respectively. See [SDK provenance](after-dark-sdk.md). This fresh
probe checks those replies before drawing; the old baseline's shorter blocker
description omitted that host decision.

An explicitly labeled research continuation past that request reaches the
existing null-pen guard. It does not implement a palette or prove drawing.
Static code selects NULL_PEN at `S2:009B`, creates a solid PALETTERGB brush at
`S2:0124`, and chooses Ellipse at `S2:01DC` or Rectangle at `S2:01F3`. It restores
the previous objects and deletes the temporary brush afterward.

**Reasoned inference:** a true-color policy analogous to our existing HSV_PAL
policy should fit these direct RGB/grayscale colors. It still needs explicit
tests and profile validation before support is claimed. No full indexed-palette
implementation is established by that inference.

### Stained Glass: best reuse after shapes, with a wider proof boundary

Its first failure is CreateSolidBrush; both default diagnostic controls and
the minimum controls reach it. Relocation-backed static calls include SetROP2
at `S8:00CA`, SetWindowOrg at `S8:02E7`, numerous SetPixel calls in S5, FrameRect
in S6, and StretchBlt/BitBlt at `S7:0864`/`S7:0894`.

This gives a concrete list to investigate as execution advances. It does not
prove every listed call is needed by the first chosen configuration. A narrow
fixed configuration remains preferable to claiming all duplication/complexity
settings. Zot!'s new intermediate-image mechanism also makes progressive
construction less of an architectural unknown, but Stained Glass would need
its own verified presentation boundaries if completed-call images are insufficient.

## Two candidates for short investigations, not promised easy additions

**Hall of Mirrors** deserves a closer look before committing to the whole bitmap
and sound family. It has no WIN87EM, AD_SND or AD_RSRC dependency and no OS fixups.
The current loader rejects MODULE's prologue at `S1:0A8A`. Static inspection
shows `MOV AX,immediate`, with a selector relocation on its operand at `S1:0A8B`
targeting data segment 2, followed by the ordinary save-DS/load-DS sequence.
The relocation's second target word is unused for this selector-only fixup.

**Reasoned inference:** this is an already-relocated DS setup which our export
prologue recognizer should validate rather than patch a second time. That makes
the first loader blocker look narrow. It does not make the whole module easy:
startup statically calls DOS INT 21h/AH=30h and GetWinFlags; later code contains
GlobalAlloc/GlobalReAlloc/GlobalHandle and BitBlt. Our local heap and fixed
global-record registry are not yet a general global-memory allocator. None of
those later paths was executed in this reassessment.

**Wrap Around** remains the most focused candidate for a floating-point/loader
experiment. Its only unbound import identity is WIN87EM `_fpMath`, but it also
contains **57 OS-fixup records** and imports that helper through a far pointer
in data segment 6 at `0438`. The helper has a register-based signature in the
existing Wine ordinal reference. Counting it as one ordinary missing Windows
method would hide a different execution contract. A successful narrow proof
could help Mountains, Vertigo and other floating-point modules; success is not
yet established, and their additional drawing/palette APIs remain separate.

## Why the other modules stay later

| Group | Remaining modules | Fresh blocker and broader known work |
| --- | --- | --- |
| Bitmap/sound family | Can of Worms, GeoBounce, Gravity, Nocturnes, Punch Out | All pass loading. Need off-screen drawing and/or sound-helper behavior beyond the current renderer. |
| Files/text/window behavior | GraphStat, Sounder | GraphStat reaches GetWindowsDirectory during INITIALIZE; Sounder reaches IsWindow. Their imports include real file/font or sound/dialog behavior, so those needs cannot all be assigned to unused options dialogs. |
| Floating-point / OS fixups | Down the Drain, Mountains, Penrose, Vertigo, Wrap Around | All stop on OS fixups. Follow-on work includes blits/regions, polygons, palettes or helper ABI behavior depending on the module. |
| AD_RSRC and broader loader/helper work | Aquatic Realm, Clocks, Globe, Marbles, Swan Lake | All stop on additive internal selector relocations; some also contain OS fixups. AD_RSRC ordinal contracts, resources and broader memory/graphics remain unresolved. |
| Separate prologue/global-memory case | Hall of Mirrors | Potentially narrow first loader issue, followed by the unproven services described above. |

The clock change moved Gravity's first failure to sound; it did not remove the
sound or off-screen drawing requirements. With its Sound control set to zero,
the baseline still calls adwOpenSound. A research-only FALSE reply then reaches
adwLoadSoundResource. Muted playback needs a coherent helper contract rather
than one false return. This agrees with the earlier experiment and now occurs
using the production clock implementation.

## Small improvements to already-supported modules

- **Rainstorm's visible lightning** was the first follow-up chosen and is now
  implemented. Its paired inversions use a verified first-return checkpoint,
  an explicit hold policy and visible-image/cancellation tests. This improves
  presentation without adding a ninth module.
- **More Fade Away styles:** the old bounded probes already exercised Plain,
  Blinds and Mesh as well as Radar on a supplied image. They need production
  profiles/selection and completion tests; other styles may benefit from the
  upcoming shape raster work. Those older mode probes were not rerun here.

The useful planning rule remains: **choose the next missing mechanism with the
best reuse, then execute far enough to discover what it actually unlocks.**
The present evidence supports doing the two shape modules next and reassessing
after Stained Glass, rather than treating every remaining module as equally close.
