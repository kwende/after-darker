# AGENTS.md

## Purpose

After Darker exists to run original 16-bit Windows After Dark screensaver
modules inside a modern 64-bit application without requiring a complete legacy
Windows virtual machine.

The project is investigatory, educational, and preservation-minded. Its goal is
not merely to imitate the pictures produced by old modules. The defining goal
is to execute their original Win16 machine code while providing the smallest
credible compatibility environment around it:

```text
original .AD module
    -> Win16 NE loader and segmented execution
    -> intercepted KERNEL / USER / GDI imports
    -> After Dark host contract
    -> modern render surface and application shell
```

The user values understanding as highly as implementation. Explain the mental
model behind meaningful changes, record proof boundaries, and leave the code
more inspectable than you found it.

## Product Boundary

After Darker is a narrow compatibility layer, not a general Windows 3.1
emulator.

In scope:

- Parsing and validating 16-bit New Executable (`NE`) modules.
- Emulating the x86 behavior required by supported After Dark modules.
- Modeling selectors, segmented memory, far pointers, and Win16 calling
  conventions faithfully enough for those modules.
- Resolving NE imports to host-managed gateway entries.
- Implementing an evidence-driven subset of Win16 `KERNEL`, `USER`, and `GDI`.
- Recreating the After Dark module lifecycle and data structures.
- Translating GDI behavior into a modern, observable rendering surface.
- Tracing, diagnostics, deterministic tests, and conformance comparisons.

Not initially in scope:

- Booting DOS or Windows 3.1.
- Emulating BIOS, disks, video cards, printers, or an entire desktop.
- General compatibility with arbitrary Win16 applications.
- Rewriting every module natively as a substitute for original-code execution.
- Perfect settings dialogs, sound, helper DLLs, or obscure GDI behavior before
  a target module demonstrates that they are required.

Native recreations may be used as behavioral oracles and educational examples,
but they must be labeled clearly. A native recreation is not evidence that the
original `.AD` code executed successfully.

## Compressed Mental Model

Think of the runtime as three replaceable pieces:

1. **Guest machine**: registers, flags, segmented memory, and instruction
   execution.
2. **Win16 personality**: NE loading, relocations, imports, handles, ABI
   marshaling, and the supported API subset.
3. **After Dark host**: lifecycle messages, settings structures, timing, and an
   HDC-like surface whose operations become modern pixels.

The import gateway is the central seam. The loader patches an imported far call
to a host-owned synthetic `selector:offset`. When execution reaches the
gateway, the runtime identifies an `ImportBinding`, leaves the guest execution
loop, reads Pascal arguments from the guest stack, invokes a typed host handler,
writes the return value, simulates the correct far return and stack cleanup,
then resumes the module.

The synthetic gateway is our runtime design, not a magical address range
provided by x86 or automatically understood by a CPU emulator.

## Authoritative Project State

Before substantial work, read:

1. `README.md`
2. `docs/architecture.md`
3. `docs/current-status.md`
4. The affected code and tests

`docs/current-status.md` is the living record of what has actually been proven.
Append meaningful discoveries, decisions, risks, and next steps before ending a
substantial session. Do not silently promote hypotheses from
`docs/architecture.md` into facts.

When behavior or an architectural boundary changes, update the explanatory
documentation in the same pull request.

## Evidence Discipline

- Distinguish four categories explicitly: artifact fact, observed runtime
  behavior, reasoned inference, and modern adaptation.
- Record source artifact hashes when they anchor a discovery, but do not commit
  proprietary modules or personal reference media.
- Use imports, relocations, disassembly, SDK declarations, traces, and pixel
  output as evidence. A module name or visual resemblance is not algorithmic
  proof.
- Treat `.A$` files as possible installer-compressed payloads, not automatically
  as executable NE modules or corrupt archives.
- Skip password-protected archives that do not open normally. Do not attempt to
  crack them.
- Preserve original behavior by default. Label pacing, scaling, antialiasing,
  palette, DPI, and presentation changes as host adaptations.
- A smoke test proves execution survived; it does not prove visual fidelity.
- When a DOS service needs implementation or behavioral clarification, consult
  the local Microsoft MS-DOS sources at `C:\repos\MS-DOS` and the
  [DOS source reference](docs/research/dos-source-reference.md). Cite the DOS
  version, revision, and relevant symbol alongside the API contract. These
  sources are a research reference, not evidence for Win16 KERNEL/USER/GDI
  semantics or a requirement to boot DOS.

## Architecture Rules

- Keep the NE parser independent from the CPU engine, Win16 shims, and renderer.
- Keep the CPU engine behind a small interface so an initial engine can be
  replaced without rewriting the loader or host.
- Do not choose an x86 engine based only on its claim to support 16-bit mode.
  First prove protected-mode selector bases and limits, segment overrides, far
  calls and returns, gateway reporting, and safe resume behavior.
- Resolve imports through a canonical registry keyed by module plus ordinal or
  name. Store calling convention, argument layout, return layout, and handler
  identity in an inspectable `ImportBinding`-style record.
- Prefer one host-gateway mechanism with a lookup table over scattered special
  cases embedded in instruction execution.
- Dispatch host services outside the emulator hook when practical. Treat the
  hook as a controlled exit, not a place to hide application logic.
- Never expose native host pointers to guest code. Represent Win16 handles and
  far pointers with explicit guest-owned values and checked translation.
- Model GDI as a stateful device context, not merely a writable pixel pointer.
  Pens, brushes, selected objects, current position, clipping, raster
  operations, and object lifetimes matter.
- Keep original GDI semantics separate from the final display backend. A
  software pixel surface is preferred for deterministic conformance tests.
- Support reverse host-to-guest callbacks only when a target module requires
  them. They are a distinct re-entrant execution boundary and must be tested as
  such.
- Bound instruction counts, memory, recursion, callbacks, and frame time. Old
  native code should never be allowed to hang or corrupt the modern host.

## Incremental Delivery Order

Prefer narrow vertical proofs over a broad incomplete emulator:

1. Characterize target artifacts and build an import/API census.
2. Prove the chosen CPU engine's segmented protected-mode behavior.
3. Parse NE metadata without executing it.
4. Load segments and apply internal relocations deterministically.
5. Route one synthetic imported far call through the host gateway and back.
6. Invoke a minimal After Dark lifecycle function with known guest structures.
7. Render a small, deterministic GDI subset to a software surface.
8. Execute a low-import module such as Spiral Gyra end to end.
9. Expand the compatibility surface only when another module supplies evidence
   for the need.

WineVDM/OTVDM is valuable prior art and may serve as a sidecar experiment or
behavioral oracle. Do not vendor, fork, or distribute it without documenting
the technical reason and reviewing its licensing consequences. A custom
in-process engine remains an architectural option, not a proven decision.

## Testing and Diagnostics

- Make parser, relocation, selector, ABI, handle, and raster tests deterministic.
- Prefer tiny hand-authored guest programs for CPU and gateway conformance before
  using a complete `.AD` module.
- Every supported import needs tests for argument order, signedness, pointer
  translation, return registers, and stack cleanup.
- Make instruction traces, import traces, selector maps, handle tables, and
  frame captures available as opt-in diagnostics.
- Compare both state and pixels where possible. For randomized modules, hold a
  seed constant while changing only the behavior under test.
- Unknown imports should fail with a symbolic diagnostic, not an unexplained
  invalid-instruction or unmapped-memory error.

## Repository and Artifact Safety

- Never commit original After Dark modules, installers, archives, ISOs, virtual
  disks, screenshots, or videos unless the user explicitly establishes that
  they are redistributable and asks for that exact addition.
- Use local bring-your-own-module paths for integration tests. Keep public tests
  self-contained or generated from source.
- Do not log personal paths, binary contents, or unrelated private data in
  public artifacts.
- Preserve unrelated work in a dirty worktree. Do not reset, discard, or rewrite
  user changes.
- Do not add dependencies casually. Record why a dependency is needed, which
  layer owns it, and its license.

## Git Workflow

- `main` is protected and represents reviewed, explainable progress.
- Create a focused branch for changes and merge through a pull request.
- Do not push directly to `main` after repository bootstrap.
- Keep commits small enough that loader, emulator, compatibility, and renderer
  changes can be reviewed independently.
- A pull request should state what is proven, what remains inferred, how it was
  tested, and whether it expands the supported Win16 surface.

## Definition of Done

A compatibility feature is done when:

- Its layer and responsibility are clear.
- Relevant deterministic tests pass.
- Failures are bounded and diagnosable.
- The proof boundary is documented.
- `docs/current-status.md` records the result.
- The user can understand the under-the-hood behavior without reverse
  engineering the implementation again.
