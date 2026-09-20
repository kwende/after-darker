# Which original modules should we tackle next?

Sweep date: 2026-09-19. Runtime baseline: `8a16729`.

**New reassessment:** after completing the heap group, all 21 remaining modules
were probed against `2a276f0`. See the [current priorities and fresh blockers](module-readiness-after-heap.md).
Hard Rain, Shapes and Stained Glass remain the recommended next implementation
order; the new report also identifies the palette decision for Shapes and a
potentially narrow loader issue in Hall of Mirrors.

**Follow-up:** [Rainstorm playback is now implemented](rainstorm-execution.md)
with tested fixed controls. Its initially missing intermediate lightning image
is now presented by the follow-up described in those execution notes.
The census/probes below remain the historical pre-Rainstorm baseline, including
their original registry hash. Their supported-module counts are not current UI state.

[Fade Away's Radar effect has also been integrated](fade-away-execution.md),
using the requested white starting image and tests through original completion.
Other styles in this sweep are still research observations, not selectable modes.

[Lasers is now integrated](lasers-execution.md) with the shared bounded local
heap. The owner chose to complete Lasers first from row 3.
[Magic now runs through the same heap and GDI services](magic-execution.md),
with a tested 100-line history and horizontal mirroring.
[String Theory](string-theory-execution.md) and [Zot!](zot-execution.md) now
complete row 3: three groups of strings reuse the movable heap, while Zot!
uses fixed blocks, a clock alias and bounded intermediate-image presentation.
The WPF player now supports eight modules. Row 4 (Hard Rain and Shapes) remains
future work. The first-blocker observations below retain the historical baseline.

**Rainstorm and selected Fade Away modes are the closest additions.** Four
more modules share a local-heap gap; two more share basic drawing gaps. Those
eight are a useful expansion target. The evidence does not support calling all
27 remaining modules low-effort work.

This is a planning and execution investigation. **The WPF player still supports
only Mondrian and Spiral Gyra.** Small adapters described below were confined to
an ignored research harness; they are not production implementations or tests.

## What was scanned and executed

- **Artifact facts:** all 29 NE modules in the local `ad/` folder were inspected
  again: 28 `.ad` files and `Aquatic Realm.ad.dll`. Their SHA-256 hashes match the
  [earlier artifact manifest](ad-import-manifest.json). There are no new module
  revisions in this particular sweep. Starry Night is not in this folder.
- **Observed runtime behavior:** 18 modules passed the existing NE load plan
  when unimplemented imports were assigned guarded gateway addresses. Eleven
  stopped in the loader. Mondrian and Spiral Gyra completed the baseline run;
  the other 16 loadable modules stopped at named, unsupported service behavior.
- **Follow-up experiments:** Rainstorm produced changed pixels and completed
  shutdown after two small service adapters. Four Fade Away styles completed
  and changed a synthetic starting image. Advancing Zot!'s clock exposed an
  allocation requirement hidden by a short idle run.
- **Reasoned inference:** sharing a missing API family makes modules good joint
  targets. It does not prove that implementing that family will finish them:
  a first failure prevents observation of everything beyond it.

The [machine-readable baseline](module-readiness.json) includes every artifact
hash, registry source hash, import gap, completed phase and first diagnostic.
An import count measures identities absent from our registry, not required
implementation effort. A registered service may reject an unsupported argument;
an absent service may be confined to an unused dialog.

## Recommended order

| Order | Modules | Shared work | Evidence and remaining uncertainty |
| --- | --- | --- | --- |
| 1 | **Rainstorm** | Stock black pen lookup; `PtInRect`; module profile and measured execution budgets | Ten draws changed pixels; CLOSE/WEP completed with zero locks/pens using research adapters. Strongest next target. |
| 2 | **Fade Away** | Stock black pen for Radar; a meaningful starting image; module profile | Plain, Blinds, Radar and Mesh changed pixels and completed ten draws plus shutdown. Other styles still need drawing services. |
| 3 | **Lasers, Magic, String Theory, Zot!** | Checked Win16 local heap: `LocalAlloc`, `LocalFree`, `LocalLock`, `LocalUnlock`; clock alias for Zot! | First three reach allocation during INITIALIZE. Zot! reaches it when enough simulated time elapses. These four are candidates, not rendered proofs. |
| 4 | **Hard Rain, Shapes** | Wider pens, null pen, solid brushes, ellipse/rectangle drawing | Both reach DRAWFRAME already. Hard Rain requests a width-2 pen; Shapes first requests the null stock pen. Shape raster semantics need real implementation and tests. |
| 5 | **Stained Glass** | Extend brush/shape work with window origin, raster operations, pixels and blits as reached | INITIALIZE reaches `CreateSolidBrush`. Its additional imports make it a follow-on investigation rather than another proven small addition. |

Start with the first two, then reassess after the local heap. If those shared
capabilities suffice, the first four rows would take us from **two supported
modules to ten**. That is a useful weekend target, not a delivery guarantee.

### What the two closest experiments actually proved

**Rainstorm:** the baseline stops at `GetStockObject(BLACK_PEN)`. Returning the
existing real stock black-pen handle exposes `USER!PtInRect`. A checked RECT
read and signed point test then allow the original module to finish ten draws,
CLOSE and WEP. It executed 246,081 instructions in total, made 520 point tests,
and ended with 11,298 nonzero RGB bytes on an initially black 320x240 surface.
Final outstanding locks and live created pens were both zero; WEP returned 1.

The point's coordinates are passed **by value**, unlike the RECT's far pointer.
The Win16 declaration and Wine ordinal signature anchor the ABI. The adapter
uses left/top inclusive and right/bottom exclusive bounds, consistent with the
[documented PtInRect geometry](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-ptinrect).
That modern reference supports rectangle semantics, not the Win16 stack layout.
This needs public boundary/ABI tests before promotion.

Rainstorm also makes hundreds of host calls per draw. Its profile cannot simply
inherit the default 128-service budget; the experiment allowed 1,024 per call.
Production limits should follow measured per-frame work at supported settings.

**Fade Away:** this module modifies an existing image. A black initial surface
hides several effects, so the follow-up supplied a synthetic white image. That
is a **modern host adaptation**, not an original desktop capture. It gives the
guest meaningful input without inventing any drawing inside the module.

| Style value | Style | Result with white starting image and the small adapters enabled |
| ---: | --- | --- |
| 0 | Plain | Completes; clears the image to black. Only existing service behavior is reached. |
| 1 | Squares | Stops at `GetStockObject(NULL_BRUSH)`. Later requirements remain unobserved. |
| 2 | Slow Zoom | Stops at `GetStockObject(NULL_BRUSH)`. Later requirements remain unobserved. |
| 3 | Blinds | Completes; changed image. Only existing service behavior is reached. |
| 4 | Radar | Completes; changed image after stock black-pen lookup is added. |
| 5 | Mesh | Completes; changed image. Only existing service behavior is reached. |
| 6 | Dissolve | Stops at `GDI!PatBlt`. |

Each completed style returned from ten draws and CLOSE/WEP. Radar's final RGB
hash was `4e7ad459f85f1c32a091c786c9a7fc8a7c7338a8d29836b89405ac44c355e72c`;
Rainstorm's was `214f04c33cc4d1ab4f265c898738c0d3bd4d851b0f51eed0251529f78f27dd71`.
These are local deterministic observations under the probe settings, not
reference Windows 3.1 images. Mesh changes byte values while leaving the count
of nonzero bytes unchanged: a nonzero-byte count alone is not a drawing test.

### Why the next heap step is useful

The four heap candidates have a particularly small shared import gap. However,
our existing `LocalInit` only validates a reserved region. It does not implement
allocation, handle/offset relationships, locking, reuse or failure behavior.
Those mechanisms should live in a reusable, per-guest heap with deterministic
tests, not four module-specific success stubs.

Zot! demonstrates why timing matters: after adding the `GetCurrentTime` alias
to our existing tick service, ten ordinary calls returned without drawing.
Increasing the virtual tick step to 1,000 ms reached `LocalAlloc` on draw five.
The clock alias is easy; it does not by itself make Zot! playable.

## Complete baseline: one stopping point per module

These are **first observed blockers**. The report deliberately does not equate
this column with the complete work needed to support the module.

| Module | Current loader | First baseline result / blocker |
| --- | --- | --- |
| Aquatic Realm | Blocked | Additive internal selector relocation; also imports AD_RSRC, AD_SND and WIN87EM. |
| Can of Worms | Pass | INITIALIZE: `USER!OffsetRect`; bitmap/DC and sound imports remain. |
| Clocks | Blocked | Additive internal selector relocation; also imports all three helper libraries. |
| Down the Drain | Blocked | OS fixup relocation; WIN87EM dependency. |
| Fade Away | Pass | DRAWFRAME: stock black pen; follow-up above. |
| GeoBounce | Pass | INITIALIZE: `GDI!CreateCompatibleDC`; also sound dependency. |
| Globe | Blocked | Additive selector and OS fixups; AD_RSRC/WIN87EM. |
| GraphStat | Pass | INITIALIZE: `KERNEL!GetWindowsDirectory`; file and text/font services are additional gaps. |
| Gravity | Pass | INITIALIZE: `USER!GetCurrentTime`, then AD_SND calls in the follow-up. |
| Hall of Mirrors | Blocked | Export 1 has an unrecognized Win16 prologue. Global allocation and blits also need work. |
| Hard Rain | Pass | DRAWFRAME: width-2 `CreatePen`; ellipse import remains unimplemented. |
| Lasers | Pass | INITIALIZE: `KERNEL!LocalAlloc`. |
| Magic | Pass | INITIALIZE: `KERNEL!LocalAlloc`. |
| Marbles | Blocked | Additive selector and OS fixups; all three helper libraries. |
| Mondrian | Pass | Baseline completes; already supported. |
| Mountains | Blocked | OS fixup relocation; WIN87EM dependency. |
| Nocturnes | Pass | INITIALIZE: `GDI!CreateCompatibleDC`; also sound dependency. |
| Penrose | Blocked | OS fixup relocation; WIN87EM dependency. |
| Punch Out | Pass | INITIALIZE: `GDI!CreateCompatibleDC`; also sound dependency. |
| Rainstorm | Pass | DRAWFRAME: stock black pen, then `PtInRect`; follow-up above. |
| Shapes | Pass | DRAWFRAME: stock null pen; brushes, rectangles and ellipses also absent. |
| Sounder | Pass | INITIALIZE: `USER!IsWindow`; substantial sound/window/import surface. |
| Spiral Gyra | Pass | Baseline completes; already supported. |
| Stained Glass | Pass | INITIALIZE: `GDI!CreateSolidBrush`; additional GDI state and geometry imports. |
| String Theory | Pass | INITIALIZE: `KERNEL!LocalAlloc`. |
| Swan Lake | Blocked | Additive selector and OS fixups; AD_RSRC/WIN87EM. |
| Vertigo | Blocked | OS fixup relocation; WIN87EM dependency. |
| Wrap Around | Blocked | OS fixup relocation; its only unbound import is WIN87EM `_fpMath`. |
| Zot! | Pass | INITIALIZE: clock alias; accelerated-clock follow-up reaches `LocalAlloc`. |

### Work that is larger than a small API addition

- **Bitmap device contexts and blits:** GeoBounce, Nocturnes and Punch Out
  immediately need a compatible DC. Can of Worms and Gravity also have broader
  drawing requirements. Off-screen surfaces, selected bitmaps, raster operations
  and object ownership form a shared capability, but it is larger than another
  line-drawing function.
- **Sound:** the recovered SDK contains `AD_SND.H`, so we now have declarations
  for the named helpers. This improves on the earlier census's name-only
  knowledge. It does not establish all failure paths. In Gravity, returning
  FALSE from `adwOpenSound()` still leads to `adwLoadSoundResource()` during
  INITIALIZE. A credible muted-sound model needs more than one failed open.
- **Floating point and OS fixups:** eight modules contain OS fixups; ten import
  WIN87EM `_fpMath`, whose calling convention is register-based. Wrap Around's
  single import gap is therefore misleading. Unicorn's x87 instruction support
  alone would not implement this helper ABI or determine the fixup policy.
- **Loader additions:** five modules contain additive internal selector
  relocations, with three also in the OS-fixup group. Ten distinct files are
  blocked by those two relocation categories. Hall of Mirrors adds a separate
  prologue case. Understanding each transformation is required before extending
  the loader; simply skipping its guards would not establish valid loading.
- **AD_RSRC:** Aquatic Realm, Clocks, Globe, Marbles and Swan Lake import this
  missing helper. Its ordinal contracts remain unresolved. The recovered SDK
  does not supply an AD_RSRC header. These are poor early small-effort targets.
- **Files and text:** GraphStat reaches a path query in INITIALIZE. Its file
  services cannot yet be dismissed as options-dialog-only behavior. A fonts/text
  path and other imports also need investigation. Sounder has 38 unbound imports
  and is not a comparable quick drawing target.

## Shared preparation with value across this folder

Every module contains four AD_CONTROL resources of the documented types 0–5.
The [recovered SDK](after-dark-sdk.md) gives us a common host-record layout and
resource format. A typed control-resource reader and explicit control-value
conversion should replace repeated per-module byte guesses. The meanings of
the four values remain module-specific. Settings UI is not required to supply
validated initial values.

The research harness also separated **binding an imported identity** from
**implementing its behavior**. Unknown callable imports received distinct
guarded gateway slots and stopped symbolically if reached; they never returned
fabricated success. That allowed investigation past unused error/UI imports.
The production registry still rejects unrecognized imports during binding.
Any generalization must retain fail-on-use diagnostics, bounds, and the
distinction between callable imports and imported constants such as `__AHSHIFT`.

For production promotion, add shared implementations and ABI tests, a bounded
module profile, settings-specific private integration tests, pixel-change and
shutdown checks, then actual WPF acceptance. Retain the content-hash gate until
another artifact revision has its own evidence. See the
[runtime code map](../runtime-code-map.md) for the relevant responsibilities.

## Probe protocol and reproduction

The ignored local harness is `artifacts/module-sweep/Program.cs`. It references
the existing Core and Runtime projects and uses the real parser, load plan,
segmented guest, import gateway, stack return logic and software surface. It is
a disposable investigation tool, not an additional maintained tutorial or a
public conformance test. A fresh clone can reproduce the static census with
local modules; the native probe source/results are retained locally, not shipped.

Each module ran in its own process. Each guest invocation had a 200,000
instruction limit and 1,024 gateway exits; the engine's cumulative execution
time limit also applied. The process watchdog was 15 seconds for the baseline
and 20 seconds for follow-ups. No final baseline process timed out.

The host supplied a 320x240, 24-bit surface; SDK-sized module records; the
existing system-record compatibility tail; and four resource-derived control
values. Numeric sliders used an in-range midpoint (or explicit low-bound probe),
**not a claim to reproduce the original engine's numeric default conversion**.
String sliders used the documented discrete bounds, and combo choices were
passed by index. Rainstorm's four values were `60, 50, 52, 40`; Fade Away's
were `style, 0, 0, 0`.

The sequence was startup, PREINITIALIZE, INITIALIZE, BLANK, up to ten DRAWFRAME
calls, CLOSE and WEP. It checked restored SS/SP/BP and recorded return values,
instructions, locks, pens and surface revisions. Follow-ups also rejected
unsupported lifecycle return codes. MODULESELECTED, settings dialogs, arbitrary
resolutions, seeds, long-run behavior and every option were not exercised.
Successful shutdown observations apply only to runs that reached shutdown.

The three small adapters were black-pen lookup, the clock alias and `PtInRect`.
The sound experiment separately returned FALSE for the zero-argument sound
capability/open calls. All other unsupported behavior still stopped execution.
White starting pixels and accelerated virtual time were explicit host inputs.

Recreate the metadata-only baseline after producing a fresh census:

```powershell
python tools/inspect-ne-imports.py ad --spec-dir artifacts/import-census > artifacts/module-sweep/census.json
python tools/sweep-ad-readiness.py artifacts/module-sweep/census.json > artifacts/module-sweep/static-readiness.json
python tools/sweep-ad-readiness.py artifacts/module-sweep/census.json --probe-results artifacts/module-sweep/runtime.json > docs/research/module-readiness.json
```

The final command additionally needs this session's local baseline results.
The comparison tool checks every included probe's artifact hash against the
fresh census. It reads the current literal C# registry; its source-table reader
must be revised if the registry becomes generated or changes representation.
The ordinal references and SDK sources are documented in the
[original census](ad-import-census.md) and [SDK notes](after-dark-sdk.md).

The durable mechanism is: **group modules by missing capability, then execute
far enough to distinguish a drawing path from an idle or unused path.**
