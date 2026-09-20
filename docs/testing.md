# Tests and tutorials

The console application and automated tests serve complementary purposes.
Tutorials expose a small, runnable mechanism. Tests preserve its contract as
the implementation grows. Neither replaces the other.

## Run the suite

Use the same Windows x64/.NET 10/Visual Studio C++ build prerequisites as the
[tutorials](tutorials.md). From the repository root:

```powershell
dotnet test
```

Useful variants:

```powershell
dotnet test --filter "TestCategory=Unit"
dotnet test --filter "TestCategory=Conformance"
dotnet test --filter "TestCategory=Tutorial"
dotnet test --report-trx
dotnet test --list-tests
```

The project is `tests/AfterDarker.Tests`. MSTest provides named assertions and
data-driven cases. `global.json` selects Microsoft.Testing.Platform for .NET
10's `dotnet test`; use MTP arguments rather than legacy VSTest switches.
TRX results are written under the ignored `TestResults/` directory.

In Visual Studio, select **AfterDarker.Tutorials** for lessons or **AfterDarker.Wpf** for the live window.
Use **Test > Test Explorer** for automated tests, with a Visual Studio version
that supports .NET 10 and Microsoft.Testing.Platform. CLI discovery/execution
is verified; an interactive Test Explorer session remains a manual check.

## Current coverage

| Category | Cases | What is established |
| --- | ---: | --- |
| Unit | 154 | Descriptor/frame layout, NE metadata/SDK plans/relocations, host record fields, lock lifetime, DOS date/time packing, invalid-input rejection, lossless PNG encoding, ring retention, reusable pixel buffers, initial-image ownership/validation, monotonic timing, pacing, concurrent latest-frame transfer, pen ownership/capacity, supported-file rejection, and local-heap identity, locking, fragmentation, growth, reuse and bounds |
| Conformance | 69 | Actual Unicorn execution: arithmetic, near/far calls, imported-call marshaling/results/cleanup, guest dereferences of locked blocks, protected-mode DOS interrupt stop/resume, bounded failures, rectangle and pen/line ABIs, and software rectangles compared with native Windows PatBlt, bounded interrupt history with per-call budgets, and 1,500 line cases compared with native GDI, plus explicit startup-register and checked stack-frame contracts, by-value POINT arguments, stock black-pen lookup, unused Fade Away import guards, local-heap implicit DS, guest writes, unsigned allocation failure and Pascal cleanup |
| Tutorial | 12 | Five CPU/heap lessons, NE inspection, and six relocation report/step/cancel/file-path checks using generated input |
| **Default total** | **235** | All passing on the current Windows x64 development host; no Watcom or private file required |
| Toolchain (opt-in) | 12 | Three real-DLL metadata cases plus nine startup/export/exit execution, ABI, failure, mutation, trace, and console checks |
| **With Watcom** | **247** | Includes rebuilding the project-owned Win16 fixture |
| LocalModule (opt-in) | 34 | Original initialization plus deterministic 30-frame capture, 180-frame removal path, slower timing gate, capture-budget failure, eight session lifetime/state/failure cases, and six retention/buffer cases including 5,000 draws; live timing, CLOSE/WEP, failure, cancellation and restart |
| **With Watcom + Mondrian** | **281** | Requires the pinned compiler and local analyzed Mondrian file |
| LocalSpiralGyra (opt-in) | 7 | Original colored line rendering, five speeds, independent deterministic guests, pen reuse and CLOSE/WEP |
| **With compiler + Mondrian + Spiral** | **288** | Requires the compiler and those two private modules |
| LocalRainstorm (opt-in) | 5 | 300 draws including paired lightning inversions, deterministic independent guests, dimension extremes, cleanup and exact-artifact rejection |
| **With compiler + Mondrian + Spiral + Rainstorm** | **293** | Requires the compiler and those three private modules |
| LocalFadeAway (opt-in) | 7 | Original Radar completion at five sizes, white restart, deterministic guests, idle/BLANK after completion, cleanup and artifact rejection |
| **With compiler + first four modules** | **300** | Requires the compiler and the four earlier private modules |
| LocalLasers (opt-in) | 6 | Local-heap growth, 1,100 draws including regeneration, independent guests, dimensions, cleanup and artifact rejection |
| **With compiler + first five modules** | **306** | Requires the compiler and the five earlier private modules |
| LocalMagic (opt-in) | 6 | 1,700 draws through history/motion/color wraps, independent deterministic guests, dimension bounds, heap/pen cleanup and artifact rejection |
| **With all seven opt-ins** | **312** | Requires the compiler and all six supported private modules |

Conformance tests use the native engine and a test-only Windows GDI raster oracle; they are not isolated unit tests or a
mock of Unicorn. Categories make the distinction explicit. All fixtures are
generated code/bytes in the default and Toolchain suites. Only explicitly enabled
original-module categories read private files supplied by environment variable.

Tests create and close their own engines and run serially for now. Guest runs
retain their instruction/time limits. No native state is shared between tests.

The decoder supports same-privilege far returns with word arguments. It is not
a general Win16 ABI decoder: the four drawing signatures now decode checked
far pointers and signed coordinates, and PtInRect decodes a by-value POINT. Other by-value structures, privilege
transitions, and wrapping stacks still need separate designs and tests when required.
The initialization gateway now tests far-pointer and DWORD returns in DX:AX.
Descriptor encoding tests establish bytes, not CPU enforcement of limits.

## Optional local Mondrian tests

Original-module initialization is a separate opt-in, independent of Watcom:

```powershell
$env:AFTER_DARKER_MONDRIAN = (Resolve-Path ad/Mondrian.ad).Path
dotnet test -p:TestLocalMondrian=true
dotnet test -p:TestLocalMondrian=true -p:BuildWin16Fixture=true
```

LocalModule tests are excluded from compilation by default. Opting in requires
the environment variable and the analyzed file hash; missing inputs fail rather
than silently skipping. Files are read in place, not copied into build outputs.
The public `Win16ImportGatewayTests` and `DosInterruptTests` exercise the same
handlers and native boundaries with original tiny guest programs. The private
tests establish actual initialization and bounded original drawing, including
repeatable pixel hashes, balanced calls, a rectangle-removal path, and exhausted
capture budgets. They do not establish general Win16 support or historical pixel
fidelity. Public drawing tests check signed coordinates, invalid pointers/handles,
void/word returns, Pascal cleanup, clipping, backwards extents, and inversion
restoration. PNG tests cover dimensions, scanlines, colors and invalid input.

## Optional compiler-built fixture

The [Hello42 Win16 DLL](../tests/fixtures/win16/hello42/README.md) exercises the
NE reader against real compiler output, independently of the hand-generated
metadata fixtures. With the [pinned Watcom installation](watcom-toolchain.md):

```powershell
dotnet test -p:BuildWin16Fixture=true
dotnet test -p:BuildWin16Fixture=true --filter "TestCategory=Toolchain"
```

This builds the DLL and adds 12 `Toolchain` cases, for 247 passing cases in
the combined suite. Three check metadata; nine exercise tutorial 06, including
real startup/HELLOWORLD/WEP execution, AX and guest stores, different caller/DLL
DS, import argument order and cleanup, DX:AX returns, initialization failure,
bad selector rejection, the error gateway, instruction bounds, and tracing.
Changing the compiled return constant to 77 produces 77 in both register and
memory; the host does not manufacture the expected result. The source and build
instructions are tracked; outputs live in ignored `artifacts/`. Default runs
remain compiler-free with 235 cases. Do not pass `--no-build` when changing this
opt-in property, since it changes which tests are compiled.

`NeLoadPlanTests` adds 17 pure unit cases using generated metadata bytes, so
chain safety and unsupported-input rejection can be tested without an emulator
or Watcom. The opt-in `Toolchain` category now includes native execution as
well as file inspection. Its LocalInit behavior checks the reservation and can inject failure. The
fixture does not call LocalAlloc; separate heap tests establish allocation behavior.
No general Windows allocator or MessageBox implementation is claimed.

Tutorial 07 adds 22 `NeInternalRelocationTests` cases and six
`RelocationTutorialTests` cases. These use a small original three-segment NE
fixture shared with the lesson. Tests distinguish moving a segment's linear
base from changing its selector, check non-exported entry targets and data
allocation tails, verify observer evidence, and reject invalid targets/chains.
Console tests cover default, explicit/prompted files, stepping, cancellation,
and read-only source preservation. Private Mondrian verification is recorded
as an integration observation in current status, not a public test dependency.

## Relationship to the lessons

```text
AfterDarker.Core
    descriptors + far Pascal frames + typed NE metadata + SDK call plan (no Unicorn dependency)
        ^                         ^
        |                         |
AfterDarker.Tutorials         unit tests
    Run() -> Execute()            |
               |                  |
               +---- observations -> conformance tests
    Run() ------------------------> tutorial smoke tests
```

`Execute()` is still in each lesson beside its annotated machine code and
emulator operations. It returns actual register/memory observations; it does
not return precomputed expectations. `Run()` supplies console narration and
checks the original example. Tests assert those observations with independent
expected values and can exercise additional inputs without parsing stdout.

Two existing mechanisms were extracted into the shared library:

- [`SegmentDescriptor16`](../src/AfterDarker.Core/X86/SegmentDescriptor16.cs)
  replaces the duplicate encoder in lessons 03 and 04.
- [`FarPascalWordFrame`](../src/AfterDarker.Core/Win16/FarPascalWordFrame.cs)
  extracts lesson 04's return/argument decoding and cleanup arithmetic.

Tutorial 05 adds `NeReader` and its typed metadata model to Core, plus a separate
`AfterDarkCallPlan` for the external SDK convention. Tests generate a complete
small metadata fixture in source: fixed/movable/constant entries, an unused
ordinal, named and ordinal imports, numeric and named resources, and a
zero-filled segment. Mutations check truncated structures, bad indices,
overflowing offsets, invalid markers/alignment, and unsupported variants.
Report tests distinguish facts from ABI assumptions and escape control bytes.
These tests never open `ad/`, execute fixture bytes, or require new packages.

The tests include a subtraction handler because addition cannot discriminate
swapped arguments. Return-value cases exercise both signed word extremes.
A throwing handler verifies exception propagation followed by a successful
fresh run; it is not a native memory-leak measurement.

Temporary mutation checks confirmed the suite is discriminating:

- Popping only IP/CS while omitting argument cleanup failed two unit cases.
- Replacing guest `RETF` with `RET` failed the conformance test: final SP was
  `0x0FFE` instead of `0x1000`.

Both mutations were restored before final verification.

## Adding Windows API tests later

For each API we implement, add focused tests around its actual contract:

1. Test the managed handler's state and behavior without a CPU where possible.
2. Test its guest ABI: argument order/width/signedness, pointer and handle
   validation, return registers, memory effects, and stack cleanup.
3. Add a tiny guest program when crossing the emulator boundary is part of
   what must be proven. Include relevant failure paths, not only success.
4. Add or update an educational lesson when it introduces a useful new concept.

Future API handlers belong in runtime code referenced by tests and tutorials.
The current test references to the tutorials reuse these foundational guest
programs; they do not make the console project the future home of Win16 APIs.
There is no placeholder implementation or claimed test coverage for APIs we
have not implemented yet.

## Runner and native-engine setup

MSTest.Sdk **4.4.1** and Microsoft.Testing.Platform **2.4.1** are test-only
dependencies, licensed MIT. The SDK version is pinned in the test project;
the resolved dependency graph is in `packages.lock.json`. The extension profile
is `None` with TRX reporting explicitly enabled; no coverage/mock library is
needed for this milestone. Core adds no external dependency, and the tutorials
do not reference MSTest. See Microsoft's [MSTest SDK configuration](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-sdk)
and [dotnet test modes](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test).

The test project builds its own executable. This matters because the existing
Unicorn/Windows CFG issue also applies to tests. The build script now permits
exactly two executable names, each within its own output directory:

- `src/AfterDarker.Tutorials/bin/**/AfterDarker.Tutorials.exe`
- `tests/AfterDarker.Tests/bin/**/AfterDarker.Tests.exe`

It applies the same `/GUARD:NO` workaround to these generated apphosts only.
MTP's verified `dotnet test` path launches the configured executable. It does
not patch `dotnet.exe`, an installed test host, or Windows policy. This remains
a process-level compatibility tradeoff, not a resolved distribution strategy.
Do not run the test DLL with `dotnet AfterDarker.Tests.dll`; that uses the shared
host whose CFG setting we deliberately leave alone.

## Actual WPF acceptance

The [player guide](wpf-player.md#local-acceptance-run) describes the opt-in
`--smoke` mode. It drives the real dispatcher/WriteableBitmap and validates RGB
readback, then Stop, fresh Run and Closing while playback is active. Both guest
shutdowns must finish CLOSE/WEP with no outstanding locks. Its local PNG/report
artifacts are ignored and require the private analyzed module. This supplements
the 300 tests; it is not counted as a unit test or an interactive F5 observation.

## Optional local Spiral Gyra tests

```powershell
$env:AFTER_DARKER_SPIRAL_GYRA = (Resolve-Path 'ad/Spiral Gyra.ad').Path
dotnet test -p:TestLocalSpiralGyra=true
# Compiler, Mondrian and Spiral suites together (288 cases):
$env:AFTER_DARKER_MONDRIAN = (Resolve-Path ad/Mondrian.ad).Path
dotnet test -p:TestLocalSpiralGyra=true -p:TestLocalMondrian=true -p:BuildWin16Fixture=true
```

Spiral tests are excluded unless explicitly enabled and never copy the original
file into outputs. A missing/wrong artifact fails rather than silently skipping.
The WPF smoke mode also accepts a final optional second-module path to test
switching during playback; it always tests rejection of unsupported content
without stopping the active guest. The native file picker remains a manual check.

## Readability refactor verification

The shared gateway tests now live in `Win16ImportGatewayTests.cs` and instantiate
`Win16ImportGateway` directly, exactly as the session does. Three additional
`Win16CallingConventionTests` prove startup inputs, non-mutating stack reads,
far-return cleanup and invalid-frame rejection. See the
[runtime code map](runtime-code-map.md) to navigate these boundaries.

The readability pass preserves original-module state/pixel checks and the
1,500-case native GDI comparison. XML documentation is emitted for Core and
Runtime consumers; documentation references are compiler-checked.

## Optional local Rainstorm tests

```powershell
$env:AFTER_DARKER_RAINSTORM = (Resolve-Path ad/Rainstorm.ad).Path
dotnet test -p:TestLocalRainstorm=true --filter "TestCategory=LocalRainstorm"
# Compiler, Mondrian, Spiral and Rainstorm (293 cases), with the other module variables also set:
dotnet test -p:TestLocalRainstorm=true -p:TestLocalSpiralGyra=true -p:TestLocalMondrian=true -p:BuildWin16Fixture=true
```

The five cases are excluded from default compilation and never copy the original
module into outputs. Missing/wrong input fails explicitly. The 300-draw case
reaches the lightning countdown, verifies 15,600 point tests and two inversions,
checks bounded diagnostics and pen lifetime, and completes CLOSE/WEP. Additional
cases exercise independent deterministic guests, 1x1/2048x2048 surfaces and
artifact rejection. Nineteen new public cases cover PtInRect geometry/ABI and
stock-pen behavior without needing any original module. See the
[Rainstorm notes](research/rainstorm-execution.md) for the intermediate-flash
presentation limitation and actual WPF acceptance commands.

## Optional local Fade Away tests

```powershell
$env:AFTER_DARKER_FADE_AWAY = (Resolve-Path 'ad/Fade Away.ad').Path
dotnet test -p:TestLocalFadeAway=true --filter "TestCategory=LocalFadeAway"
# All suites (300 cases), with the other three module variables also set:
dotnet test -p:BuildWin16Fixture=true -p:TestLocalMondrian=true -p:TestLocalSpiralGyra=true -p:TestLocalRainstorm=true -p:TestLocalFadeAway=true
```

These seven cases use the supplied private module in place, with no copy into
outputs. Radar's two passes and final black fill are checked through completion
at five sizes, including odd dimensions and both supported extremes. Later draws
and BLANK must leave the image black; a fresh guest must start white again.
The native API surface is unchanged: three public conformance cases verify that
unused Ellipse/Rectangle/PatBlt imports remain guarded. Three unit cases verify
owned initial RGB copies, mutation/revision behavior and invalid-size rejection.
See the [Fade Away execution notes](research/fade-away-execution.md) for the
fixed effect, future desktop-image boundary and standalone WPF acceptance.


## Local heap and optional Lasers tests

Tutorial 10 and the public heap tests require no original AD file. They use
`Win16LocalHeap` directly for allocator invariants and the real gateway for
DS ownership, Pascal frames, unsigned sizes, return registers and guest writes.
See [the memory ownership walkthrough](win16-local-heap.md).

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 10
dotnet test --filter "FullyQualifiedName~LocalHeap"
$env:AFTER_DARKER_LASERS = (Resolve-Path ad/Lasers.ad).Path
dotnet test -p:TestLocalLasers=true --filter TestCategory=LocalLasers
```

`TestLocalLasers=true` compiles six private integration cases. The module is
read in place, never copied into test output. The 306-case run through Lasers uses
`BuildWin16Fixture`, `TestLocalMondrian`, `TestLocalSpiralGyra`,
`TestLocalRainstorm`, `TestLocalFadeAway`, and `TestLocalLasers`, all set to true,
with the corresponding `AFTER_DARKER_*` environment variables set as shown
above and in the earlier sections. The public suite alone has 235 cases.

## Optional Magic tests

```powershell
$env:AFTER_DARKER_MAGIC = (Resolve-Path ad/Magic.ad).Path
dotnet test -p:TestLocalMagic=true --filter TestCategory=LocalMagic --report-trx
```

`TestLocalMagic=true` compiles six private cases; the module is read in place.
They verify the guest's history index, velocity-change counter and color index
on every one of 1,700 draws, plus independent guests, three dimension cases
and clean shutdown even without drawing. See [the evidence](research/magic-execution.md).
No new public API behavior is introduced; the existing public heap and
pen/gateway conformance tests cover the reused services.

For the full **312-case** regression, add `TestLocalMagic=true` to the six
opt-ins above and set `AFTER_DARKER_MAGIC` alongside their environment variables.
