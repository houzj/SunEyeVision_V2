@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
if /i "%~1"=="--restore" (
    dotnet restore SunEyeVision.slnx --locked-mode
    if errorlevel 1 goto :fail
)
echo 正在构建正式完整解决方案（Release）...
dotnet build SunEyeVision.slnx --configuration Release --no-restore
if errorlevel 1 goto :fail
where pwsh >nul 2>&1
if errorlevel 1 goto :fail
echo 正在生成本次构建的正式载荷清单...
pwsh -NoProfile -ExecutionPolicy Bypass -File installer\Seal-ReleasePayload.ps1 -AppPath "%~dp0Release\SunEyeVision_v1.0.0\SunEyeVision"
if errorlevel 1 goto :fail
echo 构建完成：Release\SunEyeVision_v1.0.0\SunEyeVision
if not defined CI pause
exit /b 0
:fail
set "BUILD_EXIT=%errorlevel%"
echo 构建失败，退出码 %BUILD_EXIT%。
if not defined CI pause
exit /b %BUILD_EXIT%
