# Additional collection extracted from Windows 98

Date: 2026-09-20. This is an extraction and static inventory record, not new
playback support. The [transfer guide](../virtualbox-file-transfer.md) describes
how to repeat the process without shared folders or clipboard integration.

## Source and extraction evidence

The owner identified guest `C:\AFTERDRK` as the installation directory.
VirtualBox inspection confirmed the `windows98` VM was powered off with no
snapshots before copying. Host-side 7-Zip 26.01 read the attached VDI, its MBR
partition table and FAT32 filesystem directly. It reported successful extraction
of 173 files from AFTERDRK, totaling 7,231,916 bytes.

The directory contains AD_SND.DLL. A read-only listing also located
`C:\WINDOWS\AD_RSRC.DLL` and `C:\WINDOWS\SYSTEM\WIN87EM.DLL`; these two files
were extracted separately, adding 43,536 bytes. The complete local collection
therefore contains **175 files totaling 7,275,452 bytes**.

The destination is ignored `ad/windows98-2026-09-20/`, preserving AFTERDRK and
WINDOWS directory structures. Existing root `ad/` inputs were not replaced.
The VM configuration, source disk and snapshot state were not changed by the
extraction. No original binary or resource is included in this document.

**Owner-supplied installation history:** both the v2 and v3 installers were run
in this guest, potentially replacing older files. The owner accepts either
version for playback but wants distinct versions preserved. Keep imports in
separate collection directories with original paths and full hashes; do not
overwrite or deduplicate by filename. The folder names below identify where
files were found, not verified pristine releases. The extraction represents the
guest's current disk contents, not its pre-installation history.

## Module census

| Guest subfolder | .AD files | Relationship to the existing collection |
| --- | ---: | --- |
| AFTERDRK/AD20 | 29 | All 29 match existing root ad/ modules by SHA-256, despite different filenames |
| AFTERDRK/AD30 | 29 | Additional module files with hashes absent from the existing collection |
| AFTERDRK/MAD | 7 | Additional module files with hashes absent from the existing collection |
| **Total** | **65** | **29 exact duplicates and 36 additional module files** |

All 65 `.AD` files have NE executable headers and pass the existing static
import inspector. No new guest execution was attempted. Filename identity,
import coverage and a readable NE header do not certify a lifecycle profile
or imply playback support.

The installation also includes an ADMODULE.SDK directory, associated resources,
and 22 DLLs. Two DLLs, ADICONEX and ADMENUEX, have PE headers and are rejected by
the NE-only import inspector as expected. The import census inspected 89
AD/DLL files in total: 87 NE successes and these two format rejections.

Local machine-readable records, both ignored by Git:

- `artifacts/windows98-import/file-inventory.json`: relative paths, byte sizes,
  SHA-256 hashes, executable signatures and matches to existing module hashes.
- `artifacts/windows98-import/ne-import-census.json`: the existing
  `tools/inspect-ne-imports.py` output, including import identities, relocation
  record counts, optional Wine ordinal references and explicit format errors.

## Recovered helper libraries

| Helper | SHA-256 | Direct imported libraries | Distinct import identities |
| --- | --- | --- | ---: |
| AD_RSRC.DLL | `901C15E7FA5E5D9F482806E9C535B3864DB56391CF42B74720AA0F859B60C1C8` | KERNEL, GDI, USER, WIN87EM | 49 |
| AD_SND.DLL | `0CC0E3E634AF958C5E95CE8A7C28B8217714EC20BE31B7FA4E8E9173B6D58801` | MMSYSTEM, KERNEL, USER | 62 |
| WIN87EM.DLL | `30D6B965D45EDDF306268C9526B01494DF4841B8B530BAB2654C379E1CF49DB2` | KERNEL | 3 |

These files were absent from the original input folder. Their recovery supplies
code and import tables to investigate the unresolved helper contracts; it does
not establish compatibility with every caller or justify executing them without
loader, ABI and lifecycle work. AD_RSRC itself references WIN87EM, so the
resource-helper and floating-point work overlap. Audio remains outside the
current playback scope.

## Readiness follow-up

The owner requested checking these additional modules for inexpensive next
targets before choosing from the original thirteen unsupported modules. The
[subsequent readiness audit](windows98-readiness-audit.md) now records current
loader/service probes for all 65 modules and two successful 600-draw diagnostic
continuations. That execution evidence is separate from this extraction record;
it does not automatically register new player profiles.
