@echo off
setlocal
set "ROOT=%~dp0"
set "APP=%ROOT%src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe"

if /I "%~1"=="--self-test" goto selftest

if not exist "%APP%" (
  echo LWBridge Release build is missing.
  echo Ask ChatGPT to rebuild it before starting the program.
  pause
  exit /b 2
)

start "" "%APP%"
exit /b 0

:selftest
if not exist "%APP%" exit /b 10
echo LWBridge launcher is ready.
exit /b 0
