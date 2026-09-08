$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe'
if (-not (Test-Path $exe)) {
    throw "LWBridge host-probe executable was not built: $exe"
}

$output = Join-Path ([System.IO.Path]::GetTempPath()) ("lwbridge-host-probe-{0}.json" -f [guid]::NewGuid().ToString('N'))
$process = $null
# IMPLEMENTATION POLICY: review 4 used a 55-second orchestration bound for this
# isolated diagnostic. This is a test-runner timeout, not a recovered LWBridge
# or game-runtime timeout. See evidence/lwbridge-implementation/2026-09-08-pm-review-4.json.
$waitTimeoutMs = 55000
try {
    # Start-Process handles the executable path separately; quote the path-valued
    # argument explicitly so temporary directories containing spaces are safe.
    $quotedOutput = '"' + $output + '"'
    $process = Start-Process -FilePath $exe -ArgumentList @('--host-probe', $quotedOutput) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($waitTimeoutMs)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
        throw "LWBridge host probe timed out after $waitTimeoutMs ms."
    }
    if ($process.ExitCode -ne 0) {
        throw "LWBridge host probe exited with code $($process.ExitCode)."
    }
    if (-not (Test-Path $output)) {
        throw 'LWBridge host probe did not produce its JSON report.'
    }

    $report = Get-Content -Raw $output | ConvertFrom-Json
    if ($report.ok -ne $true) {
        throw "LWBridge host probe failed: $($report | ConvertTo-Json -Depth 8 -Compress)"
    }

    $required = @(
        'preferenceRollbackErrorVisible',
        'preferenceBothFailedReconciled',
        'preferenceFailThenSuccessReconciled',
        'preferenceSuccessThenFailReconciled',
        'preferenceRecoveryClearsError',
        'closedWindowLateResponseSuppressed'
    )
    foreach ($name in $required) {
        if ($report.$name -ne $true) {
            throw "LWBridge host probe missing required pass gate: $name"
        }
    }
    if ($report.userConfigTouched -ne $false -or $report.liveGameCommandsPerformed -ne $false) {
        throw 'LWBridge host probe violated its isolation contract.'
    }

    Write-Output ($report | ConvertTo-Json -Depth 8)
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }
    Remove-Item $output -ErrorAction SilentlyContinue
}
