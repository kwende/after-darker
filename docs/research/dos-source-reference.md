# Local MS-DOS source reference

Microsoft's historical MS-DOS source repository is available as a sibling
checkout for investigating DOS services used by original After Dark modules.

- Local checkout: `C:\repos\MS-DOS` (outside this repository).
- Upstream: <https://github.com/microsoft/MS-DOS>.
- Cloned and verified: 2026-09-16.
- Revision inspected: `2d04cacc5322951f187bb17e017c12920ac8ebe2`.
- Contents described by upstream: MS-DOS 1.25, 2.0, and 4.0 sources, with
  binaries for the earlier releases.
- License: MIT; see the checkout's `LICENSE` and `README.md`.

This is a research reference, not an After Darker build/runtime dependency or
Git submodule. Other contributors may clone it wherever convenient:

```powershell
git clone https://github.com/microsoft/MS-DOS.git C:\repos\MS-DOS
```

## When to consult it

Consult the source when a guest DOS request needs clarification: register
inputs/outputs, flags, error behavior, date/time edge cases, or dependencies on
DOS state. Pair implementation evidence with the documented API contract and
the actual module's call site. Record the DOS version, revision, file, and
relevant symbol when a finding informs a handler or test.

Older DOS implementation details are evidence for that version, not automatic
proof of later DOS behavior or how Windows mediates the request. Win16
`KERNEL`, `USER`, and `GDI` services need their own references; this checkout
does not implement that Windows compatibility layer.

Prefer a small tested C# service matching the required contract. Reusing or
adapting source can be considered when it materially helps; record provenance,
retain required license notices, and identify any new dependency. The presence
of these sources does not change the project's scope to booting DOS.

## Starting points for Mondrian

The [Mondrian path analysis](mondrian-static-analysis.md) found `INT 21h`
date/time calls during random-seed initialization. These source links are
pinned to the inspected revision:

| Question | Local path under `C:\repos\MS-DOS` | Source link |
| --- | --- | --- |
| How do DOS 4.0's date/time handlers work? | `v4.0\src\DOS\TIME.ASM` — `$GET_DATE` at line 54, `$GET_TIME` at line 106 | [TIME.ASM](https://github.com/microsoft/MS-DOS/blob/2d04cacc5322951f187bb17e017c12920ac8ebe2/v4.0/src/DOS/TIME.ASM#L54) |
| Which service numbers dispatch to those handlers? | `v4.0\src\DOS\MS_TABLE.ASM` — 2Ah/2Ch entries at lines 306/308 | [MS_TABLE.ASM](https://github.com/microsoft/MS-DOS/blob/2d04cacc5322951f187bb17e017c12920ac8ebe2/v4.0/src/DOS/MS_TABLE.ASM#L306) |
| How did the earlier implementation express the same services? | `v2.0\source\SYSCALL.ASM` — `$GET_DATE` at line 41, `$GET_TIME` at line 94 | [SYSCALL.ASM](https://github.com/microsoft/MS-DOS/blob/2d04cacc5322951f187bb17e017c12920ac8ebe2/v2.0/source/SYSCALL.ASM#L41) |

The [MS-DOS Encyclopedia's system-call reference](https://msarchive.pcjs.org/mspl13/msdos/encyclopedia/section5/)
provides the companion API documentation for `INT 21h`, AH=2Ah and AH=2Ch.

The checkout, origin, revision, clean working tree, and source locations were
verified. No DOS build or execution was performed; cloning and reading the
source is not a new runtime conformance result.
