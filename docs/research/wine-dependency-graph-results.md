# Wine dependency graph: what is ahead of us

Date: 2026-09-25. This is a completed **static source analysis**, not a playback
test, a complete Wine index, or a count of C# methods we must implement.

## Decision-facing result

There are real, relatively small algorithms worth studying in Wine. There is
also substantial runtime state behind its API wrappers. The graph supports
**selective algorithm reuse with our existing guest-memory, handle and device
context services** as a concrete research direction. It does not establish that
translating the full reachable Wine implementation would be quicker.

The work separates into these families. Consumer counts below are distinct
screensaver binary hashes with a direct import, not confirmed execution paths
or a prediction of how many new savers one implementation will unlock.

| Family | Evidence in our collection | Approximate nature of the work |
| --- | --- | --- |
| Small value/geometry/string helpers | `OffsetRect16` has a one-function closure; `lstrcpy` is imported by 35 binaries | Usually bounded implementation and ABI/memory checks. Our existing rectangle services already demonstrate this category. |
| Global memory and handles | `GlobalAlloc`/`GlobalFree`: 38 each; `GlobalReAlloc`: 27; `GlobalHandle`: 23 | Shared capability with broad potential reuse. Build on our guest machine, but distinguish handles, selectors, locks, relocation and lifetime; Wine's heap cannot simply become ours. |
| Palette state | `SelectPalette`/`RealizePalette`: 22 each; `CreatePalette`: 11; `AnimatePalette`: 7 | A coherent subsystem rather than independent color-returning functions. Need to establish how indices, selected palettes and existing pixels interact on the exercised paths. |
| Bitmap conversion and scaling | `StretchBlt`: 3 direct consumers; DIB creation/transfer: 1–2 per API | Promising algorithm extraction boundaries. Retain our surfaces and handles; adapt geometry, pixel formats and raster semantics. The row-scaling example below is small, but it is not the complete API. |
| Text and fonts | `TextOut`: 44; `SetTextAlign`: 40; `CreateFontIndirect`: 4 | High import frequency, uncertain playback relevance. Some imports may serve errors or options. Font/backend extraction is incomplete in this build, so no small-cost claim is justified. |
| Callbacks and execution context | `MakeProcInstance`/`FreeProcInstance`: 21 each; `Catch`/`Throw`: 15 each | Potential guest re-entry and saved-context work. Determine whether the selected playback paths exercise these before expanding the runtime. |
| After Dark helper libraries | `ADXPL300` and `ADTOOL`: 12 consumers each; `AD_RSRC`: 11 | Separate from Wine. Loading the original helpers may avoid reimplementing their algorithms, but requires their contracts, relocations, initialization and Windows imports. |
| Floating-point helper | `WIN87EM`: 18 consumers | CPU/helper ABI investigation; source call graphs do not capture all assembly or emulation behavior. |
| Errors, configuration, sound and desktop integration | For example, `FatalAppExit`: 42; `MessageBox`: 29 | Trace and classify before implementing. Imports alone cannot tell us that these run during playback. Color-only and optional-audio policies still apply. |

These are relative engineering categories, **not hour/day estimates**. The graph
does not measure supported argument combinations, visual fidelity, test effort,
or the guest behavior needed to exercise them. Our 49 currently registered
Windows handlers used by these files have deliberately limited contracts.

## Inputs and coverage

- Pinned upstream Wine revision:
  [`4f4d68f6a784db76e66cf0f033f90b8288bba11e`](https://github.com/wine-mirror/wine/tree/4f4d68f6a784db76e66cf0f033f90b8288bba11e).
  This is upstream Wine, not a claim about the exact build of WineVDM used in
  the original-host trial or the implementation inside Windows 98.
- CodeQL CLI **2.27.1**, `codeql/cpp-all` **12.1.1**, Ubuntu 26.04 under WSL.
- **24 selected Wine modules**, including Win16 KERNEL/USER/GDI wrappers,
  their Win32 counterparts, `win32u` and its DIB renderer, memory/runtime,
  common controls/dialogs, shell and multimedia dependencies.
- **430 requested C/C++ object targets; 451 successful extracted compilation
  units including generated/build dependencies; zero extractor failures.**
  MinGW GCC 13 compiled PE objects; GCC 15 compiled Unix build dependencies.
- All private `.ad`/`.dll` inputs were read as metadata, deduplicated by SHA-256:
  **65 screensaver hashes, 22 helper hashes, of which seven helpers are reachable
  through module references**. Two PE files were rejected by the NE inventory.
  Different hashes may represent versions of the same named screensaver.
- Imports from the 65 savers and seven reachable helpers identify **360
  Wine-export identities**, of which **354 map to function roots** (353 distinct
  function symbols; two clock exports share one implementation). Six identities
  are constants or stubs: `__AHSHIFT`, `__AHINCR`, ordinal and named
  `__WINFLAGS`, `FatalExit`, and `GetNextQueueWindow`.

The direct screensaver imports include 156 USER, 88 GDI and 69 KERNEL identities.
Following helper module references expands these to 158, 95 and 82 respectively.
Counts include data/constant exports. Among the 322 direct Wine-mapped import
identities, our registry has 49 guarded handlers, seven explicitly unsupported
entries and 266 unregistered identities. This is **not 273 required new APIs**:
the imported set includes initialization, error and configuration paths.

The full metadata inventory has 806 import identities across all 87 NE artifacts.
That larger number includes helpers not referenced by a screensaver and
proprietary helper exports. For example, the 312 imported `ADXPL300` ordinals
are calls **into ADXPL300**, not 312 additional Windows APIs. Helper discovery
matches NE resident module names and retains all matching versions; it is a
candidate dependency graph, not a proven loader search order.

## What CodeQL produced

| Relation | Count |
| --- | ---: |
| Function symbols, including declarations/builtins | 45,731 |
| Symbols with definitions, including toolchain/header bodies | 33,829 |
| Defined symbols whose canonical location is within the Wine source tree | 20,881 |
| Normalized call sites | 125,021 |
| Direct/candidate call-target edges | 120,005 |
| Field/global access observations | 192,027 |
| Static call-argument observations | 341,282 |
| Indirect call sites | 7,945 |
| Indirect sites with structural candidate targets | 1,704 |
| Indirect sites still without candidate targets | 6,241 |

Counts describe the whole selected index, not just the transitive dependencies
of currently missing imports. External declarations are explicit nodes, even
when the named call target is known. No unresolved call is silently converted
into a harmless leaf.

The graph has two separately stored reachability modes:

- **`direct`** follows named CodeQL call targets.
- **`candidates`** additionally follows function-pointer targets found in typed
  field/variable initializers and assignments, plus `.spec` export forwarding.
  These edges are possible targets, not observed driver selections.

The generic CodeQL `resolveCall` data-flow query was attempted, then stopped
after roughly eight minutes of expensive whole-index evaluation without a
usable result. It is retained as an optional query. The published graph uses
the narrower structural model, whose limitations are explicit and tested.

## A concrete finding: StretchBlt's algorithm boundary

The extracted direct edges confirm:

```text
StretchBlt16 -> StretchBlt -> NtGdiStretchBlt
```

At the `pStretchBlt` dispatch slot the graph finds three candidates:
`dibdrv_StretchBlt`, `nulldrv_StretchBlt`, and `windrv_StretchBlt`. It also follows
the scaling row-operation slots without incorrectly pulling in unrelated fields
from the same driver table.

The buffer-oriented scaling code offers a much smaller inspection boundary:

| Entry | Defined-function closure or explicit boundary |
| --- | --- |
| `calc_1d_stretch_params` | Six including the entry: five DIB functions and `abs` |
| `stretch_row_32` | Six, all in DIB code |
| `shrink_row_32` | Six, all in DIB code |
| `stretch_bitmapinfo`, stopping at DIB directory exits | 12 with named calls; 94 with candidate row-format dispatch |
| `stretch_bitmapinfo`, unrestricted selected-index reachability | 3,304 with named calls; 4,232 with candidate dispatch/forwards |

Why does the unrestricted closure become so large? It includes diagnostics,
assert/failure handling, common DC/handle services, and their runtime dependencies;
conservative dispatch and merged source symbols can expand it further. For
example, the DIB boundary explicitly records `__assert_fail`, `wine_dbg_log`,
rectangle helpers, color-table/stride helpers, `memmove` and `memset`.

**Neither 94 nor 4,232 is the number of functions required for our StretchBlt.**
Ninety-four includes alternative pixel-format paths; the directory boundary
does not make its outside dependencies disappear. Six row helpers do not cover
clipping, coordinate transforms, formats, ROPs or Win16 argument translation.
They do demonstrate that an algorithm can be studied independently of the full
Wine runtime. See the pinned
[scaling implementation](https://github.com/wine-mirror/wine/blob/4f4d68f6a784db76e66cf0f033f90b8288bba11e/dlls/win32u/dibdrv/bitblt.c#L1112)
and [row operations](https://github.com/wine-mirror/wine/blob/4f4d68f6a784db76e66cf0f033f90b8288bba11e/dlls/win32u/dibdrv/primitives.c).

## Precision and proof boundaries

1. **Compiled configuration only.** We disabled X11, Wayland, FreeType and tests;
   other optional dependencies were unavailable. Font backends, unselected
   modules, inactive preprocessor branches and complete driver behavior are not
   covered. Zero extractor failures does not mean complete Wine coverage.
2. **Source objects, not a linked process.** We compiled objects without linking
   the final DLLs. CodeQL merges 218 symbols that have multiple definitions in
   this index, including DLL entrypoints and CRT duplicates. The `definitions`
   table preserves every definition location. Inspect it before interpreting a
   closure as a path through one particular DLL. The root mapper reports no
   multi-symbol root matches; that does not remove this definition ambiguity.
3. **Indirect calls remain approximate.** Typed-slot candidates are
   flow-insensitive. Dynamic symbol loading, callbacks supplied from elsewhere,
   parameter-dependent targets and assembly transitions may remain unresolved.
   Neither reachability mode is a rigorous bound on all actual executions.
4. **No branch specialization yet.** Constant arguments and access types are
   stored to support it. A handle-conversion call with a constant object kind
   still reaches the other cases in the source function. Access observations
   do not establish ownership, precise read/write effects, or pointer lifetime.
5. **No guest trace.** We did not attach a debugger, run new AD playback, or
   establish which imported APIs run in color-only, no-audio playback. Existing
   support is registry evidence, not full API conformance.
6. **No port or Wine dependency added to playback.** Any later source adaptation
   must retain provenance and address the source license. Proposed adaptation
   annotations are human-review candidates and do not prune this graph.

## How to use this evidence next

Choose one concrete playback blocker, then use this dataset to locate its Wine
entry, shared state, candidate backends and algorithm boundary. For the broadest
potential reuse, global memory and palettes are stronger candidates than the
three direct StretchBlt consumers. For the clearest experiment in extracting an
algorithm, the scaling helpers remain attractive.

Next compare one exercised input against a trusted implementation and record
which branches, callbacks, formats and error paths actually occur. Existing
Unicorn import traces can narrow the guest requirements; a WineVDM or Win98
trace can supply an oracle where guest execution currently stops. A Win98 trace
will identify Windows behavior, not automatically select a corresponding Wine
source path. No debugger was needed to build this graph.

The decision to make after that experiment is whether Wine's algorithm plus
our adapter and tests is clearer and faster than extending our current code.
We now have source locations and a queryable dependency model to make that a
bounded experiment, rather than assuming either wholesale porting or continued
independent implementation wins.

## Retained artifacts and reproduction

The primary local dataset is **`artifacts/wine-analysis/graph.sqlite`**. It can be
queried on Windows with Python; CodeQL and WSL are unnecessary for normal graph
questions. Alongside it are `manifest.json` with hashes, `inventory.json`,
`extraction.json`, `summary.json`, `assessment.json`, `api-summary.csv`,
`verification.json`, build configuration and compressed raw CodeQL CSV relations.
The directory is ignored. It contains metadata, not original AD binary payloads.

The semantic CodeQL database, BQRS results and pinned Wine source remain in WSL:

```text
/home/brush/after-darker-analysis/wine-codeql-gcc
/home/brush/after-darker-analysis/results-verified
/home/brush/after-darker-analysis/wine-4f4d68f6a784db76e66cf0f033f90b8288bba11e
```

Analysis sources, schema, ready-made questions and reproduction instructions are
in [tools/wine-analysis](../../tools/wine-analysis/README.md). Compact aggregate
results are retained in [wine-graph-summary.json](wine-graph-summary.json).

Verification: four Python tests passed; a separately compiled CodeQL C fixture
passed field-slot selection, conditional pointer aliases and macro call-site
identity checks; nine final graph/integrity/source-landmark checks passed. No
production code changed, so this session did not rerun playback or .NET tests.

**Durable model:** imports tell us the API surface; call graphs expose the
implementation dependencies; traces select the paths; adaptation boundaries
determine what we actually need to build.
