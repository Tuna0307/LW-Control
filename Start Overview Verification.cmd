@echo off
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0"
set "APP=%OUT%\LWBridge.Desktop.exe"
set "HELPER=%OUT%\OverviewBridge\run_overview_bridge.py"

if /I "%~1"=="--self-test" goto selftest
if not exist "%APP%" (
  echo LWBridge verification build is missing.
  echo Ask ChatGPT to rebuild it before testing.
  exit /b 2
)
start "" "%APP%"
exit /b 0

:selftest
if not exist "%APP%" exit /b 10
if not exist "%HELPER%" exit /b 11
python "%HELPER%" check-only >nul
if errorlevel 1 exit /b 12
echo Overview verification entry point is ready.
exit /b 0
