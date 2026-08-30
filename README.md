# After Darker

After Darker is an experimental compatibility layer for running original
16-bit Windows After Dark screensaver modules inside a modern 64-bit
application.

The project asks a deliberately narrow question:

> How little of Windows 3.1 do we need to recreate for an original After Dark
> module to load, execute, call Win16 GDI, and draw into a surface owned by a
> modern application?

The intended answer is not a complete virtual machine. It is a small,
inspectable runtime:

```text
                    Modern 64-bit application
                              |
                  frame presentation and controls
                              |
                    After Dark host contract
                              |
          +-------------------+-------------------+
          |                                       |
    Win16 API shims                         software surface
  KERNEL / USER / GDI                    pixels and GDI state
          |
       import gateway
          |
  NE loader + segmented x86 execution
          |
      original .AD module
```

## Why this is feasible

After Dark modules are DLL-like Win16 New Executable (`NE`) programs. After
Dark owns the window and calls a small module dispatcher while supplying a
Win16 GDI device context. The module performs ordinary imported GDI operations
such as creating pens, selecting objects, moving the current point, drawing
lines, setting pixels, and manipulating regions.

That gives the project a useful boundary: emulate the CPU and the subset of
Win16 actually used by target modules, then translate their HDC operations into
modern pixels. We do not need to boot DOS, install Windows 3.1, or reproduce an
entire desktop.

## Status

The repository is in its architecture and bootstrap phase. It contains no
working emulator yet.

Prior research has established the After Dark lifecycle, inspected several
real modules, and recovered enough behavior from Spiral Gyra and Stained Glass
to make them useful future conformance targets. The most important unproven
assumption is whether the selected embeddable x86 engine correctly exposes the
protected-mode segmented behavior required by Win16 NE code.

See:

- [Architecture](docs/architecture.md)
- [Current status and proof boundary](docs/current-status.md)
- [Agent collaboration charter](AGENTS.md)

## Guiding principles

- Execute original module code where practical; clearly label native
  recreations and modern adaptations.
- Build only the Win16 surface demonstrated by real module evidence.
- Prefer deterministic, observable components over opaque compatibility magic.
- Keep the CPU engine, NE loader, Win16 personality, After Dark host, and
  renderer replaceable at explicit boundaries.
- Make the project educational. Every supported import and loader behavior
  should be explainable.
- Treat original modules as bring-your-own local inputs. Do not commit or
  redistribute them through this repository.

## Two possible execution strategies

The architecture deliberately leaves room for two experiments:

1. Use WineVDM/OTVDM as a sidecar with a small Win16 After Dark runner and move
   frames into the modern application.
2. Embed a software x86 engine and implement a purpose-built NE loader plus the
   narrow Win16/After Dark compatibility surface in-process.

WineVDM is strong evidence that the overall approach is viable. A custom engine
would provide tighter control, better diagnostics, and a cleaner rendering
boundary, but it must first pass segmented protected-mode conformance probes.

## Legal and project status

After Darker is an independent preservation and interoperability experiment. It
is not affiliated with the original After Dark publishers or module authors.
No project license has been selected yet. Dependency and distribution licenses
must be reviewed before code or third-party components are shipped.
