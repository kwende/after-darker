# Current Status

Last updated: 2026-09-16

## Project phase

After Darker has a C# tutorial console host with real-mode addition and
near-call experiments, plus 16-bit protected-mode far-call and synthetic host
gateway experiments using Unicorn 2.1.3. No After Dark runtime, NE loader,
Win16 shim, or renderer has been implemented here yet.

## Established evidence

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
- Spiral Gyra presents a promising first original-code target because its
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

The owner authorized tutorial 04 and its commit/push for morning review.
Review tutorial 04 together before advancing. The owner wants F5-able
C# lessons in one console app, with separate implementation classes invoked
through `ITutorial`. Understanding and explicit readiness govern progression.
Issue #1 tracks the broader protected-mode and host-gateway experiments.
Tutorial 04 implements the narrow host trap; the broader issue is not complete.
See [the tutorial guide](tutorials.md).

## Session log

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
