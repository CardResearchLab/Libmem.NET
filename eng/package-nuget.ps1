param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory = '',

    [string]$PackageVersion = ''
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
$baseVersion = (Get-Content $versionPath -Raw).Trim()
$versionPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$'
if ($baseVersion -notmatch $versionPattern) {
    throw "VERSION must use MAJOR.MINOR.PATCH[-PRERELEASE] format: '$baseVersion'"
}

function Resolve-ArchitectureAssets([string]$Platform) {
    $managedRoot = Join-Path $repoRoot "artifacts\managed\$Platform\$Configuration"
    $nativeRoot = Join-Path $repoRoot "artifacts\native\$Platform\$Configuration\bin"

    $buildFiles = @(
        (Join-Path $managedRoot 'Libmem.NET.dll'),
        (Join-Path $managedRoot 'Libmem.NET.xml'),
        (Join-Path $managedRoot 'Ijwhost.dll'),
        (Join-Path $nativeRoot 'libmem.dll')
    )

    if (($buildFiles | Where-Object { -not (Test-Path $_ -PathType Leaf) }).Count -eq 0) {
        return @{
            ManagedRoot = $managedRoot
            NativeRoot = $nativeRoot
            Source = 'build'
        }
    }

    $runtimeRoot = Join-Path $repoRoot "artifacts\package\Libmem.NET-windows-$Platform"
    $runtimeFiles = @(
        (Join-Path $runtimeRoot 'Libmem.NET.dll'),
        (Join-Path $runtimeRoot 'Libmem.NET.xml'),
        (Join-Path $runtimeRoot 'Ijwhost.dll'),
        (Join-Path $runtimeRoot 'libmem.dll')
    )

    if (($runtimeFiles | Where-Object { -not (Test-Path $_ -PathType Leaf) }).Count -eq 0) {
        return @{
            ManagedRoot = $runtimeRoot
            NativeRoot = $runtimeRoot
            Source = 'runtime-package'
        }
    }

    throw "Required $Platform NuGet assets were not found. Build $Platform locally or stage artifacts/package/Libmem.NET-windows-$Platform first."
}

$x64Assets = Resolve-ArchitectureAssets 'x64'
$x86Assets = Resolve-ArchitectureAssets 'x86'

Write-Host "NuGet x64 assets: $($x64Assets.Source) -> $($x64Assets.ManagedRoot)"
Write-Host "NuGet x86 assets: $($x86Assets.Source) -> $($x86Assets.ManagedRoot)"

$repositoryCommit = (& git -C $repoRoot rev-parse HEAD).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $repositoryCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Could not resolve the current repository commit.'
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $shortCommit = $repositoryCommit.Substring(0, 12)
    $PackageVersion = if ($baseVersion.Contains('-')) {
        "$baseVersion.dev.$shortCommit"
    } else {
        "$baseVersion-dev.$shortCommit"
    }
}

if ($PackageVersion -notmatch $versionPattern) {
    throw "PackageVersion is not a supported semantic version: '$PackageVersion'"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$project = Join-Path $repoRoot 'packaging\Libmem.NET.csproj'

dotnet pack $project `
    -c $Configuration `
    -p:PackageVersion=$PackageVersion `
    -p:RepositoryCommit=$repositoryCommit `
    -p:LibmemNetX64ManagedRoot="$($x64Assets.ManagedRoot)" `
    -p:LibmemNetX64NativeRoot="$($x64Assets.NativeRoot)" `
    -p:LibmemNetX86ManagedRoot="$($x86Assets.ManagedRoot)" `
    -p:LibmemNetX86NativeRoot="$($x86Assets.NativeRoot)" `
    -p:NuGetAudit=false `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

$package = Join-Path $OutputDirectory "Libmem.NET.$PackageVersion.nupkg"
if (-not (Test-Path $package -PathType Leaf)) {
    throw "Expected NuGet package was not produced: $package"
}

$verifier = Join-Path $repoRoot 'tests\verify_nuget_package.py'
python $verifier `
    --package $package `
    --expected-version $PackageVersion `
    --expected-commit $repositoryCommit

if ($LASTEXITCODE -ne 0) {
    throw "NuGet package verification failed with exit code $LASTEXITCODE."
}

$versionOutput = Join-Path $OutputDirectory 'package-version.txt'
Set-Content -Path $versionOutput -Value $PackageVersion -Encoding ascii

Write-Host "Libmem.NET multi-architecture NuGet version: $PackageVersion"
Write-Host "Libmem.NET NuGet package: $package"
