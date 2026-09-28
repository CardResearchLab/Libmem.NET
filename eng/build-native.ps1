[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'third_party\libmem'
$stage = Join-Path $root "artifacts\native\$Platform\$Configuration"

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw 'Visual Studio Installer (vswhere.exe) was not found.'
}
$visualStudioPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudioPath) {
    throw 'Visual Studio C++ build tools were not found.'
}
$developerCommand = Join-Path $visualStudioPath 'Common7\Tools\VsDevCmd.bat'
if (-not (Test-Path $developerCommand)) {
    throw "Visual Studio developer command script was not found: $developerCommand"
}
$environmentLines = & $env:ComSpec /d /s /c "`"$developerCommand`" -no_logo -arch=$Platform >nul && set"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to initialize the Visual Studio $Platform build environment."
}
foreach ($line in $environmentLines) {
    $separator = $line.IndexOf('=')
    if ($separator -gt 0) {
        $name = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        Set-Item -Path "Env:$name" -Value $value
    }
}
$build = Join-Path $root ".build\libmem-nmake\$Platform\$Configuration"

if (-not (Test-Path (Join-Path $source 'CMakeLists.txt'))) {
    throw 'The libmem submodule is missing. Run git submodule update --init --recursive.'
}

git -C $source submodule update --init --recursive
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to initialize nested libmem dependencies.'
}

& cmake -S $source -B $build -G 'NMake Makefiles' "-DCMAKE_BUILD_TYPE=$Configuration" -DLIBMEM_BUILD_TESTS=OFF -DLIBMEM_BUILD_STATIC=OFF
if ($LASTEXITCODE -ne 0) {
    throw 'CMake configuration for libmem failed.'
}

& cmake --build $build --parallel
if ($LASTEXITCODE -ne 0) {
    throw 'Native libmem build failed.'
}

$configurationOutput = $build
$importLibrary = Get-ChildItem $configurationOutput -Recurse -Filter 'libmem.lib' -File | Select-Object -First 1
$nativeDll = Get-ChildItem $configurationOutput -Recurse -Filter 'libmem.dll' -File | Select-Object -First 1
if (-not $importLibrary -or -not $nativeDll) {
    throw "libmem build succeeded, but libmem.lib or libmem.dll was not found below $configurationOutput."
}

$includeDestination = Join-Path $stage 'include\libmem'
$libraryDestination = Join-Path $stage 'lib'
$binaryDestination = Join-Path $stage 'bin'
New-Item -ItemType Directory -Force -Path $includeDestination, $libraryDestination, $binaryDestination | Out-Null
Copy-Item (Join-Path $source 'include\libmem\*.h') $includeDestination -Force
Copy-Item $importLibrary.FullName (Join-Path $libraryDestination 'libmem.lib') -Force
Copy-Item $nativeDll.FullName (Join-Path $binaryDestination 'libmem.dll') -Force

Write-Host "Native libmem staged at: $stage"
