"""Check graph semantics independently of private AD files and CodeQL installs."""
import csv
import json
from pathlib import Path
import tempfile
import unittest

from assemble import create_graph, distances
from inventory import load_inspector, read_exports, read_registry


class GraphTests(unittest.TestCase):
    def test_cycles_shared_dependencies_and_shortest_distance(self):
        graph = {1: {2, 3}, 2: {4}, 3: {4, 5}, 4: {1}, 5: {6}}
        self.assertEqual(distances({1}, graph), {1: 0, 2: 1, 3: 1, 4: 2, 5: 2, 6: 3})

    def test_export_aliases_and_nonfunctions_remain_distinct(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary) / "dlls/gdi.exe16"
            directory.mkdir(parents=True)
            (directory / "gdi.exe16.spec").write_text(
                "35 pascal -ret16 StretchBlt(word long) StretchBlt16\n"
                "99 equate ImportedConstant 3\n100 stub Unfinished\n")
            exports = read_exports(Path(temporary), load_inspector())
            self.assertEqual(exports["GDI", 35]["target"], "StretchBlt16")
            self.assertEqual(exports["GDI", 99]["kind"], "equate")
            self.assertEqual(exports["GDI", 100]["kind"], "stub")

    def test_registry_drawing_guard_is_not_marked_unimplemented(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "registry.cs"
            path.write_text('(\"GDI\", 34, \"BitBlt\", enableDrawing ? Handler.BitBlt : Handler.Unsupported, 20),\n'
                            '(\"GDI\", 35, \"StretchBlt\", Handler.Unsupported, null),\n'
                            '(\"ADWOPENSOUND\", Handler.SoundOpen, 0)')
            registry = read_registry(path)
            self.assertEqual(registry["GDI", 34]["implementation"], "guarded_handler")
            self.assertEqual(registry["GDI", 35]["implementation"], "unsupported")
            self.assertEqual(registry["AD_SND", "ADWOPENSOUND"]["implementation"], "guarded_handler")

    def test_database_preserves_unresolved_sites_and_separates_candidate_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixtures = {
                "functions": (["id", "name", "file", "line", "end_line", "has_definition", "signature"], [
                    ["a", "StretchBlt16", "dlls/gdi.exe16/gdi.c", 1, 3, 1, "StretchBlt16"],
                    ["b", "helper", "dlls/win32u/dibdrv/bitblt.c", 4, 8, 1, "helper"],
                    ["c", "backend", "dlls/win32u/dibdrv/bitblt.c", 9, 10, 1, "backend"]]),
                "calls": (["id", "caller", "file", "line", "column", "expression", "kind"], [
                    ["s1", "a", "dlls/gdi.exe16/gdi.c", 2, 1, "helper", "direct"],
                    ["s2", "b", "dlls/win32u/dibdrv/bitblt.c", 5, 1, "dispatch", "indirect"],
                    ["s3", "b", "dlls/win32u/dibdrv/bitblt.c", 6, 1, "unknown", "indirect"]]),
                "targets": (["call_id", "target", "evidence"], [
                    ["s1", "b", "direct"], ["s2", "c", "codeql_candidate"]]),
                "state-access": (["function_id", "file", "line", "member", "type", "kind", "owner_type"], []),
                "function-references": (["target", "file", "line", "enclosing_function"], [])}
            for name, (columns, rows) in fixtures.items():
                with (root / (name + ".csv")).open("w", newline="") as stream:
                    writer = csv.writer(stream)
                    writer.writerow(columns)
                    writer.writerows(rows)
            inventory = dict(registry_sha256="fixture", artifacts=[], helper_edges=[], roots=[dict(
                identity="GDI!#35", module="GDI", ordinal=35, name="StretchBlt", consumers=[],
                registry={"implementation": "unsupported"},
                export={"target": "StretchBlt16", "kind": "pascal"})])
            path = root / "inventory.json"
            path.write_text(json.dumps(inventory))
            connection = create_graph(root, path, root / "graph.sqlite", "fixture")
            self.assertEqual(connection.execute("SELECT count(*) FROM unresolved_calls").fetchone()[0], 1)
            counts = dict(connection.execute("SELECT mode,count(*) FROM reachability GROUP BY mode"))
            self.assertEqual(counts, {"direct": 2, "candidates": 3})
            connection.close()


if __name__ == "__main__":
    unittest.main()
