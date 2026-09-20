"""Compare a fresh local import census with the current Win16 registry.

This is a read-only planning tool, not a compatibility certification. It emits
metadata only: names, hashes, import gaps and optional bounded-probe outcomes.
It never reads or copies executable bytes and never executes guest code.

First run tools/inspect-ne-imports.py. Optional probe results must match each
census file's SHA-256 exactly. See docs/research/module-readiness-sweep.md.
"""

import argparse
import hashlib
import json
import re
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parent.parent


def read_registry(source_path):
    """Inspect the current literal registry, with drawing enabled for this census.

    This deliberately depends on the simple tuple format in Win16Imports.cs.
    It is not a C# evaluator. If that table becomes generated or computed, update
    this reader; the runtime remains authoritative about callable behavior.
    """
    source = source_path.read_text(encoding="utf-8")
    pattern = r'\("([A-Z]+)", (\d+), "([^"]+)", ([^\n]+)'
    registry = {}
    for module, ordinal, name, declaration in re.findall(pattern, source):
        callable_with_drawing = (
            "Handler.Unsupported" not in declaration or "enableDrawing ?" in declaration
        )
        registry[module, int(ordinal)] = {
            "name": name,
            "callable_with_drawing": callable_with_drawing,
        }
    if not registry:
        raise ValueError("No literal registry entries found; update the source-table reader.")
    return registry


def import_label(import_entry):
    """Prefer a resolved symbol while retaining library identity."""
    symbol = import_entry["name"] or f"#{import_entry['ordinal']}"
    return f"{import_entry['module']}!{symbol}"


def summarize_probe(probe, artifact):
    """Retain bounded evidence without registers, stack words, paths or resource contents."""
    if probe["Sha256"].lower() != artifact["sha256"].lower():
        raise ValueError(f"Probe hash does not match fresh census: {artifact['file']}")

    # Replace unresolved ordinal labels in diagnostic text with census names.
    def resolve_symbols(message):
        for imported in artifact["imports"]:
            if imported["ordinal"] is not None and imported["name"]:
                message = re.sub(
                    rf"{re.escape(imported['module'])}!#{imported['ordinal']}(?!\d)",
                    import_label(imported), message,
                )
        return message

    phases = probe.get("Phases", [])
    summary = {
        "loader_passed": probe.get("Loader") == "pass",
        "completed_phases": [phase["Name"] for phase in phases],
        "stopped_during": probe.get("FailedPhase"),
        "diagnostic": resolve_symbols(probe.get("Error", "")),
        "last_completed_observation": phases[-1] if phases else None,
        "control_kinds": [control["Kind"] for control in probe.get("Controls", [])],
        "resource_type_counts": probe.get("ResourceTypes", {}),
    }
    if probe.get("ProcessTimeout") or probe.get("ProcessExit"):
        summary["process_failure"] = probe.get("ProcessTimeout") or probe.get("ProcessExit")
    return summary


def build_report(census, registry, probes):
    """Keep unbound imports distinct from known-but-disabled handlers and runtime requirements."""
    probe_by_file = {probe["File"]: probe for probe in probes}
    modules = []
    for artifact in census["files"]:
        if "error" in artifact:
            raise ValueError(f"Census inspection failed: {artifact['file']}: {artifact['error']}")
        unbound_imports = []
        disabled_imports = []
        supported_imports = []
        for imported in artifact["imports"]:
            definition = registry.get((imported["module"], imported["ordinal"]))
            if definition is None:
                unbound_imports.append(import_label(imported))
            elif not definition["callable_with_drawing"]:
                disabled_imports.append(import_label(imported))
            else:
                supported_imports.append(import_label(imported))

        module = {
            "file": artifact["file"],
            "sha256": artifact["sha256"],
            "segment_count": artifact["segment_count"],
            "total_imports": len(artifact["imports"]),
            "supported_imports": supported_imports,
            "known_but_disabled_imports": disabled_imports,
            "unbound_imports": unbound_imports,
            "helper_libraries": [
                library for library in artifact["module_references"]
                if library not in ("KERNEL", "USER", "GDI")
            ],
            "os_fixup_records": artifact["relocation_record_counts"].get("3", 0),
        }
        if artifact["file"] in probe_by_file:
            module["bounded_probe"] = summarize_probe(probe_by_file[artifact["file"]], artifact)
        modules.append(module)
    return modules


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("census", type=Path, help="JSON from inspect-ne-imports.py")
    parser.add_argument("--probe-results", type=Path, help="Optional local baseline probe JSON array")
    arguments = parser.parse_args()
    census_bytes = arguments.census.read_bytes()
    census = json.loads(census_bytes.decode("utf-8-sig"))
    registry_path = REPOSITORY_ROOT / "src/AfterDarker.Core/Win16/Win16Imports.cs"
    probes = []
    if arguments.probe_results:
        probes = json.loads(arguments.probe_results.read_text(encoding="utf-8-sig"))
    report = {
        "schema_version": 1,
        "scope": "Static import gaps and optional bounded first-failure probes; not supported-module certification.",
        "registry_sha256": hashlib.sha256(registry_path.read_bytes()).hexdigest(),
        "census_sha256": hashlib.sha256(census_bytes).hexdigest(),
        "files": build_report(census, read_registry(registry_path), probes),
    }
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
