param(
    [Parameter(Mandatory = $true)][string]$SourceBundle,
    [Parameter(Mandatory = $true)][string]$ExpectedVersion,
    [Parameter(Mandatory = $true)][string]$LogPath
)

$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $sha.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

try {
    while (Get-Process -Name acad -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 2
    }

    $source = [IO.Path]::GetFullPath($SourceBundle)
    $targets = @(
        'C:\Program Files (x86)\Autodesk\ApplicationPlugins\CadHdScreenshot.bundle',
        (Join-Path $env:APPDATA 'Autodesk\ApplicationPlugins\CadHdScreenshot.bundle')
    )
    $files = @(
        'PackageContents.xml',
        'Contents\Windows\CadHdScreenshot.Core.dll',
        'Contents\Windows\CadHdScreenshot.Plugin.dll'
    )

    foreach ($target in $targets) {
        $destination = [IO.Path]::GetFullPath($target)
        if (-not $destination.EndsWith('\Autodesk\ApplicationPlugins\CadHdScreenshot.bundle',
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing unexpected destination: $destination"
        }

        foreach ($relative in $files) {
            $sourceFile = Join-Path $source $relative
            $destinationFile = Join-Path $destination $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationFile)) | Out-Null
            [IO.File]::Copy($sourceFile, $destinationFile, $true)
            if ((Get-Sha256 $sourceFile) -ne (Get-Sha256 $destinationFile)) {
                throw "Hash verification failed: $destinationFile"
            }
        }

        $manifest = [IO.File]::ReadAllText((Join-Path $destination 'PackageContents.xml'))
        if (-not $manifest.Contains('AppVersion="' + $ExpectedVersion + '"')) {
            throw "Version verification failed: $destination"
        }
    }

    [IO.File]::WriteAllText($LogPath, "DEPLOYED $ExpectedVersion " + [DateTime]::Now.ToString('O'))
}
catch {
    [IO.File]::WriteAllText($LogPath, "FAILED " + $_.Exception.ToString())
    exit 1
}
