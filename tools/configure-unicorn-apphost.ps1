param(
    [Parameter(Mandatory = $true)][string] $ExecutablePath,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][string] $ExecutableName
)

$ErrorActionPreference = 'Stop'
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1"
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1"

# The consuming executable explicitly imports our build target and supplies its
# own output directory/name. Never change dotnet.exe or machine mitigation policy.
# See docs/localappdata-publishing.md for the native longjmp/CFG compatibility tradeoff.
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path.TrimEnd('\', '/')
if ([IO.Path]::GetFileName($ExecutableName) -ne $ExecutableName -or
    [IO.Path]::GetExtension($ExecutableName) -ne '.exe' -or
    $ExecutableName -eq 'dotnet.exe' -or
    [IO.Path]::GetFileName($executable) -ne $ExecutableName -or
    [IO.Path]::GetDirectoryName($executable) -ne $output) {
    throw 'Only the explicitly named application executable directly inside its build/publish output may be configured.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio with Desktop development with C++ for editbin.' }
$editbin = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC/Tools/MSVC/**/bin/Hostx64/x64/editbin.exe' |
    Sort-Object -Descending | Select-Object -First 1
if (-not $editbin) { throw 'Install Visual Studio Desktop development with C++ to provide editbin.' }

& $editbin /NOLOGO /GUARD:NO $executable
if ($LASTEXITCODE -ne 0) { throw 'Could not configure the application executable for Unicorn.' }

# Inspect the final bytes, rather than assuming successful tool execution proves
# the flag changed. IMAGE_DLLCHARACTERISTICS_GUARD_CF is bit 0x4000 in a PE image.
$bytes = [IO.File]::ReadAllBytes($executable)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
if ([BitConverter]::ToUInt32($bytes, $peOffset) -ne 0x4550) { throw 'Expected a PE executable.' }
$dllCharacteristics = [BitConverter]::ToUInt16($bytes, $peOffset + 24 + 70)
if (($dllCharacteristics -band 0x4000) -ne 0) { throw 'The configured executable still advertises CFG.' }
