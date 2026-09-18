function Find-MSBuild {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installation = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($installation) {
            $candidate = Join-Path $installation "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "MSBuild was not found. Install Visual Studio Build Tools with the .NET desktop build tools workload."
}

function Find-DotNetSdk {
    $candidates = @()

    if ($env:DOTNET_ROOT) {
        $candidates += (Join-Path $env:DOTNET_ROOT "dotnet.exe")
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        $candidates += $command.Source
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not (Test-Path $candidate)) {
            continue
        }

        $sdks = & $candidate --list-sdks
        if ($LASTEXITCODE -eq 0 -and $sdks) {
            return $candidate
        }
    }

    return $null
}

function Test-AutoCAD2024Directory {
    param([string]$Path)

    if (-not $Path -or -not (Test-Path $Path)) {
        return $false
    }

    foreach ($file in @("acad.exe", "AcCoreMgd.dll", "AcDbMgd.dll", "AcMgd.dll", "AdWindows.dll")) {
        if (-not (Test-Path (Join-Path $Path $file))) {
            return $false
        }
    }

    return $true
}

function Find-AutoCAD2024 {
    param([string]$PreferredPath)

    if ($PreferredPath) {
        if (Test-AutoCAD2024Directory $PreferredPath) {
            return [System.IO.Path]::GetFullPath($PreferredPath)
        }

        throw "The specified AutoCAD 2024 directory is invalid: $PreferredPath"
    }

    $candidates = @("C:\Program Files\Autodesk\AutoCAD 2024")

    foreach ($drive in (Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue)) {
        $candidates += (Join-Path $drive.Root "Program Files\Autodesk\AutoCAD 2024")
    }

    $registryRoot = "HKLM:\SOFTWARE\Autodesk\AutoCAD\R24.3"
    if (Test-Path $registryRoot) {
        foreach ($productKey in (Get-ChildItem $registryRoot -ErrorAction SilentlyContinue)) {
            $properties = Get-ItemProperty $productKey.PSPath -ErrorAction SilentlyContinue
            foreach ($propertyName in @("OEUPILocation", "GlobUPILocation")) {
                $location = $properties.$propertyName
                if ($location) {
                    $candidates += (Split-Path $location -Parent)
                }
            }
        }
    }

    foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        if (Test-AutoCAD2024Directory $candidate) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "AutoCAD 2024 was not found. Pass -AutoCAD2024Dir with its installation directory."
}

function Find-VSTest {
    $command = Get-Command vstest.console.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installation = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($installation) {
            $candidate = Join-Path $installation "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "VSTest Console was not found. Install Visual Studio Build Tools with the testing tools component."
}
