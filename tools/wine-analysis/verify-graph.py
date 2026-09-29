"""Check known source landmarks and preserve the checks beside the graph.

These are extraction checks, not Wine execution or rendering conformance tests.
The synthetic dispatch fixture separately tests the query's pointer semantics.
"""
import argparse
import json
from pathlib import Path
import sqlite3

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("database", type=Path)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
connection = sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True)
checks = {}
checks["sqlite_integrity"] = connection.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
checks["foreign_keys"] = not connection.execute("PRAGMA foreign_key_check").fetchall()
checks["zero_extraction_failures"] = connection.execute(
    "SELECT count(*) FROM extraction_units WHERE exit_code!=0 OR error_count!=0").fetchone()[0] == 0
for caller, callee in (("StretchBlt16", "StretchBlt"), ("StretchBlt", "NtGdiStretchBlt"),
                       ("stretch_bitmapinfo", "calc_1d_stretch_params")):
    checks[f"{caller} -> {callee}"] = bool(connection.execute(
        "SELECT 1 FROM calls c JOIN functions f ON f.id=c.caller "
        "JOIN targets t ON t.call_id=c.id JOIN functions dest ON dest.id=t.target "
        "WHERE f.name=? AND dest.name=? AND t.evidence='direct'", (caller, callee)).fetchone())
dispatch = {row[0] for row in connection.execute(
    "SELECT DISTINCT f.name FROM calls c JOIN targets t ON t.call_id=c.id "
    "JOIN functions f ON f.id=t.target WHERE c.expression LIKE '%pStretchBlt%' "
    "AND t.evidence='structural_candidate'")}
checks["stretch_driver_candidates"] = dispatch == {
    "dibdrv_StretchBlt", "nulldrv_StretchBlt", "windrv_StretchBlt"}
checks["unresolved_dispatch_retained"] = connection.execute(
    "SELECT count(*) FROM unresolved_calls WHERE kind='indirect'").fetchone()[0] > 0
checks["all_definitions_retained"] = connection.execute(
    "SELECT count(*) FROM definitions WHERE function_id IN (SELECT id FROM functions WHERE name='DllMain')").fetchone()[0] > 1
report = {"checks": checks, "stretch_driver_candidates": sorted(dispatch),
          "passed": all(checks.values()), "proof": "Source extraction and database consistency only"}
args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
connection.close()
print(json.dumps(report, indent=2))
raise SystemExit(0 if report["passed"] else 1)
