@echo off
setlocal
where pwsh.exe >nul 2>nul
if errorlevel 1 (
  echo PowerShell 7 is required. Install it, then retry.
  pause
  exit /b 1
)
pwsh.exe -NoProfile -File "%~dp0Pack-Release.ps1" %*
set "packExit=%ERRORLEVEL%"
echo.
if not "%packExit%"=="0" echo Packaging failed. Review the error and artifacts/staging log.
pause
exit /b %packExit%
