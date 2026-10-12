$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml
$source = Join-Path $PSScriptRoot '..\src\CadHdScreenshot.Plugin\CadHdScreenshot.Plugin.ScreenshotWindow.xaml'
$markup = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $source).Path)
$markup = $markup -replace 'x:Class="[^"]+"', ''
$window = [Windows.Markup.XamlReader]::Parse($markup)
$window.FindName('InputMethodTab').IsSelected = $true
$window.FindName('InputMethodPanel').DataContext = [pscustomobject]@{
    Enabled=$false; Diagnostics=$false; Status='已关闭（不干预输入法）'; Detail=''
}
$content = $window.Content
$window.Content = $null
$content.Background = $window.Background
$size = [Windows.Size]::new(414, 522)
$content.Measure($size)
$content.Arrange([Windows.Rect]::new($size))
$content.UpdateLayout()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(621,783,144,144,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($content)
$png = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$output = Join-Path $PSScriptRoot '..\artifacts\ime-panel.png'
$stream = [IO.File]::Create($output)
try { $png.Save($stream) } finally { $stream.Dispose() }
Write-Output "PANEL_RENDER: $output"
