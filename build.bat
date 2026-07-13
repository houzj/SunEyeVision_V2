@echo off
cd /d "%~dp0"
echo ========================================
echo SunEyeVision 构建脚本 (Debug模式)
echo ========================================
echo.

echo 正在编译主解决方案...
dotnet build SunEyeVision.sln --configuration Debug
if %errorlevel% neq 0 (
    echo.
    echo ========================================
    echo 编译失败！请检查错误信息。
    echo ========================================
    pause
    exit /b 1
)

echo.
echo 正在初始化 ScriptRefs（3 个必备 DLL）...
powershell -ExecutionPolicy Bypass -File scripts\Init-ScriptRefs.ps1

echo.
echo ========================================
echo 构建成功！
echo ========================================
echo.
echo 插件目录: src\UI\bin\Debug\net9.0-windows\plugins\
echo 运行应用程序请使用: run_app.bat
pause
