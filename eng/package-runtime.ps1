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
$packageName = "Libmem.NET-windows-$Platform"
$destination = Join-Path $packageRoot $packageName
$archive = Join-Path $packageRoot "$packageName.zip"
$archiveChecksum = "$archive.sha256"

$requiredFiles = @(
    (Join-Path $managed 'Libmem.NET.dll'),
    (Join-Path $managed 'Libmem.NET.xml'),
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

$manifestScript = Join-Path $PSScriptRoot 'write-manifest.ps1'
& $manifestScript -Destination $destination -Configuration $Configuration -Platform $Platform

if (Test-Path $archive) {
    Remove-Item $archive -Force
}
if (Test-Path $archiveChecksum) {
    Remove-Item $archiveChecksum -Force
}
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $archive -CompressionLevel Optimal

$archiveHash = (Get-FileHash -Path $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = "$archiveHash  $packageName.zip"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($archiveChecksum, $checksumLine + [Environment]::NewLine, $utf8NoBom)

Write-Host "Runtime package: $destination"
Write-Host "Runtime archive: $archive"
Write-Host "Runtime archive checksum: $archiveChecksum"
