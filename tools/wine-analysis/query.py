"""Read-only SQLite queries with JSON results; no third-party Python dependency.

Example: python query.py artifacts/wine-analysis/graph.sqlite --sql "SELECT * FROM metadata"
"""
import argparse
import json
from pathlib import Path
import sqlite3

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("database", type=Path)
parser.add_argument("--sql", required=True)
args = parser.parse_args()
connection = sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True)
connection.row_factory = sqlite3.Row
print(json.dumps([dict(row) for row in connection.execute(args.sql)], indent=2))
connection.close()
