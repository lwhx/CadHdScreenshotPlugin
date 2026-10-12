param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. "$PSScriptRoot\common.ps1"

$testProject = "$root\tests\CadHdScreenshot.Tests\CadHdScreenshot.Tests.csproj"
$dotnet = Find-DotNetSdk

if ($dotnet) {
    & $dotnet build $testProject -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Core test build failed with exit code $LASTEXITCODE"
    }

    $testAssembly = "$root\tests\CadHdScreenshot.Tests\bin\$Configuration\net48\CadHdScreenshot.Tests.exe"
    & $testAssembly
    if ($LASTEXITCODE -ne 0) {
        throw "Core tests failed with exit code $LASTEXITCODE"
    }

    return
}

$msbuild = Find-MSBuild
& $msbuild $testProject /restore /t:Build /m /p:Configuration=$Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Core test build failed with exit code $LASTEXITCODE"
}

$testAssembly = "$root\tests\CadHdScreenshot.Tests\bin\$Configuration\net48\CadHdScreenshot.Tests.exe"
& $testAssembly
if ($LASTEXITCODE -ne 0) {
    throw "Core tests failed with exit code $LASTEXITCODE"
}
