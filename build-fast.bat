@echo off
setlocal
cd /d "%~dp0"
rem ================================================================
rem  build-fast.bat - 日常快速构建（跳过 13 个测试项目）
rem
rem  用法:
rem    build-fast            只编译主程序依赖链（最快, ~30s）
rem    build-fast all        主程序 + 全部工具插件（App.slnx）
rem    build-fast full       全量构建含测试（SunEyeVision.slnx）
rem    build-fast --restore  新增 NuGet 包 / 首次构建时先恢复再编译
rem ================================================================

set RESTORE=--no-restore
set EXTRA=-v:q -tl /p:RunAnalyzers=false

if "%1"=="--restore" set RESTORE=

if "%1"=="full" (
    echo [全量] 构建 SunEyeVision.slnx ^(含测试^)...
    dotnet build SunEyeVision.slnx -c Debug %RESTORE% %EXTRA%
    goto :end
)

if "%1"=="all" (
    echo [主程序+插件] 构建 SunEyeVision.App.slnx ...
    dotnet build SunEyeVision.App.slnx -c Debug %RESTORE% %EXTRA%
    if errorlevel 1 goto :fail
    goto :end
)

echo [快速] 构建主程序依赖链 ...
dotnet build src\UI\SunEyeVision.UI.csproj -c Debug %RESTORE% %EXTRA%
if errorlevel 1 goto :fail
echo 主程序构建完成。

:end
echo.
echo 提示: 首次构建或新增 NuGet 包时, 请改用 build-fast --restore
pause
exit /b 0

:fail
echo.
echo *** 构建失败! ***
pause
exit /b 1
