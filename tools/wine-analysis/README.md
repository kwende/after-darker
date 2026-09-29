# Wine dependency analysis

These are offline research tools. They are not dependencies of After Darker
playback and do not execute proprietary AD code. Start with the findings in
`docs/research/wine-dependency-mapping.md` and the dated graph report linked there.

## Data to retain

- **SQLite** is the primary portable analysis format: functions, call sites,
  candidate targets, state accesses, address-taken functions, NE import roots,
  consumer identities, helper dependencies and per-root reachability. Arguments
  include expressions, types and constants for later branch specialization.
  `definitions` retains all definition sites when the unlinked source index
  merges a symbol; `call_descriptions` retains alternate source descriptions.
- **CSV** exports preserve the original CodeQL result relations and provide a
  convenient API summary. Unknown sites remain explicit.
- **CodeQL database and BQRS** retain the semantic extraction for new queries.
  Keep these locally, including its source archive and diagnostics; they are
  substantially larger than the portable graph.
- **JSON** inventory, extraction report and manifest retain input identities,
  pinned versions, coverage and reproducibility information.

No original AD bytes belong in any of these outputs. Input paths are relative
to the private input root. Reports distinguish a file's imports from observed
execution: importing a shared DLL does not prove that every path in it runs.

## Reproduction environment

The first run used Ubuntu 26.04 under WSL. Tools live in the user's Linux home
under `after-darker-analysis`, separately from the Windows runtime build.

Pinned inputs:

- Wine `4f4d68f6a784db76e66cf0f033f90b8288bba11e`.
- CodeQL CLI `2.27.1`, Linux x64 ZIP from GitHub's official CLI releases.
  Verify its published checksum before unpacking.
- `codeql/cpp-all` `12.1.1`; transitive versions are in `codeql-pack.lock.yml`.
- The successful PE compiler was MinGW GCC 13; Unix tools used GCC 15.

Installed Ubuntu packages: `build-essential gcc-multilib flex bison clang llvm
lld unzip git pkg-config gcc-mingw-w64-i686-posix`. Clang was tried first; its
valid Wine build exposed CodeQL parser errors around GNU attributes after
Microsoft calling-convention keywords. Switching to the MinGW compiler removed
those extraction failures without a source patch. Do not use that initial
Clang database for the published counts.

Set `analysis_root` to the Linux analysis directory and `repo_root` to the WSL
path of this checkout. Download/extract the pinned Wine source beneath
`analysis_root`, and unpack CodeQL as `analysis_root/codeql`. Then:

```bash
wine_source="$analysis_root/wine-4f4d68f6a784db76e66cf0f033f90b8288bba11e"
mkdir "$analysis_root/build-gcc"
cd "$analysis_root/build-gcc"
"$wine_source/configure" --with-mingw --without-x --without-wayland \
    --without-freetype --disable-tests > configure.log 2>&1

"$analysis_root/codeql/codeql" database create "$analysis_root/wine-codeql-gcc" \
    --language=cpp --source-root="$wine_source" --threads=4 --ram=9000 \
    --command="python3 $repo_root/tools/wine-analysis/extract-build.py --build-dir $analysis_root/build-gcc"

cd "$repo_root/tools/wine-analysis"
"$analysis_root/codeql/codeql" pack install
bash run-queries.sh "$analysis_root" wine-codeql-gcc results-verified
```

Use a fresh build directory: objects that are already up to date will not be
compiled or extracted again. `extract-build.py` records the selected object
targets. Its 24-module allowlist is an explicit boundary, not an assertion that
all of Wine was analyzed. Generated headers and build tools are included when
the build needs them. No Wine executable needs to be linked or launched.

The configuration excludes display drivers, FreeType and other unavailable
optional dependencies. Core GDI and DIB algorithms are still indexed, but
FreeType-backed font behavior and excluded backend branches are not covered.
Inline assembly, `.spec` forwarding, runtime DLL resolution and callbacks need
separate attention even with zero parser failures. Unlinked object extraction
is a source graph, not a proof of the final DLL link layout.

Prepare the private inventory and portable database:

```bash
output="$repo_root/artifacts/wine-analysis"
python3 inventory.py --input "$repo_root/ad" --wine "$wine_source" \
    --registry "$repo_root/src/AfterDarker.Core/Win16/Win16Imports.cs" \
    --output "$output/inventory.json"
python3 extraction-report.py --database "$analysis_root/wine-codeql-gcc" \
    --build "$analysis_root/build-gcc" --output "$output/extraction.json"
python3 assemble.py --csv "$analysis_root/results-verified" \
    --inventory "$output/inventory.json" --extraction "$output/extraction.json" \
    --revision 4f4d68f6a784db76e66cf0f033f90b8288bba11e \
    --output "$output/graph.sqlite"
python3 assess.py "$output/graph.sqlite" --output "$output/assessment.json"
python3 verify-graph.py "$output/graph.sqlite" --output "$output/verification.json"
```

`assemble.py` refuses to replace an existing database. Use a new output filename
for a new run, so later comparisons do not erase the old evidence.

The completed September 25 run is in `artifacts/wine-analysis/graph.sqlite`,
with CodeQL's semantic database retained at
`/home/brush/after-darker-analysis/wine-codeql-gcc` and raw results under
`/home/brush/after-darker-analysis/results-verified` in Ubuntu WSL. The original
Clang database and intermediate result directories are diagnostic history, not
the published snapshot. Use `manifest.json` to identify the final files.

After copying `configure.log` and `analysis-build-targets.json` (as
`build-targets.json`) from the build directory, preserve the raw relations and
seal the snapshot:

```bash
(cd "$analysis_root/results-verified" && tar -czf "$output/codeql-relations.tar.gz" -- *.csv)
python3 manifest.py --output "$output/manifest.json" --analysis-root "$analysis_root"
```

The CodeQL database/source and the portable outputs are ignored local research
artifacts, not files to add to a package or pull request. Compact aggregate
results may be published separately. Do not include private input paths or
proprietary binary data in public reports.

## Asking questions later

`query.py` opens the SQLite database read-only and emits JSON. It only needs
standard Python, so the portable graph is usable from Windows without WSL or
CodeQL. Examples and larger questions are in `questions.sql`.

```powershell
python tools/wine-analysis/query.py artifacts/wine-analysis/graph.sqlite `
    --sql "SELECT * FROM apis WHERE name LIKE '%Palette%'"
```

The `direct` reachability mode follows named calls only; it deliberately stops
at indirect dispatch. `candidates` also follows structural candidates extracted
by CodeQL from field/variable initializers and explicit assignments. This model
is flow-insensitive and does not determine the actual selected driver instance.
Neither mode is a runtime trace, nor a complete lower/upper bound on
all executions. Unresolved targets can undercount; conservative candidates can
overcount. Root mappings marked `ambiguous_spec_symbol` require inspection.

The general `resolveCall` data-flow query is retained in `targets.ql` and can be
enabled with `WINE_GRAPH_FULL_DATAFLOW=1`. The initial whole-index attempt was
stopped after roughly eight minutes of evaluation and heavy SSA/cache activity;
it produced no result used in this graph. This is an explicit precision/runtime
tradeoff, not a claim that CodeQL resolved every pointer. A focused future
data-flow analysis can refine individual remaining dispatch sites.

State accesses inventory fields/globals, not exact read/write side effects,
ownership or pointer lifetime. A `guarded_handler` registry label means we have
an implementation with a limited contract, not complete API compatibility.
`inventory.py` recognizes the current tuple-based registry source; its hash is
stored. Recheck its parser if that C# layout changes. Function IDs and integer
keys identify this snapshot, not guaranteed cross-revision symbol identities.

## Verification

```powershell
python -m unittest discover -s tools/wine-analysis -p test_analysis.py -v
```

Tests cover cycle-safe reachability, shared dependencies, unresolved call-site
retention, direct/candidate separation, export aliases/constants and registry
guard classification. Assembly also checks SQLite integrity and foreign keys.
Verify important graph edges against the pinned source before using them as an
implementation boundary. Preserve extraction telemetry alongside the results.

The source-authored C fixture also verifies the actual CodeQL model, independent
of Wine and private AD artifacts:

```bash
"$analysis_root/codeql/codeql" database create "$analysis_root/fixture-codeql" \
    --language=cpp --source-root="$repo_root/tools/wine-analysis/fixtures" \
    --command="gcc -c $repo_root/tools/wine-analysis/fixtures/dispatch.c -o $analysis_root/dispatch.o"
bash verify-fixture.sh "$analysis_root"
```

Use a new database directory when repeating extraction. This fixture catches a
particularly misleading mistake: traversing every child of a function-table
expression can pull unrelated slots into the target set. The query follows
the value of the selected slot instead. It also checks conditional local aliases
and two distinct calls expanded from a single macro invocation.
