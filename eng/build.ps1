param(
    [string]$Configuration = "Release",
    [string]$AutoCAD2024Dir,
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$required = @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AdWindows.dll")
. "$PSScriptRoot\common.ps1"

$AutoCAD2024Dir = Find-AutoCAD2024 $AutoCAD2024Dir
Write-Host "AutoCAD 2024: $AutoCAD2024Dir"

foreach ($dll in $required) {
    $path = Join-Path $AutoCAD2024Dir $dll
    if (-not (Test-Path $path)) {
        throw "Missing AutoCAD 2024 assembly: $path"
    }
}

& "$PSScriptRoot\test-core.ps1" -Configuration $Configuration

$dotnet = Find-DotNetSdk
$autoCadProperty = "-p:AutoCAD2024Dir=$AutoCAD2024Dir"

if ($dotnet) {
    & $dotnet build "$root\CadHdScreenshotPlugin.sln" -c $Configuration $autoCadProperty
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
}
else {
    $msbuild = Find-MSBuild
    & $msbuild "$root\CadHdScreenshotPlugin.sln" /restore /m /p:Configuration=$Configuration "/p:AutoCAD2024Dir=$AutoCAD2024Dir"
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }
}

$bundle = "$root\artifacts\CadHdScreenshot.bundle"
Remove-Item $bundle -Recurse -Force -ErrorAction SilentlyContinue
New-Item "$bundle\Contents\Windows" -ItemType Directory -Force | Out-Null
Copy-Item "$root\packaging\CadHdScreenshot.bundle\PackageContents.xml" "$bundle\PackageContents.xml"
Copy-Item "$root\src\CadHdScreenshot.Plugin\bin\$Configuration\net48\CadHdScreenshot.Plugin.dll" "$bundle\Contents\Windows\"
Copy-Item "$root\src\CadHdScreenshot.Core\bin\$Configuration\net48\CadHdScreenshot.Core.dll" "$bundle\Contents\Windows\"
Write-Host "Bundle created: $bundle"

if ($Install) {
    if (Get-Process acad -ErrorAction SilentlyContinue) {
        throw "AutoCAD is running. Close AutoCAD before installing the updated plug-in."
    }

    & "$PSScriptRoot\install-user.ps1" -BundlePath $bundle
}
