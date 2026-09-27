[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64')]
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

$manifest = [ordered]@{
    schemaVersion = 1
    packageVersion = $version
    repository = 'HearthstoneModding/Libmem'
    repositoryCommit = $repositoryCommit
    libmemCommit = $libmemCommit
    targetFramework = 'net8.0'
    platform = "win-$Platform"
    configuration = $Configuration
    files = @(
        'LibmemCli.dll'
        'Ijwhost.dll'
        'libmem.dll'
    )
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$manifestPath = Join-Path $Destination 'manifest.json'
$json = $manifest | ConvertTo-Json -Depth 4
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, $utf8NoBom)

Write-Host "Runtime manifest: $manifestPath"
