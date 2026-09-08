$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe'
if (-not (Test-Path $exe)) {
    throw "LWBridge host-probe executable was not built: $exe"
}

$output = Join-Path ([System.IO.Path]::GetTempPath()) ("lwbridge-host-probe-{0}.json" -f [guid]::NewGuid().ToString('N'))
try {
    $process = Start-Process -FilePath $exe -ArgumentList @('--host-probe', $output) -Wait -PassThru
    if (-not (Test-Path $output)) {
        throw "LWBridge host probe did not produce its JSON report (exit $($process.ExitCode))."
    }

    $report = Get-Content -Raw $output | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or $report.ok -ne $true) {
        throw "LWBridge host probe failed (exit $($process.ExitCode)): $($report | ConvertTo-Json -Depth 8 -Compress)"
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
    Remove-Item $output -ErrorAction SilentlyContinue
}
