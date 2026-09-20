# Current Status

Last updated: 2026-09-19

## Project phase

After Darker has a C# tutorial console host with real-mode addition and
near-call experiments, plus 16-bit protected-mode far-call and synthetic host
gateway experiments using Unicorn 2.1.3. Tutorial 06 adds a narrow NE load plan
and executes the project-owned Hello42 DLL with limited startup host responses.
Tutorial 07 extends preparation to internal references, entry ordinals, and
selector/offset/far-pointer patches, with a step-by-step console walkthrough.
Tutorial 08 executes original Mondrian startup and PREINITIALIZE/INITIALIZE
with a narrowly scoped host environment. Tutorial 09 continues in that guest
through BLANK and DRAWFRAME, producing changed PNG frames on a deterministic
software surface. The WPF host now also executes Spiral Gyra through a shared
module session, adding five pen/line imports to the original nine-service slice.
Rainstorm is the third playable module, with fixed controls, shared black-pen
lookup and PtInRect support; its intermediate lightning image is not presented.
Fade Away is the fourth, running its original Radar effect on host-supplied
white pixels and remaining black after the fade completes.
Lasers is the fifth: its original three-ray drawing owns a movable allocation
in the new shared local heap. Tutorial 10 exposes allocation, locking, guest
writes, freeing and reuse in a small source-authored program.
Magic is the sixth: its original 100-line history and horizontal mirroring reuse
that heap and the existing pen/line APIs without new Win16 implementations.
String Theory is the seventh, with three groups of 100 strings using that same
heap and renderer. Zot! is the eighth: fixed local blocks and a clock alias
support its original lightning, with explicit intermediate presentation because
it draws and erases inside a single DRAWFRAME. These complete row 3 of the sweep.
This remains narrow compatibility support, not general Win16 emulation.
`AfterDarker.Core` contains two extracted binary-layout helpers, a Windows NE
metadata reader, a CPU-independent load plan, and a separate After Dark
invocation-plan model. The C# MSTest project covers these mechanisms and the
ten educational console lessons (06 needs the optional Watcom fixture;
08/09 need the analyzed local Mondrian file).

## Established evidence

- **String Theory and Zot! complete the heap group:** six String Theory and
  nine Zot! private cases pass, alongside all earlier tests: **333 combined
  cases** and **241 public cases**. String Theory runs 1,500 draws through its
  history/motion/color cycles; Zot! runs 30 forced strikes with 60 allocations
  and frees and 45 visible/45 erased intermediate images. Actual WPF acceptance
  passes both alone and switching both ways, with RGB readback, restart and
  close while playing. All shutdowns leave no allocations, locks or pens.
  String Theory adds no API; Zot! adds USER #15 GetCurrentTime as an alias of
  the existing tick service. Profile-owned import-return checkpoints expose
  transient images outside the native hook, with bounded, cancellable 80-ms
  live holds as a modern adaptation. See [String Theory](research/string-theory-execution.md)
  and [Zot!](research/zot-execution.md) for settings, hashes and proof limits.

- **Magic playback through shared services:** one 1,520-byte movable allocation
  holds the original line history. Six private tests pass, including 1,700 draws
  through ring, motion and color wraps, independent guests and clean shutdown.
  All **312 combined tests** and **235 public tests** pass. Actual WPF acceptance
  passed Magic alone and switching both ways with Lasers, with exact RGB
  readback, restart and close while playing; no allocations, locks or pens
  remained. Fixed controls select 100 lines, horizontal mirroring, line speed
  100 and color speed 85. No new Win16 APIs were required. See
  [Magic's evidence and proof limits](research/magic-execution.md).

- **Shared local heap and Lasers:** `Win16LocalHeap` manages fixed/movable
  allocations, zero-init, lock counts, reuse/coalescing and bounded growth in
  already-mapped guest memory. Gateway context checks caller DS explicitly.
  Original Lasers allocates 1,836 bytes beyond its initial 1-KiB reservation,
  writes ray history, draws through periodic regeneration, and frees it on
  CLOSE. No new GDI API was needed. Six private cases plus public allocator,
  ABI and tutorial cases pass; all 306 combined tests and 235 public tests pass.
  Actual standalone WPF and switching checks pass with clean shutdown. See
  [the heap guide](win16-local-heap.md) and [Lasers proof and limits](research/lasers-execution.md).
  No Windows arena reconstruction, compaction, global allocator or indexed
  palette implementation is claimed. String Theory and Zot! now reuse this heap
  as recorded above.

- **Fade Away Radar playback:** a fresh session starts white, then the original
  code erases it in coarse and fine sweeps and finishes with a black fill.
  At 640x480 the tested path completes in 3,376 draws; further draws and BLANK
  keep it black. Seven private cases cover completion at five sizes, restart,
  cleanup and artifact rejection. Six public cases cover initial RGB ownership
  and unused-import guards. All 274 tests pass with every opt-in. WPF acceptance
  passes standalone Fade Away plus switching to/from Rainstorm, readback,
  restart and close. See [execution notes](research/fade-away-execution.md).
  Radar is the only exposed effect; desktop capture is still future work.
- **Rainstorm playback:** the shared session executes startup, initialization,
  rain drawing and CLOSE/WEP for the verified artifact. Nineteen public cases
  add signed rectangle/POINT and stock-pen coverage; five private cases include
  300 draws, deterministic independent guests and dimension extremes. All 261
  tests pass with every opt-in enabled. Actual WPF acceptance verifies RGB
  readback, restart, switching to/from Spiral Gyra and close during playback.
  Final locks/pens are zero. See [execution notes](research/rainstorm-execution.md).
  The original lightning path inverts twice inside one DRAWFRAME; our final-frame
  presentation omits the intermediate flash. This is a documented fidelity gap.
- **Whole-folder readiness sweep:** all 29 local module hashes match the prior
  census. Eighteen pass the current load plan with guarded unknown import slots;
  eleven stop in the loader. Research-only adapters let Rainstorm produce
  changed pixels through ten draws and CLOSE/WEP. Four Fade Away styles change
  a synthetic starting image and complete shutdown. Lasers, Magic, String Theory
  and timed Zot! execution expose a shared local-heap requirement. See the
  [ranked report and all first blockers](research/module-readiness-sweep.md).
  These probes do not expand the WPF supported-module list or establish visual
  fidelity, endurance, exact default settings or all-mode compatibility.
- **Readable runtime boundaries:** startup register conventions, imported-call
  stack management, typed ABI conversion, service dispatch, caller-code emission
  and DOS interrupts now have purpose-named classes. Module profiles and host
  contracts have separate files; GDI context state and line stepping are also
  isolated. The [runtime code map](runtime-code-map.md) gives direct navigation
  and a worked stack example. XML comments accompany the new boundaries and
  library builds emit IntelliSense documentation. No new Windows APIs or SDK
  allocation changes are introduced.
- **Windows SDK recovered:** the original Windows 3.0 SDK header and Windows
  2.0 programmer manual establish shared `AD_SYSTEM` / `AD_MODULE` layouts,
  including four module-interpreted control values. Control resource layouts
  are also documented. See [sources and layout findings](research/after-dark-sdk.md).
  Downloads remain ignored local references. The extra system words at 0x2C
  and 0x34 remain unexplained by the public header; runtime behavior is unchanged.
- **Spiral Gyra now runs in WPF alongside Mondrian.** File > Load AD file…
  identifies supported versions by content hash, starts playback, and switches
  modules after orderly shutdown. An unsupported selection leaves the active
  guest intact. The owner has also confirmed the earlier Mondrian window works.
- The shared `AfterDarkSession<TState>` owns loading/execution/lifecycle; profiles
  hold artifact-specific records and typed checks. Host slots follow the actual
  segment count, avoiding collision with Spiral's sixth segment. The original
  Mondrian facade and all educational lesson tests remain functional.
- Five new shared GDI services implement solid pens, selection/deletion, current
  position and lines. Public tests verify real guest ABI round trips, bounded
  reusable handles, and 1,500 raster cases against Windows GDI. Seven private
  Spiral cases verify all exposed speeds, repeatability, 100 draws and cleanup.
- Actual WPF acceptance passed original-code presentation, exact pixel readback,
  unsupported-file rejection while playing, Stop/restart, both directions of
  module switching, and window close during playback. All completed guests
  returned through CLOSE/WEP with zero locks/pens. Spiral peaked at two pens.
  See [the execution notes](research/spiral-gyra-execution.md) for the hash,
  observed paths, fixed controls, budgets and palette/fidelity limitations.


- The live WPF player now executes the analyzed original Mondrian continuously
  with elapsed-time pacing, bounded diagnostics and latest-frame presentation.
  The actual-window acceptance run read back exact published RGB bytes after
  30 presentations, stopped, restarted, presented again and closed while playing.
  Both sessions returned from original CLOSE and WEP (AX=1), restored SP=1000
  and caller DS=0048, and ended with zero global locks before engine disposal.
  This is bounded runtime evidence, not indefinite endurance or historical fidelity.
- Step 5 adds explicit, idempotent Shutdown on healthy Ready sessions. Faults
  prohibit more guest calls; Dispose still releases the engine without executing
  guest code. CLOSE's 208-service budget accommodates 200 rectangle inversions,
  blanking and locks; other calls retain 128. Tests cover empty/30/300-draw
  shutdown, both clearing settings, failure within CLOSE, and restart/cancellation.
  CLOSE leaves the rectangle count unchanged; optional clear followed by XOR
  replay explains why shutdown need not leave a black image. No new Win16 API
  is added. Tutorial captures retain their existing deterministic outputs.

- WPF step 4 adds the F5-runnable live window, serialized background playback,
  a single pending-frame mailbox and dispatcher-owned WriteableBitmap. Actual
  WPF acceptance presented 30 images, verified exact bitmap RGB readback and
  nonblank original-code output, stopped and released the guest with zero locks.
  Two public tests cover mailbox coalescing, ownership and concurrent coherence.
  Step 4 still stops without original CLOSE/WEP; step 5 adds that lifecycle.


- WPF step 2 bounds default history to 256 import calls, phases and interrupts
  per kind; lifetime totals and fixed-key import counts remain complete. Full
  recording is explicit in bounded tutorials. Pixel copying into caller-owned
  storage allocates no new frame arrays; tutorial 09 swaps two reusable buffers
  and lends a read-only span to the PNG sink. A 5,000-draw run at 64x48 retains
  the fixed history size, balances global locks and uses one host pixel buffer.
  Retention capacities 0/1/8 preserve full-recording guest state, pixels and totals.
  All 30 default PNGs and report contents match the prior committed step exactly.

- Step 1 of the WPF plan introduces `AfterDarker.Runtime.MondrianSession`:
  explicit load, Initialize, Blank, DrawFrame, snapshots and idempotent Dispose.
  Both console lessons use it; a future window can reference the library without
  depending on the tutorial executable. Eight new private-module cases verify
  lifecycle order, persistent/independent guests, detached snapshots, disposal
  and faulted-session rejection. Original frame output remains unchanged.

- [Tutorial 09](tutorial-09-mondrian-frames.md) executed original Mondrian BLANK
  and 30 DRAWFRAME calls, producing 30 distinct 640x480 PNGs in 11,859 total
  instructions. Its four new imports are SetRect, GetStockObject (black brush),
  FillRect, and InvertRect. All nine imports predicted for the successful path
  were reached. Two independent runs produced identical frame hashes/state;
  all return frames and global locks balanced. A 180-image test also reached
  original rectangle removal and exceeded 50,000 lifetime instructions.
  PNGs passed independent CRC/zlib and Pillow checks and visual inspection.
  Negative rectangle extents match measured modern Windows PatBlt behavior;
  this is not Windows 3.1 pixel-fidelity proof. That bounded tutorial does not invoke CLOSE/WEP; the live host now does.

- The shared [Win16Api](../src/AfterDarker.Core/Win16/Win16Api.cs) now contains
  the named Windows implementation methods used by lessons 06, 08 and 09.
  Per-guest state lives in `Win16ApiState`; import metadata/ABI conversion lives
  in static `Win16Imports`. The implementations have no tutorial, NE loader,
  or emulator dependency. Three additional direct API cases cover independent
  guest state, configured heap failure, and environment pointer/content checks.
- [Tutorial 08](tutorial-08-mondrian-initialize.md) executes the original Mondrian
  DLL startup (AX=1), PREINITIALIZE (compatibility flag=1), and INITIALIZE.
  Eleven imported service calls and three DOS interrupts complete; real guest
  pointer dereferences read the host system/module records. Every phase returns
  with restored caller DS and balanced SS:SP/BP, and releases its global locks.
  All five supported speeds produce the expected guest-selected thresholds.
  Public synthetic tests cover import argument/return frames, guest dereferences,
  and the protected-mode INT boundary; optional private tests cover original
  initialization. LocalInit remains a checked reservation model, not a Windows
  allocator. Drawing imports stop by name; no BLANK/DRAWFRAME/CLOSE is invoked.
- Loader readability pass: `NeFormat` names the relocation tags, flags, and
  chain markers; `NeLoadPlan` distinguishes x86 sizes from host layout choices.
  Its explicit field-writing switch and comments explain destination lookup,
  chain preservation, and patching copied code/data rather than NE tables.
  Tutorial 07 uses the same names. Behavior remains unchanged; all 129
  Watcom-enabled tests pass, including the existing DLL execution checks.
- [Tutorial 07](tutorial-07-relocations.md) prepares the local Mondrian image:
  five segments, 27 internal + 17 imported relocation records, 66 chained
  relocation writes, and three export-prologue patches. Independent raw-NE
  inspection agrees with all 66 destination fields and replacement values.
  A built-in original metadata example demonstrates the same algorithm without
  private files. Import addresses are explicitly non-callable placeholders;
  no AD code or API executes. Source files remain read-only.
- [Tutorial 06](tutorial-06-load-library.md) copies the compiled DLL's segments,
  assigns descriptors, patches imported far pointers and shared-data export
  prologues, then runs real guest far calls. Observed startup AX=1, HELLOWORLD
  AX=42 and a guest store of 42, then WEP(1) AX=1; SP returns to 1000 each time.
  HELLOWORLD establishes DLL DS=0010 and restores caller DS=0030. LocalInit
  is a checked test double, GetVersion is fixed, and MessageBox fails by name.
  This is fixture execution, not an After Dark or Windows heap implementation.
- The [Hello42 fixture](../tests/fixtures/win16/hello42/README.md) builds with
  pinned Watcom into a 1,034-byte Windows NE DLL with header startup,
  `HELLOWORLD` (returns 42 in source/compiled instructions), and resident `WEP`.
  Three opt-in metadata tests and nine execution/lesson tests pass. The DLL
  imports KERNEL GetVersion/LocalInit and USER MessageBox from its runtime.
  Windows 95 execution and general Win16 compatibility remain unproven.
- [Tutorial 05](tutorials.md#tutorial-05-read-a-windows-ne-file) reads a local
  Windows NE file into typed C# records, reports startup/export addresses,
  import identities/fixup heads, resource identifiers/ranges, and an externally
  defined After Dark call plan. All 29 local NE inputs inspect successfully.
  This does not load segments, apply fixups, decode resources, or execute them.
- The [Mondrian code-path analysis](research/mondrian-static-analysis.md)
  follows startup and lifecycle branches beyond the import census. Its
  successful visuals path appears to need nine Windows imports plus DOS
  date/time services (`INT 21h`, AH=2Ah/2Ch). No files, threads, task waits,
  dialogs, or callbacks were found on that path. Clock seeding, tick-based
  pacing, two host memory records, and static rectangle history are identified.
  Tutorials 08 and 09 verify initialization and bounded drawing by execution;
  shutdown is now also verified by the live-host and session tests.
- The [C# test suite](testing.md) has 181 default passing cases: 124 unit, 46
  engine/raster conformance, and 11 tutorial checks. Twelve optional Watcom
  cases, 34 local Mondrian cases, and seven local Spiral Gyra cases bring the
  combined total to 234.
  Public tests use generated NE
  fixtures and the same guest
  programs as the lessons, with typed observations and independent assertions.
  Temporary omitted-cleanup and wrong-return mutations were rejected. This
  coverage does not extend to unimplemented Windows APIs or segment protection.
- A static [import census](research/ad-import-census.md) covers all 29 local
  Windows NE modules: 51 GDI, 35 USER, and 43 KERNEL targets (including one
  imported constant), plus 59 AD_RSRC, 10 AD_SND, and one WIN87EM target.
  No direct thread/synchronization or Win16 task-wait/yield imports were found.
  Memory management, files/settings, clocks, dialog callbacks, helper libraries,
  and GraphStat's `WinExec` import are visible. The helper DLLs are absent, so
  their transitive dependencies remain unexamined. This is artifact evidence,
  not proof of which APIs run during drawing. Per-input hashes and the full
  API-to-module mapping accompany the report.
- Tutorial 01 runs `MOV AX, 7; ADD AX, 5` in Unicorn from a native Windows C#
  host, reads `AX=12` and `IP=0x1006`, and exits successfully. This is a bounded
  two-instruction real-mode probe, not protected-mode conformance.
- Tutorial 02 maps a separate guest stack, runs a near `CALL`/`RET`, and verifies
  `AX=12`, in-call `SP=0x8FFE`, restored `SP=0x9000`, a saved return word of
  `0x1006`, final `CS:IP=0000:100E`, and unchanged `SS=0`. The program exits with
  code 0. This is observed real-mode behavior only.
- Tutorial 03 observes a same-privilege protected-mode far call between code
  descriptors with distinct nonzero bases. The callee captures `CS=0010` and
  `SP=0FFC`, adds five, and executes `RETF`. The caller stores `12` at
  `DS:0020` (linear `0x30020`). The saved return is `0008:0008`, final execution
  is `0008:000B`, and `SP=1000` is restored. `CR0.PE=1` is verified.
- Tutorial 04 observes two protected-mode far calls to synthetic gateway
  `0010:0200`. The code hook reports linear `0x20200` and stops before the
  gateway instruction executes. After `EmuStart` returns, C# reads signed
  Pascal arguments `(7, 5)` and `(-7, 5)` from `SS:0FF8`, invokes a typed
  handler, writes AX, and simulates same-privilege `RETF 4`. The resumed guest
  stores `12` and `-2` at `0018:0020` and `0018:0022`. Final `CS:IP=0008:001C`,
  restored `SP=1000`, preserved DS/SS, and `CR0.PE=1` are verified.

- Installed After Dark Windows modules can be 16-bit New Executable (`NE`)
  libraries with `MZ` containers, segment tables, imports, exports, resources,
  and relocation records.
- `.A$` files in After Dark distributions can be installer-compressed payloads
  rather than executable modules. The installed `.AD` artifact is the useful
  execution and reverse-engineering input.
- The recovered After Dark 2.0 SDK exposes a far Pascal module dispatcher:

  ```c
  int FAR PASCAL Module(int iMessage, HDC hDrawDC, HANDLE hADSystem);
  ```

- The host supplies the HDC and guest structures, and the module performs its
  drawing through imported Win16 GDI operations.
- Spiral Gyra is now the second executable target; its observed pen/line
  vocabulary is implemented and tested. Stained Glass remains a later target.
- Stained Glass is a larger 25,008-byte NE module with nine segments and imports
  from `KERNEL`, `GDI`, and `USER`. It is a useful second-stage target because it
  exercises broader drawing and object behavior and exposes rapid synchronous
  construction.
- WineVDM/OTVDM is existing evidence that Win16 applications can execute on
  modern 64-bit Windows without booting a complete Windows guest.
- Tutorial 04 demonstrates the synthetic gateway execution boundary. Tutorial
  06 now maps the fixture's real imported far pointers onto that mechanism.

## Important unproven assumptions

- Tutorials 03 and 04 are narrow protected-mode successes, not completion of
  the CPU conformance ladder. Segment-limit/access enforcement, segment
  overrides, privilege transitions, general pointer translation, and broader
  ABI layouts remain unproven. Two successful host exits/resumes do not establish
  compatibility with arbitrary Win16 guest code.
- Supported application playback is limited to the analyzed Mondrian, Spiral
  Gyra, Rainstorm, Fade Away, Lasers, Magic, String Theory and Zot! artifacts, with Radar as the only
  Fade Away style, Lasers fixed to three rays and Magic fixed to 100 lines with
  horizontal mirroring. String Theory uses three groups of 100 strings; Zot!
  uses Few Forks and Stormy frequency, with explicitly adapted flash timing.
  Lasers requires dimensions of at least 141x141; Magic and String Theory
  require at least 3x3 to avoid zero divisors in coordinate calculations.
  The whole-folder research probes supply narrower
  observations for other modules without making them supported application
  playback. Other revisions and historical visual/pacing fidelity remain unproven.
- Production profiles supply the observed fields for their supported paths.
  The public SDK system/module structure layouts have since been recovered;
  the extra observed system words and complete cross-version behavior remain
  unresolved. Research probes use the public module-record size.
- Only the rectangle/black-brush and solid cosmetic pen/line drawing subset
  is implemented. Other object types, palettes, text, mapping modes and resource
  rendering remain unproven.
- The permanent engine strategy—WineVDM sidecar, custom in-process runtime, or a
  staged combination—has not been selected.

## Fidelity boundary

Prior native recreations of Rose, Spiral Gyra, and Stained Glass are valuable
algorithmic and visual references, but they are not original-code execution.
They should be treated as potential conformance oracles, not as proof that this
runtime works.

Original reference modules and media remain bring-your-own local artifacts and
must not be committed unless their redistribution status is established
explicitly.

## Next planning point

The String Theory/Zot! increment is on `codex/string-theory-zot`, created from
reviewed main after Magic merged (`082ebcc`, PR #17). It completes row 3 of the
[sweep](research/module-readiness-sweep.md), following the owner's request to
finish both remaining entries. Stop here for review. Changes remain local and
uncommitted; publication was not requested. Row 4 (Hard Rain and Shapes) is a
future step. The heap guide and Tutorial 10 retain the focused allocation
lesson; Zot!'s notes explain fixed blocks and presentation inside an active call.
Other Fade Away styles, Rainstorm's intermediate lightning presentation, and
historical pixel/timing comparisons remain explicit limitations.

The project keeps one F5-able console app, with separate tutorial classes
invoked through `ITutorial`, alongside the live WPF host. Future expansion
remains module-driven and educational; settings dialogs, general Windows APIs
and additional modules are outside this completed step.
Issue #1 tracks the broader protected-mode and host-gateway experiments.
Tutorial 04 implements the narrow host trap; the broader issue is not complete.
See [the tutorial guide](tutorials.md).

## Session log

### 2026-09-19 - String Theory and Zot! complete the heap group

- Created `codex/string-theory-zot` from reviewed main `082ebcc`. Added two
  hash-specific profiles, typed state observations, a shared SDK-record builder,
  WPF selection and opt-in integration tests. Proprietary inputs/captures remain ignored.
- String Theory requests 4,560 movable, zeroed bytes for three groups of history.
  Controls `2/60/96/1` select three groups, 100 strings, a 21-update color cycle
  step and initial clearing. No Win16 API implementation changed for this module.
  Its 1,500-draw run observes 2,208,250 instructions and 9,000 LineTo calls.
- Zot! controls `33/0/100/0` select Few Forks/Stormy. USER #15 aliases the shared
  DWORD tick clock. Its strict deadline gate, two fixed allocations (800/1,600
  bytes) and same-call frees are checked with original import traces. No timer
  or threading service is required on this path.
- Zot! draws white/gray bolts, delays and erases black before returning. Added
  generic bounded import-return image checkpoints, configured only by Zot!'s
  profile, plus a short cancellable WPF hold. Guest code and CPU delays still
  execute. Callback reentry is rejected; Stop finishes the active call before
  CLOSE/WEP. This is explicitly adapted presentation, not historical timing.
- Six new public cases check the clock alias ABI, larger bounded service limits,
  and checkpoint matching/ownership/bounds. Six String Theory and nine Zot!
  private cases cover long runs, independent guests, dimensions, exact clock
  boundaries, fixed allocation traces, cancellation and reentrancy. Full suite:
  333 passing; compiler/module-free suite: 241 passing.
- Actual WPF acceptance passes String Theory alone, Zot! alone and switching
  both ways, with exact visible-image readback, restart and close while playing.
  Captured windows were inspected. All guests complete CLOSE/WEP with no live
  allocations, locks or pens. Research notes record hashes and local evidence.
- Group 3 is complete at its fixed tested settings. Hard Rain/Shapes, other
  settings, Rainstorm's flash presentation and historical fidelity remain open.

### 2026-09-19 - Magic reuses the heap and line renderer

- Created `codex/magic-player` from reviewed main `7427aaf`. Implemented Magic
  only, preserving the owner's one-module-at-a-time approach.
- Traced the original settings, history allocation and drawing loop. Supplied
  explicit controls `60/100/85/1`, producing 100 history slots, one update per
  call, a 76-update color interval and horizontal mirroring. The generic WPF
  speed selector is disabled for these fixed controls.
- INITIALIZE allocates 1,520 zeroed movable bytes; observed handle `0002` resolves
  to `0028:0424`. Existing bounded heap growth admits this request beyond the
  initial 1,024 bytes. No shared API, ABI, stack or rendering changes were needed.
- Each update erases two old lines, draws two new lines and overwrites one
  ten-byte history record. Original code controls ring wrap, endpoint motion
  and the color cycle. CLOSE clears black and frees the allocation.
- Six private tests verify 1,700 draws (1,507,579 instructions and 6,800 LineTo
  calls), every history/motion/color counter, independent deterministic guests,
  dimensions and cleanup. All 312 combined tests and 235 public cases pass.
- Actual WPF Magic-only, Magic-to-Lasers and Lasers-to-Magic acceptance passed,
  with bitmap readback, restart and close while playing. Inspected the MAGIC
  window capture; no allocations, locks or pens remained after cleanup.
- Added the typed profile/state and [execution notes](research/magic-execution.md).
  Fixed settings, direct RGB, host pacing and absence of historical fidelity
  comparisons remain explicit boundaries. String Theory/Zot! were not executed
  or enabled. Private modules, disassembly, test reports and captures stay ignored.

### 2026-09-19 - shared local heap and original Lasers

- Created `codex/local-heap-lasers` from reviewed main. Implemented only Lasers
  from the grouped heap candidates, following the owner's explicit selection.
- Added a CPU-independent per-guest allocator and four Win16 Local* services.
  Fixed handles equal near offsets; movable identities resolve to offsets.
  Caller DS is passed separately from stack arguments and checked against the
  owning heap. Free returns ranges to a coalescing free list, not the OS.
- Lasers requests `LocalAlloc(0042, 1836)` while its NE header reserves only
  1,024 heap bytes. Its profile opts into mapping the full bounded data segment
  before startup, then enabling growth into that tail as needed. This is an
  explicit host policy; no live descriptor resizing or compaction is claimed.
- Added F5 Tutorial 10 with visible x86: allocate, lock, write/read BEEF, unlock,
  free, reuse stale bytes, explicitly zero reused storage, and free again.
  The host independently reads the actual guest write before free. The same
  gateway and heap implementation serve tutorial and original module.
- The real module passes 1,100 draws including periodic regeneration: 6,690
  LineTo calls and 1,256,277 instructions before CLOSE; one live 1,836-byte
  allocation during playback, zero after CLOSE/WEP. Independent sessions,
  dimension boundaries, empty playback, hash rejection and cleanup pass.
- BLANK returns the SDK HSV_PAL request. The explicit profile accepts this for
  24-bit RGB drawing without claiming indexed-palette support. Regression tests
  found the same existing Spiral Gyra reply; its policy is now explicit too.
  Failed initialization and unknown drawing replies stop symbolically.
- Verified 306 combined tests and 235 public tests. Actual standalone LASERS
  window readback/restart/close and switching with Spiral Gyra in both directions
  pass. Shutdown leaves no local allocations/locks, global locks or owned pens.
  Source-authored tests and documents are public; AD inputs, Wine reference
  source, captures, disassembly and detailed reports remain ignored.
- Paused after this module with implementation and documentation uncommitted.

### 2026-09-19 - Fade Away Radar and the starting-image boundary

- Created `codex/fade-away-player` from clean main after PR #14. Added the
  verified Fade Away artifact to the shared session/catalog/WPF path, with
  fixed Radar settings, typed observations and a white initial image.
- Added `ModuleProfile.CreateInitialPixels` and checked copying through
  `PixelSurface.LoadRgb`. Other profiles retain black initial pixels. The
  image is supplied once per session, not restored per frame or on completion.
- No Windows API implementation was added. Ellipse/Rectangle/PatBlt imports
  from unused styles now bind to explicit unsupported entries and fail by name
  if reached; native tests verify those guards before argument decoding.
- Observed two sweep passes (step 2 then step 1), the original completion flag
  and one final black FillRect. Five sizes reach completion; the standard
  640x480 run takes 3,376 draws and 515,335 instructions through completion.
  Subsequent calls stay black. Fresh sessions start white and reproduce pixels.
- All 274 tests pass with compiler and four private-module suites. Actual WPF
  checks pass standalone Fade Away (no switching) and both switching directions
  with Rainstorm, including RGB readback, unsupported-file rejection, restart
  and close during playback. All completed guests release locks/pens.
- Preserved original one-time fade behavior rather than inventing a repeat
  animation. Only Radar is exposed. Original files and local captures remain
  ignored; work stops here for the owner's review.

### 2026-09-19 - Rainstorm, one module at a time

- Added a hash-specific Rainstorm profile and typed guest observations through
  the existing session/catalog/WPF path. Its fixed controls are strength 60,
  lightning 50, 52 drops and wind 40; the unrelated speed selector is disabled.
- Added shared stock BLACK_PEN lookup and PtInRect. POINT is passed by value,
  with Y/X push order decoded into a named X/Y value; managed implementations
  stay independent of emulator registers and stack return mechanics.
- All 261 tests passed with Watcom and all three original-module opt-ins.
  Rainstorm's 300-draw case executed 15,600 point tests and both lightning
  inversions; it peaked at one owned pen and completed CLOSE/WEP with no locks
  or pens remaining. Asymmetric negative-coordinate conformance cases verify
  marshaling, guest result stores, preserved registers and Pascal cleanup.
- Actual WPF acceptance passed in both switching directions with Spiral Gyra,
  including pixel readback, unsupported-file rejection, restart and window close
  during playback. Captures and original modules remain ignored.
- New finding: Rainstorm inverts the surface twice during a single DRAWFRAME.
  The current final-frame mailbox does not present the intermediate lightning
  image. Kept the actual API behavior and documented this presentation/timing
  boundary instead of inventing a flash duration. Paused after this increment.

### 2026-09-19 - whole-folder readiness sweep

- Created `codex/module-readiness-sweep` from committed readability work
  (`8a16729`). Re-scanned all 29 original modules; hashes match the prior census.
- Used the current loader and runtime in bounded, isolated research processes.
  Guarded unknown import slots permit reaching the first unsupported call;
  they do not invent API behavior. Eighteen load plans succeed and eleven fail
  on relocation/prologue requirements. All baseline processes finish within
  their watchdog; no production runtime or application code changed.
- Rainstorm succeeds through ten draws and shutdown with black-pen lookup and
  PtInRect adapters, with nonblack pixels and zero final locks/pens. Fade Away's
  Plain, Blinds, Radar and Mesh modes change synthetic initial pixels and
  complete; other styles expose null-brush/PatBlt gaps. These are research
  configurations, not supported WPF profiles or full compatibility proofs.
- An accelerated-clock Zot! probe reaches LocalAlloc after several idle calls.
  Gravity still loads a sound resource after a failed sound-open response.
  These observations prevent mistaking idle returns or muted sound for complete
  rendering support. Recovered AD_SND declarations improve the sound research
  position; AD_RSRC contracts and WIN87EM behavior remain unresolved.
- Added a metadata-only registry comparison utility, hash-matched baseline
  results, and a ranked report covering every file. Original binaries, SDK
  downloads, probe code and detailed raw results remain ignored locally.

### 2026-09-19 - runtime readability and contribution map

- Created `codex/readable-runtime`, then fast-forwarded it to merged PR #12.
- Moved the production import path out of the session into `Win16ImportGateway`,
  `Win16Stack`, `Win16RegisterConvention`, and `Win16ApiDispatcher`. Startup uses
  `LibraryStartupContext` and `SetUpPrologRegisters`; DOS interrupt responses
  remain a separate no-frame boundary. Service implementations remain CPU-free.
- Split profiles, playback contracts, trace types, and memory-layout policy into
  named files. Expanded compact argument/raster logic into descriptive steps.
  Kept tutorial 04/06 mechanics local and linked them to the reusable path.
- Added a navigation guide, register-purpose table, stack example, IntelliSense
  documentation, and contributor readability guidance. Extra SDK compatibility
  words remain unexplained; guest records and supported API scope are unchanged.
- All 237 tests passed with Watcom and both local modules, including three new
  startup/frame checks and the existing native-GDI and original-code checks.
  The first run exposed one obsolete diagnostic-text expectation; it now checks
  the shared gateway's accurate service-disabled diagnostic.
- The default 184-test suite also passes. WPF builds with zero warnings/errors.
  Core/Runtime XML documentation parses successfully, including descriptions on
  synthesized record properties; checked guide links resolve and diff whitespace
  checks pass. Interactive IDE hover and a new WPF UI run were not performed.
- The imported-call trace type and dispatcher moved from session-level types to
  explicit shared types; repository callers were migrated. This is a source API
  organization change, not a claim of backward compatibility for external users.

### 2026-09-19 - Spiral Gyra and the AD load menu

- The owner requested implementation of a second module and a Load AD file menu,
  with initial support restricted to the two known versions. Branched from main
  to codex/spiral-gyra-player. No commit/push requested for this work.
- Extracted the common runner and host record fields; retained typed Mondrian
  facade and deterministic tutorials. Spiral's six segments use dynamic host
  placement and its own record layout instead of reusing Mondrian offsets.
- Original Spiral initialized, rendered colored line patterns, and completed
  CLOSE/WEP. Added CreatePen, SelectObject, DeleteObject, MoveTo and LineTo to
  the existing Win16 implementation and registry. No new dependency was needed.
- Solution builds without warnings. All 234 cases passed with compiler and both
  local module opt-ins. Private Spiral tests include repeatability, all five
  speed choices, pen reuse and cleanup. Mondrian's prior frame hashes still pass.
- Actual WPF runs verified switching Spiral -> Mondrian and Mondrian -> Spiral,
  invalid selection preserving playback, restart and close. Reports/images are
  under ignored artifacts/wpf-smoke; original files and output remain untracked.


### 2026-09-19 - WPF steps 4 and 5: actual window and original shutdown

- Step 4 committed the WPF host and single-frame transfer as 9f80f16. No guest
  state is touched from the UI; one sequential background task owns Unicorn.
- Step 5 executes original MODULE(CLOSE) and WEP(1) before native disposal on
  healthy Stop/close, and rejects guest cleanup after a fault. Stop cancels the
  host loop/pacer, allowing an in-flight bounded invocation to return first.
- Actual WPF smoke artifacts in ignored artifacts/wpf-smoke/step5 show 30 first
  presentations, exact WriteableBitmap readback, three presentations after Run
  again, and Closing while active. Both runs completed original shutdown; the
  last WEP returned AX=1, SP=1000, DS=0048 with zero locks. The window content
  rendering was visually inspected. These are local proprietary-output artifacts.
- Public and optional suites pass together (222 cases). Original default frame
  hashes remain asserted, and the console lessons retain their stopping points.
  Invalid-input WPF acceptance also failed promptly with the expected hash diagnostic
  and exit 1. No private inputs or generated images are staged.

### 2026-09-19 - WPF step 3: live timing and pacing

- The owner approved committing step 2 and completing steps 3-5 without further
  review pauses. Step 2 is committed as 079c8b0.
- Win16Api now reads an injected IWin16Clock. Existing defaults use the same
  per-request stepping clock; live timing uses monotonic elapsed milliseconds
  with DWORD wrap. DOS civil time is captured once at live session creation.
  Initialization validates the actual returned tick, without reading time again.
- FramePacer accounts for work time, waits cancellably, and resets late deadlines
  without catch-up bursts. Its cadence is host adaptation, not historical timing.
- Clock/pacer tests cover repeated reads, rollover, wall-clock changes, independent
  deterministic clocks, captured civil time, late deadlines and cancellation.
  Original code progresses when a manual live clock advances between invocations.

### 2026-09-19 - WPF step 2: bounded history and reusable buffers (awaiting review)

- Confirmed the owner's step-1 commit and clean branch before implementing only
  step 2. No changes to synthetic time, pacing, WPF, threading or CLOSE/WEP.
- Added DiagnosticOptions/DiagnosticHistory: a default 256-slot ring per record
  kind, chronological detached snapshots, zero-capacity counters-only mode and
  explicit full recording for bounded lessons. Session and native interrupt
  histories use the same policy. Lifetime totals are independent of retention.
- Instruction, service-history and drawing counters are saturating 64-bit
  values. Safety budgets use a separate per-invocation instruction counter;
  servicing an import/interrupt does not reset it. Initialization validation uses
  total interrupts, not retained records. Per-import counters have fixed keys.
- Added checked CopyRgbTo/CopyPixelsTo and PixelByteCount. The session never
  retains or exposes a caller buffer. Tutorial capture reuses two image arrays
  and swaps after a change. Its synchronous FrameSink now receives a borrowed
  ReadOnlySpan; retaining consumers must explicitly copy. PNG output is identical.
  Suppressed repeated string formatting when no diagnostic writer is supplied.
- Verified 169 default cases and 206 with both opt-ins. New tests cover ring
  order/wrap/snapshots, zero/full policies, buffer bounds/isolation, allocation-free
  copies, interrupt budget/retention, exact full-versus-recent results and totals,
  and a 5,000-draw original-code run with bounded history. Public copy and session
  copy paths allocate zero bytes during repeated copies into an existing buffer.
- Regenerated 30 default PNGs and compared their bytes and report contents to
  step 1: identical, including 11,859 instructions. This does not claim zero
  allocations throughout the emulator or prove hours-long native memory stability.
- Changes remain uncommitted for the owner's code review. Steps 3-5 have not begun.

### 2026-09-19 - WPF step 1: explicit session lifetime (awaiting review)

- Replaced callback-scoped `MondrianRunner.Execute` with a host-owned
  `MondrianSession`. Constructor prepares/maps only; Initialize runs original
  startup and initialization; Blank/DrawFrame preserve the same guest between
  ordinary C# calls. Disposal releases native resources once. Failed setup
  releases its partial engine, and interrupted lifecycle calls become Faulted.
- Moved session and existing SegmentedGuest into `AfterDarker.Runtime`, keeping
  Unicorn out of Core and removing future WPF dependence on the console app.
  Native asset restore/copy belongs to Runtime; apphost configuration remains
  per executable. Existing pinned dependencies and API behavior are unchanged.
- Lessons 08 and 09 now own sessions with using. Initialization-only behavior,
  clock inputs, pixel generation, captures and reports are preserved. Diagnostic
  snapshots have detached lists; pixel snapshots remain copies. Unbounded
  internal history and per-call allocations remain for the separate step 2.
- Eight new lifetime cases pass, along with existing suites: 159 default and
  190 combined cases. Verified fresh host output/native asset propagation and
  compared all 30 generated PNGs and the report with the pre-refactor capture.
- No clock/pacing, WPF/threading, cancellation or guest CLOSE/WEP work is included.
  No commit or push; stop for the owner's code review before proceeding.

### 2026-09-19 - tutorial 09: original Mondrian PNG capture

- Added four typed shared Win16 API implementations and opt-in drawing bindings.
  SetRect16/InvertRect16 use void returns; signed RECT16 coordinates and far
  pointers are decoded explicitly, with named-handle rejection and checked memory.
- Extracted shared `MondrianRunner` from lesson 08. Lesson 09 continues the same
  guest and HDC after initialization, calls BLANK then bounded DRAWFRAME, and
  compares RGB snapshots before counting/saving changed frames. Existing lesson
  08 remains initialization-only and its tests continue to pass.
- Added software RGB pixels, HDC registry, stock black brush, and dependency-free
  PNG writing with .NET zlib. No C# implementation generates Mondrian geometry.
  Capture uses speed 100, fixed civil time and synthetic ticks. PNGs, report and
  a local play/step HTML viewer stay in ignored artifacts; no private inputs or
  output media are tracked.
- Investigated reversed rectangle corners. A provisional one-pixel adjustment
  inferred from a Wine helper failed five modern Windows PatBlt comparisons.
  Corrected to the measured half-open sorted bounds for identity coordinates;
  ten native-oracle cases now pass. Guest RECT bytes are preserved verbatim.
  This documents a modern compatibility choice, not historical GDI equivalence.
- Default original run: 30 changed images in 30 draws; 11,859 instructions;
  SetRect=31, GetStockObject=1, FillRect=1, InvertRect=30; 66 locks and 66 unlocks.
  First rectangle `(367,430)-(307,284)` changes 8,760 pixels. Final RGB SHA-256
  `66D8954D3F8D6BD5BA311662C2D958E91CC614BEA8199C22A689A9791950EE59`.
- Verified 159 public tests and 182 with both opt-ins. New coverage includes
  all four drawing ABIs and failures, PNG round-trip scanlines, repeated original
  30-frame state/hash results, 180-frame original removal, slow timing gates, and
  intentional capture-budget exhaustion. Independent Python/Pillow inspection
  validated every saved PNG and matched report hashes; inspected final pixels.
- Execution budgets now apply per host invocation; cumulative instruction totals
  remain observable. Overall draw/image/dimension limits bound captures. No
  historical-first claim, Win3.1 visual equivalence, or CLOSE/WEP proof is made.

### 2026-09-19 — separate shared Win16 implementations for inspection

- At the owner's request, moved service bodies into `Win16Api`, with one
  readable method per API. Both Hello42 and Mondrian use that class. Removed
  the mixed `MondrianServices` class; kept metadata/marshaling in `Win16Imports`.
- Kept implementations instance-based because handle, heap, and clock state
  belongs to one guest. Static binding/ABI helpers carry no shared mutable state.
- Preserved Hello42's configurable version and LocalInit failure path, and
  Mondrian's backed handles, empty environment, tick policy, and reservation-only
  heap scope. Direct tests exercise implementations without emulator setup.
- Verified 138 public cases and 157 combined cases including compiled DLL and
  original-module regressions. No additional Windows API behavior was added.

### 2026-09-19 — tutorial 08: original Mondrian initialization

- Mapped all five prepared original segments and supplied caller/stack/gateway
  storage. Added a hash-specific typed host-record/state profile and checked
  resident global-handle registry. Kept native CPU code separate from services.
- Ran header startup and MODULE messages 12 and 0. Startup saved its instance;
  the guest accepted compatibility fields and consumed options through returned
  far pointers. All three phases returned with balanced frames and locks.
- Added five scoped Win16 handlers and DOS date/time responses. LocalInit checks
  the reserved zero-filled tail only; no allocator metadata or allocation API
  is implemented. All other imported services fail symbolically if reached.
- Synthetic protected-mode execution proved INT advances IP without a stack
  frame in Unicorn 2.1.3. The runtime enforces that contract before dispatching
  DOS requests and resumes without RETF/IRET. DOS 4.0 TIME.ASM and Wine 10.0
  declarations/global-handle code informed the handlers; no dependencies added.
- Default input observed time=2C1E2460, seed=00002460, tick=12345678, threshold=30,
  clear=1, rectangles=0, compatibility=1; 11 imports and three interrupts.
  Five speed inputs yielded thresholds 140/70/30/0/0 in original guest code.
- Added 18 public tests and seven opt-in local-module cases. Combined suite with
  Watcom and local input: 154 passing cases. Public inputs remain generated;
  local file is read in place and never copied into test outputs. Console
  path/prompt/trace use is verified; interactive Visual Studio remains manual.
- Stops after initialization. No original drawing, CLOSE, WEP, device context,
  or rendered pixels are claimed. Changes remain local for review.

### 2026-09-17 — tutorial 07: reconnect internal NE references

- Branched from merged tutorial 06. Extended the existing Core loader rather
  than adding another console app or an independent relocation implementation.
- Added deterministic segment placements, fixed and entry-ordinal target
  lookup, selector16/offset16/far16:16 chain writes, and typed per-write evidence
  with an observer callback. Tutorial 06 uses the same code through its binding
  adapter; no invented ABI is required for load-only import placeholders.
- Added an original three-segment NE metadata example and default/path/prompted
  modes with Enter-to-advance and explicit cancellation. The walkthrough
  explains source versus target versus next-chain addresses and debugger points.
- Verified Mondrian hash
  `781979da1a6a6fdf99eebec4dab67e7a645bfc8787be1671e20f13a8ca6b1aed`:
  44 relocation records become 66 writes, plus three export-prologue patches.
  An independent Python decoder agrees on all relocation replacements.
  Reports and original input stay ignored; no private bytes were added to tests.
- Added 22 unit and six tutorial cases. Default suite: 117 passed; Watcom suite:
  129 passed. Existing compiled-DLL execution remains a regression check.
- Normal output, step mode, and local-file preparation are verified. Interactive
  Visual Studio debugger use remains a manual check. This is preparation in
  managed arrays, not installed guest memory or original-module execution.

### 2026-09-17 — tutorial 06: load and call a compiler-built Win16 DLL

- Created `codex/tutorial-06-load-win16-library` from clean main after PR #6.
- Added a pure loading plan for segment copies/zero-fill, checked imported
  far-pointer chains, and recognized shared-data export prologues. Unsupported
  relocation forms and DLL modes fail instead of being silently skipped.
- Kept memory maps, GDT, register writes, guest callers, hook, host dispatch,
  return simulation, and observations visible in the new console lesson.
  Added normal and instruction-trace launch profiles and a detailed walkthrough.
- Source inspection of pinned Watcom LibEntry established DS/CX/DI/ES:SI inputs;
  inspecting Wine's loader established the export-prologue fixup. These are
  references, not vendored dependencies. No DOS services execute on this path.
- Observed startup -> LocalInit/GetVersion gateway calls -> LibMain -> caller,
  then HELLOWORLD -> caller store 42, then WEP -> balanced return. The runtime's
  actual data writes reflect the supplied Windows version. LocalInit remains
  a checked test double; MessageBox is an error trap.
- A compiled-byte mutation from 42 to 77 changes both AX and guest memory.
  Failed initialization leaves export result markers untouched. Tests check
  stack frames, cleanup, DS transitions, import arguments/returns, bounded
  failure, invalid selector rejection, and the error gateway.
- Verified 101 opt-in / 89 default cases and normal/trace CLI launch profiles.
  Interactive Visual Studio F5 remains a manual check. Generated binaries and
  the trace remain in ignored artifacts; no new packages or private inputs.
- Executed fixture SHA-256:
  `a2904e8332dd5232040b26ccd1ce446a76049dcced368ddce89625702d9c0c12`
  (1,034 bytes; compiler/fixture source unchanged from PR #6).

### 2026-09-16 — compiler-built Hello42 fixture

- Added original C source for `HelloWorld`, `LibMain`, and `WEP`, using Watcom's
  conventional Win16 DLL startup rather than constructing the NE bytes ourselves.
- Added a scoped build script with process-environment restoration and ignored
  outputs, plus three optional C# tests against the actual generated DLL.
- Build has zero compiler/linker warnings. Opt-in suite: 75 passed; default
  suite: 72 passed without compiling the fixture. Tutorial 05 reads the DLL.
- Recorded runtime dependencies and the distinction between header startup
  and named exports. Inspected compiled MOV AX,42/RETF, but did not execute it.

### 2026-09-16 — Watcom fixture branch and installation handoff

- Created `codex/watcom-win16-test-library` from clean, merged main.
- Recorded exact Open Watcom release/asset, size/SHA-256, portable extraction,
  per-process environment setup, and the vendor DLL smoke-build commands in
  `docs/watcom-toolchain.md`; linked it from README and agent instructions.
- The existing installation at `C:\tools\open-watcom\2026-09-01` builds the
  supplied vendor DLL as Windows NE. No execution compatibility was tested.
  A project-owned test DLL remains the branch's intended completion milestone.

### 2026-09-16 — tutorial 05: typed NE inspection

- Created `codex/tutorial-05-ne-inspector`, preserving prior uncommitted
  Mondrian research and DOS-reference notes. Added the lesson through the
  existing `ITutorial` interface, with an F5 profile and optional path argument.
- Added a CPU-independent Core reader/model for headers, segments, entry
  bundles, names, imports, raw fixups, and numeric/named resource metadata.
  A separate typed SDK call plan resolves MODULE by name rather than guessing
  an ordinal, and keeps DLL initialization distinct from lifecycle messages.
- Added generated fixtures and deterministic tests. The 72-case suite passes;
  private input files remain outside automated tests. All 29 local NE modules
  produce reports; Mondrian's startup/export addresses match the prior audit.
  Import identities and relocation counts also agree with the independent
  Python census for all 29 inputs, normalizing module-name case for comparison.
- Reported seven custom Mondrian resources without inferring their purpose.
  Resource bytes are not decoded or copied into public artifacts. Ordinal
  annotations reuse factual Wine 10.0 census metadata, clearly labeled as a
  reference rather than names/signatures found in the inspected file.
- Verified launch-profile execution and both prompted/explicit paths. No
  interactive Visual Studio F5 session was observed. No new packages, loader,
  service handlers, or original-module execution were introduced.

### 2026-09-16 — local DOS source reference

- At the owner's request, cloned Microsoft's MS-DOS repository into sibling
  `C:\repos\MS-DOS`, at revision
  `2d04cacc5322951f187bb17e017c12920ac8ebe2`. Verified origin, clean checkout,
  MIT license declaration, and date/time service source locations.
- Added a [source guide](research/dos-source-reference.md) and agent guidance
  to consult those sources when DOS behavior needs clarification, recording
  version/revision and pairing implementation evidence with API documentation.
- This is a research checkout only. No DOS build/execution, runtime dependency,
  or expansion of supported services was introduced.

### 2026-09-16 — Mondrian static lifecycle analysis

- Created `codex/mondrian-static-analysis` from the testing foundation branch.
  Added a hash-specific research inspector and a factual path report; no
  original module was executed and no runtime service was implemented.
- Followed NE relocation chains, all entry points, direct calls/branches, and
  the bounded lifecycle switch. Thirty roots decode without unresolved control
  transfers; Capstone and Iced agree on instruction lengths in those traversals.
- Separated five compatibility-error text imports and three diagnostic imports
  from the nine-service successful lifecycle. Imported placeholders still need
  bindings; excluded services should fail by name if reached.
- Found clock interrupts invisible to the import census, a timezone environment
  lookup, and tick calibration that cannot use a permanently constant clock.
- Recorded host field offsets/constraints, undefined return-value edges, the
  200-entry static rectangle history, and outstanding rectangle-semantics tests.
- Verified inspector structural invariants on the identified input. Research
  dependencies, original binaries, and generated full listings remain ignored.
  C# projects and runtime dependencies are unchanged; no new runtime proof is
  claimed. The owner's first milestone is visuals with fixed supplied options.

### 2026-09-16 — C# testing foundation

- Created `codex/foundation-unit-tests`, preserving the uncommitted import
  census work. The owner subsequently authorized committing and pushing both
  the census and test foundation for review before merging.
- Added MSTest.Sdk 4.4.1/Microsoft.Testing.Platform 2.4.1 with pinned dependencies
  and a .NET 10 test-runner selection. The generated test executable receives
  the same restricted CFG workaround as the tutorial executable.
- Extracted descriptor encoding and far Pascal word-frame decoding into a
  dependency-free Core library. Lessons still own their guest bytes, memory
  maps, hooks, register operations, and console success checks. Their new
  `Execute` entry points return actual observations for independent assertions.
- Added 32 cases for binary layouts, malformed input, word arithmetic bounds,
  near/far calls, gateway marshaling and guest stores, signed return extremes,
  handler failure, and the original tutorial entry points.
- Verified unit-test filtering/discovery and native execution through MTP.
  Temporary omitted argument cleanup caused two unit failures; temporary RET
  in place of RETF caused the far-call conformance failure. Sources restored.
- The full suite and four console launch profiles pass. Interactive Test
  Explorer remains a manual check. No Windows API mocks were added.

### 2026-09-16 — local import/API census

- Created `codex/ad-import-census` from the tutorial 04 branch for the requested
  inspection. Added an offline research inspector and factual reports; no
  emulator or Win16 service implementation changed.
- Read all `.ad`/`.dll` candidates under the ignored `ad/` directory. All 29
  parse as Windows NE; the two supporting non-executable files were skipped.
- Resolved Windows/WIN87EM ordinal names against Wine 10.0 export metadata.
  AD_RSRC's 59 distinct ordinal contracts remain unresolved. Ten AD_SND names
  are present in callers, but that does not recover their full ABI/behavior.
- Verified the inspector using generated ordinal/name imports, internal
  references, truncated inputs, an invalid module index, and unresolved imports.
  The complete collection passes structural checks. Original binaries remain
  ignored; the report contains hashes and metadata only.
- The scan supports a narrow initial guest context, not a claim that the whole
  collection needs no scheduling. Dialog re-entrancy and asynchronous sound
  need separate investigation when those modules become targets.

### 2026-09-16 — tutorial 04: host gateway

- Branched from merged tutorial 03 on `main` to `codex/tutorial-04-host-gateway`.
- Added `Tutorial04HostGateway` and its F5 launch profile in the existing app,
  with no new dependencies. The same descriptor setup stays visible in the
  lesson; a small typed service table makes the gateway binding inspectable.
- Observed two bounded stop/dispatch/resume cycles on one engine. Stack
  arguments, return addresses, hook timing/address, guest-only result stores,
  and final registers are asserted. Host dispatch runs outside the hook.
- Temporary negative mutations each exited 1: unknown gateway offset, incorrect
  argument offset, omitted Pascal argument cleanup, and a missing guest store.
  Restoring the source restored success; mutations are not committed.
- Tutorial 04's launch-profile run and regression runs of tutorials 01–03
  pass. Interactive Visual Studio F5 remains a manual check.
- This is a synthetic service using one fixed ABI and known stack descriptor.
  It adds no NE loader, real Win16 import, privilege transition, or renderer.

### 2026-09-16 — tutorial 03: protected-mode far call

- Branched from merged tutorial 02 on `main` to
  `codex/tutorial-03-protected-far-call`.
- Added `Tutorial03FarCall` and its F5 launch profile in the same console app.
  GDT construction, register setup, guest bytes, and assertions remain visible
  in the lesson. No new dependencies were added.
- Source inspection found Unicorn 2.1.3's `UC_MODE_16` register/start APIs assume
  real-mode addressing. The lesson uses `UC_MODE_32` with 16-bit descriptors.
  The observed run begins with offset EIP=0 and stops at the caller's linear
  completion address; both API conventions are documented in the code.
- Verified separate caller/callee/data/stack bases, captured callee CS/SP,
  four saved return bytes, restored CS:IP/SP, preserved DS/SS, and the guest's
  store of the returned AX into previously marked data memory.
- A temporary `RETF` to `RET` substitution fails with exit 1: CS stays `0010`,
  SP is only restored to `0FFE`, and result memory retains `0xCCCC`. Restored
  `RETF` passes with exit 0. The negative mutation is not part of the lesson.
- Tutorials 01 and 02 remain regression checks. No host trap, selector protection
  claim, privilege transition, or Win16 import support is introduced.

### 2026-09-16 — tutorial 02: guest stack and near call

- Branched from merged tutorial 01 on `main` to `codex/tutorial-02-stack-call`.
- Added `Tutorial02StackCall` through the existing `ITutorial` interface and a
  separate F5 launch profile; tutorial 01 remains the default lesson.
- Allocated guest stack memory explicitly in the lesson, initialized `SS:SP`,
  and checked the in-call stack pointer, saved return word, restored stack
  pointer, arithmetic result, segment registers, and final instruction offset.
- The guest captures its in-call `SP` in `DX`; no hooks, host callbacks, or
  intermediate host-driven stops are used.
- The tutorial 02 launch-profile run and tutorial 01 regression run pass.
  No protected-mode or far-call behavior is claimed or implemented.

### 2026-09-15 — tutorial 01: 16-bit addition

- Added one .NET 10 Windows x64 console app, a solution, a launch profile, and
  an `ITutorial` interface with an explicitly registered addition lesson.
- Kept the six guest bytes beside their assembly explanation. No private
  artifacts, hooks, gateway handlers, assembler, or NE parser are involved.
- Verified the two-instruction result and completion address, with bounded
  execution and explicit native-engine cleanup.
- Pinned the upstream .NET binding and native engine to 2.1.3. Automated the
  missing Windows DLL restore with archive/DLL hash checks.
- Diagnosed native fail-fast as CFG rejecting Unicorn's `longjmp` return path;
  `/GUARD:NO` on only the generated tutorial apphost allows successful execution.
  Recorded this process-level protection tradeoff and dependency licenses in
  `docs/tutorials.md`; Windows-wide settings are untouched.
- Automated launch succeeds. Interactive Visual Studio F5 and the owner's
  tutorial walkthrough remain to be observed; no later lesson has been started.

### 2026-09-15 — local test artifacts

- Designated the root `ad/` directory for private After Dark copies and
  supporting files used in local testing.
- Added a directory-wide Git exclusion so all contents, including files without
  legacy executable extensions, remain excluded from ordinary staging.
- Verified that no files under `ad/` are tracked or appear in the locally
  available Git history. This is repository hygiene, not an execution proof.

### 2026-08-30 — repository bootstrap

- Established the preservation and educational charter.
- Recorded the narrow Win16/After Dark product boundary.
- Serialized the proposed NE loader, import gateway, ABI, GDI surface, and host
  architecture.
- Marked protected-mode CPU-engine behavior as a required proof rather than an
  assumption.
- Added repository exclusions for private legacy modules and reference media.
