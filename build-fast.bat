@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
rem App正常构建引用已包含product/ToolComponents.props中的全部正式工具。
rem 用法：build-fast [all^|full] [--restore]；默认App范围，full为完整图。
set "BUILD_TARGET=src\UI\SunEyeVision.UI.csproj"
set "DO_RESTORE="
for %%A in (%*) do (
    if /i "%%~A"=="full" set "BUILD_TARGET=SunEyeVision.slnx"
    if /i "%%~A"=="--restore" set "DO_RESTORE=1"
)
if defined DO_RESTORE (
    dotnet restore "%BUILD_TARGET%" --locked-mode
    if errorlevel 1 goto :fail
)
echo 正在构建 %BUILD_TARGET%（Release）...
dotnet build "%BUILD_TARGET%" --configuration Release --no-restore
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
