# Current Status

Last updated: 2026-08-30

## Project phase

After Darker is initialized as a documentation-first research repository. No
emulator, loader, Win16 shim, or renderer has been implemented here yet.

## Established evidence

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

The next session will define a small bootstrap backlog. The first technical
milestone should be a generated, redistributable 16-bit conformance program that
proves segmented far calls and a synthetic host-gateway round trip before a real
After Dark module is loaded.

## Session log

### 2026-08-30 — repository bootstrap

- Established the preservation and educational charter.
- Recorded the narrow Win16/After Dark product boundary.
- Serialized the proposed NE loader, import gateway, ABI, GDI surface, and host
  architecture.
- Marked protected-mode CPU-engine behavior as a required proof rather than an
  assumption.
- Added repository exclusions for private legacy modules and reference media.
