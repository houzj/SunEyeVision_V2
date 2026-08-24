@echo off
setlocal
cd /d "%~dp0"
rem ================================================================
rem  build-full.bat - 全量构建(含测试) + ScriptRefs 初始化
rem
rem  替代原 build.bat 的原因:
rem  原脚本 `dotnet build SunEyeVision.sln` 实际是失败的——
rem  SunEyeVision.sln 已损坏(二进制内容, MSBuild 报
rem  MSB5010 未找到文件格式头), 这里改用 SunEyeVision.slnx。
rem ================================================================

echo [1/2] 全量编译 (Debug) ...
dotnet build SunEyeVision.slnx --configuration Debug -v:q -tl
if errorlevel 1 goto :fail

echo [2/2] 初始化 ScriptRefs（3 个必备 DLL）...
powershell -ExecutionPolicy Bypass -File scripts\Init-ScriptRefs.ps1
if errorlevel 1 goto :fail

echo.
echo ========================================
echo 构建成功！
echo 插件目录: src\UI\bin\Debug\net9.0-windows\plugins\
echo ========================================
pause
exit /b 0

:fail
echo.
echo *** 构建失败! ***
pause
exit /b 1
