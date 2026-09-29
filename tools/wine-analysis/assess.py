"""Summarize the graph without equating Wine reachability with a port backlog."""
import argparse
from collections import Counter, defaultdict, deque
import json
from pathlib import Path
import sqlite3

from assemble import distances


def assess(database):
    connection = sqlite3.connect(database.resolve().as_uri() + "?mode=ro", uri=True)
    connection.row_factory = sqlite3.Row
    functions = {row["id"]: dict(row) for row in connection.execute("SELECT * FROM functions")}
    graphs = {"direct": defaultdict(set), "candidates": defaultdict(set)}
    for row in connection.execute("SELECT c.caller,t.target,t.evidence FROM targets t JOIN calls c ON c.id=t.call_id"):
        graphs["candidates"][row["caller"]].add(row["target"])
        if row["evidence"] == "direct":
            graphs["direct"][row["caller"]].add(row["target"])
    for row in connection.execute("SELECT source,target FROM export_forwards"):
        graphs["candidates"][row["source"]].add(row["target"])
    findings = {
        "artifact_counts": [dict(row) for row in connection.execute(
            "SELECT role,reachable,count(*) AS count FROM artifacts GROUP BY role,reachable")],
        "imports_by_library": [dict(row) for row in connection.execute(
            "SELECT a.module,count(DISTINCT CASE WHEN c.role='screensaver' THEN a.id END) AS direct_modules,"
            "count(DISTINCT a.id) AS including_helpers FROM apis a JOIN api_consumers c ON c.api=a.id "
            "WHERE c.reachable=1 GROUP BY a.module ORDER BY including_helpers DESC")],
        "wine_root_mapping": dict(connection.execute(
            "SELECT count(DISTINCT a.id) AS referenced_wine_exports,"
            "count(DISTINCT CASE WHEN EXISTS(SELECT 1 FROM roots r WHERE r.api=a.id) THEN a.id END) AS mapped "
            "FROM apis a JOIN api_consumers c ON c.api=a.id WHERE c.reachable=1 AND a.spec_file IS NOT NULL").fetchone()),
        "distinct_referenced_root_functions": connection.execute(
            "SELECT count(DISTINCT r.function_id) FROM roots r JOIN api_consumers c ON c.api=r.api "
            "WHERE c.reachable=1").fetchone()[0],
        "unmapped_wine_exports": [dict(row) for row in connection.execute(
            "SELECT a.identity,a.name,a.export_kind FROM apis a WHERE a.spec_file IS NOT NULL "
            "AND EXISTS(SELECT 1 FROM api_consumers c WHERE c.api=a.id AND c.reachable=1) "
            "AND NOT EXISTS(SELECT 1 FROM roots r WHERE r.api=a.id)")],
        "unresolved_by_kind": [dict(row) for row in connection.execute(
            "SELECT kind,count(*) AS count FROM unresolved_calls GROUP BY kind")],
        "dispatch_resolution": dict(connection.execute(
            "SELECT count(*) AS indirect_sites, sum(EXISTS(SELECT 1 FROM targets t WHERE t.call_id=c.id)) AS with_candidates "
            "FROM calls c WHERE kind='indirect'").fetchone()),
        "export_forward_edges": connection.execute("SELECT count(*) FROM export_forwards").fetchone()[0],
        "multiple_definition_symbols": connection.execute(
            "SELECT count(*) FROM (SELECT function_id FROM definitions GROUP BY function_id HAVING count(*)>1)").fetchone()[0],
        "call_arguments": connection.execute("SELECT count(*) FROM arguments").fetchone()[0],
        "direct_wine_registry_coverage": [dict(row) for row in connection.execute(
            "SELECT a.implementation,count(DISTINCT a.id) AS identities FROM apis a "
            "JOIN api_consumers c ON c.api=a.id WHERE c.role='screensaver' AND a.spec_file IS NOT NULL "
            "GROUP BY a.implementation")],
        "most_shared_missing_imports": [dict(row) for row in connection.execute(
            "SELECT a.identity,a.name,a.implementation,count(DISTINCT c.sha256) AS consumers "
            "FROM apis a JOIN api_consumers c ON c.api=a.id WHERE c.role='screensaver' "
            "AND a.spec_file IS NOT NULL AND a.implementation!='guarded_handler' "
            "AND a.export_kind NOT IN ('equate','extern','stub','variable') GROUP BY a.id "
            "ORDER BY consumers DESC,a.identity LIMIT 40")],
        "helper_consumers": [dict(row) for row in connection.execute(
            "SELECT e.module,count(DISTINCT e.caller) AS direct_screensaver_consumers "
            "FROM helper_edges e JOIN artifacts a ON a.sha256=e.caller WHERE a.role='screensaver' "
            "GROUP BY e.module ORDER BY direct_screensaver_consumers DESC")],
        "algorithm_samples": [],
    }
    for name in ("OffsetRect16", "calc_1d_stretch_params", "stretch_bitmapinfo", "stretch_row_32",
                 "shrink_row_32", "AnimatePalette16", "LocalAlloc16"):
        starts = {key for key, value in functions.items() if value["name"] == name and value["has_definition"]}
        sample = {"function": name, "root_ids": sorted(starts)}
        for mode, graph in graphs.items():
            reachable = distances(starts, graph)
            sample[mode] = {
                "defined_functions": sum(functions[key]["has_definition"] for key in reachable),
                "external_or_builtin_nodes": sum(not functions[key]["has_definition"] for key in reachable),
                "subsystems": dict(Counter(functions[key]["subsystem"] for key in reachable
                                             if functions[key]["has_definition"])),
            }
            # An inspectable extraction seam, NOT a claim that dependencies outside
            # this directory can be omitted. They become explicit boundary nodes.
            if any(functions[key]["file"].startswith("dlls/win32u/dibdrv/") for key in starts):
                found, boundary, pending = set(starts), set(), deque(starts)
                while pending:
                    node = pending.popleft()
                    for target in graph.get(node, ()):
                        if not functions[target]["file"].startswith("dlls/win32u/dibdrv/"):
                            boundary.add(target)
                        elif target not in found:
                            found.add(target)
                            pending.append(target)
                sample[mode]["dib_directory_functions"] = len(found)
                sample[mode]["boundary_dependencies"] = sorted(
                    {functions[key]["name"] for key in boundary})
        findings["algorithm_samples"].append(sample)
    connection.close()
    return findings


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    findings = assess(args.database)
    args.output.write_text(json.dumps(findings, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"wine_root_mapping": findings["wine_root_mapping"],
                      "dispatch_resolution": findings["dispatch_resolution"]}, indent=2))
