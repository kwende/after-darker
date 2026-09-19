param([Parameter(Mandatory = $true)][string] $ExecutablePath)

$ErrorActionPreference = 'Stop'
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1"
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1"

# Change only our generated executables, never a shared dotnet host or
# Windows policy. See docs/tutorials.md for the observed CFG failure and tradeoff.
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$allowedOutputs = @{
    'AfterDarker.Tutorials.exe' = '../src/AfterDarker.Tutorials/bin/'
    'AfterDarker.Tests.exe' = '../tests/AfterDarker.Tests/bin/'
    'AfterDarker.Wpf.exe' = '../src/AfterDarker.Wpf/bin/'
}
$outputDirectory = $allowedOutputs[[IO.Path]::GetFileName($executable)]
if (-not $outputDirectory -or -not $executable.StartsWith(
        [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $outputDirectory)),
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only a named After Darker executable inside its own bin directory may be configured.'
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
if ($LASTEXITCODE -ne 0) { throw 'Could not configure the project apphost for Unicorn.' }
