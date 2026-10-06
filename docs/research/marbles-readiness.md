# Marbles: current coverage and effort assessment

Investigated October 5, 2026, against the current production loader and import
registry. This is planning evidence, not new module support.

## Exact input and fresh probe

The selected `AD20/MARBLES.AD` has SHA-256
`F49847FEEF36A2BE22215C36F76BD82A2152B30794D841914A78A2CC6C22B389`.
It has five NE segments, a 1,024-byte initial local-heap request, MODULE at
S4:173D and WEP at S1:0087. The existing opt-in ModuleReadinessProbe was rebuilt
and run for this file only. It still stops during loading at S1:0154. No guest
initialization or drawing was executed.

The private detailed report is under ignored
`artifacts/marbles-assessment-2026-10-05/`. Its import coverage comes from actual
`Win16Imports.BindImports` results, including guarded functions and named audio
imports, rather than the older regex census.

| Imported library | Implemented identities | Unimplemented identities |
| --- | ---: | ---: |
| KERNEL | 4 | 10 |
| USER | 3 | 3 |
| GDI | 11 | 4 |
| AD_SND | 7 | 0 |
| AD_RSRC | 0 | 49 |
| WIN87EM | 0 | 1 |
| Total | 25 | 67 |

These are distinct imports across the entire file, not observed calls, required
visual-path functions, or a percentage of implementation effort. An existing
implementation may also reject an untested argument or raster operation.

## What is reusable now

The CPU/gateway/session foundation, supported NE loading, presentation, awaited
shutdown and Ocuvera selection are already available. Existing drawing includes
Ellipse, BitBlt, compatible bitmaps/DCs, object selection/deletion, solid brushes,
FillRect and color/background state. The seven imported sound functions already
have the explicit unavailable-audio behavior used by other silent profiles.

Current local heaps and GlobalLock/GlobalUnlock for registered global blocks are
useful groundwork. They do not implement a general GlobalAlloc/GlobalReAlloc/
GlobalFree allocator.

## Gaps and important qualifications

1. **Additive selector relocations: small, bounded loader work.** There are four
   internal additive selector records, at S1:0154, 0164, 018A and 0253. Their stored
   words are all zero. Wine's `NE_FixupSegment` explicitly recognizes this
   zero-addend selector pattern. This is much narrower than supporting every
   additive relocation kind; it still needs deterministic tests and validation.
2. **Floating point: a new conformance boundary.** There are 964 OS-fixup records,
   across types 2, 3, 4, 5 and 6, plus WIN87EM ordinal 1 (`_fpMath`). Spot-inspected
   sites contain x87/wait instruction patterns. Wine ignores these emulator
   fixups and retains direct floating-point instructions. That is a promising
   experiment for Unicorn, not proof that we can simply ignore every record.
   `_fpMath` uses register-selected operations; the current Pascal stack gateway
   does not supply that contract. Test FPU state/control and the operations the
   guest actually reaches before implementing a broader floating-point runtime.
3. **Global allocation and environment services.** Unimplemented KERNEL imports
   include GlobalAlloc, GlobalReAlloc, GlobalFree, GlobalHandle, GetWinFlags,
   LoadLibrary, GetModuleFileName, FreeResource, UnlockSegment and lstrcpy.
   Reachability is unresolved. A real global allocator needs checked selector/
   handle ownership and resizing semantics; allocating another host array alone
   does not fulfill that contract.
4. **AD_RSRC helper behavior is the largest uncertainty.** All 49 referenced
   ordinals remain unbound. We do have the private helper, SHA-256
   `901C15E7FA5E5D9F482806E9C535B3864DB56391CF42B74720AA0F859B60C1C8`:
   30,736 bytes, three segments, and 49 imports of its own. Its exports do not
   supply names for these ordinals. Small static samples show that not every
   export is a large subsystem: ordinal 97 is a short structure-field accessor;
   ordinal 79 directly references familiar DC/stock-object functions. Other
   samples call internal helper routines. Some returns use caller-cleaned far
   calls, so host replacements need explicit calling-convention evidence.
   Executing the original helper is an alternative to modeling reached exports,
   but requires multi-module loading, per-DLL state/initialization and additional
   imports. Neither approach is already implemented.
5. **Display queries/palettes and module profile.** GetDeviceCaps, GetObject,
   SelectPalette and RealizePalette are missing. The color policy can avoid
   optional grayscale, but it does not justify inventing arbitrary palette or
   object handles. TextOut, SetTextAlign and MessageBox are also imported; do not
   implement them just from their presence, or assume they are all unused.
   Fixed controls, startup/BLANK behavior, resources, drawing and cleanup still
   need runtime evidence and a registered profile.

The local helper's broader imports include palette operations and resource/
global-memory functions. Loading it wholesale therefore does not erase the
compatibility work. Conversely, counting 49 helper exports as 49 equally large
new implementations would overstate it.

The Wine reference inspected was revision
`4f4d68f6a784db76e66cf0f033f90b8288bba11e`: `dlls/krnl386.exe16/ne_segment.c`
(`NE_FixupSegment`, OS-fixup and additive-selector branches), and
`dlls/win87em.dll16/win87em.c` (`__fpMath`). These are behavioral research
references; no Wine implementation was copied and no runtime policy was changed.

## Planning assessment

Marbles is a medium-to-large next target, not another cheap profile registration.
The largest new work is global memory, floating-point compatibility and the
helper contract, rather than basic ellipse/blit rendering or WPF integration.
Allow roughly **10–25 hours of focused implementation and verification**, with
low confidence and possible overrun if the reached helper/resource paths are
broad. This is an engineering judgment, not a measured schedule or guarantee.

The most useful first checkpoint is a **2–4 hour bounded investigation/implementation
budget** for tiny FPU/register-ABI tests and the narrow loader cases, then a fresh
Marbles initialization trace. Stop and reassess at the first real helper calls.
That trace can shrink or expand the larger estimate without committing to a
general AD_RSRC port. Wrap Around remains a possible smaller FPU proving ground;
it is not a prerequisite to selecting Marbles.

Shared work here could help Aquatic Realm, Clocks and Globe, which also reference
AD_RSRC, and other floating-point modules. That is potential reuse, not automatic
compatibility for those screensavers. No support status changed in this assessment.
