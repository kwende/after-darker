"""Preserve extractor coverage and parser diagnostics as first-class evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--database", type=Path, required=True)
parser.add_argument("--build", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
records = []
for log in sorted((args.database / "log" / "extractor").rglob("*.log")):
    text = log.read_text(errors="replace")
    command = re.search(r"Command: (.+)", text)
    if not command:
        continue
    source = re.search(r"(?:wine-[0-9a-f]{40}/)(\S+\.(?:c|cpp|cc))(?=\s|$)", command[1])
    if source:
        source_file = source[1]
    else:
        generated = re.search(r"(?:^|\s)([^\s]+\.(?:c|cpp|cc))(?=\s|$)", command[1])
        if not generated:
            continue
        source_file = "@build/" + generated[1]
    errors = []
    for match in re.finditer(r'"[^"]*wine-[0-9a-f]{40}/([^"\n]+)", line (\d+): error: ([^\n]+)', text):
        errors.append(dict(file=match[1], line=int(match[2]), message=match[3]))
    exit_match = re.search(r"Extractor exiting with code (\d+)", text)
    records.append(dict(file=source_file, error_count=len(errors), errors=errors,
                        exit_code=int(exit_match[1]) if exit_match else 0,
                        log=log.relative_to(args.database).as_posix()))
summary_path = args.database / "diagnostic/extractors/cpp/summary.jsonl"
summary = json.loads(summary_path.read_text())["attributes"]
report = dict(schema_version=1, telemetry=summary,
              build_targets=json.loads((args.build / "analysis-build-targets.json").read_text()),
              translation_units=records,
              exit_counts=dict(Counter(record["exit_code"] for record in records)))
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(dict(telemetry=summary, exit_counts=report["exit_counts"]), indent=2))
