# The managed NuGet package does not include a Windows DLL. Cache the official
# MinGW x64 build locally; no compiler or manually installed Unicorn is needed.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# MSBuild may inherit PowerShell 7's module path; use Windows PowerShell's own modules.
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Utility/Microsoft.PowerShell.Utility.psd1"
Import-Module "$PSHOME/Modules/Microsoft.PowerShell.Management/Microsoft.PowerShell.Management.psd1"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $repositoryRoot 'artifacts/unicorn/2.1.3/win-x64'
$nativeDll = Join-Path $cache 'unicorn.dll'
$dllHash = '24E79674A4C631EB11D3C923C88A1DE23096B69BAB3B809359BF3752F9700FCC'
$archiveHash = '884C0D29FF5D95079A875E7A70FBE0BD75F2F3DF08DB9DBA6D3644043547145E'
$url = 'https://github.com/unicorn-engine/unicorn/releases/download/2.1.3/windows-mingw64-shared.7z'

if (Test-Path -LiteralPath $nativeDll) {
    if ((Get-FileHash -LiteralPath $nativeDll -Algorithm SHA256).Hash -ne $dllHash) {
        throw "Cached Unicorn DLL hash mismatch: $nativeDll"
    }
    return
}

New-Item -ItemType Directory -Force -Path $cache | Out-Null
$archive = Join-Path $cache 'windows-mingw64-shared.7z'
if (-not (Test-Path -LiteralPath $archive)) {
    Write-Host 'Downloading the official Unicorn 2.1.3 Windows x64 native library...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $archive
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $archiveHash) {
    throw "Unicorn archive hash mismatch. Remove $archive and retry."
}

# Extract just the required library, not the upstream test programs or build files.
& tar.exe -xf $archive -C $cache 'bin/libunicorn.dll'
if ($LASTEXITCODE -ne 0) { throw 'Could not extract Unicorn with Windows tar.exe.' }
$extractedDll = Join-Path $cache 'bin/libunicorn.dll'
if ((Get-FileHash -LiteralPath $extractedDll -Algorithm SHA256).Hash -ne $dllHash) {
    throw 'Extracted Unicorn DLL hash mismatch.'
}
# The .NET binding imports "unicorn", while the MinGW build calls it libunicorn.dll.
Copy-Item -LiteralPath $extractedDll -Destination $nativeDll
