# Current Status

Last updated: 2026-09-16

## Project phase

After Darker has a C# tutorial console host with real-mode addition and
near-call experiments, plus 16-bit protected-mode far-call and synthetic host
gateway experiments using Unicorn 2.1.3. No After Dark runtime, NE loader,
Win16 shim, or renderer has been implemented here yet.
`AfterDarker.Core` contains two extracted binary-layout helpers, a Windows NE
metadata reader, and a separate After Dark invocation-plan model. The C# MSTest
project covers these mechanisms and the five educational console lessons.

## Established evidence

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
  This is static evidence only; Mondrian has not executed in this runtime.
- The [C# test suite](testing.md) has 72 passing cases: 54 unit, 13 native-engine
  conformance, and five tutorial entry-point checks. It uses generated NE
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
- Spiral Gyra remains a promising later original-code target because its
  observed rendering vocabulary is small: pen creation/selection, current-point
  movement, line drawing, stock objects, and object deletion.
- Stained Glass is a larger 25,008-byte NE module with nine segments and imports
  from `KERNEL`, `GDI`, and `USER`. It is a useful second-stage target because it
  exercises broader drawing and object behavior and exposes rapid synchronous
  construction.
- WineVDM/OTVDM is existing evidence that Win16 applications can execute on
  modern 64-bit Windows without booting a complete Windows guest.
- Tutorial 04 demonstrates the synthetic gateway execution boundary. Mapping
  real NE imports onto it remains a proposed loader responsibility.

## Important unproven assumptions

- Tutorials 03 and 04 are narrow protected-mode successes, not completion of
  the CPU conformance ladder. Segment-limit/access enforcement, segment
  overrides, privilege transitions, general pointer translation, and broader
  ABI layouts remain unproven. Two successful host exits/resumes do not establish
  compatibility with arbitrary Win16 guest code.
- No `.AD` module has executed inside an After Darker-owned compatibility layer.
- No After Dark system/module structure has been constructed and consumed by
  original module code in this repository.
- No GDI import has yet crossed an After Darker gateway into an After
  Darker-owned render surface.
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

The owner requested static analysis to bound the Windows support needed for
one original screensaver's visuals. Mondrian is the current first candidate;
fixed host-supplied options are sufficient for the proposed scope, without
options dialogs or settings persistence. The owner authorized the typed NE
inspection tutorial and accompanying tests. Review tutorial 05's parsed values
and contract-based call plan together before implementing the loader/services.
The owner wants F5-able
C# lessons in one console app, with separate implementation classes invoked
through `ITutorial`. Understanding and explicit readiness govern progression.
Issue #1 tracks the broader protected-mode and host-gateway experiments.
Tutorial 04 implements the narrow host trap; the broader issue is not complete.
See [the tutorial guide](tutorials.md).

## Session log

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
