$ErrorActionPreference = "Stop"
$pluginsDirectory = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$destination = [System.IO.Path]::GetFullPath(
    (Join-Path $pluginsDirectory "CadHdScreenshot.bundle"))

if (-not (Test-Path $destination)) {
    Write-Host "CadHdScreenshot is not installed for the current user."
    return
}

$resolved = (Resolve-Path $destination).Path
if ($resolved -ne $destination) {
    throw "Refusing to remove unexpected installation path: $resolved"
}

Remove-Item $destination -Recurse -Force
Write-Host "Uninstalled: $destination"
