[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $root 'VERSION'

if (-not (Test-Path $versionFile)) {
    throw "VERSION file was not found: $versionFile"
}

$version = (Get-Content $versionFile -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION must use MAJOR.MINOR.PATCH format. Actual: $version"
}

function Get-GitValue {
    param(
        [string]$WorkingDirectory,
        [string[]]$Arguments
    )

    if (-not (Test-Path $WorkingDirectory)) {
        return 'unknown'
    }

    $value = & git -C $WorkingDirectory @Arguments 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $value) {
        return 'unknown'
    }

    return ($value | Select-Object -First 1).Trim()
}

$repositoryCommit = Get-GitValue -WorkingDirectory $root -Arguments @('rev-parse', 'HEAD')
$libmemCommit = Get-GitValue -WorkingDirectory (Join-Path $root 'third_party\libmem') -Arguments @('rev-parse', 'HEAD')

$packageFiles = @(
    Get-ChildItem -Path $Destination -File |
        Where-Object { $_.Name -ne 'manifest.json' } |
        Sort-Object Name |
        ForEach-Object {
            [ordered]@{
                name = $_.Name
                size = [UInt64]$_.Length
                sha256 = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)

$manifest = [ordered]@{
    schemaVersion = 2
    packageVersion = $version
    repository = 'HearthstoneModding/Libmem'
    repositoryCommit = $repositoryCommit
    libmemCommit = $libmemCommit
    targetFramework = 'net8.0'
    platform = "win-$Platform"
    configuration = $Configuration
    files = $packageFiles
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$manifestPath = Join-Path $Destination 'manifest.json'
$json = $manifest | ConvertTo-Json -Depth 4
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, $utf8NoBom)

Write-Host "Runtime manifest: $manifestPath"
