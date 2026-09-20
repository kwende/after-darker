# Rainstorm: original-code playback

Verified on 2026-09-19. Supported artifact SHA-256:

`41611FED9E314B2F4F1C0653D1A5B948E41E70E464293D2F6489691E4C8DF5C7`

The WPF player now accepts this Rainstorm revision through **File > Load AD
file…**. Original x86 code initializes the drops, decides their positions and
colors, draws them, and shuts down. No rain animation was recreated in C#.

## The small Windows additions

- `GetStockObject(BLACK_PEN)` returns the existing stock black-pen handle.
  The device context already used this pen by default; it is host-owned and
  never consumes a slot in the created-pen pool.
- `USER!PtInRect` (ordinal 76) reads a checked guest RECT and tests a signed
  by-value POINT. Left/top are included, right/bottom excluded; inverted/empty
  rectangles contain no point. The Win16 declaration is in the pinned Watcom
  `h/win/win16.h`; Wine 10.0's `user.exe16.spec` gives `ptr long` and a 16-bit
  return. Microsoft's [PtInRect documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-ptinrect)
  supports the geometry contract, not the Win16 ABI.

The reusable behavior remains in
[Win16Api](../../src/AfterDarker.Core/Win16/Win16Api.cs). The dispatcher decodes
the rectangle pointer and [Point16](../../src/AfterDarker.Core/Win16/Point16.cs)
value; the existing gateway handles AX and Pascal stack cleanup. No new
allocator, loader mode, callback, dependency or renderer was required.

POINT is the first supported by-value coordinate structure. Its memory layout
is `X, Y`, but the decoded Pascal push sequence contains `Y, X`.
[Win16ArgumentReader.ReadPoint](../../src/AfterDarker.Core/Win16/Win16ArgumentReader.cs)
names this distinction. Native conformance tests use asymmetric negative
coordinates to catch swaps, check the guest's own result store, preserve DX
and caller registers, and verify removal of all 12 frame/argument bytes.
Bad rectangle pointers stop before returning to guest code.

## Host settings and observed guest state

[RainstormProfile](../../src/AfterDarker.Runtime/Modules/RainstormProfile.cs)
owns this artifact's record construction and checks. It uses a full 34-byte
SDK AD_MODULE allocation, the shared system record with the observed
compatibility tail, and a 24-bit drawing surface. Unused region, palette and
sound fields are zero. Early tutorial profiles keep their existing layouts.

The four fixed controls are **strength 60, lightning 50, drops 52, wind 40**.
These are validated host choices from the sweep, not reconstructed original
numeric slider defaults. The resource labels describe strength 60 as Downpour
and wind 40 as Zephyr. Rainstorm has no speed slider; `PlaybackOptions.Speed`
is ignored and the WPF selector is disabled for it. Host cadence is unchanged;
actual throughput depends on guest work. Alternate controls and settings dialogs
are outside this milestone.

| Data offset | Observed meaning | Code anchor |
| --- | --- | --- |
| `0010` | Compatibility flag | S1:0095–00BD validates system version/tail |
| `03B2` | Saved DLL instance | S1:0014–0017 copies startup's instance word |
| `0398` | Drop recycling RECT | S2:003B–0055 builds `(-25,-25,width+5,height+5)` |
| `03A6` | Drop count | S2:0034–0038 copies control 3 |
| `03B0` | Strength | S2:0062–006A copies control 1 |
| `03B4` | Signed wind factor | S2:006D–00F3 maps control 4 and chooses its sign |
| `03AC` | Lightning countdown | S2:0020–0031 computes `425 - 4 * lightning` |
| `03A8`, `09F6` | System/module far pointers | S1:0058–0080 stores GlobalLock replies |

[RainstormState](../../src/AfterDarker.Runtime/Modules/RainstormState.cs) gives
these fields names. The profile validates them after original initialization;
it does not manufacture the observations. Dimensions 1x1 and 2048x2048 were
exercised in addition to 320x240 and the player's 640x480.

## The novel presentation boundary: lightning within a frame

**Artifact fact:** S2:02B2–030A decrements the lightning countdown and, when
due, calls `InvertRect` twice in succession over the drawing rectangle, then
resets the countdown. With lightning 50 the first pair occurs on draw 226.

**Observed runtime behavior:** the 300-draw test executes both inversions,
15,600 point tests and 5,255,349 instructions before shutdown. It peaks at one
created pen, retains bounded diagnostics, and returns from CLOSE/WEP with no
outstanding locks or created pens. Inversion restores pixels when applied twice.

**Modern presentation behavior:** WPF publishes pixels after a whole DRAWFRAME
returns. The intermediate inverted image is therefore not presented as a
lightning flash. Rain motion is visible; faithful lightning timing is not
implemented. Behavior on period hardware has not been measured, so we do not
assert the original flash duration.

Correct API effects and correct final pixels can still omit an intermediate
visual effect. Preserving it would require an explicit presentation/timing
policy inside a guest call, with bounded queuing and cancellation, rather than
changing `InvertRect`. No arbitrary flash duration or fabricated animation was
added in this milestone.

## Verification and limits

- Nineteen new public cases cover geometry, stock-pen identity/lifetime,
  by-value POINT marshaling, return registers, invalid pointers and stack cleanup.
- Five opt-in Rainstorm cases cover 300 draws including lightning, independent
  deterministic guests, the irrelevant speed option, dimension extremes,
  cleanup and rejection of a modified artifact.
- All **261 cases** passed with Watcom, Mondrian, Spiral Gyra and Rainstorm
  enabled. The **203 public cases** require no private module.
- Actual WPF acceptance presented 30 frames, verified exact RGB readback,
  stopped/restarted, rejected unsupported input while playing, switched in
  both directions with Spiral Gyra, and closed during playback. Both directions
  returned CLOSE/WEP with zero locks/pens. The captured rain image was inspected.
  The native file-picker interaction itself remains a manual check.

Each Rainstorm invocation allows at most 200,000 instructions and 1,024 managed
exits, with the existing per-invocation time bounds. A draw processes 52 drops
and can issue hundreds of geometry, pen and line calls. Other profiles' budgets
are unchanged.

```powershell
$env:AFTER_DARKER_RAINSTORM = (Resolve-Path ad/Rainstorm.ad).Path
dotnet test -p:TestLocalRainstorm=true --filter "TestCategory=LocalRainstorm"
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke ad/Rainstorm.ad artifacts/wpf-smoke/rainstorm-to-spiral "ad/Spiral Gyra.ad"
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke "ad/Spiral Gyra.ad" artifacts/wpf-smoke/spiral-to-rainstorm ad/Rainstorm.ad
```

Original modules and local captures remain ignored. This is bounded execution
evidence, not historical pixel/timing fidelity, all-settings coverage, another
Rainstorm revision, or indefinite endurance. The Rainstorm playback increment
is complete with the lightning limitation recorded; the next module has not
been started.
