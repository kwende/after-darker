"""Verify the Milestone 1 manifest against local artifacts and the runtime registry.

Read-only metadata audit. Requires the owner's ignored ad/ collection. It does
not execute guest code, infer support from filenames, or upgrade status based
on historical smoke tests. Update the manifest deliberately when support changes.
"""
import hashlib
import json
from pathlib import Path
import re
import struct
from collections import Counter


def audit(repository):
    manifest = json.loads((repository / "docs/milestones/milestone-1.json").read_text(encoding="utf-8"))
    registry = (repository / "src/AfterDarker.Runtime/SupportedModules.cs").read_text(encoding="utf-8")
    constants = dict(re.findall(r'public const string (\w+) = "([A-F0-9]{64})"', registry))
    mondrian = (repository / "src/AfterDarker.Core/AfterDark/MondrianInitialization.cs").read_text(encoding="utf-8")
    constants["MondrianInitialization.Sha256"] = re.search(r'Sha256 = "([A-F0-9]{64})"', mondrian)[1]
    supported = {constants[symbol]: name for symbol, name in re.findall(
        r'^\s*(\w+(?:\.Sha256)?) => "([^"]+)"', registry, re.MULTILINE) if symbol in constants}
    entries = manifest["entries"]
    errors = []
    checked_paths = 0
    if len(entries) != 26 or len({entry["id"] for entry in entries}) != 26:
        errors.append("Milestone must contain exactly 26 distinct approved identities.")
    for entry in entries:
        primary = [artifact for artifact in entry["artifacts"] if artifact["role"] == "primary"]
        if entry["runtime_status"] == "supported" and not primary:
            errors.append(f'{entry["name"]}: supported status has no primary artifact')
        for artifact in entry["artifacts"]:
            if artifact["role"] == "primary":
                profile = supported.get(artifact["sha256"])
                if (profile is not None) != (entry["runtime_status"] == "supported"):
                    errors.append(f'{entry["name"]}: manifest status disagrees with production registry')
                if profile is not None and profile != entry["name"]:
                    errors.append(f'{entry["name"]}: registry name is {profile}')
            for relative_path in artifact["paths_relative_to_ad"]:
                path = repository / "ad" / relative_path
                if not path.is_file():
                    errors.append(f'Missing local artifact: {relative_path}')
                    continue
                data = path.read_bytes()
                checked_paths += 1
                if hashlib.sha256(data).hexdigest().upper() != artifact["sha256"]:
                    errors.append(f'Artifact hash changed: {relative_path}')
                if len(data) < 64 or data[:2] != b"MZ":
                    errors.append(f'Not an MZ executable: {relative_path}')
                    continue
                header_offset = struct.unpack_from("<I", data, 0x3C)[0]
                if data[header_offset:header_offset + 2] != b"NE":
                    errors.append(f'Not an NE executable: {relative_path}')
        for evidence in entry.get("host_evidence", []):
            path = repository / "ad" / evidence["path_relative_to_ad"]
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest().upper() != evidence["sha256"]:
                errors.append(f'{entry["name"]}: host evidence missing or changed')
    counts = dict(Counter(entry["runtime_status"] for entry in entries))
    if counts != manifest["counts"]:
        errors.append("Manifest summary counts disagree with entries.")
    return {"milestone_entries": len(entries), "counts": counts, "checked_artifact_paths": checked_paths,
            "supported_registry_entries": len(supported),
            "supported_outside_milestone": sorted(set(supported.values()) - {entry["name"] for entry in entries}),
            "errors": errors, "proof": "Artifact/registry metadata only; no guest execution"}


if __name__ == "__main__":
    result = audit(Path(__file__).resolve().parents[1])
    print(json.dumps(result, indent=2))
    raise SystemExit(bool(result["errors"]))
