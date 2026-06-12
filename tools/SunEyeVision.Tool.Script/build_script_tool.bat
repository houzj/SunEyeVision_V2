@echo off
cd /d "D:\MyWork\SunEyeVision_V2-script"
dotnet restore "tools\SunEyeVision.Tool.Script\SunEyeVision.Tool.Script.csproj" 2>&1
dotnet build "tools\SunEyeVision.Tool.Script\SunEyeVision.Tool.Script.csproj" 2>&1
pause
