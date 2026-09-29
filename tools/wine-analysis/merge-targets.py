"""Combine relation exports while preserving the evidence behind each edge."""
import csv
from pathlib import Path
import sys

directory = Path(sys.argv[1])
include_dataflow = len(sys.argv) > 2 and sys.argv[2] == "1"
files = [directory / "direct-targets.csv", directory / "table-targets.csv"]
if include_dataflow:
    # The raw data-flow query uses the combined filename; preserve it separately.
    original = directory / "targets.csv"
    preserved = directory / "dataflow-targets.csv"
    preserved.write_bytes(original.read_bytes())
    files.append(preserved)
rows = set()
for path in files:
    with path.open(newline="", encoding="utf-8") as stream:
        rows.update(tuple(row.values()) for row in csv.DictReader(stream))
with (directory / "targets.csv").open("w", newline="", encoding="utf-8") as stream:
    writer = csv.writer(stream)
    writer.writerow(["call_id", "target", "evidence"])
    writer.writerows(sorted(rows))
