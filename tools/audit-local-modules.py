"""Run opt-in C# module probes in separate processes with a wall-clock watchdog.

First: dotnet build tests/AfterDarker.Tests -p:AuditLocalModules=true
Then: python tools/audit-local-modules.py ad/windows98-2026-09-20
Reports stay under ignored artifacts/. No file is executed as a native host DLL.
See docs/research/windows98-readiness-audit.md for scope and interpretation.
"""

import argparse
import json
import os
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("--output", type=Path, default=Path("artifacts/windows98-audit/baseline"))
    parser.add_argument("--match", default="", help="Optional case-insensitive relative-path substring")
    parser.add_argument("--frames", type=int, default=30)
    parser.add_argument("--host-version", type=int, default=200)
    parser.add_argument("--tick-step", type=int, default=2500)
    parser.add_argument("--white-background", action="store_true")
    parser.add_argument("--module-selected", action="store_true",
                        help="Send the SDK MODULESELECTED message before PREINITIALIZE")
    parser.add_argument("--direct-rgb-palette-requests", action="store_true",
                        help="Explicitly investigate BLANK palette replies 10/11/13 on the existing RGB24 surface")
    parser.add_argument("--controls", help="Four comma-separated diagnostic control words")
    parser.add_argument("--timeout", type=int, default=45)
    arguments = parser.parse_args()
    if not 1 <= arguments.frames <= 600 or not 1 <= arguments.timeout <= 120:
        parser.error("Use 1-600 frames and a 1-120 second process watchdog.")
    controls = [int(value) for value in arguments.controls.split(",")] if arguments.controls else None
    if controls is not None and (len(controls) != 4 or any(value < 0 or value > 65535 for value in controls)):
        parser.error("Controls must be four unsigned 16-bit words.")

    root = Path(__file__).resolve().parent.parent
    executable = root / "tests/AfterDarker.Tests/bin/Debug/net10.0/win-x64/AfterDarker.Tests.exe"
    source_directory = arguments.input.resolve()
    output_directory = arguments.output.resolve()
    output_directory.mkdir(parents=True, exist_ok=True)
    reports = []
    for module_path in sorted(source_directory.rglob("*")):
        if not module_path.is_file() or module_path.suffix.lower() != ".ad":
            continue
        relative_path = module_path.relative_to(source_directory).as_posix()
        if arguments.match.lower() not in relative_path.lower():
            continue
        # Preserve subdirectories so equal names never share or overwrite reports.
        report_path = output_directory / (relative_path + ".json")
        report_path.parent.mkdir(parents=True, exist_ok=True)
        if report_path.exists():
            raise FileExistsError(f"Choose a new output directory; report already exists: {report_path}")
        request = dict(ModulePath=str(module_path), ReportPath=str(report_path), RelativePath=relative_path,
                       Frames=arguments.frames, TickStep=arguments.tick_step, HostVersion=arguments.host_version,
                       WhiteBackground=arguments.white_background, Controls=controls,
                       DirectRgbPaletteRequests=arguments.direct_rgb_palette_requests,
                       ModuleSelected=arguments.module_selected)
        environment = dict(os.environ, AFTER_DARK_AUDIT_REQUEST=json.dumps(request))
        process_failure = None
        try:
            completed = subprocess.run([str(executable), "--filter", "FullyQualifiedName~ModuleReadinessProbe.InspectAndProbe"],
                                       cwd=root, env=environment, capture_output=True, text=True, timeout=arguments.timeout)
            (report_path.with_suffix(".process.txt")).write_text(completed.stdout + completed.stderr, encoding="utf-8")
            if completed.returncode:
                process_failure = f"Test process exited {completed.returncode}"
        except subprocess.TimeoutExpired:
            # subprocess.run kills and waits for this isolated native-engine process.
            process_failure = f"Process watchdog expired after {arguments.timeout} seconds"
        try:
            report = json.loads(report_path.read_text(encoding="utf-8-sig"))
        except (FileNotFoundError, json.JSONDecodeError) as error:
            report = {"File": relative_path, "Error": f"No complete report; verify the opt-in probe build: {type(error).__name__}"}
        if process_failure:
            report["ProcessFailure"] = process_failure
            report.pop("Outcome", None)
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        reports.append(report)
        print(relative_path, ":", report.get("FailedPhase", report.get("Phase", "")),
              process_failure or report.get("Error", report.get("Outcome", "No outcome")), flush=True)
    if not reports:
        parser.error("No .AD files matched.")
    (output_directory / "runtime.json").write_text(json.dumps(reports, indent=2), encoding="utf-8")
    return int(any(report.get("ProcessFailure") or "Sha256" not in report for report in reports))


if __name__ == "__main__":
    raise SystemExit(main())
