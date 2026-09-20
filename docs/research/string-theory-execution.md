# String Theory: three groups sharing one local allocation

Date: 2026-09-19. Branch `codex/string-theory-zot` began at reviewed main
`082ebcc` (Magic merged through PR #17). This increment completes String Theory
and Zot!, the remaining two modules in row 3 of the [readiness sweep](module-readiness-sweep.md).

## Artifact and chosen controls

The supported local artifact has SHA-256:

```text
76A6905FBA156A2B510EE645511B74EA7DECE650BAA62781941D4FE95B9B66B8
```

**Artifact facts:** five NE segments, automatic data segment 5, startup
`S4:0000`, MODULE `S1:004C`, 24 imported identities, 29 internal and 24 imported
relocation records, and 94 actual loader patches. The initial heap is 1,024 bytes.

The full 34-byte AD_MODULE record supplies these explicit host choices:

| Control | Value | Original code's interpretation |
| --- | ---: | --- |
| Groups | 2 | Three independent groups |
| Strings | 60 | A 100-entry history for each group |
| Color Speed | 96 | Threshold `(100 - 96) * 5 = 20`; change after 21 updates |
| Clear Screen First | 1 | Start with a black surface |

These are tested settings, not a reconstruction of saved user preferences.
WPF disables its generic speed selector for this profile. The system record
advertises 24-bit color; palette and sound fields are zero. Initialization
divides by `width - 2` and `height - 2`, so the profile rejects dimensions below
3 before entering the guest. The shared maximum remains 2048.

## Reused mechanism

**Observed runtime behavior:** String Theory needs no new Win16 implementation.
The same local heap, pen/line services, loader, gateway and lifecycle support
that run Lasers and Magic also run this module.

INITIALIZE requests `LocalAlloc(0042, 4560)`: movable, zero-initialized storage
for three groups of 152 ten-byte records. Our selected history uses 100 slots
per group; the original allocation still reserves 152. The guest locks the
allocation, saves its offset and unlocks it. On the tested host, handle `0002`
resolves to `0028:0464`. C# provides storage; guest instructions maintain its
contents and compute the animation.

Each DRAWFRAME erases one old line and draws one new colored line for each of
the three groups: six LineTo calls. The shared history index wraps at 100,
the motion counter wraps after 701 updates, and each group's color advances
after 21 updates through a 21-color cycle. Temporary pens are restored/deleted
before the call returns. The group color array in
[StringTheoryState](../../src/AfterDarker.Runtime/Modules/StringTheoryState.cs)
is a detached observation, not the state which drives the animation.

**Modern adaptation:** the profile uses the existing bounded heap-growth
policy. Static allocation ends at `0462`; the four-byte-aligned first allocation
begins at `0464`. Mapping the full data segment supplies 64,414 bytes of capacity,
of which 64,412 can form the aligned free extent after cleanup. Those two bytes
are alignment, not a leaked allocation. See [heap ownership](../win16-local-heap.md).

BLANK fills black and returns `10`, the SDK's HSV_PAL request. As with Magic,
the host accepts that request while consuming the module's COLORREFs directly;
indexed-palette behavior is not implemented. CLOSE clears the image, frees the
history and clears its saved handle. WEP returns 1.

The new [StandardModuleRecords](../../src/AfterDarker.Runtime/Modules/StandardModuleRecords.cs)
builder supplies the common record layout for String Theory and Zot!; their
profiles retain the module-specific control meanings and observations.

## Static anchors

These are NE segment:offset addresses for this exact artifact. Full disassembly
and local reports stay under ignored `artifacts/string-theory/`.

| Location | Mechanism |
| --- | --- |
| S1:001E / S1:00D0 | Stores instance at data `03EA` and compatibility at `0018` |
| S2:002D..0048 | Group count and color threshold from host controls |
| S2:004B..025C | Strings lookup, including control 60 -> 100 |
| S2:02B2..02CC | Allocates and locks 4,560 bytes; handle at `0012`, offset at `0014` |
| S2:033E..0452 | Initial endpoint placement and per-group state |
| S2:046A..04D8 | BLANK and HSV_PAL request |
| S2:04D9..06C7 | DRAWFRAME, history wrap, color and motion counters |
| S2:0721..0726 | CLOSE frees history and clears the saved handle |
| S2:0A1E..0A64 | Group color increment, wrapping 22 to 1 |

## Verification and proof boundary

Six private cases exercise the original module without copying it into build
outputs. The long run checks every returned history index, motion counter and
group color for **1,500 draws** at 640x480. Before CLOSE it executes
**2,208,250 instructions** and **9,000 LineTo calls**. RGB SHA-256:

```text
896ED1DA6258DFFDF90FE6346840D59508FAE3EF7FD12C10BC978D44A0923AF2
```

Other cases check independent deterministic guests through a ring wrap,
continued execution after another guest closes, 101 draws at 3x3, 2048x2048
and 321x239, empty playback and invalid dimensions/artifacts. Shutdown restores
the caller's stack and DS, with no allocations, locks or owned pens left.

Actual WPF acceptance passes standalone String Theory and switching both ways
with Zot!, including bitmap readback, Stop/restart and closing while playing.
The captured window was visually inspected. Local reports and images remain
under `artifacts/wpf-smoke/string-theory-*` and `zot-to-string-theory`.

This establishes original-code drawing, deterministic state/pixels and bounded
lifecycle behavior at these settings. Other controls, historical pixel/color
fidelity and indefinite endurance are not established. The approximately
60-calls/second host cadence is a modern presentation choice.

```powershell
$env:AFTER_DARKER_STRING_THEORY = (Resolve-Path 'ad/String Theory.ad').Path
dotnet test -p:TestLocalStringTheory=true --filter TestCategory=LocalStringTheory --report-trx
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- 'ad/String Theory.ad'
```

The reusable idea: **another animation can reuse the same allocation and GDI
contracts while keeping all of its geometry, history and color logic in x86.**
