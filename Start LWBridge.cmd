@echo off
setlocal
set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\LWBridge.Desktop\LWBridge.Desktop.csproj"
set "APP=%ROOT%src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe"

if /I "%~1"=="--self-test" goto selftest

call :ensure_current
if errorlevel 1 exit /b %errorlevel%

start "" "%APP%"
exit /b 0

:ensure_current
set "HEAD="
for /f "usebackq delims=" %%H in (`git -C "%ROOT%" rev-parse HEAD 2^>nul`) do set "HEAD=%%H"
if not exist "%APP%" goto rebuild
if not defined HEAD exit /b 0

set "APPVER="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Get-Item -LiteralPath '%APP%').VersionInfo.ProductVersion"`) do set "APPVER=%%V"
echo %APPVER% | findstr /I /C:"%HEAD%" >nul
if not errorlevel 1 exit /b 0

:rebuild
echo Updating LWBridge Release build...
dotnet build "%PROJECT%" -c Release --nologo
if errorlevel 1 (
  echo LWBridge could not rebuild the current Release executable.
  pause
  exit /b 3
)
exit /b 0

:selftest
if not exist "%APP%" exit /b 10
set "HEAD="
for /f "usebackq delims=" %%H in (`git -C "%ROOT%" rev-parse HEAD 2^>nul`) do set "HEAD=%%H"
if defined HEAD (
  set "APPVER="
  for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Get-Item -LiteralPath '%APP%').VersionInfo.ProductVersion"`) do set "APPVER=%%V"
  echo %APPVER% | findstr /I /C:"%HEAD%" >nul
  if errorlevel 1 exit /b 11
)
echo LWBridge launcher is ready.
exit /b 0
