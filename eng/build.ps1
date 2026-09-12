param(
    [string]$Configuration = "Release",
    [string]$AutoCAD2024Dir = "C:\Program Files\Autodesk\AutoCAD 2024"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$required = @("AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AdWindows.dll")

foreach ($dll in $required) {
    $path = Join-Path $AutoCAD2024Dir $dll
    if (-not (Test-Path $path)) {
        throw "Missing AutoCAD 2024 assembly: $path"
    }
}

dotnet restore "$root\CadHdScreenshotPlugin.sln"
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

dotnet test "$root\tests\CadHdScreenshot.Core.Tests\CadHdScreenshot.Core.Tests.csproj" -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }

$msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue)
if (-not $msbuild) {
    throw "MSBuild was not found. Run this script from a Visual Studio 2022 Developer PowerShell or add MSBuild to PATH."
}

& $msbuild.Source "$root\CadHdScreenshotPlugin.sln" /m /p:Configuration=$Configuration /p:AutoCAD2024Dir="$AutoCAD2024Dir"
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

$bundle = "$root\artifacts\CadHdScreenshot.bundle"
Remove-Item $bundle -Recurse -Force -ErrorAction SilentlyContinue
New-Item "$bundle\Contents\Windows" -ItemType Directory -Force | Out-Null
Copy-Item "$root\packaging\CadHdScreenshot.bundle\PackageContents.xml" "$bundle\PackageContents.xml"
Copy-Item "$root\src\CadHdScreenshot.Plugin\bin\$Configuration\net48\CadHdScreenshot.Plugin.dll" "$bundle\Contents\Windows\"
Copy-Item "$root\src\CadHdScreenshot.Core\bin\$Configuration\net48\CadHdScreenshot.Core.dll" "$bundle\Contents\Windows\"
Write-Host "Bundle created: $bundle"
