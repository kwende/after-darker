[CmdletBinding()]
param(
    [string]$WatcomRoot = 'C:\tools\open-watcom\2026-09-01'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repoRoot 'tests\fixtures\win16\hello42'
$outputRoot = Join-Path $repoRoot 'artifacts\win16\hello42'
$WatcomRoot = (Resolve-Path -LiteralPath $WatcomRoot).Path
foreach ($relativePath in @('binnt64\wcc.exe', 'binnt64\wlink.exe', 'h\win\windows.h', 'lib286\win')) {
    if (-not (Test-Path -LiteralPath (Join-Path $WatcomRoot $relativePath))) {
        throw "Incomplete Watcom installation: missing $relativePath. See docs/watcom-toolchain.md."
    }
}

# Keep target-specific headers and library selection local to this invocation.
$savedEnvironment = @{}
foreach ($variableName in @('WATCOM', 'PATH', 'INCLUDE', 'WINDOWS_INCLUDE')) {
    $savedEnvironment[$variableName] = [Environment]::GetEnvironmentVariable($variableName, 'Process')
}
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
Push-Location $outputRoot
try {
    $env:WATCOM = $WatcomRoot
    $env:PATH = "$WatcomRoot\binnt64;$WatcomRoot\binnt;$env:PATH"
    $env:INCLUDE = "$WatcomRoot\h;$WatcomRoot\h\win"
    $env:WINDOWS_INCLUDE = $env:INCLUDE

    # Build from this directory so every compiler/linker output stays in artifacts.
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'hello42.c'), (Join-Path $sourceRoot 'hello42.lnk') -Destination $outputRoot
    & "$WatcomRoot\binnt64\wcc.exe" hello42.c /mc /zu /zc /bd /bt=windows /w4 /we
    if ($LASTEXITCODE -ne 0) { throw 'Win16 fixture compilation failed.' }
    & "$WatcomRoot\binnt64\wlink.exe" '@hello42.lnk'
    if ($LASTEXITCODE -ne 0) { throw 'Win16 fixture linking failed.' }
    Write-Host "Built $(Join-Path $outputRoot 'hello42.dll')"
} finally {
    Pop-Location
    foreach ($variableName in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($variableName, $savedEnvironment[$variableName], 'Process')
    }
}
