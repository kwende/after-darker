#!/usr/bin/env bash
# Refresh the three query relations used to verify an existing fixture database.
set -euo pipefail
analysis_root=${1:?Pass the WSL analysis directory}
query_root=$(cd -- "$(dirname -- "$0")" && pwd)
mkdir -p "$analysis_root/fixture-results"
for query in functions calls table-targets; do
    "$analysis_root/codeql/codeql" query run "$query_root/$query.ql" \
        --database="$analysis_root/fixture-codeql" --threads=4 --ram=9000 \
        --output="$analysis_root/fixture-results/$query.bqrs"
    "$analysis_root/codeql/codeql" bqrs decode "$analysis_root/fixture-results/$query.bqrs" \
        --format=csv --output="$analysis_root/fixture-results/$query.csv"
done
python3 "$query_root/verify-codeql-fixture.py" "$analysis_root/fixture-results"
