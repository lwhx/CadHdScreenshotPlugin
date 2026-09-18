@echo off
chcp 65001 >nul
setlocal

tasklist /FI "IMAGENAME eq acad.exe" 2>nul | find /I "acad.exe" >nul
if not errorlevel 1 (
    echo AutoCAD 正在运行，请先正常保存图纸并关闭 AutoCAD。
    pause
    exit /b 1
)

set "INSTALL_SCRIPT=%~dp0eng\install-user.ps1"
if not exist "%INSTALL_SCRIPT%" (
    echo 未找到安装脚本：%INSTALL_SCRIPT%
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%" -BundlePath "%~dp0artifacts\CadHdScreenshot.bundle"
if errorlevel 1 (
    echo.
    echo 安装失败，请检查上面的错误信息。
    pause
    exit /b 1
)

echo.
echo CAD 高清截图插件安装成功。重新打开 AutoCAD 即可使用。
pause
