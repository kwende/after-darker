# WineVDM tracing as a compatibility planning tool

Date: 2026-09-26. Source-verified assessment against WineVDM v0.9.0, the
portable release used by our original-host experiment. No new screensaver was
launched, no debugger attached, and no trace capture was performed in this
assessment. The owner's earlier Starry Night playback observation remains the
runtime evidence; the selected-module discovery problem still needs checking.

## Decision

Use a small tracing pilot before implementing more compatibility families.
The expected value is high for determining **which API contracts a chosen
screensaver actually exercises**. It is lower for estimating exact engineering
time, and it does not automatically reveal the Wine source branches taken.

Favor a few owner-selected favorites. Completeness across the entire historical
suite is an optional objective, not a reason to implement an expensive subsystem
for a module nobody particularly wants. Assess incremental cost and reuse across
the favorites; do not rank by raw imported-function count.

## Verified mechanisms

Source tag v0.9.0 resolves to `e9e87734fd36eaa5c2e38d299a8cdc20786f4783`.

| Mechanism | What the source provides | Limits |
| --- | --- | --- |
| `WINEDEBUG=+relay` | Calls from Win16 into compatibility APIs, DLL/ordinal/name, scalar arguments, segmented pointer values, some strings, return values, guest return CS:IP and DS, thread ID | Not arbitrary pointed-to buffer snapshots or every internal function call |
| Reverse relay logging | `CallTo16` / `RetFrom16`, target address, stack/register context and return values | Requires address-to-module/export mapping to distinguish callbacks and host entrypoints |
| `+snoop` | Instrumented exported calls between original Win16 DLLs, useful for AD modules/helper libraries | Argument counts can be inferred from stack cleanup; bounded raw-word capture, not a trustworthy typed ABI decoder; must validate compatibility on the selected target |
| `+loaddll`, `+module` | Module loading and NE metadata useful for provenance and caller attribution | Logging volume and exact selector coverage must be checked on the release binary; a small probe may be needed for a complete selector map |
| Targeted channels such as `+gdi` | Existing implementation diagnostics | Only explicit trace statements, not automatic coverage of every GDI branch |
| Relay/snoop include/exclude lists | Source supports function/ordinal/module filtering through `HKCU\Software\Wine\Debug` | Check the actual registry view and existing settings before changing filters; do not silently alter persistent settings |

Primary sources:

- [Relay calls, argument formatting, returns and filters](https://github.com/otya128/winevdm/blob/v0.9.0/krnl386/relay.c).
- [Reverse callbacks and relay logging](https://github.com/otya128/winevdm/blob/v0.9.0/krnl386/wowthunk.c).
- [Win16 DLL snooping and argument-count inference](https://github.com/otya128/winevdm/blob/v0.9.0/krnl386/snoop.c).
- [WINEDEBUG parsing and stderr output](https://github.com/otya128/winevdm/blob/v0.9.0/wine/debug.c).
- [Module loading and metadata diagnostics](https://github.com/otya128/winevdm/blob/v0.9.0/krnl386/ne_module.c).

A source-supported initial channel selection is
`-all,+relay,+loaddll,+module,err+all,warn+all`. Start with a process-local
environment on `otvdm.exe` and redirect stderr to a run-specific file. This
selection has not yet been runtime-validated here. Avoid `+all` or instruction
logging initially. Add snooping or memory/graphics diagnostics only for a
specific unanswered question, and check that instrumentation preserves playback.

## The important runtime distinction

WineVDM uses Wine-derived Win16-to-Win32 conversion code on Windows and delegates
many operations to native Windows. Its [architecture description](https://github.com/otya128/winevdm/blob/v0.9.0/README.md#how-does-it-work)
and [GDI wrappers](https://github.com/otya128/winevdm/blob/v0.9.0/gdi/gdi.c)
show this boundary.

Consequently, observing a WineVDM `StretchBlt` request does not demonstrate that
upstream Wine's `win32u/dibdrv` scaling functions executed. Join observed API
identities and argument constraints to the [static graph](wine-dependency-graph-results.md)
as implementation research, not as measured branch coverage. Actual Wine branch
coverage would require executing a matching Wine build with instrumentation.
That larger setup is unnecessary for the first API-requirement census.

## Questions with high planning value

- Is `TextOut` used while drawing, or only by setup/error dialogs?
- Which raster operations, scale directions and bitmap formats are exercised?
- Are global allocations fixed or movable? Which lock, resize, ownership and
  lifetime patterns appear? Memory allocation addresses alone do not answer all
  of these questions.
- Does a palette change affect an existing image, or only future drawing?
- Do callbacks or saved-context APIs execute during playback?
- Which original helper exports run, and which Windows APIs do they invoke?
- Does a module need file input to draw, or only to load/save options?

One observed demanding contract can raise the cost substantially; thousands of
repetitions of a supported contract may add little implementation effort. A
request that was not observed remains unobserved, not proven unnecessary.

## Bounded pilot

1. Select one module already supported by After Darker as a calibration case,
   plus two or three owner-prioritized unsupported modules. First confirm that
   the original host actually selected and loaded the intended artifact.
2. Record artifact/helper hashes, release/configuration, resolution, settings
   and enabled trace channels. Use color playback and disable optional audio
   where possible; do not discard monochrome masks used by color rendering.
3. Capture startup, selection, initialization, a bounded animation sample and
   shutdown. One to two minutes is an initial sampling budget, not a completeness
   guarantee. Extend runs for meaningful animation cycles, scene changes or rare
   events, rather than collecting hours of identical calls.
4. Separate host-only calls, module calls, helper calls and callbacks. Use caller
   addresses, module/selector mappings and nested export activity where available.
   Mark ambiguous attribution explicitly. Subtracting one host baseline is not
   sufficient: helpers and the host can legitimately do work on a module's behalf.
5. Summarize distinct API/argument contracts by lifecycle phase. Compare them
   against our actual guarded handlers and the static source graph. Treat new
   argument variants of implemented APIs as potential missing work.
6. Add narrow pre/post memory capture only where pointer contents or selected
   object state block an answer. Validate snoop argument layouts against SDK/
   import signatures. Do not begin by tracing every x86 instruction.

Continue to the next module only once the pilot can reliably identify the
module, separate meaningful calls from host UI noise, and produce an actionable
list of missing contracts. If it cannot, one targeted logger modification or
debugger investigation is justified. Do not grow a general tracing platform
without demonstrating that it changes a compatibility decision.

## Durable output to build after the pilot

Retain raw logs and a manifest locally. Parse normalized events into JSONL and
import summaries/relationships into SQLite beside the existing static graph.
Suggested fields: run/artifact IDs, sequence, thread, phase, caller module and
guest address, nesting, API identity, decoded arguments, raw arguments, return,
attribution confidence and optional bounded pre/post payload references.

Mark unavailable values explicitly. The stock relay does not promise timestamps
on every line or portable semantic object identities. Normalize handles to
run-local objects and pointers to module/segment/offset where possible; numeric
addresses and handles cannot be blindly replayed in our process. Raw logs alone
are not self-contained conformance fixtures. Captures may contain proprietary
resource data and must remain ignored.

The per-module decision report should contain:

- Owner priority and desired visual mode.
- Observed missing API contracts and new variants of supported contracts.
- Shared capabilities also needed by other favorites.
- Hard boundaries: callbacks, helper loading, fonts, palette state, FPU, etc.
- Evidence coverage and remaining uncertainty.
- Recommendation: implement now, run one more targeted experiment, or defer.

Do not infer guest multithreading from native thread IDs alone: WineVDM and
Windows may create their own threads. Do not use heavily logged wall time as an
unbiased performance measurement. Win98 remains useful for behavior that fails
or differs under WineVDM; it need not be the starting environment for this pilot.

**Compressed model:** the static graph shows possible dependencies; API traces
show exercised contracts; targeted state capture explains those contracts; the
owner's priorities determine which compatibility work is worthwhile.
