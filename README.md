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

The repository is in its bootstrap phase. Its C# tutorials run real-mode
addition and near calls, a 16-bit protected-mode `CALL FAR`/`RETF` round trip,
and a synthetic C# host gateway with stop, dispatch, and guest resume in Unicorn.
Tutorial 05 reads Windows NE files into typed C# metadata and reports their
entry points, imports, resources, and an assumed After Dark invocation plan.
There is no After Dark runtime, NE loader, Win16 shim, or renderer yet.

Prior research has established the After Dark lifecycle, inspected several
real modules, and recovered enough behavior from Spiral Gyra and Stained Glass
to make them useful future conformance targets. A narrow protected-mode
far-call probe and a two-call host-gateway probe now pass; segment protection
and broader Win16 behavior still require conformance experiments.

See:

- [Architecture](docs/architecture.md)
- [Current status and proof boundary](docs/current-status.md)
- [Local module import/API census](docs/research/ad-import-census.md)
- [Mondrian's constrained visuals path](docs/research/mondrian-static-analysis.md)
- [Local MS-DOS source reference](docs/research/dos-source-reference.md)
- [Agent collaboration charter](AGENTS.md)
- [Run and understand the tutorials](docs/tutorials.md)
- [Run the automated tests](docs/testing.md)

Open `AfterDarker.sln` in Visual Studio with .NET 10 support and press **F5**.
The single console project runs tutorial 01 by default and exits after printing
`AX = 12 (0x000C)`. The first build restores its dependencies automatically.
See the tutorial guide for prerequisites and the Windows compatibility setting.
Select the `Tutorial 02 - stack and near call` launch profile to run the stack
experiment instead.
Select `Tutorial 03 - protected-mode far call` for the guest-to-guest far call.
Select `Tutorial 04 - host gateway` for a far call serviced by C#.
Select `Tutorial 05 - NE file inspection` to inspect a local `.AD` file. It
prompts for a path, reports metadata, and exits without executing guest code.
To supply the path from the command line:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 05 "C:\repos\after-darker\ad\Mondrian.ad"
```

Run `dotnet test` from the repository root for the C# test suite. The solution
contains a small shared `AfterDarker.Core` library, the educational console app,
and `AfterDarker.Tests`. Keep `AfterDarker.Tutorials` as the startup project for
F5; automated tests are available separately through Test Explorer.

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

Local After Dark copies and supporting files for testing belong in the root
`ad/` directory, which Git ignores in its entirety. These are private,
bring-your-own inputs; do not force-add or redistribute them. Public tests must
use self-contained fixtures or artifacts generated from source.

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
