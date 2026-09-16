# Architecture

## 1. Objective

After Darker should execute the original machine code from selected Win16 After
Dark `.AD` modules and present their drawing through a modern application-owned
surface.

The target is a compatibility *slice*:

```text
16-bit x86
+ NE module loading
+ observed Win16 APIs
+ After Dark lifecycle
+ deterministic GDI-compatible surface
```

It is explicitly not a commitment to emulate all of Windows 3.1.

## 2. Known After Dark host boundary

The recovered After Dark 2.0 Windows SDK describes a DLL-like dispatcher:

```c
int FAR PASCAL Module(int iMessage, HDC hDrawDC, HANDLE hADSystem);
```

The important lifecycle messages are:

```text
INITIALIZE       0
BLANK            1
DRAWFRAME        2
CLOSE            3
MODULESELECTED   5
ABOUT            6
BUTTONMESSAGE    7 through 10
PREINITIALIZE   12
```

The dispatcher stores the supplied HDC and system handle, locks the After Dark
system and module structures, and invokes parameterless module routines such as
`DoInitialize`, `DoDrawFrame`, and `DoClose`.

The initial runtime can avoid module-provided configuration dialogs. It can
construct the system/module records, supply control values directly, and focus
on `PREINITIALIZE`, `INITIALIZE`, `BLANK`, `DRAWFRAME`, and `CLOSE`.

The [Mondrian static path analysis](research/mondrian-static-analysis.md) now
grounds this first target: nine Windows imports and two DOS date/time interrupt
services appear sufficient for its successful startup/drawing/close path.
This is a static scope estimate, not an execution proof. The owner explicitly
prioritizes original-code visuals with fixed options over dialogs or settings
persistence. Supply options in guest records and preserve the guest's drawing
logic; do not build unrelated Windows services in anticipation of other modules.

## 3. Layered design

### 3.1 Artifact inspector

Responsibilities:

- Distinguish installer-compressed `.A$` payloads from executable `.AD`
  modules.
- Validate `MZ` and `NE` headers.
- Enumerate segments, flags, entry bundles, exports, resources, module
  references, imported names and ordinals, and relocation records.
- Produce a stable machine-readable inspection report without executing code.

This layer must not depend on the CPU engine or renderer.

`AfterDarker.Core.Ne.NeReader` now implements the Windows NE inspection subset
described in [tutorial 05](tutorials.md#tutorial-05-read-a-windows-ne-file).
It returns a typed `NeImage`. Segment numbers remain file-level identifiers;
the parser neither assigns selectors nor applies relocations. Raw relocation
records identify imports, but their source chains are not expanded or patched.
Resource records expose identifiers and stored byte ranges without decoding
their contents. `AfterDarkCallPlan` separately combines parsed export addresses
with the external SDK lifecycle contract; that contract is not inferred from NE.

### 3.2 NE loader

Responsibilities:

- Allocate guest storage for NE code and data segments.
- Establish segment descriptors/selectors and limits.
- Initialize automatic data, stack, heap, and module instance state as required.
- Resolve fixed and movable entry points.
- Apply internal relocations and imported-name/ordinal fixups, including chained
  source relocations.
- Load explicitly supported helper NE modules recursively or produce a symbolic
  unsupported dependency error.

An NE segment is not a PE section mapped into a single flat address space. The
guest observes `selector:offset` addresses, and the runtime must preserve the
meaning of code, data, stack, and far-pointer selectors.

### 3.3 CPU engine adapter

The CPU engine owns instruction semantics and register state. After Darker owns
the meaning of the loaded process around it.

Required capabilities include:

- 16-bit x86 instructions used by the target compiler output.
- Protected-mode segment descriptors, bases, limits, and access rules.
- `CS:IP`, `DS`, `ES`, and `SS:SP` behavior.
- Near and far calls/returns.
- Segment overrides.
- Controlled execution hooks or exits.
- Instruction-count and time limits.
- Register and memory inspection and mutation at a host exit.

An engine claiming `16-bit mode` has not satisfied this contract until a probe
demonstrates the protected-mode behaviors above.

Tutorial 03 now demonstrates a narrow subset with Unicorn 2.1.3: nonzero
descriptor bases for two code segments, data, and stack; a same-privilege
16-bit far call and return; and a guest memory write after return. It initializes
Unicorn with `UC_MODE_32` for its protected-mode API behavior, then installs
descriptors with D/B=0 for 16-bit code and stack semantics. This is a lesson-local
setup, not a general CPU adapter. Tutorial 04 additionally demonstrates a code
hook reporting the gateway's linear address, stopping before its first
instruction, and resuming after host-managed return simulation, twice on the
same engine. Limit/access enforcement and segment overrides remain unproven.
See [tutorial 03](tutorials.md#tutorial-03-protected-mode-far-call) and
[tutorial 04](tutorials.md#tutorial-04-host-gateway).

`AfterDarker.Core` now holds the shared descriptor encoder and the narrow far
Pascal word-frame decoder extracted from these lessons. The lesson classes
still contain the guest programs and emulator operations, and return typed
observations to `AfterDarker.Tests`. Unit tests exercise the pure helpers;
conformance tests exercise actual Unicorn execution. This is testable shared
code, not yet a general CPU adapter or Win16 ABI layer. See [testing](testing.md).

### 3.4 Import gateway

The import gateway translates guest calls into typed host operations.

At load time, an imported NE relocation such as:

```text
module: GDI
target: ordinal for LineTo
source: far-call operand in module code segment
```

is resolved to a host-owned synthetic far address:

```text
HOST_GATEWAY_SELECTOR:thunkOffset
```

The selector maps to a reserved linear execution range observed by the CPU
adapter. One gateway exit is associated with an inspectable binding:

```text
ImportBinding
    module name
    imported name and/or ordinal
    guest selector and offset
    calling convention
    parameter layout and byte count
    return-register layout
    host handler identity
```

Runtime flow:

```text
guest executes CALL FAR gateway:offset
    -> CPU performs ordinary far-call stack transition
    -> engine reports execution entering the gateway
    -> engine stops at a controlled boundary
    -> runtime resolves offset to ImportBinding
    -> ABI layer reads arguments from guest SS:SP
    -> host handler executes
    -> ABI layer writes AX or DX:AX and any guest-memory outputs
    -> runtime simulates RETF plus Pascal argument cleanup
    -> guest execution resumes after the original call
```

The gateway range and lookup scheme belong to After Darker. A CPU engine may
provide the code hook or exit facility, but it does not inherently understand
Win16 imports.

Tutorial 04 is an observed, lesson-local prototype of this execution boundary:
`0010:0200` maps to `Tutorial!HostAdd`, which takes two signed 16-bit Pascal
arguments and returns AX. The hook only records the exit and stops; C# decodes
the stack, invokes the handler, and restores CS:IP/SP after `EmuStart` returns.
Two successive calls produce guest stores of `12` and `-2`. The service is
synthetic: NE import resolution, a general ABI registry, and actual Win16 APIs
remain design work.

### 3.5 Win16 ABI and object model

The compatibility layer must explicitly model:

- Win16 Pascal parameter order and callee stack cleanup.
- 16-bit scalar signedness and structure packing.
- 32-bit values returned through `DX:AX` where appropriate.
- Near and far pointers.
- Global/local allocation handles and lock/unlock behavior.
- Guest strings and structures copied through checked selector translation.
- 16-bit handles backed by host objects.

Native pointers must never appear in guest memory. A guest HDC or HPEN is a
small token resolved through a checked host table.

### 3.6 Win16 modules

Built-in host modules are registered by their Win16 names, initially:

```text
KERNEL
GDI
USER
```

Each module exposes an ordinal/name registry. Unsupported imports fail during
load with a report containing the requesting module, library, ordinal/name, and
relocation source.

The supported API surface grows from target-module evidence. It should not be
filled speculatively with incomplete approximations.

### 3.7 GDI-compatible surface

After Dark passes an HDC, not a raw framebuffer. The module may create and
select objects and issue stateful drawing operations.

The host device context therefore needs at least:

```text
target surface
current pen and brush
current drawing position
text/background colors and modes when required
clipping region
raster operation
viewport/window origins when required
selected bitmap, palette, or font when required
```

The first backend should be a deterministic software surface. Pixel-exact tests
are easier when antialiasing, GPU drivers, DPI, and presentation timing are not
part of the result.

A separate presenter can copy or upload completed pixels into WPF, WinUI,
Direct2D, Skia, or another modern shell.

### 3.8 After Dark host

The host constructs guest-compatible `AD_SYSTEM` and `AD_MODULE` records,
supplies a guest-visible HDC handle, invokes the module export using the correct
far Pascal ABI, and sequences lifecycle calls.

It also owns:

- Frame pacing and cancellation.
- Screen dimensions, aspect, bit depth, and DPI values reported to the module.
- Control values and modes.
- Surface publication.
- Logging and fault isolation.

Modern pacing adjustments must not silently alter the recovered simulation. A
uniform time-scale or presentation policy should be explicit.

## 4. Rendering data flow

A typical vector path crosses the boundary like this:

```text
CreatePen guest import
    -> host creates Pen object
    -> returns guest HPEN token in AX

SelectObject guest import
    -> resolves guest HDC and HPEN tokens
    -> updates host device-context state

MoveTo guest import
    -> updates current position

LineTo guest import
    -> rasterizes from current position with selected pen
    -> updates current position
    -> pixels now exist in the software surface
```

For bitmap APIs, a far pointer is translated into bounded guest memory and the
shim reads the bitmap metadata/pixels from that memory. The original module
never receives a direct pointer into the modern process.

Some modules perform large synchronous GDI bursts inside one `DRAWFRAME` call.
Publishing only after the call returns can hide progressive construction that
was visible on slower historical hardware. The renderer should eventually
support deterministic intermediate publication by elapsed host time, operation
count, or explicit flush points.

## 5. Reverse callbacks

Some USER APIs accept guest far procedure pointers, such as dialog procedures.
Those require the opposite transition:

```text
host API
    -> save host/shim execution state
    -> push callback arguments onto guest stack
    -> set guest CS:IP to callback far pointer
    -> run until a controlled callback return
    -> collect result and resume host API
```

This is nested and re-entrant. The initial drawing proof should avoid it by
injecting module settings directly. Add callback support only with a focused
test and a module that requires it.

## 6. Candidate execution strategies

### WineVDM sidecar

WineVDM already demonstrates Win16 execution on 64-bit Windows, NE loading,
Win16-to-Win32 relays, and handle conversion. A small Win16 runner could load an
`.AD` module and supply the After Dark host contract while a modern application
receives frames through a child window, capture bridge, or shared surface.

Advantages:

- Reuses a mature compatibility surface.
- Fastest route to proving an original module can run.
- Valuable oracle for traces and behavior.

Costs:

- Not designed as a small embeddable 64-bit library.
- Clean frame transport may require invasive integration.
- Distribution and modification carry licensing considerations.

### Custom in-process runtime

An embeddable CPU engine can run inside the 64-bit application while After
Darker owns NE loading and every host service.

Advantages:

- Explicit, inspectable control of every compatibility boundary.
- Direct access to the renderer and diagnostics.
- Only required APIs need to exist.

Costs:

- Loader, selector, ABI, handle, callback, and GDI semantics are ours to prove.
- CPU-engine protected-mode behavior remains an initial technical risk.
- Compatibility expands module by module.

These paths are not mutually exclusive. A WineVDM experiment can provide an
early execution proof and reference trace while the custom runtime develops.

## 7. First conformance ladder

Before attempting a complete module, prove each boundary separately:

1. Execute a generated 16-bit instruction sequence and inspect registers.
2. Establish two protected-mode selectors with different bases and limits.
3. Read and write through `DS`, `ES`, and `SS` and test segment overrides.
4. Perform near and far calls and returns across selectors.
5. Enter a synthetic gateway and report the correct selector/offset or mapped
   linear address.
6. Stop, mutate registers/stack, simulate Pascal `RETF n`, and resume safely.
7. Parse a real NE module without loading it.
8. Load segments and validate relocation results against an independent parser.
9. Route a tiny generated guest import through one host handler.
10. Construct the After Dark records and call a minimal test module.
11. Render deterministic pen/line operations to a software surface.
12. Attempt Mondrian as the first real-module vertical slice, guided by its
    static path analysis. Retain Spiral Gyra as a later line/pen target.

Each step should leave a test and an explanatory trace. A later step does not
erase uncertainty in an earlier one.

## 8. Safety boundary

Legacy machine code is untrusted input even when its provenance is familiar.
The runtime must provide:

- Checked segment limits and pointer arithmetic.
- Bounded memory and handle counts.
- Instruction/time budgets and cancellation.
- Re-entrancy and callback-depth limits.
- No arbitrary host filesystem or process access through incomplete shims.
- Symbolic failure for unsupported imports and invalid guest behavior.
- Optional out-of-process isolation for risky or broadly compatible modes.

The most compatible implementation is not automatically the safest one.
