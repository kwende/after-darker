#!/usr/bin/env bash
# Run after extraction. Keep BQRS results alongside portable CSV exports.
set -euo pipefail
analysis_root=${1:?Pass the WSL analysis directory}
database_name=${2:-wine-codeql}
results_name=${3:-results}
query_root=$(cd -- "$(dirname -- "$0")" && pwd)
mkdir -p "$analysis_root/$results_name"
queries=(functions calls direct-targets table-targets state-access function-references arguments definitions)
# Optional whole-program data-flow resolution is much more expensive. The
# default graph keeps structural candidate edges and explicit unresolved sites.
if [[ ${WINE_GRAPH_FULL_DATAFLOW:-0} == 1 ]]; then queries+=(targets); fi
for query in "${queries[@]}"; do
    "$analysis_root/codeql/codeql" query run "$query_root/$query.ql" \
        --database="$analysis_root/$database_name" --threads=4 --ram=9000 \
        --output="$analysis_root/$results_name/$query.bqrs"
    "$analysis_root/codeql/codeql" bqrs decode "$analysis_root/$results_name/$query.bqrs" \
        --format=csv --output="$analysis_root/$results_name/$query.csv"
done
python3 "$query_root/merge-targets.py" "$analysis_root/$results_name" \
    "${WINE_GRAPH_FULL_DATAFLOW:-0}"
