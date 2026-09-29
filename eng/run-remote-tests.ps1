[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fixturePlatform = if ($Platform -eq 'x86') { 'Win32' } else { 'x64' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild was not found.' }

& $msbuild (Join-Path $root 'tests/LoadFixture/LoadFixture.vcxproj') "/p:Configuration=$Configuration" "/p:Platform=$fixturePlatform" /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Remote load fixture build failed.' }

& dotnet run --project (Join-Path $root 'tests/LibmemCli.RemoteTests/LibmemCli.RemoteTests.csproj') -c $Configuration "-p:Platform=$Platform"
if ($LASTEXITCODE -ne 0) { throw 'Remote process tests failed.' }
