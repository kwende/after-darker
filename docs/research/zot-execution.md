# Zot!: fixed local blocks and images inside a DRAWFRAME call

Date: 2026-09-19. Implemented alongside [String Theory](string-theory-execution.md)
on `codex/string-theory-zot`, based on reviewed main `082ebcc`. These complete
row 3 of the [readiness sweep](module-readiness-sweep.md).

## Artifact, settings and clock

The supported local artifact has SHA-256:

```text
B1C71AC07BD0CB55BF520B5E2BD69127726206136FE0AE7BB85A4B04ABB04E76
```

**Artifact facts:** five NE segments, automatic data segment 5, startup
`S4:0000`, MODULE `S1:003E`, 25 imported identities, 27 internal and 25 imported
relocation records, and 100 actual loader patches. The initial heap is 1,024 bytes.

The profile supplies controls `33, 0, 100, 0`: Few Forks, unused hidden
Kinkiness, Stormy, and an unused fourth field. The original code computes a
fork divisor of `(75 - 33) / 2 = 21`. Frequency 100 schedules the next strike
500..1999 milliseconds after its sampled clock. The generic WPF speed selector
is disabled. These are explicit host choices, not all settings or saved defaults.

The one added import identity is **USER #15 GetCurrentTime**, an alias of the
existing tick service: no arguments, DWORD in DX:AX. Wine 10.0's
[Win16 USER export declarations](https://raw.githubusercontent.com/wine-mirror/wine/wine-10.0/dlls/user.exe16/user.exe16.spec)
map both USER #13 GetTickCount and #15 GetCurrentTime to GetTickCount. This is
reference evidence for the alias and ABI, not a new dependency. Public tiny-guest
tests check both names, shared clock progression, unsigned wrap, return registers
and far-return stack cleanup.

**Observed runtime behavior:** DRAWFRAME waits until unsigned `now > nextStrike`;
equality still waits. Deterministic tests advance that clock explicitly. Live
playback uses the existing monotonic clock. The module requires no Windows
timer, worker thread or synchronization primitive on this path. Its original
unsigned comparison is not repaired by the host; multi-day clock rollover is
outside the bounded playback proof.

## The fixed-allocation path

Unlike the persistent movable histories in Lasers, Magic and String Theory,
each strike allocates **two fixed blocks**:

| Request | Payload | Typical handle and near offset |
| --- | --- | --- |
| LocalAlloc(0, 800) | 100 eight-byte main-bolt records | `03E8` |
| LocalAlloc(0, 1600) | 200 eight-byte fork records | `0708` |

For a fixed block, handle and near offset are the same value. LocalLock returns
that offset in AX and caller DS (`0028`) in DX; it creates no movable pin.
Original instructions compute the geometry and write the arrays directly.
Both blocks are unlocked/freed before DRAWFRAME returns, and the saved handles
are reset to zero. Repeated strikes reuse their coalesced free extent.

**Modern adaptation:** as with the other heap profiles, the automatic data
segment is mapped to 64 KiB and allocator growth is bounded within it. Static
storage ends at `03E6`; alignment consumes two bytes before the first block.
Capacity is 64,538 bytes and the post-cleanup aligned free extent is 64,536.
No allocator implementation changed for Zot!. See [the shared heap guide](../win16-local-heap.md).

## Why a working guest initially showed a black window

**Observed runtime behavior:** the entire visible event occurs inside one call:

```text
DRAWFRAME
  -> allocate bolt arrays and generate geometry
  -> draw white bolt and gray forks
  -> execute original CPU delay loop
  -> draw the same geometry black
  -> optionally flash again
  -> free both arrays
  -> return with a black surface
```

Capturing only the returned surface misses the lightning. More Win16 drawing
APIs would not solve that presentation problem.

[ZotProfile](../../src/AfterDarker.Runtime/Modules/ZotProfile.cs) identifies two
completed-image points in the hashed artifact: `S2:0771` after the white/gray
procedure's final DeleteObject and `S2:063F` after black erasure's DeleteObject.
These are **original import return addresses**, resolved by the NE load plan.
The module bytes are not patched to add calls.

[ImportFrameCapture](../../src/AfterDarker.Runtime/Presentation/ImportFrameCapture.cs)
matches DRAWFRAME, the returned CS:IP, and the GDI import identity after normal
gateway dispatch completes. It offers a copied image outside Unicorn's hook,
then execution resumes normally. It does not change the guest's registers,
stack, time or surface. The session rejects attempts to draw, shut down or
dispose reentrantly from that callback. At most eight checkpoints may be
configured and 32 visits may occur per DRAWFRAME, with one reusable RGB buffer.

**Modern adaptation:** the WPF worker publishes each changed checkpoint image
to the existing single-slot mailbox and holds it for **80 ms** before resuming
the guest. The UI dispatcher stays free. This makes transient images visible
but does not claim original-machine timing. Original CPU delay loops still
execute. The configured hold is bounded to 100 ms; no image queue grows, and a
severely stalled UI can still miss a transient image. Cancellation wakes the
hold and suppresses further intermediate publication, lets the current bounded
guest call finish, and then runs CLOSE/WEP.

The mechanism is reusable. The [Rainstorm follow-up](rainstorm-execution.md)
now uses it for the first of its paired InvertRect calls, with its own verified
return address and the same explicit 80-ms live hold.

## Execution bounds and static anchors

Zot! allows **2,000,000 instructions**, **4,096 service exits**, and **3-second
native slices** per invocation, retaining the runtime's five-second cumulative
native execution budget. Other profiles retain their own smaller limits.
Managed image holds have their separate 32-visits x 100-ms maximum. The original
busy waits and fork drawing exceeded the earlier experimental instruction and
service limits; those were bounded failures, not missing kernel services.

Full local disassembly remains under ignored `artifacts/zot/`.

| Location | Mechanism |
| --- | --- |
| S1:0017 / S1:00BD | Instance at data `03E0`, compatibility at `001E` |
| S2:0012..0062 | Controls and first strike deadline |
| S2:016A..01A0 | Strict clock gate and count resets |
| S2:01BC..0204 | Fixed allocation/locking; handles at `03D2` and `03D4` |
| S2:0247..04B3 | Random geometry and array writes |
| S2:04BC..04CA | CPU delay request, followed by black erasure |
| S2:04D8..0512 | Optional additional flashes and delays |
| S2:0513..053C | Unlock/free both arrays and clear handles |
| S2:0549..0647 | Black erasure; checkpoint at `063F` |
| S2:0648..0779 | White/gray drawing; checkpoint at `0771` |
| S2:077A..07B3 | CPU-counted delay loop |
| S2:07B4..082B | Select next strike deadline |

## Verification and limits

Nine private cases cover 30 forced strikes; exact deadline boundaries; ABI
traces of fixed allocation/locking; independent guests and intermediate image
hashes; 1x1, 2048x2048 and 321x239 dimensions with different seeds; empty
playback; artifact rejection; callback reentrancy; and cancellation while a
lit image is actually published. The cancellation test joins the worker and
verifies original cleanup rather than abandoning a running task.

The 30-strike run executes **12,692,853 instructions**, with a measured peak of
**810,461 instructions / 1,352 imports** in one strike. It produces **45 visible
and 45 erased checkpoint images**, makes 60 allocations and 60 frees, and
returns a black surface with no allocations or pens after every strike.
These are measured results for a fixed seed, not maximum-work guarantees.

Actual WPF acceptance passes Zot! alone and switching both ways with String
Theory. It verifies exact RGB readback of a visible lightning image, Stop/restart
and close while playing. The captured window was visually inspected. Reports
remain under ignored `artifacts/wpf-smoke/zot-*` and `string-theory-to-zot`.
Shutdown restores SP `1000` and caller DS `0048`, returns WEP=1 and leaves no
allocations, locks or owned pens.

This proves execution and visible original lightning at the chosen controls.
It does not establish historical visual/pacing fidelity, other settings, other
revisions, useful visuals at 1x1, or indefinite endurance.

```powershell
$env:AFTER_DARKER_ZOT = (Resolve-Path 'ad/Zot!.ad').Path
dotnet test -p:TestLocalZot=true --filter TestCategory=LocalZot --report-trx
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- 'ad/Zot!.ad'
```

The reusable idea: **a DRAWFRAME call is a procedure, not necessarily one image.
An animation can draw and erase its entire visible event before returning.**
