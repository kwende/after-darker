"""Inventory private NE inputs and map imports to a pinned Wine export contract.

Only metadata is emitted. Original module bytes never enter the analysis output.
Run with --help. This reads files; it does not load or execute guest code.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import struct


def load_inspector():
    spec = importlib.util.spec_from_file_location(
        "ne_inspector", Path(__file__).parents[1] / "inspect-ne-imports.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def read_exports(wine, inspector):
    exports = {}
    modules = dict(inspector.SPEC_MODULES)
    modules.update({"commdlg.dll16": "COMMDLG", "keyboard.drv16": "KEYBOARD"})
    for directory, module in modules.items():
        path = wine / "dlls" / directory / (directory + ".spec")
        if not path.exists():
            continue
        for line_number, line in enumerate(path.read_text().splitlines(), 1):
            match = re.match(r"\s*(\d+)\s+(\w+)\s+(?:-\S+\s+)*([^\s(]+)(.*)", line)
            if not match:
                continue
            ordinal, kind, name, remainder = match.groups()
            target = name
            if ")" in remainder:
                suffix = remainder.split(")", 1)[1].split("#", 1)[0].strip()
                if suffix:
                    target = suffix.split()[0]
            exports[module, int(ordinal)] = dict(
                name=name, kind=kind, target=target,
                spec_file=path.relative_to(wine).as_posix(), spec_line=line_number)
    return exports


def read_registry(path):
    entries = {}
    text = path.read_text(encoding="utf-8-sig")
    for match in re.finditer(r'\("(KERNEL|USER|GDI)", (\d+), "([^"]+)", ([^,]+),', text):
        module, ordinal, name, handler = match.groups()
        entries[module, int(ordinal)] = dict(
            registry_name=name,
            implementation="guarded_handler" if handler != "Handler.Unsupported" else "unsupported",
            handler_expression=handler)
    for match in re.finditer(r'\("(ADW[A-Z]+)", (Handler\.\w+), \d+\)', text):
        name, handler = match.groups()
        entries["AD_SND", name] = dict(registry_name=name,
            implementation="guarded_handler", handler_expression=handler)
    return entries


def read_forwarders(wine):
    """Capture export aliases omitted by compiling C objects without DLL linking."""
    forwarders = []
    modules = {"user32", "gdi32", "kernel32", "kernelbase", "ntdll", "winmm",
               "advapi32", "shell32", "shlwapi", "comdlg32", "comctl32", "msvcrt"}
    for module in sorted(modules):
        path = wine / "dlls" / module / (module + ".spec")
        for line_number, line in enumerate(path.read_text().splitlines(), 1):
            match = re.match(r"\s*(?:@|\d+)\s+(\w+)\s+(?:-\S+\s+)*([^\s(]+)\([^)]*\)\s+([^\s#]+)", line)
            if match and match[1] not in ("stub", "extern") and match[2] != match[3]:
                forwarders.append(dict(module=module, symbol=match[2], target=match[3],
                    spec_file=path.relative_to(wine).as_posix(), spec_line=line_number))
    return forwarders


def inventory(input_root, wine, registry_path):
    inspector = load_inspector()
    exports = read_exports(wine, inspector)
    names = {key: {"name": value["name"], "export_kind": value["kind"]}
             for key, value in exports.items()}
    registry = read_registry(registry_path)
    artifacts, rejected = {}, []
    for path in sorted(input_root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in (".ad", ".dll"):
            continue
        relative = path.relative_to(input_root).as_posix()
        data = path.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        if digest in artifacts:
            artifacts[digest]["paths"].append(relative)
            continue
        try:
            info = inspector.inspect(data, names)
            ne = struct.unpack_from("<I", data, 0x3C)[0]
            resident = ne + struct.unpack_from("<H", data, ne + 0x26)[0]
            length = data[resident]
            module_name = data[resident + 1:resident + 1 + length].decode("ascii").upper()
        except (ValueError, UnicodeError, IndexError, struct.error) as error:
            rejected.append(dict(path=relative, sha256=digest, error=str(error)))
            continue
        artifacts[digest] = dict(sha256=digest, paths=[relative], module_name=module_name,
            role="screensaver" if path.suffix.lower() == ".ad" or path.name.lower().endswith(".ad.dll") else "helper",
            imports=info["imports"], module_references=info["module_references"])

    # Reach helper libraries by the NE resident module identity, not a guessed filename.
    by_module = {}
    for artifact in artifacts.values():
        by_module.setdefault(artifact["module_name"], []).append(artifact)
    queue = [a for a in artifacts.values() if a["role"] == "screensaver"]
    reachable = {a["sha256"] for a in queue}
    helper_edges = []
    for artifact in queue:
        for dependency in artifact["module_references"]:
            for helper in by_module.get(dependency, []):
                if helper["role"] != "helper":
                    continue
                helper_edges.append(dict(caller=artifact["sha256"], callee=helper["sha256"], module=dependency))
                if helper["sha256"] not in reachable:
                    reachable.add(helper["sha256"])
                    queue.append(helper)
    roots = {}
    for artifact in artifacts.values():
        artifact["reachable_from_screensaver"] = artifact["sha256"] in reachable
        for imported in artifact["imports"]:
            identity = f'{imported["module"]}!{("#" + str(imported["ordinal"])) if imported["ordinal"] is not None else imported["name"]}'
            key = imported["module"], imported["ordinal"]
            if identity not in roots:
                export = exports.get(key)
                if key[1] is None:
                    matches = [value for (module, _), value in exports.items()
                               if module == key[0] and value["name"] == imported["name"]]
                    if len(matches) == 1:
                        export = matches[0]
                roots[identity] = dict(identity=identity, module=key[0], ordinal=key[1],
                    name=imported["name"], export=export,
                    registry=registry.get((key[0], key[1] if key[1] is not None else imported["name"]),
                                          {"implementation": "unregistered"}),
                    consumers=[], reachable_consumers=[])
            root = roots[identity]
            root["consumers"].append(artifact["sha256"])
            if artifact["sha256"] in reachable:
                root["reachable_consumers"].append(artifact["sha256"])
    return dict(schema_version=1, artifacts=list(artifacts.values()), rejected=rejected,
                helper_edges=helper_edges, roots=list(roots.values()),
                forwarders=read_forwarders(wine),
                registry_sha256=hashlib.sha256(registry_path.read_bytes()).hexdigest())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--wine", type=Path, required=True)
    parser.add_argument("--registry", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    report = inventory(args.input, args.wine, args.registry)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"artifacts": len(report["artifacts"]), "rejected": len(report["rejected"]),
                      "import_identities": len(report["roots"])}))
