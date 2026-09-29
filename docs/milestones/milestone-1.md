# Milestone 1: selected original After Dark screensavers

Approved by the owner on **2026-09-27**. This is the current v1 scope and takes
priority over the older whole-collection candidate lists. Additional modules
are optional after this milestone, at the owner's discretion. This is a scope
decision, not a commitment to support every After Dark release through 1999.

The deliverable is these **26 original screensavers running in After Darker and
integrated into the Ocuvera Toasters collection**, alongside its existing scenes.
Finish the chosen modules first, then do the consumer integration. Keep that
integration requirement in view while evolving the runtime.

## Current position

As of the September 28 Spheres production verification and refreshed registry audit:

- **14 supported** by the production After Darker runtime and WPF player, for
  their exact registered artifacts and documented profiles.
- **11 identified but unimplemented.** Their last recorded runtime probes stop
  in the loader; fixing those first barriers does not establish the rest of the
  work required.
- **1 identity/execution investigation:** Starry Night. The original host plays
  it according to the owner, but no standalone module was found in our collection.
- **Ocuvera integration remains pending for all 26.** Production-player support
  is not milestone completion.

The repository has **18 supported modules in total**. Can of Worms, Punch Out,
Puzzle and Zot! are already supported but are outside this selected list. Keep
their support; they do not add new Milestone 1 requirements.

## Approved list and identity audit

Paths in this table are relative to the private extracted `AFTERDRK` collection.
The [machine-readable manifest](milestone-1.json) records full SHA-256 identities,
duplicate local paths, original requested names, aliases, and dated evidence.
It preserves the owner's order; order is not a mandated implementation sequence.

| # | Milestone name | Identified artifact | Current runtime state |
| --- | --- | --- | --- |
| 1 | Starry Night | No standalone `.AD`; name found in `ADW30.EXE` and `ADTASK.DLL` | Identity/execution investigation; original-host playback only |
| 2 | Rose | `AD30/ROSE.AD` | Unimplemented; prior loader barrier: OS fixup |
| 3 | Spheres | `AD30/SPHERES.AD` | **Supported**: shaded color spheres, clearing after 100 spheres |
| 4 | Aquatic Realm | `AD20/AQUA.AD` | Unimplemented; prior loader barrier: additive relocation |
| 5 | Clocks | `AD20/CLOCKS.AD`; `AD30/CLOX3.AD` is an alternative version | Unimplemented; prior loader barrier: additive relocation |
| 6 | Down the Drain | `AD20/DRAINO.AD` | Unimplemented; prior loader barrier: OS fixup |
| 7 | Fade Away | `AD20/FADE.AD` | **Supported**: Radar effect over a white image |
| 8 | GeoBounce | `AD20/3DBOUNCE.AD` | **Supported**: shaded tetrahedron profile |
| 9 | Globe | `AD20/GLOBE.AD` | Unimplemented; prior loader barrier: additive relocation |
| 10 | Gravity | `AD20/GRAV.AD` | **Supported**: four colored balls, silent |
| 11 | Hard Rain | `AD20/RAIN.AD` | **Supported**: five-drop profile |
| 12 | Lasers | `AD20/LASER.AD` | **Supported**: three-ray profile |
| 13 | Magic | `AD20/MAGIC.AD` | **Supported**: 100-line trail, horizontal mirroring |
| 14 | Marbles | `AD20/MARBLES.AD` | Unimplemented; prior loader barrier: additive relocation |
| 15 | Mondrian | `AD20/MONDRIAN.AD` | **Supported** |
| 16 | Mountains | `AD20/MOUNTAIN.AD` | Unimplemented; prior loader barrier: OS fixup |
| 17 | Nocturnes | `AD20/NOCTURNE.AD` | **Supported**: colored eye sprites |
| 18 | Penrose | `AD20/PENROSE.AD` | Unimplemented; prior loader barrier: OS fixup |
| 19 | Rainstorm | `AD20/RAINSTOR.AD` | **Supported**, including intermediate lightning presentation |
| 20 | Shapes | `AD20/SHAPES.AD` | **Supported**: color profile with direct-RGB adaptation |
| 21 | Spiral Gyra | `AD20/SGYRA.AD` | **Supported** |
| 22 | Stained Glass | `AD20/STAINED.AD` | **Supported**: fixed color controls |
| 23 | String Theory | `AD20/STRING.AD` | **Supported**: three groups of 100 strings |
| 24 | Vertigo | `AD20/VERTIGO.AD` | Unimplemented; prior loader barrier: OS fixup |
| 25 | Wrap Around | `AD20/WRAP.AD` | Unimplemented; prior loader barrier: OS fixup |
| 26 | Flying Toasters | `AD30/TOASTER3.AD`, internally **Flying Toasters Pro** | Unimplemented; prior loader barrier: additive relocation |

“Prior loader barrier” refers to the September 20 execution audit of the same
hash, not a newly executed probe or an exhaustive missing-feature list. The
manifest retains the original diagnostic text. Existing support is grounded in
the current [production registry](../../src/AfterDarker.Runtime/SupportedModules.cs)
and earlier execution/test records, not filename matching.

## Naming decisions and version boundaries

- **Mondrias → Mondrian.** Both the embedded title and registered profile say
  Mondrian. The requested spelling is retained as an alias in the manifest.
- **Spyral Glyra → Spiral Gyra.** `SGYRA.AD` contains the title Spiral Gyra,
  matching our existing profile. No new module is implied by the spelling.
- **Geo Bounce → GeoBounce.** This is the existing `3DBOUNCE.AD` profile.
- **Flying Toasters:** our identified artifact is **Flying Toasters Pro**,
  resident module `TOASTPRO`. Target that available version for this family;
  do not claim to have identified or supported a separate classic version.
- **Clocks:** both Clocks and Clocks 3.0 are present as different hashes. The
  older `CLOCKS.AD` is the starting target, not a requirement to support both.
- **Aquatic Realm:** `AQUA.AD` matches the original local
  `Aquatic Realm.ad.dll` byte for byte. `FISH3.AD` identifies itself as Fish Pro
  and is not silently substituted for Aquatic Realm.
- **Wrap Around is not Warp.** The successful `WARP.AD` diagnostic does not
  count toward `WRAP.AD`. Likewise, `TOILET.AD` is Flying Toilets, not Down the
  Drain, and Hard Rain and Rainstorm remain separate selections.
- **Starry Night:** the host and `ADTASK.DLL` contain its name and the owner has
  seen it run through WineVDM. This is consistent with a built-in host effect,
  but strings alone do not locate its executable implementation. Determine
  where the original code lives and how to invoke it in our runtime. Do not
  count a native recreation or whole-host WineVDM playback as completion.

One verified version per selection is sufficient, consistent with the owner's
acceptance of older or updated modules. Preserve distinct versions and record
any target change by hash; do not overwrite binaries or silently broaden the
milestone to every variant. The proposed Pro/older-Clocks choices are explicit
starting targets and can be changed without expanding the 26-name scope.

## Completion criteria

For each selected screensaver:

1. Identify the original artifact and any required helper/resource versions.
2. Execute original guest code through the shared runtime with a supported
   color-playback profile. Fixed options and documented host adaptations remain
   acceptable; optional audio, grayscale and complete original option dialogs
   are not requirements.
3. Verify initialization, representative animation, clean bounded shutdown and
   relevant memory/ABI/rendering behavior. Record visual review and any known
   fidelity limits. A loader pass or a few surviving draws is insufficient.
4. Register the supported identity and preserve the educational console and
   standalone WPF host as consumers of the same implementation.

Then complete the [Ocuvera integration acceptance work](../ocuvera-compatibility.md):
package consumption, stable original-module identities, user-supplied file
discovery, selection/rotation alongside native scenes, copied frame delivery,
bounded execution and awaited shutdown, multi-monitor behavior, and the published
Windows x64 `.scr`/native-dependency path. Integration needs its own evidence;
it does not follow automatically from WPF playback.

The white starting image remains the supported Fade Away input. Desktop capture
is a future enhancement, not a newly added blocker for this milestone.

## How this changes the next work

**Spheres is complete in the production runtime.** Its [execution record](../research/spheres-execution.md)
verifies 3,401 draws, the 100-sphere clear cycle, bounded brush ownership and WPF
lifecycle. No new Win16 implementation was required. Choose the next selected
module from the eleven known artifacts with loader barriers, or investigate
Starry Night's original code; the older candidate list does not expand v1 scope.

For the remaining choices, focus tracing and shared-service work on these names.
Use the [WineVDM tracing assessment](../research/winevdm-tracing-assessment.md)
when an unknown API contract affects cost or correctness. Do not make completion
of an exhaustive suite-wide trace or Wine port a prerequisite to progress.

## Audit evidence

September 27 verification read current private files, calculated SHA-256, checked
NE signatures/resident names, inspected relevant embedded titles, and compared
exact identities to `SupportedModules.Identify`. All **25 standalone primary
artifacts** match their September 20 audit hashes; the additional Clocks version
is preserved separately. At that audit, thirteen primary identities matched supported profiles and the
registry contained 17 supported identities. The September 28 refresh finds
**fourteen selected profiles and eighteen total**, after adding Spheres.

Earlier runtime evidence is linked from the
[65-module audit](../research/windows98-readiness-audit.md),
[current status](../current-status.md), and
[test guide](../testing.md). The original scope audit did not execute guests. The September 28 Spheres work
adds six local execution cases (359 tests including the public suite) and a
passing WPF playback/readback and lifecycle smoke. Ocuvera remains unverified.

Recheck local identities and registry status with:

```powershell
python tools/audit-milestone-1.py
```

Update this document and the JSON manifest together when a selected profile or
integration status advances.
