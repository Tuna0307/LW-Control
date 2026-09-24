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
if not exist "%APP%" goto rebuild
powershell -NoProfile -Command "$head=(& git -C '%ROOT%' rev-parse HEAD 2>$null); if($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)){exit 0}; $head=$head.Trim(); $ver=(Get-Item -LiteralPath '%APP%').VersionInfo.ProductVersion; if($ver -like ('*'+$head+'*')){exit 0}; exit 1"
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
powershell -NoProfile -Command "$head=(& git -C '%ROOT%' rev-parse HEAD 2>$null); if($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)){exit 0}; $head=$head.Trim(); $ver=(Get-Item -LiteralPath '%APP%').VersionInfo.ProductVersion; if($ver -like ('*'+$head+'*')){exit 0}; exit 1"
if errorlevel 1 exit /b 11
echo LWBridge launcher is ready.
exit /b 0
