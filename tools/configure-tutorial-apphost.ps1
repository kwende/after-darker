param([Parameter(Mandatory = $true)][string] $ExecutablePath)

$ErrorActionPreference = 'Stop'
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1"
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1"

# Change only this project's generated executable, never a shared dotnet host or
# Windows policy. See docs/tutorials.md for the observed CFG failure and tradeoff.
$expectedDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/AfterDarker.Tutorials/bin/'))
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
if (-not $executable.StartsWith($expectedDirectory, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($executable) -ne 'AfterDarker.Tutorials.exe') {
    throw 'Only the tutorial executable inside its build output directory may be configured.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio with the Desktop development with C++ tools is required for editbin.'
}
$editbin = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC/Tools/MSVC/**/bin/Hostx64/x64/editbin.exe' |
    Sort-Object -Descending | Select-Object -First 1
if (-not $editbin) {
    throw 'Install the Visual Studio Desktop development with C++ tools to provide editbin.'
}

& $editbin /NOLOGO /GUARD:NO $executable
if ($LASTEXITCODE -ne 0) { throw 'Could not configure the tutorial apphost for Unicorn.' }
