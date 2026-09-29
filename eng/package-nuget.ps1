param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\nuget'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot $OutputDirectory
}

$versionPath = Join-Path $repoRoot 'VERSION'
$version = (Get-Content $versionPath -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "VERSION must use MAJOR.MINOR.PATCH format: '$version'"
}

$requiredArtifacts = @(
    (Join-Path $repoRoot "artifacts\managed\x64\$Configuration\LibmemCli.dll"),
    (Join-Path $repoRoot "artifacts\managed\x64\$Configuration\LibmemCli.xml"),
    (Join-Path $repoRoot "artifacts\managed\x64\$Configuration\Ijwhost.dll"),
    (Join-Path $repoRoot "artifacts\native\x64\$Configuration\bin\libmem.dll")
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path $artifact -PathType Leaf)) {
        throw "Required x64 artifact is missing: $artifact. Run build.ps1 first."
    }
}

$repositoryCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $repositoryCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Could not resolve the current repository commit.'
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$project = Join-Path $repoRoot 'packaging\HearthstoneModding.LibmemCli.csproj'

dotnet pack $project `
    -c $Configuration `
    -p:PackageVersion=$version `
    -p:RepositoryCommit=$repositoryCommit `
    -p:NuGetAudit=false `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Join-Path $OutputDirectory "HearthstoneModding.LibmemCli.$version.nupkg"
if (-not (Test-Path $package -PathType Leaf)) {
    throw "Expected NuGet package was not produced: $package"
}

$verifier = Join-Path $repoRoot 'tests\verify_nuget_package.py'
python $verifier `
    --package $package `
    --expected-version $version `
    --expected-commit $repositoryCommit

if ($LASTEXITCODE -ne 0) {
    throw "NuGet package verification failed with exit code $LASTEXITCODE."
}

Write-Host "NuGet prototype package: $package"
