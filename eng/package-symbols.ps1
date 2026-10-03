[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $root "artifacts\managed\x64\$Configuration"
$packageRoot = Join-Path $root 'artifacts\symbols'
$packageName = 'Libmem.NET-symbols-windows-x64'
$destination = Join-Path $packageRoot $packageName
$archive = Join-Path $packageRoot "$packageName.zip"
$archiveChecksum = "$archive.sha256"

# Libmem.NET is C++/CLI and MSVC emits Windows PDBs. NuGet.org's .snupkg
# symbol server accepts Portable PDBs only, so Windows symbols are published
# as a dedicated GitHub Release/CI archive instead.
$requiredFiles = @(
    (Join-Path $managed 'Libmem.NET.dll'),
    (Join-Path $managed 'Libmem.NET.pdb'),
    (Join-Path $managed 'Libmem.NET.xml'),
    (Join-Path $root 'VERSION'),
    (Join-Path $root 'LICENSE'),
    (Join-Path $root 'THIRD_PARTY_NOTICES.md')
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file -PathType Leaf)) {
        throw "Required symbol-package file was not found: $file"
    }
}

if (Test-Path $destination) {
    Remove-Item $destination -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null

foreach ($file in $requiredFiles) {
    Copy-Item $file $destination -Force
}

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

Write-Host "Symbol package: $destination"
Write-Host "Symbol archive: $archive"
Write-Host "Symbol archive checksum: $archiveChecksum"
