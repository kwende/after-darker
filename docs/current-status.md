# Current Status

Last updated: 2026-09-15

## Project phase

After Darker has a C# tutorial console host and one working 16-bit real-mode
addition experiment using Unicorn 2.1.3. No After Dark runtime, NE loader,
Win16 shim, or renderer has been implemented here yet.

## Established evidence

- Tutorial 01 runs `MOV AX, 7; ADD AX, 5` in Unicorn from a native Windows C#
  host, reads `AX=12` and `IP=0x1006`, and exits successfully. This is a bounded
  two-instruction real-mode probe, not protected-mode conformance.

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
- A synthetic import gateway can conceptually map an NE import to a host-owned
  far address, stop guest execution on entry, marshal Win16 arguments, call a
  host handler, simulate the far Pascal return, and resume.

## Important unproven assumptions

- No candidate CPU engine has yet passed an After Darker protected-mode
  conformance probe.
- In particular, 16-bit mode support alone does not prove correct selector
  bases/limits, segment overrides, far control flow, gateway address reporting,
  or safe resume after a host exit.
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

Walk through tutorial 01 together before advancing. The owner wants F5-able
C# lessons in one console app, with separate implementation classes invoked
through `ITutorial`. Understanding and explicit readiness govern progression.
Issue #1 tracks the later protected-mode and host-gateway experiments; those
remain unimplemented. See [the tutorial guide](tutorials.md).

## Session log

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
