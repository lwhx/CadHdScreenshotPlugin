param([string]$BundlePath)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $BundlePath) {
    $BundlePath = "$root\artifacts\CadHdScreenshot.bundle"
}

$BundlePath = [System.IO.Path]::GetFullPath($BundlePath)
if (-not (Test-Path "$BundlePath\PackageContents.xml")) {
    throw "Bundle was not found or is incomplete: $BundlePath. Run .\eng\build.ps1 first."
}

$pluginsDirectory = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$destination = Join-Path $pluginsDirectory "CadHdScreenshot.bundle"
$expectedDestination = [System.IO.Path]::GetFullPath($destination)

New-Item $pluginsDirectory -ItemType Directory -Force | Out-Null

if (Test-Path $expectedDestination) {
    $resolved = (Resolve-Path $expectedDestination).Path
    if ($resolved -ne $expectedDestination) {
        throw "Refusing to remove unexpected installation path: $resolved"
    }

    Remove-Item $expectedDestination -Recurse -Force
}

Copy-Item $BundlePath $expectedDestination -Recurse
Write-Host "Installed: $expectedDestination"
