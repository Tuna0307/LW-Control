param(
    [string]$ExecutablePath = "",
    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $repoRoot "src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe"
}
$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Release executable not found: $ExecutablePath"
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot ".codex-live\normal-release-window-check.json"
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null

$stateRoot = Join-Path $env:LOCALAPPDATA "LWBridgeRebuild"
$presentation = Join-Path $stateRoot "Presentation"
$presentationBackup = Join-Path $env:TEMP ("lwbridge-presentation-check-" + [Guid]::NewGuid().ToString("N"))
$evidenceRoot = Join-Path $env:TEMP ("lwbridge-normal-window-check-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LwbridgeNormalWindowProbe {
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
}
'@

function Get-ProcessCount([string]$Name) {
    return @(Get-Process -Name $Name -ErrorAction SilentlyContinue).Count
}

function Get-OptionalSha256([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
    return $null
}

function Remove-DirectoryWithRetry([string]$Path, [int]$TimeoutMilliseconds = 20000) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $lastError = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force
            return
        } catch {
            $lastError = $_.Exception
            Start-Sleep -Milliseconds 250
        }
    }
    $message = if ($null -eq $lastError) { "unknown error" } else { $lastError.Message }
    throw "Timed out waiting to remove $Path after WebView2 shutdown: $message"
}

function Read-SanitizedEvidence([string]$Directory) {
    $eventTypes = @()
    $blocked = @()
    $log = Get-ChildItem -LiteralPath $Directory -Filter "ui-session-*.jsonl" -File | Select-Object -First 1
    if ($null -eq $log) { return @{ eventTypes=@(); blockedCommands=@() } }
    foreach ($line in Get-Content -LiteralPath $log.FullName) {
        try {
            $row = $line | ConvertFrom-Json
            if ($row.eventType) { $eventTypes += [string]$row.eventType }
            if ($row.eventType -eq "owner-command-blocked" -and $row.details.command) {
                $blocked += [string]$row.details.command
            }
        } catch {}
    }
    return @{ eventTypes=@($eventTypes); blockedCommands=@($blocked | Sort-Object -Unique) }
}

function Invoke-NormalView([string]$View) {
    $directory = Join-Path $evidenceRoot $View
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $process = Start-Process -FilePath $ExecutablePath -ArgumentList @(
        "--view", $View, "--owner-evidence", $directory) -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        $hwnd = [IntPtr]::Zero
        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 100
            $process.Refresh()
            if ($process.HasExited) {
                throw "LWBridge exited before the $View window became ready (exit $($process.ExitCode))."
            }
            if ($process.MainWindowHandle -ne 0) {
                $hwnd = [IntPtr]$process.MainWindowHandle
                break
            }
        }
        if ($hwnd -eq [IntPtr]::Zero) { throw "LWBridge $View window did not expose a main HWND." }
        Start-Sleep -Seconds 3
        $process.Refresh()
        $messageResult = [IntPtr]::Zero
        $wmNull = [LwbridgeNormalWindowProbe]::SendMessageTimeout(
            $hwnd, 0, [IntPtr]::Zero, [IntPtr]::Zero, 2, 2000, [ref]$messageResult)
        $evidence = Read-SanitizedEvidence $directory
        return [ordered]@{
            view = $View
            responding = [bool]$process.Responding
            isHungAppWindow = [bool][LwbridgeNormalWindowProbe]::IsHungAppWindow($hwnd)
            wmNullSucceeded = ($wmNull -ne [IntPtr]::Zero)
            lastWarProcesses = Get-ProcessCount "LastWar"
            launcherProcesses = Get-ProcessCount "LastWarLauncher"
            helperProcesses = Get-ProcessCount "LWBridge.OverviewHelper"
            evidenceEvents = $evidence.eventTypes
            blockedCommands = $evidence.blockedCommands
        }
    } finally {
        if (-not $process.HasExited) {
            [void]$process.CloseMainWindow()
            if (-not $process.WaitForExit(10000)) {
                Stop-Process -Id $process.Id -Force
                throw "LWBridge $View did not exit after normal CloseMainWindow."
            }
        }
    }
}

$configPath = Join-Path $stateRoot "config.json"
$configBackupPath = Join-Path $stateRoot "config.backup.json"
$configBefore = Get-OptionalSha256 $configPath
$configBackupBefore = Get-OptionalSha256 $configBackupPath
$movedPresentation = $false
$result = $null

try {
    if ((Get-ProcessCount "LWBridge.Desktop") -ne 0) {
        throw "LWBridge.Desktop is already running; normal-window verification requires exclusive ownership."
    }
    if ((Get-ProcessCount "LastWar") -ne 0 -or (Get-ProcessCount "LastWarLauncher") -ne 0) {
        throw "Last War or its launcher is already running; refusing a passive normal-window verification."
    }

    if (Test-Path -LiteralPath $presentation) {
        Move-Item -LiteralPath $presentation -Destination $presentationBackup
        $movedPresentation = $true
    }
    New-Item -ItemType Directory -Force -Path $presentation | Out-Null

    $overview = Invoke-NormalView "overview"
    $mapData = Invoke-NormalView "map-data"
    $configAfter = Get-OptionalSha256 $configPath
    $configBackupAfter = Get-OptionalSha256 $configBackupPath
    $finalProcesses = [ordered]@{
        desktop = Get-ProcessCount "LWBridge.Desktop"
        lastWar = Get-ProcessCount "LastWar"
        launcher = Get-ProcessCount "LastWarLauncher"
        helper = Get-ProcessCount "LWBridge.OverviewHelper"
    }

    $ok = $overview.responding -and -not $overview.isHungAppWindow -and $overview.wmNullSucceeded -and
        $mapData.responding -and -not $mapData.isHungAppWindow -and $mapData.wmNullSucceeded -and
        $overview.lastWarProcesses -eq 0 -and $overview.launcherProcesses -eq 0 -and $overview.helperProcesses -eq 0 -and
        $mapData.lastWarProcesses -eq 0 -and $mapData.launcherProcesses -eq 0 -and $mapData.helperProcesses -eq 0 -and
        $configBefore -eq $configAfter -and $configBackupBefore -eq $configBackupAfter -and
        $finalProcesses.desktop -eq 0 -and $finalProcesses.lastWar -eq 0 -and
        $finalProcesses.launcher -eq 0 -and $finalProcesses.helper -eq 0

    $result = [ordered]@{
        ok = [bool]$ok
        mode = "normal-production-services-with-passive-owner-evidence"
        autoLaunchSuppressed = $true
        stateChangingOwnerCommandsBlocked = $true
        executableSha256 = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
        configSha256Unchanged = ($configBefore -eq $configAfter)
        configBackupSha256Unchanged = ($configBackupBefore -eq $configBackupAfter)
        presentationProfileHadExistingState = $movedPresentation
        presentationProfileRestoredOnSuccessfulExit = $false
        overview = $overview
        mapData = $mapData
        finalProcesses = $finalProcesses
    }
} finally {
    if (Test-Path -LiteralPath $presentation) {
        Remove-DirectoryWithRetry $presentation
    }
    if ($movedPresentation -and (Test-Path -LiteralPath $presentationBackup)) {
        Move-Item -LiteralPath $presentationBackup -Destination $presentation
    }
    if (Test-Path -LiteralPath $evidenceRoot) {
        Remove-DirectoryWithRetry $evidenceRoot
    }
    if ($null -ne $result) {
        $result.presentationProfileRestoredOnSuccessfulExit = $true
    }
}

if ($null -eq $result) { throw "Normal-window verification did not produce a result." }
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result | ConvertTo-Json -Depth 8
if (-not $result.ok) { exit 2 }
