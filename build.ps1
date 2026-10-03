[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64',
    [switch]$NativeOnly
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

git -C $root submodule update --init --recursive
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to initialize the libmem submodule and its dependencies.'
}

& (Join-Path $root 'eng\build-native.ps1') -Configuration $Configuration -Platform $Platform
if ($NativeOnly) {
    return
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'Visual Studio Installer (vswhere.exe) was not found.'
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found. Install Visual Studio with C++/CLI support.'
}

& $msbuild (Join-Path $root 'Libmem.NET.sln') /m /restore "/p:Configuration=$Configuration" "/p:Platform=$Platform"
if ($LASTEXITCODE -ne 0) {
    throw 'Libmem.NET build failed.'
}

Write-Host "Libmem.NET output: $(Join-Path $root "artifacts\managed\$Platform\$Configuration")"
