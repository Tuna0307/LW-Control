@echo off
setlocal
cd /d "%~dp0"
where pythonw.exe >nul 2>nul
if errorlevel 1 (
  echo The LWBridge recorder could not find Python. Please stop and tell ChatGPT.
  pause
  exit /b 2
)
echo Preparing the LWBridge owner check. Please wait...
echo Do not click anything until the "LWBridge owner check" message appears.
start "" /wait pythonw.exe "%~dp0tools\collect_owner_resource_evidence.py" %*
exit /b %errorlevel%
