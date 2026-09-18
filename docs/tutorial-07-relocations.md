# Tutorial 07: watch the loader reconnect references

This lesson turns our two-pass explanation into something you can stop and
inspect: **assign every destination first, then patch the references to those
destinations.** It uses the existing console app and reusable code in Core.
It does not create Unicorn or execute any guest instruction.

## Start with the small example

Select **Tutorial 07 - relocation walkthrough** in Visual Studio and press F5.
It uses a built-in, original NE metadata fixture: three segments, four
relocation records, and five patch sites. There are no private files, compiler
requirements, or external APIs to implement before this lesson works.

The console prints one completed patch and waits for Enter. Press `q` to stop.
The step prompt happens after the write to the private array, so a debugger can
inspect both the patch record and the changed memory. End-of-input also stops
the walkthrough; it does not claim the remaining patches completed.

Command-line equivalents from the repository root:

```powershell
# Run all patches without pausing:
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 07

# Pause after every write:
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 07 --step

# Use your own local library instead:
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 07 ad/Mondrian.ad --step
```

The **Tutorial 07 - local NE relocations** launch profile asks for a file path
and then steps through its patches. No personal path is saved in the profile.
Removing `--step` from a command runs the same algorithm with continuous output.

## Where to read and set breakpoints

Start with [Tutorial07Relocations.Execute](../src/AfterDarker.Tutorials/Lessons/Tutorial07Relocations.cs).
It parses the bytes, obtains segment placements, assigns demonstration import
addresses, and calls the shared loader. Its patch callback handles printing
and optional pauses; the console does not implement the relocation algorithm.

Then open [NeLoadPlan](../src/AfterDarker.Core/Ne/NeLoadPlan.cs):

1. `PlaceSegments`: assigns selectors and linear bases. It does not allocate CPU memory.
2. `CreateWithImportResolver`: allocates managed arrays and copies stored segment bytes.
3. `ResolveTarget`: turns a file-level target into a runtime far address.
4. The relocation loop: saves each chain link and creates the replacement bytes.
5. `Patch`: writes the bytes, records before/after evidence, and invokes the observer.

Useful debugger values are `byNumber`, `entries`, `relocation`, `target`,
`offset`, `next`, and `segment.Bytes`. Put a breakpoint just before
`replacement.CopyTo` to observe a write before it happens; the console's step
pause shows the same write after it happens.

[RelocationDemo.Create](../src/AfterDarker.Tutorials/Fixtures/RelocationDemo.cs)
constructs the small input explicitly. Those segment bytes are pointer fields
for this exercise, not a program we intend to execute.

## Pass one: make the destination map

The code separates three sources of numbers. [NeFormat](../src/AfterDarker.Core/Ne/NeFormat.cs)
names values defined by the file format, such as `Selector16`, `Offset16`,
`FarPointer16`, and `EndOfRelocationChain`. These are tags and markers, not sizes:
`FarPointer16` has tag 3 but requires four bytes. Constants in `NeLoadPlan`
separately name x86 facts (eight-byte GDT descriptors, two-byte words) and our
layout choices (at most 16 segments, placed in separate 64 KiB slots).

The relocation loop's `switch` shows exactly which part of the resolved address
gets written. A selector-only field receives the selector, an offset-only field
receives the offset, and a far-pointer field receives both, offset word first.
The destination's offset remains relative to its segment; we do not add the
linear base to it. Later, the CPU uses the selector's descriptor to find that base.

This is the distinction behind “reconnecting”: the NE tables describe the work,
but `Patch` changes **address fields inside our copied code/data arrays**. It
does not rewrite the original tables, move methods, or change a CALL opcode.
The separate export-prologue pass does rewrite recognized instructions to
establish the DLL's data segment, and is labeled separately for that reason.

The default layout gives each file segment a distinct 64 KiB linear slot and
a GDT selector. For the three-segment example:

| File identifier | Assigned selector | Assigned linear base |
| --- | --- | --- |
| S1 | `0008` | `10000` |
| S2 | `0010` | `20000` |
| S3 | `0018` | `30000` |

Addresses here are hexadecimal. These are host allocation choices. The actual
prepared arrays are sized from the stored/minimum allocation sizes plus the
automatic data segment's heap request. Allocated tails are zero-filled.

Every destination is assigned before resolving references. This is why a
reference from S1 to S3 is no harder to resolve than one from S3 back to S1.
There is no need to recursively chase references or sort the segments by who
uses whom.

Remember the two levels:

```text
Far pointer stored in guest code/data:  selector:offset
Linear address used for memory access: descriptor[selector].base + offset
```

If S2 moves to another linear base but keeps selector `0010`, its far pointers
remain `0010:...`; only the descriptor's base changes. If we give S2 selector
`0048`, those pointers must contain `0048` instead. A test deliberately changes
these choices independently to demonstrate the distinction.

No descriptor table is installed by tutorial 07. The placements and prepared
arrays are the material a later execution layer would consume.

## Pass two: resolve a target, then write the required part

The default example's first relocation identifies:

```text
Source:       S1:0004
Target:       fixed segment S2, offset 0012
Address form: far16:16
```

The dictionary resolves S2 to selector `0010`, giving target `0010:0012`.
For a 16:16 pointer, x86 stores the offset word first, then the selector word,
each little-endian. The output shows:

```text
Patch 1: S1:0004 [far16:16]
  Write destination: 0008:0004, planned linear 0x10004
  Target lookup: internal fixed S2:0012
  Resolved address: 0010:0012
  Saved chain link BEFORE overwrite: 0010 = next source offset
  Before: 10 00 00 00 -> After: 12 00 10 00
```

There are three different addresses in that example:

- **Source S1:0004:** where the pointer is stored.
- **Target S2:0012 / 0010:0012:** where the pointer should point.
- **Next source offset 0010:** another location to patch in S1.

The next-source offset is not part of the final pointer! Before loading, its
first word links to the next patch site. After saving that link, the loader
overwrites the site and follows the saved offset. At S1:0010, the original
word is `FFFF`, which terminates the chain. Both sites receive `12 00 10 00`.

One relocation record therefore produces two writes. This is why record counts
and patch-location counts need not match.

## Internal references can go through the entry table

A fixed internal relocation directly names a segment and offset. A movable
internal relocation instead contains the marker `FF` and an **entry ordinal**:

```text
relocation target = entry #2
    -> entry table: #2 is S2:0020
    -> placement map: S2 uses selector 0010
    -> resolved address: 0010:0020
```

The ordinal is not a segment number. Also, an entry does not need to be a
public export to be used by another part of the same library. The demo's entry
#2 is intentionally not exported; the shared resolver handles it correctly.
Missing entries and constant entries cannot serve as internal far addresses
in this implementation and fail explicitly.

The address form determines which portion gets written:

| NE address type | Write | Width | Example |
| --- | --- | ---: | --- |
| 2, selector | Assigned selector | 2 bytes | S3 becomes `18 00` |
| 3, far16:16 | Offset then selector | 4 bytes | S2:0012 becomes `12 00 10 00` |
| 5, offset16 | Target offset | 2 bytes | Entry #2's offset becomes `20 00` |

The pointer may designate code or data, including valid zero-filled data. We
do not assume every relocated reference is a method call. Selector-only fixed
references do not use the record's offset field; they only need the segment.

## Imports use an explicit resolver

The shared loader accepts:

```csharp
Func<NeImport, FarPointer16> resolveImport
```

That callback answers only **where the imported target will be**. It does not
have to know how a host handler is implemented. Tutorial 06's existing `Create`
overload delegates to the same loader with addresses from its real binding
table. Tutorial 07 supplies placeholder addresses for inspection only.

The console explicitly says:

```text
IMPORT ADDRESSES ONLY: these slots have NO implementation, ABI, or executable memory
```

This lets us prepare a real AD file without pretending all its imports are
implemented or assigning invented argument counts. Before execution, an actual
runtime must provide correct target bindings, including the distinction between
functions, data, and imported constants. Merely allocating a slot is not proof
of any of those semantics. An unresolved import in the shared loader still fails.

`NePatch` records preserve the source relocation, resolved address, next chain
offset, source location, and before/after bytes. The complete `NeLoadPlan` can be
passed to other C# code; console output is only one presentation of that data.

## Real-module observation: Mondrian

The local module with SHA-256
`781979da1a6a6fdf99eebec4dab67e7a645bfc8787be1671e20f13a8ca6b1aed`
prepared successfully:

- Five segments.
- 27 internal relocation records and 17 imported records.
- 66 relocation writes after expanding chains.
- Three recognized export-prologue writes, retained from tutorial 06.

Most internal records reference entry ordinals. One patches a selector directly.
An independent Python read of the raw NE tables matched all 66 source locations
and replacement values reported by the C# loader. Generated reports remain in
ignored artifacts, and the private module is not a test dependency.

This establishes file/relocation preparation for that specific input. It does
not establish DLL initialization, a working Windows environment, or screensaver
execution. The source file is opened read-only; patches affect private copies.

## Supported boundaries and tests

This increment supports non-additive internal and imported selector16,
offset16, and far16:16 relocations. It rejects additive, byte, 32-bit-offset,
48-bit-pointer, and OS fixups. It retains the existing single-data Win16 DLL
restriction and 16-segment layout limit. It does not implement segment movement,
demand loading, helper DLL loading, or general Windows memory management.

The relocation definitions and target interpretation were checked against
Wine 10.0's [NE relocation loader](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/ne_segment.c#L116).
The implementation is local C#; no Wine code or runtime was added. Our malformed
input policy is deliberately stricter: reject cycles, invalid targets, null
import selectors, overlapping writes, and incomplete source fields.

```powershell
dotnet test
dotnet test -p:BuildWin16Fixture=true
```

The default suite has 117 passing cases; the Watcom-enabled suite has 129.
The increment adds 22 unit cases and six console lesson cases. They cover fixed
and ordinal targets, chained writes, all three widths, changed selectors versus
changed bases, zero-filled targets, observer records, invalid indices/ranges,
cycles, missing/constant entries, resolver failures, step/cancel behavior, and
explicit/prompted file paths. Existing tutorial 06 execution remains a regression
check for imported far calls and export-prologue patching.

**The invariant to keep in mind: destinations are established before references
are patched; chain links are saved before their bytes are overwritten.**
