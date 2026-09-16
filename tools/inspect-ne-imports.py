"""Read-only NE import census. Python 3 standard library; no guest execution.

Example: python tools/inspect-ne-imports.py ad --spec-dir artifacts/import-census
Wine .spec files are optional ordinal-name references, not runtime dependencies.
JSON goes to stdout; input paths are relative to the supplied directory.
This research inspector does not load segments or implement relocation chains.
"""

import argparse
import hashlib
import json
import re
import struct
from collections import Counter
from pathlib import Path


SPEC_MODULES = {
    "krnl386.exe16": "KERNEL", "user.exe16": "USER", "gdi.exe16": "GDI",
    "mmsystem.dll16": "MMSYSTEM", "sound.drv16": "SOUND",
    "shell.dll16": "SHELL", "system.drv16": "SYSTEM",
    "win87em.dll16": "WIN87EM",
}


def read_specs(directory):
    names, sources = {}, []
    if directory is None:
        return names, sources
    for stem, module in SPEC_MODULES.items():
        path = directory / (stem + ".spec")
        if not path.exists():
            continue
        data = path.read_bytes()
        sources.append({"file": path.name, "sha256": hashlib.sha256(data).hexdigest()})
        for line in data.decode("utf-8").splitlines():
            match = re.match(r"^\s*(\d+)\s+(\S+)\s+(?:-\S+\s+)*([^\s(]+)", line)
            if match:
                ordinal, kind, name = match.groups()
                names[module, int(ordinal)] = {"name": name, "export_kind": kind}
    return names, sources


def inspect(data, names):
    def span(offset, size):
        if offset < 0 or size < 0 or offset + size > len(data):
            raise ValueError(f"truncated structure at file offset 0x{offset:X}, size {size}")
        return data[offset:offset + size]

    def word(offset):
        return struct.unpack("<H", span(offset, 2))[0]

    def counted_string(offset):
        size = span(offset, 1)[0]
        return span(offset + 1, size).decode("ascii", errors="strict")

    if span(0, 2) != b"MZ":
        raise ValueError("not an MZ executable")
    ne = struct.unpack("<I", span(0x3C, 4))[0]
    if span(ne, 2) != b"NE":
        raise ValueError("not an NE executable")
    span(ne, 0x40)
    segment_count, module_count = word(ne + 0x1C), word(ne + 0x1E)
    segment_table = ne + word(ne + 0x22)
    module_table = ne + word(ne + 0x28)
    name_table = ne + word(ne + 0x2A)
    alignment = word(ne + 0x32)
    if alignment > 31:
        raise ValueError("invalid segment alignment shift")
    modules = [counted_string(name_table + word(module_table + i * 2)).upper()
               for i in range(module_count)]
    imports, flags_seen, relocation_types = {}, set(), Counter()
    span(segment_table, segment_count * 8)
    for index in range(segment_count):
        sector, length, flags, minimum = struct.unpack("<4H", span(segment_table + index * 8, 8))
        flags_seen.add(flags)
        if not sector:
            if flags & 0x100:
                raise ValueError("relocations on segment with no file data")
            continue
        # Iterated segments require decoding their disk representation first.
        if flags & 8:
            raise ValueError("iterated segment is outside this inspector's supported subset")
        start, size = sector << alignment, length or 65536
        span(start, size)
        if not flags & 0x100:
            continue
        relocations = start + size
        count = word(relocations)
        span(relocations + 2, count * 8)
        for r in range(count):
            position = relocations + 2 + r * 8
            address_type, relocation_flags, source, target1, target2 = struct.unpack(
                "<BBHHH", span(position, 8))
            kind = relocation_flags & 3
            relocation_types[kind] += 1
            if kind not in (1, 2):
                continue  # Internal references and OS fixups are not imports.
            if not 1 <= target1 <= module_count:
                raise ValueError("import references invalid module-table index")
            module = modules[target1 - 1]
            ordinal = target2 if kind == 1 else None
            identity = str(ordinal) if kind == 1 else counted_string(name_table + target2)
            key = (module, kind, identity)
            if key not in imports:
                resolved = names.get((module, ordinal), {}) if kind == 1 else {"name": identity}
                imports[key] = {"module": module, "ordinal": ordinal,
                                "name": resolved.get("name"),
                                "export_kind": resolved.get("export_kind"), "relocations": []}
            # A record can head a chain of fixup sites: do not call this a call count.
            imports[key]["relocations"].append({"segment": index + 1,
                "source_offset": source, "record_file_offset": position,
                "address_type": address_type, "flags": relocation_flags})
    return {"format": "NE", "target_os": span(ne + 0x36, 1)[0],
            "ne_flags": word(ne + 0x0C), "segment_count": segment_count,
            "segment_flags": sorted(flags_seen), "module_references": modules,
            "relocation_record_counts": dict(sorted(relocation_types.items())),
            "imports": [imports[key] for key in sorted(imports)]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--spec-dir", type=Path)
    args = parser.parse_args()
    names, sources = read_specs(args.spec_dir)
    files, skipped = [], []
    for path in sorted(args.input.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(args.input).as_posix()
        if path.suffix.lower() not in (".ad", ".dll"):
            skipped.append(relative)
            continue
        data = path.read_bytes()
        result = {"file": relative, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
        try:
            result.update(inspect(data, names))
        except (ValueError, UnicodeError) as error:
            result["error"] = str(error)
        files.append(result)
    print(json.dumps({"schema_version": 1, "ordinal_sources": sources,
                      "files": files, "skipped": skipped}, indent=2))
    return int(any("error" in file for file in files) or not files)


if __name__ == "__main__":
    raise SystemExit(main())
