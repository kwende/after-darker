"""Validate structural pointer resolution and macro-site identity on real QL output."""
import csv
from pathlib import Path
import sys

root = Path(sys.argv[1])
def rows(name):
    with (root / (name + ".csv")).open(newline="", encoding="utf-8") as stream:
        return list(csv.DictReader(stream))

functions = {row["id"]: row["name"] for row in rows("functions")}
calls = rows("calls")
assert len(calls) == len({call["id"] for call in calls}), "Call sites collided"
targets = rows("table-targets")
for function in ("through_field", "through_local"):
    sites = {call["id"] for call in calls if functions[call["caller"]] == function}
    observed = {functions[row["target"]] for row in targets if row["call_id"] in sites}
    assert observed == {"first_row", "second_row"}, (function, observed)
macro_calls = [call for call in calls if functions[call["caller"]] == "through_macro"]
assert len(macro_calls) == 2, macro_calls
print("CodeQL fixture passed: correct field slots, conditional local alias, distinct macro calls.")
