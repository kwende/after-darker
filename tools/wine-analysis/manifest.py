"""Seal a local graph snapshot with tool/input identities and content hashes."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--analysis-root", required=True, help="Location of retained WSL semantic database")
args = parser.parse_args()
directory = args.output.parent
tool_directory = Path(__file__).resolve().parent
repository = tool_directory.parents[1]
files = ("graph.sqlite", "inventory.json", "extraction.json", "summary.json", "assessment.json",
         "api-summary.csv", "verification.json", "codeql-relations.tar.gz", "build-targets.json", "configure.log")
manifest = {
    "schema_version": 1,
    "generated_utc": datetime.now(timezone.utc).isoformat(),
    "wine_revision": "4f4d68f6a784db76e66cf0f033f90b8288bba11e",
    "wine_source_archive_sha256": "4abe80d9e08e1f37d98b9b3fafddb7f67e086882fd2eb9e041cc5f0ecbbd3960",
    "codeql_cli": "2.27.1", "cpp_all_pack": "12.1.1",
    "configure_arguments": ["--with-mingw", "--without-x", "--without-wayland",
                            "--without-freetype", "--disable-tests"],
    "repository_base_commit": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip(),
    "semantic_database": args.analysis_root.rstrip("/") + "/wine-codeql-gcc",
    "bqrs_and_csv": args.analysis_root.rstrip("/") + "/results-verified",
    "source": args.analysis_root.rstrip("/") + "/wine-4f4d68f6a784db76e66cf0f033f90b8288bba11e",
    "model": {
        "direct": "CodeQL named call targets",
        "candidates": "Direct plus typed-slot initializers/assignments and spec forwards; flow-insensitive",
        "full_pointer_dataflow": "Attempt stopped; no results included",
        "unlinked_objects": "Multiple-definition symbols retained; inspect definitions table before specialization",
        "scope": "24 selected Wine modules, generated dependencies and toolchain headers; not all Wine",
        "excludes": "Runtime evidence, complete loader/callback/assembly edges and exact port-effort estimates"
    },
    "artifacts": {name: {"bytes": (directory / name).stat().st_size, "sha256": sha256(directory / name)}
                  for name in files},
    "analysis_sources": {path.relative_to(tool_directory).as_posix(): sha256(path)
                         for path in sorted(tool_directory.rglob("*")) if path.is_file()
                         and path.suffix in (".py", ".ql", ".qll", ".yml", ".json", ".sh", ".sql", ".c")
                         and "__pycache__" not in path.parts},
}
args.output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(args.output)
