param(
    [switch]$RunTerminationFailureSelfTest
)

$ErrorActionPreference = 'Stop'

function Stop-OwnedProcessBounded {
    param(
        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory)]
        [int]$WaitTimeoutMs,

        [scriptblock]$StopProcessAction = {
            param([int]$ProcessId)
            Stop-Process -Id $ProcessId -Force -ErrorAction Stop
        }
    )

    $stopError = $null
    try {
        & $StopProcessAction $Process.Id
    }
    catch {
        $stopError = $_.Exception
    }

    $exited = $Process.WaitForExit($WaitTimeoutMs)
    if (-not $exited) {
        $detail = if ($null -ne $stopError) {
            " Termination command failed: $($stopError.Message)"
        }
        else {
            ''
        }
        throw "Owned process $($Process.Id) did not exit within $WaitTimeoutMs ms after cleanup was requested.$detail"
    }

    if ($null -ne $stopError) {
        throw "Termination command failed for owned process $($Process.Id): $($stopError.Message)"
    }
}

# IMPLEMENTATION POLICY: cleanup gets a separate five-second orchestration bound
# after a forced-stop request. This prevents the test helper from hanging if
# termination fails; it is not a recovered LWBridge/game-runtime timeout.
$cleanupWaitTimeoutMs = 5000

if ($RunTerminationFailureSelfTest) {
    $hostPath = (Get-Process -Id $PID).Path
    $testProcess = Start-Process `
        -FilePath $hostPath `
        -ArgumentList @('-NoLogo', '-NoProfile', '-Command', 'Start-Sleep -Seconds 30') `
        -WindowStyle Hidden `
        -PassThru
    try {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $observedError = $null
        try {
            Stop-OwnedProcessBounded `
                -Process $testProcess `
                -WaitTimeoutMs 100 `
                -StopProcessAction {
                    param([int]$ProcessId)
                    throw "SIMULATED_TERMINATION_FAILURE:$ProcessId"
                }
        }
        catch {
            $observedError = $_.Exception
        }
        finally {
            $stopwatch.Stop()
        }

        if ($null -eq $observedError) {
            throw 'Termination-failure self-test expected cleanup to fail.'
        }
        if ($observedError.Message -notmatch 'SIMULATED_TERMINATION_FAILURE') {
            throw "Termination-failure self-test lost the stop error: $($observedError.Message)"
        }
        if ($stopwatch.ElapsedMilliseconds -gt 2000) {
            throw "Termination-failure self-test exceeded its bounded observation window: $($stopwatch.ElapsedMilliseconds) ms."
        }

        Write-Output ([pscustomobject]@{
            ok = $true
            terminationFailureReported = $true
            bounded = $true
            elapsedMs = $stopwatch.ElapsedMilliseconds
        } | ConvertTo-Json)
    }
    finally {
        if (-not $testProcess.HasExited) {
            Stop-Process -Id $testProcess.Id -Force -ErrorAction Stop
            if (-not $testProcess.WaitForExit($cleanupWaitTimeoutMs)) {
                throw "Termination-failure self-test could not clean up its owned process $($testProcess.Id)."
            }
        }
    }
    return
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe'
if (-not (Test-Path $exe)) {
    throw "LWBridge host-probe executable was not built: $exe"
}

$output = Join-Path ([System.IO.Path]::GetTempPath()) ("lwbridge-host-probe-{0}.json" -f [guid]::NewGuid().ToString('N'))
$process = $null
$primaryError = $null
$cleanupError = $null
$renderedReport = $null
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

    $renderedReport = $report | ConvertTo-Json -Depth 8
}
catch {
    $primaryError = $_.Exception
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        try {
            Stop-OwnedProcessBounded -Process $process -WaitTimeoutMs $cleanupWaitTimeoutMs
        }
        catch {
            $cleanupError = $_.Exception
        }
    }
    Remove-Item $output -ErrorAction SilentlyContinue
}

if ($null -ne $primaryError) {
    if ($null -ne $cleanupError) {
        throw "$($primaryError.Message) Cleanup also failed: $($cleanupError.Message)"
    }
    throw $primaryError
}
if ($null -ne $cleanupError) {
    throw $cleanupError
}

Write-Output $renderedReport
