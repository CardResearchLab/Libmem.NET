[CmdletBinding()]
param(
    [string]$Channel = '8.0'
)

$ErrorActionPreference = 'Stop'

if (-not $env:RUNNER_TEMP) {
    throw 'RUNNER_TEMP is required for the CI x86 runtime setup.'
}
if (-not $env:GITHUB_ENV) {
    throw 'GITHUB_ENV is required for the CI x86 runtime setup.'
}

$installDirectory = Join-Path $env:RUNNER_TEMP 'dotnet-x86'
$installer = Join-Path $env:RUNNER_TEMP 'dotnet-install-x86.ps1'

Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer

& $installer -Channel $Channel -Runtime dotnet -Architecture x86 -InstallDir $installDirectory -NoPath
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to install the x86 .NET runtime.'
}

$fxrRoot = Join-Path $installDirectory 'host\fxr'
$hostFxr = Get-ChildItem $fxrRoot -Recurse -Filter 'hostfxr.dll' -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $hostFxr) {
    throw "x86 hostfxr.dll was not found below $fxrRoot."
}

# Architecture-specific variables take precedence over the generic DOTNET_ROOT
# set by actions/setup-dotnet. Set both names used by Windows .NET host probing.
"DOTNET_ROOT_X86=$installDirectory" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
"DOTNET_ROOT(x86)=$installDirectory" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append

Write-Host "x86 .NET runtime: $installDirectory"
Write-Host "x86 hostfxr: $($hostFxr.FullName)"
