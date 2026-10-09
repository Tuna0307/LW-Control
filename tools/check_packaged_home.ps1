param(
    [string]$Executable = (Join-Path $PSScriptRoot '../src/LWBridge.Desktop/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.exe'),
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path ([IO.Path]::GetTempPath()) ('lwbridge-home-smoke-' + [guid]::NewGuid().ToString('N'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$capture = Join-Path $OutputDirectory 'home.png'
$beforeRoots = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'lwbridge-capture-probe-*' | ForEach-Object FullName)
# Capture uses the explicit inert fixture composition, never the live launch path.
$process = Start-Process -FilePath $Executable -ArgumentList @('--capture', ('"' + $capture + '"'), '--view', 'overview') -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(30000)) { throw "Home capture did not exit: PID $($process.Id)" }
if ($process.ExitCode -ne 0) { throw "Home capture failed: exit $($process.ExitCode)" }
if (!(Test-Path -LiteralPath $capture)) { throw 'Home capture image missing' }
$bytes = [IO.File]::ReadAllBytes($capture)
if ($bytes.Length -lt 24 -or [BitConverter]::ToString($bytes, 0, 8) -ne '89-50-4E-47-0D-0A-1A-0A') { throw 'Home capture is not a PNG' }
$diagnostics = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($capture, '.json')) -Raw | ConvertFrom-Json
if ($diagnostics.view -ne 'overview') { throw 'Wrong captured route' }
if ($diagnostics.mode -ne 'fixture' -or !$diagnostics.homeRendered) { throw 'Isolated fixture Home was not rendered' }
if (@($diagnostics.errors).Count -ne 0) { throw 'Capture reported frontend failures' }
$newRoots = @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'lwbridge-capture-probe-*' | Where-Object { $_.FullName -notin $beforeRoots })
if ($newRoots.Count -ne 0) { throw 'Temporary capture configuration/browser root was not removed' }
[pscustomobject]@{ok=$true;mode='isolated-inert-capture';capture=$capture;exitCode=$process.ExitCode;temporaryRootsRemaining=0} | ConvertTo-Json
