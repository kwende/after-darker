# Recovered Windows After Dark SDK

Retrieved 2026-09-19 from developer James V. Signorile's
[programming tools page](https://jamessignorile.com/program.html).
This is the Windows SDK, not the separate Macintosh SDK on the same page.

## Sources and reproduction

Download and extract locally under ignored `artifacts/reference/after-dark-sdk/`:

| Archive | Contents verified | SHA-256 |
| --- | --- | --- |
| [ad3sdk.zip](https://jamessignorile.com/zips/ad3sdk.zip) | Windows 3.0 SDK: MODULE.H, MODULE.CPP, PROGRAMM.TXT, CONTROLS.TXT, RES_IDS.H, BLANKER/KALEID/SAMPLE sources | `689C979BA11125E227CD3950D0C4584115F3E461460571BF9C39A5B5077B05DA` |
| [proman.zip](https://jamessignorile.com/zips/proman.zip) | Windows 2.0 programmer manual, plain text and RTF | `28BF7305867FC00FCF7C6F089001C0489A3058D6B6E90B69F817AC516BE8A93F` |
| [visual.zip](https://jamessignorile.com/zips/visual.zip) | 16-bit Visual C++ example, including another MODULE.H | `B5EAD284C81538315CC45058EF0B0375DDD0B28E620DC3733013C2A18E8A8341` |

The SDK README identifies Berkeley Systems, author Bob Bickford, revision 1.3,
Windows 3.1, and Visual C/C++ 1.00 or Borland C++ 3.1. Archive identity is
recorded for reproducibility, not a cryptographic claim of publisher provenance.
No downloaded executables were run. Keep these reference archives and extracted
files out of Git; availability on a historical website is not a redistribution
license. An additional `adbcpp.zip` downloaded successfully but uses compression
unsupported by PowerShell extraction; it is not needed for these findings.

## Shared record layout

**Artifact fact:** MODULE.H defines common AD_SYSTEM and AD_MODULE structures.
The Windows 2.0 manual independently documents the same fields. AD_MODULE
contains four control values and four control resource IDs. It has no generic
LPVOID field for an arbitrary module settings structure. Its palette pointer
has the specific LPLOGPALETTE type and purpose.

**Derived layout:** Using Win16 two-byte int/BOOL/handles, four-byte POINT and
four-byte far pointers gives this AD_MODULE mapping:

| Byte offset | SDK field | Role |
| --- | --- | --- |
| 0x00 | hDrawRgn | Drawing region handle |
| 0x02 | ptRgnSize | Width and height, two signed words |
| 0x06 | iControlValue[4] | Four signed words; interpretation belongs to module |
| 0x0E | iControlID[4] | Four control resource IDs |
| 0x16 | hModule | Module DLL handle |
| 0x18 | hPalette | Palette handle |
| 0x1A | lpLogPalette | Far pointer to palette description |
| 0x1E | bWantSnd | Sound request |
| 0x20 | iModRunner | Direct runner identity |

This yields 0x22 bytes for the documented AD_MODULE. It agrees with the
width/height, control reads and MODULESELECTED writes already observed in our
two original modules. Current 10-/12-byte module records are only sufficient
for the executed paths; they are not complete SDK records.

AD_SYSTEM's documented layout is 0x2C bytes. Observed offsets now have names:
0x0A is iBitsPerPixel, 0x14 is iADVersion, 0x20 is hModuleInfo, and 0x28 is
iRunner. The last documented member is bPalAvail at 0x2A.

Hard Rain now also exercises `ptAspect`: signed X/Y words at 0x0C and 0x0E.
Its drawing code divides by the X word while correcting a ring's horizontal
radius. The profile supplies 1:1 for the software surface's square pixels;
zero-filled values cause a guest divide-by-zero. See [the execution evidence](hard-rain-execution.md).

The bitmap-family profiles also supply `ptScreenSize.x/y` at 0x06/0x08.
The shared single-surface host gives AD_SYSTEM and AD_MODULE matching dimensions.
Punch Out uses the system dimensions when allocating its desktop backing bitmap;
zero-filled system dimensions would not describe the host's actual pixels.

**Unresolved:** our current host also supplies compatibility words at system
offsets 0x2C and 0x34. Those are beyond this public structure. Do not assign
them SDK field names, shrink the existing allocation, or claim the header
fully explains those binary checks without further investigation.

## Settings and lifecycle implications

The 2.0 manual explains that control values represent number sliders, text
sliders, combo boxes, and checkboxes. MODULESELECTED can change the four
resource IDs to choose controls appropriate to the current configuration.
The structure is shared; the meaning of a given control slot is module-specific.

CONTROLS.TXT documents the resource byte layouts, including 32-byte headers,
single-byte packing, control types, defaults, and slider selection behavior.
This supports a generic resource reader and default-value initialization;
it does not by itself reveal every module's interpretation of each value.

MODULE.H and MODULE.CPP also provide lifecycle message values, return codes,
palette requests and sample dispatch behavior. The SDK sample modules provide
source-level references for testing our host contract. No new runtime behavior
or compatibility claim results from finding these documents alone.

Next useful work: implement complete typed guest record serializers, reconcile
the extra system words, and parse the controls of both supported modules using
the documented resource layout. Keep Win16 serialization explicit rather than
using host-native C# layout or pointer sizes.
