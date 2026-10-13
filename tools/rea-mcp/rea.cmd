@echo off
setlocal
node "%~dp0node_modules\rea-agents\scripts\rea.mjs" %*
exit /b %ERRORLEVEL%
