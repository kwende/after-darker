# From an NE resource to pixels

Nocturnes now supplies a complete example of resource loading. Its original
Win16 code calls `LoadBitmap(instance, "eyes")`, selects the returned handle
into a memory DC, and copies portions of that bitmap to the display.
The animation and choice of sprite rectangle remain guest code.

```text
guest LoadBitmap(instance, "eyes")
    -> checked guest string
    -> RT_NAMETABLE: EYES maps to type 2, ID 1
    -> NE resource directory: file offset + length
    -> DIB header + color table + packed pixels
    -> decoded RGB image + monochrome-source metadata
    -> owned guest HBITMAP
    -> SelectObject(memoryDC, bitmap)
    -> BitBlt uses the bitmap's pixels
    -> DeleteObject after deselection / DeleteDC
```

## Walk it in the debugger

Select **Tutorial 11 - NE bitmap resources** in Visual Studio and press F5.
It prompts for a local module, prints aliases and image metadata, writes PNGs
under ignored `artifacts/resource-bitmaps/`, and exits without constructing a CPU.
The equivalent command from the repository root is:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 11 "ad/Nocturnes.ad" artifacts/nocturnes-resources
```

Start at `Tutorial11BitmapResources.Run`, then step into
`NeResourceCatalog.Find` and `DibBitmapDecoder.Decode`. The returned
`ResourceBitmap` and `DecodedBitmap` records are actual typed data, not just
console reports. The production `Win16Api.Resources.LoadBitmap` uses these same
Core implementations and adds instance validation, guest-pointer translation,
and GDI ownership.

## Two directories, then an image

The existing NE parser already reads resource entries. They identify a type,
an ID/name, an aligned file offset, a stored length, and flags. These are file
ranges, not executable segments or guest far pointers. Resource loading does
not relocate these bytes or allocate them from the module's Win16 local heap.

Nocturnes' directory has **BITMAP #1**, while the call asks for **EYES**. Its
separate resource of type 15 (`RT_NAMETABLE`) supplies the translation. Each
record has this layout:

```text
+0 WORD  record length, including this word; zero ends the table
+2 WORD  resource type, with high bit indicating a textual alias
+4 WORD  resource ID, with high bit indicating a textual alias
+6 BYTE[] zero-terminated type name
          zero-terminated resource name
          optional padding up to the record length
```

The low 15 bits identify the numeric directory entry. Notice that the high-bit
meaning here differs from the ordinary NE resource directory. The loader
checks every record and string against its containing payload. It also
supports directly named directory entries and case-insensitive lookup.

For the supported Nocturnes artifact, the first alias record is 12 bytes long:
type `0x0002`, ID `0x8001`, empty type string, `EYES`, then a zero byte.
This is artifact metadata; no original resource bytes are included in tests.

BITMAP #1 is at file offset `0x21C0`, with 1,328 stored bytes:

| Payload offset | Bytes | Meaning |
| --- | ---: | --- |
| `0x00` | 40 | BITMAPINFOHEADER: width 102, height 80, planes 1, bit depth 1, BI_RGB |
| `0x28` | 8 | Two B,G,R,reserved palette entries: black and white |
| `0x30` | 1,280 | 80 rows of 16 bytes each, bottom row first |

There is **no 14-byte BMP file header** in this resource. Row storage is
`((width * bitsPerPixel + 31) / 32) * 4` bytes. For 102 one-bit pixels, that is
16 bytes: the first 102 bits describe pixels and the rest pad the row.
Bits are read most-significant first. The decoder reverses bottom-up rows,
expands palette indices, and produces 102 × 80 × 3 = **24,480 RGB bytes**.

One-bit images are essential masks, even in the color-only playback policy.
For canonical black/white one-bit resources we retain monochrome-source
metadata: a source zero expands to the destination DC's text color and a source
one to its background color. Nocturnes sets those colors before its mask blits.
`SetBkMode(TRANSPARENT)` does not make a bitmap copy transparent; the ROP3
operation determines how source, brush, and destination combine.

## Existing open-source work

This problem has been solved before, and that work helps directly:

- **Wine** implements Win16 resource lookup. Its
  [`NE_FindNameTableId` / `FindResource16`](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/resource.c)
  establish the legacy alias-table mechanism; its
  [`LoadBitmap16`](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.c)
  connects the Win16 API to image loading.
- **[WineVDM / OTVDM](https://github.com/otya128/winevdm)** applies Wine's Win16
  machinery to running old Windows programs on modern Windows. It is useful
  prior art and a potential independent behavior oracle.
- **[Kaitai Struct's BMP schema](https://github.com/kaitai-io/kaitai_struct_formats/blob/master/image/bmp.ksy)**
  is a declarative format reference and a possible parser-generation starting
  point. It is not an NE loader or a guest GDI-handle implementation.

Our decoder is a small original C# implementation of the documented DIB layout;
we have not vendored any of these projects or added a dependency. Wine's cited
resource implementation carries LGPL-2.1-or-later terms; WineVDM's repository
contains its own license notices. Any future code reuse needs a file-specific
license review. Microsoft's [bitmap structures](https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-structures)
and [bitmap header types](https://learn.microsoft.com/en-us/windows/win32/gdi/bitmap-header-types)
are the format reference for the decoder.

The reusable part is directory lookup and image decoding. The integration still
needs our checked guest pointers, instance identities, HBITMAP ownership, DC
selection, and raster operations. The remaining proprietary `AD_RSRC` helper
imports are a separate API contract; standard DIB support alone does not
implement that helper DLL.

## Deliberate boundary

Supported: 12-byte CORE headers (1/4/8/24 bits), 40-byte INFO headers
(1/4/8/24/32 bits), uncompressed BI_RGB, indexed color tables, DWORD row padding,
and bottom-up or INFO top-down images. Dimensions are bounded to 1..2048.
The fourth BI_RGB byte is ignored, not treated as alpha.

Compressed RLE, bitfields, other DIB headers, icons, cursors and arbitrary
custom resources remain unsupported. Loaded monochrome resources are blit
sources; drawing back into them is explicitly guarded. A new memory DC's
default one-pixel bitmap supports only the black/white Rectangle operation
observed in Punch Out before it selects its owned color bitmap.

Public tests generate their own images and alias table, check malformed bounds,
orientation, padding, palettes and channel order, and execute real synthetic
far calls through LoadBitmap. Original images and captures remain local.
