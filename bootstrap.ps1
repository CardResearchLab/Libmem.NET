[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipWrapperBuild
)

$ErrorActionPreference = 'Stop'
$arguments = @('-Configuration', $Configuration)
if ($SkipWrapperBuild) {
    $arguments += '-NativeOnly'
}

& (Join-Path $PSScriptRoot 'build.ps1') @arguments
exit $LASTEXITCODE
