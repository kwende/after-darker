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

In Visual Studio, keep **AfterDarker.Tutorials** as the startup project for F5.
Use **Test > Test Explorer** for automated tests, with a Visual Studio version
that supports .NET 10 and Microsoft.Testing.Platform. CLI discovery/execution
is verified; an interactive Test Explorer session remains a manual check.

## Current coverage

| Category | Cases | What is established |
| --- | ---: | --- |
| Unit | 54 | Descriptor/frame layout plus typed NE header, segments, entries, imports, resources, malformed-input rejection, report evidence boundaries, and SDK invocation-plan selection |
| Conformance | 13 | Actual Unicorn execution: word addition including wraparound, near CALL/RET, protected far CALL/RETF, two gateway stop/resume cycles, signed/custom returns, real guest stores, and handler exception propagation |
| Tutorial | 5 | Four CPU lessons plus NE inspection through both prompted and explicit paths using generated input |
| **Total** | **72** | All passing on the current Windows x64 development host |

Conformance tests use the native engine; they are not isolated unit tests or a
mock of Unicorn. Categories make the distinction explicit. All fixtures are
generated code/bytes. Nothing reads the private `ad/` directory.

Tests create and close their own engines and run serially for now. Guest runs
retain their instruction/time limits. No native state is shared between tests.

The decoder supports same-privilege far returns with word arguments. It is not
a general Win16 ABI decoder: structures, far pointers, DX:AX results, privilege
transitions, and wrapping stacks need separate designs and tests when required.
Descriptor encoding tests establish bytes, not CPU enforcement of limits.

## Optional compiler-built fixture

The [Hello42 Win16 DLL](../tests/fixtures/win16/hello42/README.md) exercises the
NE reader against real compiler output, independently of the hand-generated
metadata fixtures. With the [pinned Watcom installation](watcom-toolchain.md):

```powershell
dotnet test -p:BuildWin16Fixture=true
dotnet test -p:BuildWin16Fixture=true --filter "TestCategory=Toolchain"
```

This builds the DLL and adds three `Toolchain` cases, for 75 passing cases in
the combined suite. They check Windows-library startup/data, public code exports
and resident WEP, and the exact linked import set/resources. They do not execute
the DLL or assert a runtime return value. The source and build instructions are
tracked; outputs live in ignored `artifacts/`. Default runs remain compiler-free
and contain the original 72 cases. Do not pass `--no-build` when changing this
opt-in property, since it changes which tests are compiled.

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
