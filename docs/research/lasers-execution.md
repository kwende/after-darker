# Lasers: original drawing with a shared local heap

Date: 2026-09-19. Branch began at merged main `4b0fc18` (PR #15).
The owner chose **shared heap plus Lasers first**, preserving one module at a
time. Magic, String Theory and Zot! are not promoted by this work.

## Artifact and supported host inputs

The content-hash gate accepts the local Lasers artifact with SHA-256:

```text
8B800EABA0E1F121EF52947BB116FC6254724CEEB5E460315DF0F21E347AC430
```

Artifact facts: five NE segments, automatic data segment 5, header startup
`S4:0000`, MODULE `S1:004C`, 24 distinct imported identities, initial heap
1,024 bytes. The four controls have SDK types combo, string slider, numeric
slider and checkbox. The production profile supplies:

| Control | Host value | Observed guest meaning |
| --- | ---: | --- |
| Rays | 2 | Guest adds one: three rays |
| Width | 35 | Original lookup chooses a 30-position trail |
| Color Speed | 50 | Guest computes `(100 - 50) * 5 = 250` |
| Clear Screen First | 1 | Original BLANK/reset path clears the surface |

These are explicit host choices, not a claim to reproduce every original
default or slider conversion. The WPF speed selector is disabled for Lasers.
The system record advertises 24-bit color, and unused palette/sound fields
remain zero. Dimensions are constrained to 141..2048 on each axis: the original
origin calculation divides by `width - 140` and `height - 140`.

## What was missing

The existing NE loader, segmented CPU, import gateway and pen/line renderer
already supplied the other needed mechanisms. This increment adds four shared
KERNEL implementations: LocalAlloc #5, LocalFree #7, LocalLock #8 and
LocalUnlock #9. LocalInit now publishes a real bounded allocator after its
existing reservation checks. No additional GDI API was required.

The [local-heap guide](../win16-local-heap.md) explains memory ownership,
implicit DS, fixed versus movable handles, growth, free-list merging, and
the public source-authored tutorial. Heap behavior is in
[Win16LocalHeap](../../src/AfterDarker.Core/Win16/Win16LocalHeap.cs), independent
of Lasers and Unicorn. Per-import API entry points remain in Win16Api.

**Observed runtime behavior:** INITIALIZE calls `LocalAlloc(0x42, 1836)`:
movable, zero-initialized, three 612-byte ray records. The returned handle is
locked to obtain a near offset. Original x86 instructions fill and update those
records directly in DS. Drawing locks/unlocks that same allocation; CLOSE frees
it. There is one live local allocation during playback and none after shutdown.

The initial NE heap is smaller than the first request. **Host adaptation:** the
profile opts into pre-mapping the data segment's full 64-KiB address space. The
allocator grows logically into its bounded tail on demand. Static allocation
ends at `04B8`, leaving 64,328 bytes of capacity. Observed handle `0002` resolves
to `0028:04B8`; the numeric identity is ours, while the original module supplies
the size, flags and all ray-history contents.

## Disassembly and SDK anchors

Offsets below refer to this artifact, not a cross-version structure layout.
The full private disassembly remains under ignored `artifacts/local-heap/`.

| Location | Mechanism |
| --- | --- |
| S1:001B..001E | DLL startup saves its instance at data `03FA` |
| S1:006B..0093 | Locks supplied global records; saves far pointers at `03E6` and `04B2` |
| S1:00A8..00D0 | Checks host compatibility and sets data `0016` |
| S2:0035..004D | Decodes ray count and color threshold |
| S2:0050..0108 | Maps width-control positions to trail lengths |
| S2:0209..021A | Pushes flags `0042` and `0264 * rayCount`, calls LocalAlloc, saves handle at `0012` |
| S2:03FD..0406 | LocalLock supplies near pointer stored at `0014` |
| S2:0422..044F | Chooses origin using signed divisions by dimension minus 140 |
| S2:02B6..02E2 | Draw counter triggers periodic trail cleanup and ray regeneration after 1,000 |
| S2:0A01..0A37 | Locks ray history, indexes its records and reads through the returned near pointer |
| S2:0302..0312 | CLOSE calls LocalFree and clears the stored handle |

BLANK returns `10`, the recovered SDK's **HSV_PAL** palette request, rather than
the ordinary zero reply. The explicit profile check accepts this on the
existing 24-bit RGB rendering path, which consumes guest COLORREFs directly.
This is not indexed-palette realization. Tightening reply checks also exposed
the same already-existing Spiral Gyra reply; its profile now records the same
true-color policy. Other profiles retain their zero BLANK reply check. Unknown
DRAWFRAME replies and failed INITIALIZE replies now stop explicitly.

The [SDK notes](after-dark-sdk.md) identify MODULE.H and the downloaded source
hashes. The Win16 heap ABI/reference symbols are linked in the heap guide.
No proprietary payload or third-party implementation source is added to Git.

## Verification and proof limits

Six private tests cover:

- Initialization with one 1,836-byte movable allocation exceeding the initial
  reservation, matching guest handle/offset globals and balanced local locks.
- 1,100 draws at 640x480, including the original periodic regeneration.
  Before CLOSE: 1,256,277 instructions, 6,690 LineTo calls and one allocation.
  Final RGB SHA-256 at that point:
  `0BD9E131242ED809A74A9560221C9F412239D3D68BA37206F39D214786AD0DBA`.
- Independent deterministic sessions with the same inputs, one session staying
  usable after the other frees its heap, and ignored generic speed changes.
- 141x141, 2048x2048 and asymmetric 321x239 drawing and shutdown.
- Shutdown before the first draw, idempotent shutdown, small-dimension rejection
  and unknown-revision rejection.

The public tests exercise allocator fragmentation/coalescing, zeroing and reuse,
fixed/movable handles, zero-size and null calls, lock saturation, locked free,
capacity/16-bit boundaries, invalid inputs, per-guest state, and real far-call
argument/return/DS/stack behavior. Tutorial 10 independently reads the guest's
write before freeing the allocation; no expected heap payload is written by C#.

Actual WPF acceptance ran **Lasers alone**, with the report confirming
`Module = Lasers` and no switch. It presented 30 images, checked exact RGB
readback, stopped/restarted, and closed during active playback. CLOSE and WEP
returned successfully with SP `1000`, caller DS `0048`, zero global locks,
zero pens and zero local allocations/locks. Its captured LASERS window was
visually inspected. The acceptance suite also exercises module switching;
reports and captures stay in ignored `artifacts/wpf-smoke/lasers-*` folders.

These are original-code execution and bounded deterministic/runtime proofs.
They do not establish Windows 3.1 pixel fidelity, historical palette/timing
equivalence, every control combination, arbitrary small dimensions, compaction,
or indefinite endurance. The exposed mode remains three rays with the fixed
controls above. Other heap candidates remain separate future increments.

Reproduce the private tests without copying the module into test outputs:

```powershell
$env:AFTER_DARKER_LASERS = (Resolve-Path ad/Lasers.ad).Path
dotnet test -p:TestLocalLasers=true --filter TestCategory=LocalLasers --report-trx
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke ad/Lasers.ad artifacts/wpf-smoke/lasers-only
```
