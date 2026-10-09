@echo off
call "%~dp0Pack-Release.cmd" -Local %*
exit /b %ERRORLEVEL%
