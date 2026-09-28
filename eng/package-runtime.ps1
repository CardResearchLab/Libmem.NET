[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $root "artifacts\managed\$Platform\$Configuration"
$native = Join-Path $root "artifacts\native\$Platform\$Configuration\bin"
$packageRoot = Join-Path $root 'artifacts\package'
$packageName = "LibmemCli-windows-$Platform"
$destination = Join-Path $packageRoot $packageName
$archive = Join-Path $packageRoot "$packageName.zip"

$requiredFiles = @(
    (Join-Path $managed 'LibmemCli.dll'),
    (Join-Path $managed 'Ijwhost.dll'),
    (Join-Path $native 'libmem.dll'),
    (Join-Path $root 'VERSION'),
    (Join-Path $root 'LICENSE'),
    (Join-Path $root 'THIRD_PARTY_NOTICES.md')
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        throw "Required runtime file was not found: $file"
    }
}

if (Test-Path $destination) {
    Remove-Item $destination -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null

foreach ($file in $requiredFiles) {
    Copy-Item $file $destination -Force
}

$pdb = Join-Path $managed 'LibmemCli.pdb'
if (Test-Path $pdb) {
    Copy-Item $pdb $destination -Force
}

$manifestScript = Join-Path $PSScriptRoot 'write-manifest.ps1'
& $manifestScript -Destination $destination -Configuration $Configuration -Platform $Platform

if (Test-Path $archive) {
    Remove-Item $archive -Force
}
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $archive -CompressionLevel Optimal

Write-Host "Runtime package: $destination"
Write-Host "Runtime archive: $archive"
