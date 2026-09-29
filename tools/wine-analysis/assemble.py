"""Assemble CodeQL CSV exports and NE metadata into a queryable SQLite graph.

Raw CodeQL evidence is retained. Reachability has separate direct-call and
candidate-call modes. Unresolved sites and declaration-only endpoints are never
silently treated as implemented leaves. This is source reachability, not a
runtime trace or an automatic list of functions that must be ported.
"""
import argparse
from collections import defaultdict, deque
import csv
import hashlib
import json
from pathlib import Path
import sqlite3


SCHEMA = """
PRAGMA foreign_keys=ON;
CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE artifacts(sha256 TEXT PRIMARY KEY, module TEXT, role TEXT, reachable INTEGER);
CREATE TABLE artifact_paths(artifact TEXT REFERENCES artifacts, path TEXT, PRIMARY KEY(artifact,path));
CREATE TABLE apis(id INTEGER PRIMARY KEY, identity TEXT UNIQUE, module TEXT, ordinal INTEGER,
 name TEXT, implementation TEXT, wine_target TEXT, export_kind TEXT, spec_file TEXT, spec_line INTEGER);
CREATE TABLE artifact_imports(artifact TEXT REFERENCES artifacts, api INTEGER REFERENCES apis,
 PRIMARY KEY(artifact,api));
CREATE TABLE helper_edges(caller TEXT REFERENCES artifacts, callee TEXT REFERENCES artifacts, module TEXT,
 PRIMARY KEY(caller,callee,module));
CREATE TABLE functions(id INTEGER PRIMARY KEY, ql_id TEXT UNIQUE, name TEXT, file TEXT, line INTEGER,
 end_line INTEGER, has_definition INTEGER, signature TEXT, subsystem TEXT);
CREATE TABLE calls(id INTEGER PRIMARY KEY, ql_id TEXT UNIQUE, caller INTEGER REFERENCES functions,
 file TEXT, line INTEGER, column_number INTEGER, expression TEXT, kind TEXT);
CREATE TABLE call_descriptions(call_id INTEGER REFERENCES calls, expression TEXT,
 PRIMARY KEY(call_id,expression));
CREATE TABLE definitions(function_id INTEGER REFERENCES functions, file TEXT, line INTEGER, end_line INTEGER,
 PRIMARY KEY(function_id,file,line,end_line));
CREATE TABLE targets(call_id INTEGER REFERENCES calls, target INTEGER REFERENCES functions, evidence TEXT,
 PRIMARY KEY(call_id,target,evidence));
CREATE TABLE state_access(function_id INTEGER REFERENCES functions, file TEXT, line INTEGER, member TEXT,
 type TEXT, kind TEXT, owner_type TEXT);
CREATE TABLE arguments(call_id INTEGER REFERENCES calls, position INTEGER, expression TEXT, type TEXT,
 constant TEXT);
CREATE TABLE function_references(target INTEGER REFERENCES functions, file TEXT, line INTEGER,
 enclosing_function INTEGER REFERENCES functions);
CREATE TABLE extraction_units(file TEXT, exit_code INTEGER, error_count INTEGER, log TEXT);
CREATE TABLE annotations(function_id INTEGER REFERENCES functions, disposition TEXT, reason TEXT,
 evidence TEXT, PRIMARY KEY(function_id,disposition));
CREATE TABLE export_forwards(source INTEGER REFERENCES functions, target INTEGER REFERENCES functions,
 spec_file TEXT, spec_line INTEGER, evidence TEXT, PRIMARY KEY(source,target,spec_file,spec_line));
CREATE TABLE roots(api INTEGER REFERENCES apis, function_id INTEGER REFERENCES functions, evidence TEXT,
 PRIMARY KEY(api,function_id));
CREATE TABLE reachability(api INTEGER REFERENCES apis, function_id INTEGER REFERENCES functions,
 mode TEXT, depth INTEGER, PRIMARY KEY(api,function_id,mode));
CREATE INDEX calls_caller ON calls(caller);
CREATE INDEX targets_target ON targets(target);
CREATE INDEX state_function ON state_access(function_id);
CREATE INDEX reach_function ON reachability(function_id,mode);
CREATE VIEW unresolved_calls AS
 SELECT c.* FROM calls c WHERE NOT EXISTS(SELECT 1 FROM targets t WHERE t.call_id=c.id);
CREATE VIEW api_consumers AS
 SELECT DISTINCT i.api,a.sha256,a.module,a.role,a.reachable FROM artifact_imports i
 JOIN artifacts a ON a.sha256=i.artifact;
CREATE VIEW defined_reachability AS
 SELECT r.*, f.name, f.file, f.line, f.subsystem FROM reachability r
 JOIN functions f ON f.id=r.function_id WHERE f.has_definition=1;
"""


def read_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(stream)


def distances(starts, adjacency):
    """Cycle-safe breadth-first closure; shared functions are counted once."""
    found = {node: 0 for node in starts}
    pending = deque(starts)
    while pending:
        node = pending.popleft()
        for target in adjacency.get(node, ()):
            if target not in found:
                found[target] = found[node] + 1
                pending.append(target)
    return found


def subsystem(path):
    if "/dibdrv/" in path:
        return "win32u/dibdrv"
    parts = path.split("/")
    if len(parts) > 1 and parts[0] in ("dlls", "libs"):
        return parts[1]
    return parts[0] if parts else "external"


def create_graph(csv_root, inventory_path, output, revision, extraction_path=None):
    inventory = json.loads(inventory_path.read_text(encoding="utf-8"))
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        raise ValueError(f"Refusing to overwrite an existing analysis database: {output}")
    connection = sqlite3.connect(output)
    connection.executescript(SCHEMA)
    metadata = {
        "schema_version": 1, "wine_revision": revision, "codeql_version": "2.27.1",
        "cpp_pack_version": "12.1.1", "registry_sha256": inventory["registry_sha256"],
        "inventory_sha256": hashlib.sha256(inventory_path.read_bytes()).hexdigest(),
        "proof_boundary": "Compiled-source graph; candidate targets are not observed execution."
    }
    for key, value in metadata.items():
        connection.execute("INSERT INTO metadata VALUES (?,?)", (key, json.dumps(value)))
    if extraction_path:
        extraction = json.loads(extraction_path.read_text(encoding="utf-8"))
        connection.execute("INSERT INTO metadata VALUES (?,?)", ("extraction", json.dumps(extraction["telemetry"])))
        connection.executemany("INSERT INTO extraction_units VALUES (?,?,?,?)", (
            (unit["file"], unit["exit_code"], unit["error_count"], unit["log"])
            for unit in extraction["translation_units"]))
    for artifact in inventory["artifacts"]:
        connection.execute("INSERT INTO artifacts VALUES (?,?,?,?)", (
            artifact["sha256"], artifact["module_name"], artifact["role"], artifact["reachable_from_screensaver"]))
        connection.executemany("INSERT INTO artifact_paths VALUES (?,?)", (
            (artifact["sha256"], path) for path in artifact["paths"]))
    for edge in inventory["helper_edges"]:
        connection.execute("INSERT OR IGNORE INTO helper_edges VALUES (?,?,?)",
                           (edge["caller"], edge["callee"], edge["module"]))
    for api_id, root in enumerate(sorted(inventory["roots"], key=lambda root: root["identity"]), 1):
        export = root["export"] or {}
        connection.execute("INSERT INTO apis VALUES (?,?,?,?,?,?,?,?,?,?)", (
            api_id, root["identity"], root["module"], root["ordinal"], root["name"],
            root["registry"]["implementation"], export.get("target"), export.get("kind"),
            export.get("spec_file"), export.get("spec_line")))
        connection.executemany("INSERT INTO artifact_imports VALUES (?,?)", (
            (consumer, api_id) for consumer in root["consumers"]))

    function_ids = {}
    for record in read_csv(csv_root / "functions.csv"):
        values = (record["id"], record["name"], record["file"], int(record["line"]),
                  int(record["end_line"]), int(record["has_definition"]), record["signature"],
                  subsystem(record["file"]))
        connection.execute("INSERT OR IGNORE INTO functions VALUES (NULL,?,?,?,?,?,?,?,?)", values)
    function_ids.update(connection.execute("SELECT ql_id,id FROM functions"))
    if (csv_root / "definitions.csv").exists():
        connection.executemany("INSERT OR IGNORE INTO definitions VALUES (?,?,?,?)", (
            (function_ids[record["function_id"]], record["file"], int(record["line"]), int(record["end_line"]))
            for record in read_csv(csv_root / "definitions.csv")))
    annotations = json.loads((Path(__file__).parent / "adaptation-boundaries.json").read_text())
    for entry in annotations["entries"]:
        for (function_id,) in connection.execute("SELECT id FROM functions WHERE name=? AND file=?",
                                                 (entry["name"], entry["file"])).fetchall():
            connection.execute("INSERT INTO annotations VALUES (?,?,?,?)", (
                function_id, entry["disposition"], entry["reason"], "human_review_candidate"))
    for forward in inventory.get("forwarders", []):
        target_name = forward["target"].rsplit(".", 1)[-1]
        target_module = forward["target"].rsplit(".", 1)[0] if "." in forward["target"] else forward["module"]
        sources = connection.execute("SELECT id FROM functions WHERE name=? AND has_definition=0",
                                     (forward["symbol"],)).fetchall()
        destinations = connection.execute("SELECT id FROM functions WHERE name=?", (target_name,)).fetchall()
        definitions = connection.execute("SELECT id FROM functions WHERE name=? AND has_definition=1 AND file LIKE ?",
                                          (target_name, f"dlls/{target_module}/%")).fetchall()
        if definitions:
            destinations = definitions
        for (source_id,) in sources:
            for (target_id,) in destinations:
                if source_id != target_id:
                    connection.execute("INSERT OR IGNORE INTO export_forwards VALUES (?,?,?,?,?)", (
                        source_id, target_id, forward["spec_file"], forward["spec_line"], "spec_forward_candidate"))
    call_ids = {}
    for record in read_csv(csv_root / "calls.csv"):
        connection.execute("INSERT OR IGNORE INTO calls VALUES (NULL,?,?,?,?,?,?,?)", (
            record["id"], function_ids[record["caller"]], record["file"], int(record["line"]),
            int(record["column"]), record["expression"], record["kind"]))
    call_ids.update(connection.execute("SELECT ql_id,id FROM calls"))
    connection.executemany("INSERT OR IGNORE INTO call_descriptions VALUES (?,?)", (
        (call_ids[record["id"]], record["expression"]) for record in read_csv(csv_root / "calls.csv")))
    for record in read_csv(csv_root / "targets.csv"):
        connection.execute("INSERT OR IGNORE INTO targets VALUES (?,?,?)", (
            call_ids[record["call_id"]], function_ids[record["target"]], record["evidence"]))
    for record in read_csv(csv_root / "state-access.csv"):
        connection.execute("INSERT INTO state_access VALUES (?,?,?,?,?,?,?)", (
            function_ids[record["function_id"]], record["file"], int(record["line"]),
            record["member"], record["type"], record["kind"], record["owner_type"]))
    for record in read_csv(csv_root / "function-references.csv"):
        connection.execute("INSERT INTO function_references VALUES (?,?,?,?)", (
            function_ids[record["target"]], record["file"], int(record["line"]),
            function_ids.get(record["enclosing_function"])))
    if (csv_root / "arguments.csv").exists():
        for record in read_csv(csv_root / "arguments.csv"):
            connection.execute("INSERT INTO arguments VALUES (?,?,?,?,?)", (
                call_ids[record["call_id"]], int(record["position"]), record["expression"],
                record["type"], record["constant"]))

    # Export mappings are evidence, not name-based linking of arbitrary call sites.
    # A target may be forwarded (kernel32.GetTickCount); retain every matching
    # definition if the source index cannot disambiguate it and report multiplicity.
    for api_id, target, export_kind in connection.execute(
            "SELECT id,wine_target,export_kind FROM apis WHERE wine_target IS NOT NULL").fetchall():
        if export_kind in ("equate", "extern", "variable", "stub"):
            continue
        symbol = target.rsplit(".", 1)[-1]
        candidates = connection.execute(
            "SELECT id,file FROM functions WHERE name=? AND has_definition=1", (symbol,)).fetchall()
        forwarded = False
        if not candidates:
            pending_symbols, visited_symbols = [symbol], {symbol}
            for pending_symbol in pending_symbols:
                for forward in inventory.get("forwarders", []):
                    if forward["symbol"] != pending_symbol:
                        continue
                    destination = forward["target"].rsplit(".", 1)[-1]
                    candidates.extend(connection.execute(
                        "SELECT id,file FROM functions WHERE name=? AND has_definition=1", (destination,)).fetchall())
                    if destination not in visited_symbols:
                        visited_symbols.add(destination)
                        pending_symbols.append(destination)
            candidates = list(dict.fromkeys(candidates))
            forwarded = bool(candidates)
        # Win16 target names are usually unique. Forwarded exports can resolve
        # to kernelbase rather than a definition in kernel32.
        for function_id, _ in candidates:
            connection.execute("INSERT OR IGNORE INTO roots VALUES (?,?,?)", (
                api_id, function_id, "ambiguous_spec_symbol" if len(candidates) > 1 else
                "spec_forward_candidate" if forwarded else "spec_symbol"))

    adjacency = {"direct": defaultdict(set), "candidates": defaultdict(set)}
    for caller, target, evidence in connection.execute(
            "SELECT c.caller,t.target,t.evidence FROM targets t JOIN calls c ON c.id=t.call_id"):
        adjacency["candidates"][caller].add(target)
        if evidence == "direct":
            adjacency["direct"][caller].add(target)
    for source, target in connection.execute("SELECT source,target FROM export_forwards"):
        adjacency["candidates"][source].add(target)
    starts = defaultdict(set)
    for api_id, function_id in connection.execute("SELECT api,function_id FROM roots"):
        starts[api_id].add(function_id)
    for api_id, functions in starts.items():
        for mode, graph in adjacency.items():
            connection.executemany("INSERT INTO reachability VALUES (?,?,?,?)", (
                (api_id, function_id, mode, depth)
                for function_id, depth in distances(functions, graph).items()))
    connection.commit()
    assert not connection.execute("PRAGMA foreign_key_check").fetchall()
    assert connection.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
    return connection


def write_summary(connection, directory):
    def scalar(sql):
        return connection.execute(sql).fetchone()[0]
    summary = {
        "functions": scalar("SELECT count(*) FROM functions"),
        "defined_functions": scalar("SELECT count(*) FROM functions WHERE has_definition=1"),
        "call_sites": scalar("SELECT count(*) FROM calls"),
        "indirect_sites": scalar("SELECT count(*) FROM calls WHERE kind='indirect'"),
        "unresolved_sites": scalar("SELECT count(*) FROM unresolved_calls"),
        "target_edges": scalar("SELECT count(*) FROM targets"),
        "state_accesses": scalar("SELECT count(*) FROM state_access"),
        "mapped_apis": scalar("SELECT count(DISTINCT api) FROM roots"),
        "ambiguous_root_apis": scalar("SELECT count(DISTINCT api) FROM roots WHERE evidence='ambiguous_spec_symbol'"),
        "subsystems": list(connection.execute(
            "SELECT subsystem,count(*) FROM functions WHERE has_definition=1 GROUP BY subsystem ORDER BY count(*) DESC")),
    }
    rows = []
    for api in connection.execute("SELECT id,identity,module,name,implementation,wine_target,export_kind FROM apis").fetchall():
        api_id, identity, module, name, implementation, target, kind = api
        row = dict(identity=identity, module=module, name=name, implementation=implementation,
                   wine_target=target, export_kind=kind)
        row["direct_module_consumers"] = scalar(
            f"SELECT count(*) FROM api_consumers WHERE api={api_id} AND role='screensaver'")
        row["reachable_helper_consumers"] = scalar(
            f"SELECT count(*) FROM api_consumers WHERE api={api_id} AND role='helper' AND reachable=1")
        row["root_definitions"] = scalar(f"SELECT count(*) FROM roots WHERE api={api_id}")
        for mode in ("direct", "candidates"):
            row[f"{mode}_definitions"] = scalar(
                f"SELECT count(*) FROM defined_reachability WHERE api={api_id} AND mode='{mode}'")
            row[f"{mode}_unresolved_sites"] = scalar(
                f"SELECT count(*) FROM unresolved_calls c JOIN reachability r ON r.function_id=c.caller "
                f"WHERE r.api={api_id} AND r.mode='{mode}'")
            row[f"{mode}_external_nodes"] = scalar(
                f"SELECT count(*) FROM reachability r JOIN functions f ON f.id=r.function_id "
                f"WHERE r.api={api_id} AND r.mode='{mode}' AND f.has_definition=0")
        rows.append(row)
    with (directory / "api-summary.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    (directory / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return summary


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csv", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--extraction", type=Path)
    args = parser.parse_args()
    connection = create_graph(args.csv, args.inventory, args.output, args.revision, args.extraction)
    print(json.dumps(write_summary(connection, args.output.parent), indent=2))
    connection.close()
