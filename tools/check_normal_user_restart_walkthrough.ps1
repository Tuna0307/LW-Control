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
    $OutputPath = Join-Path $repoRoot ".codex-live\normal-user-restart-walkthrough.json"
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPath) | Out-Null

$stateRoot = Join-Path $env:LOCALAPPDATA "LWBridgeRebuild"
$configPath = Join-Path $stateRoot "config.json"
$configBackupPath = Join-Path $stateRoot "config.backup.json"
$tempRoot = Join-Path $env:TEMP ("lwbridge-normal-user-walkthrough-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LwbridgeUserWalkthroughInput {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
}
'@

function Get-OptionalSha256([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }
    return $null
}

function Set-TemporaryAutoLaunchSuppression([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($null -eq $value.PSObject.Properties["autoLaunchGame"]) {
        $value | Add-Member -NotePropertyName "autoLaunchGame" -NotePropertyValue $false
    } else {
        $value.autoLaunchGame = $false
    }
    $json = $value | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
}

function Get-ProcessCount([string]$Name) {
    return @(Get-Process -Name $Name -ErrorAction SilentlyContinue).Count
}

function Get-PackageIdentity {
    $root = Join-Path $env:USERPROFILE "AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts"
    $result = [ordered]@{
        version = $null
        files = [ordered]@{}
    }
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { return $result }
    $versionPath = Join-Path $root "version.txt"
    if (Test-Path -LiteralPath $versionPath -PathType Leaf) {
        $result.version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    }
    foreach ($name in @("LWScripts.data", "LWScripts.txt", "version.txt")) {
        $path = Join-Path $root $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $item = Get-Item -LiteralPath $path
            $result.files[$name] = [ordered]@{
                length = $item.Length
                sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            }
        }
    }
    return $result
}

function Wait-ReadyWindow($Process) {
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 150
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "LWBridge exited before its normal window became ready (exit $($Process.ExitCode))."
        }
        if ($Process.MainWindowHandle -ne 0) {
            Start-Sleep -Seconds 5
            $Process.Refresh()
            $hwnd = [IntPtr]$Process.MainWindowHandle
            $messageResult = [IntPtr]::Zero
            $wmNull = [LwbridgeUserWalkthroughInput]::SendMessageTimeout(
                $hwnd, 0, [IntPtr]::Zero, [IntPtr]::Zero, 2, 2000, [ref]$messageResult)
            if ($wmNull -eq [IntPtr]::Zero -or [LwbridgeUserWalkthroughInput]::IsHungAppWindow($hwnd)) {
                throw "LWBridge normal window is not responsive."
            }
            return $hwnd
        }
    }
    throw "LWBridge normal window did not expose a main HWND."
}

function Invoke-ClientClick([IntPtr]$Hwnd, [int]$X, [int]$Y) {
    [void][LwbridgeUserWalkthroughInput]::SetForegroundWindow($Hwnd)
    Start-Sleep -Milliseconds 250
    $point = New-Object LwbridgeUserWalkthroughInput+POINT
    $point.X = $X
    $point.Y = $Y
    if (-not [LwbridgeUserWalkthroughInput]::ClientToScreen($Hwnd, [ref]$point)) {
        throw "ClientToScreen failed for normal-user click."
    }
    [void][LwbridgeUserWalkthroughInput]::SetCursorPos($point.X, $point.Y)
    [LwbridgeUserWalkthroughInput]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [LwbridgeUserWalkthroughInput]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}

function Save-ClientCapture([IntPtr]$Hwnd, [string]$Path) {
    $rect = New-Object LwbridgeUserWalkthroughInput+RECT
    if (-not [LwbridgeUserWalkthroughInput]::GetClientRect($Hwnd, [ref]$rect)) {
        throw "GetClientRect failed for normal-user capture."
    }
    $origin = New-Object LwbridgeUserWalkthroughInput+POINT
    $origin.X = 0
    $origin.Y = 0
    if (-not [LwbridgeUserWalkthroughInput]::ClientToScreen($Hwnd, [ref]$origin)) {
        throw "ClientToScreen failed for normal-user capture."
    }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen($origin.X, $origin.Y, 0, 0, $bitmap.Size)
        } finally {
            $graphics.Dispose()
        }
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }

    return [ordered]@{
        width = $width
        height = $height
        sha256 = Get-OptionalSha256 $Path
        bytes = (Get-Item -LiteralPath $Path).Length
    }
}

function Get-CapturePixel([string]$Path, [int]$X, [int]$Y) {
    $bitmap = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        $pixel = $bitmap.GetPixel($X, $Y)
        return [ordered]@{ r=[int]$pixel.R; g=[int]$pixel.G; b=[int]$pixel.B }
    } finally {
        $bitmap.Dispose()
    }
}

function Test-ActiveBlue($Pixel) {
    return $Pixel.r -lt 80 -and $Pixel.g -ge 70 -and $Pixel.g -le 180 -and $Pixel.b -gt 180
}

function Close-Normally($Process, [string]$Label) {
    if ($Process.HasExited) { return }
    if (-not $Process.CloseMainWindow()) {
        throw "$Label CloseMainWindow returned false."
    }
    if (-not $Process.WaitForExit(12000)) {
        Stop-Process -Id $Process.Id -Force
        throw "$Label failed to exit after normal window close."
    }
}

function Invoke-Calibration {
    $directory = Join-Path $tempRoot "calibration"
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $process = Start-Process -FilePath $ExecutablePath -ArgumentList @("--owner-evidence", $directory) -PassThru
    try {
        $hwnd = Wait-ReadyWindow $process
        Invoke-ClientClick $hwnd 94 229
        Start-Sleep -Seconds 7
    } finally {
        Close-Normally $process "calibration"
    }

    $events = @()
    $log = Get-ChildItem -LiteralPath $directory -Filter "ui-session-*.jsonl" -File | Select-Object -First 1
    if ($null -eq $log) { throw "Owner-evidence calibration produced no session log." }
    foreach ($line in Get-Content -LiteralPath $log.FullName) {
        try {
            $row = $line | ConvertFrom-Json
            if ($row.eventType) { $events += [string]$row.eventType }
        } catch {}
    }
    if ($events -notcontains "city-search-response" -or $events -notcontains "city-render-observation") {
        throw "The calibrated Map Data click did not produce the real saved City search/render path."
    }

    return [ordered]@{
        applicationArguments = @("--owner-evidence", "<temporary-read-only-directory>")
        clickClient = [ordered]@{ x=94; y=229 }
        eventTypes = @($events)
        mapDataCorrelated = $true
    }
}

function Invoke-NormalUserRun([string]$Label) {
    $directory = Join-Path $tempRoot $Label
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $process = Start-Process -FilePath $ExecutablePath -PassThru
    try {
        $hwnd = Wait-ReadyWindow $process

        $overviewPath = Join-Path $directory "overview.png"
        $mapPath = Join-Path $directory "map-data.png"
        $returnPath = Join-Path $directory "overview-return.png"

        $overview = Save-ClientCapture $hwnd $overviewPath
        Invoke-ClientClick $hwnd 94 229
        Start-Sleep -Seconds 6
        $mapData = Save-ClientCapture $hwnd $mapPath
        Invoke-ClientClick $hwnd 94 143
        Start-Sleep -Seconds 4
        $overviewReturn = Save-ClientCapture $hwnd $returnPath

        $overviewPixel = Get-CapturePixel $overviewPath 23 143
        $overviewMapPixel = Get-CapturePixel $overviewPath 23 229
        $mapOverviewPixel = Get-CapturePixel $mapPath 23 143
        $mapPixel = Get-CapturePixel $mapPath 23 229
        $returnPixel = Get-CapturePixel $returnPath 23 143
        $returnMapPixel = Get-CapturePixel $returnPath 23 229

        if (-not (Test-ActiveBlue $overviewPixel) -or (Test-ActiveBlue $overviewMapPixel)) {
            throw "$Label initial Overview active-nav indicator did not match the shipped sidebar."
        }
        if ((Test-ActiveBlue $mapOverviewPixel) -or -not (Test-ActiveBlue $mapPixel)) {
            throw "$Label Map Data click did not move the active-nav indicator to Map Data."
        }
        if (-not (Test-ActiveBlue $returnPixel) -or (Test-ActiveBlue $returnMapPixel)) {
            throw "$Label Overview return click did not restore the active-nav indicator."
        }
        if ($overview.sha256 -eq $mapData.sha256 -or $mapData.sha256 -eq $overviewReturn.sha256) {
            throw "$Label navigation did not change rendered client content."
        }

        return [ordered]@{
            label = $Label
            processId = $process.Id
            applicationArguments = @()
            responding = [bool]$process.Responding
            overview = [ordered]@{
                capture = $overview
                activeIndicatorPixel = $overviewPixel
                mapIndicatorPixel = $overviewMapPixel
            }
            mapData = [ordered]@{
                capture = $mapData
                overviewIndicatorPixel = $mapOverviewPixel
                activeIndicatorPixel = $mapPixel
            }
            overviewReturn = [ordered]@{
                capture = $overviewReturn
                activeIndicatorPixel = $returnPixel
                mapIndicatorPixel = $returnMapPixel
            }
        }
    } finally {
        Close-Normally $process $Label
    }
}

$configBefore = Get-OptionalSha256 $configPath
$configBackupBefore = Get-OptionalSha256 $configBackupPath
$configOriginalCopy = Join-Path $tempRoot "config.json.original"
$configBackupOriginalCopy = Join-Path $tempRoot "config.backup.json.original"
$hadConfig = Test-Path -LiteralPath $configPath -PathType Leaf
$hadConfigBackup = Test-Path -LiteralPath $configBackupPath -PathType Leaf
$packageBefore = Get-PackageIdentity
$result = $null

try {
    if ((Get-ProcessCount "LWBridge.Desktop") -ne 0 -or
        (Get-ProcessCount "LastWar") -ne 0 -or
        (Get-ProcessCount "LastWarLauncher") -ne 0 -or
        (Get-ProcessCount "LWBridge.OverviewHelper") -ne 0) {
        throw "Normal-user walkthrough requires exclusive ownership and no pre-existing LWBridge/game processes."
    }
    if (-not $hadConfig) {
        throw "Normal-user walkthrough requires an existing user config so auto-launch can be suppressed and restored exactly."
    }

    Copy-Item -LiteralPath $configPath -Destination $configOriginalCopy
    if ($hadConfigBackup) {
        Copy-Item -LiteralPath $configBackupPath -Destination $configBackupOriginalCopy
    }
    Set-TemporaryAutoLaunchSuppression $configPath
    Set-TemporaryAutoLaunchSuppression $configBackupPath
    $acceptanceConfigBefore = Get-OptionalSha256 $configPath
    $acceptanceConfigBackupBefore = Get-OptionalSha256 $configBackupPath

    $calibration = Invoke-Calibration
    if ((Get-ProcessCount "LWBridge.Desktop") -ne 0) {
        throw "Calibration LWBridge process remained after normal close."
    }

    $run1 = Invoke-NormalUserRun "run1"
    Start-Sleep -Seconds 2
    if ((Get-ProcessCount "LWBridge.Desktop") -ne 0) {
        throw "First normal-user LWBridge process remained after normal close."
    }
    $run2 = Invoke-NormalUserRun "run2"
    Start-Sleep -Seconds 2

    $acceptanceConfigAfter = Get-OptionalSha256 $configPath
    $acceptanceConfigBackupAfter = Get-OptionalSha256 $configBackupPath
    $packageAfter = Get-PackageIdentity
    $finalProcesses = [ordered]@{
        desktop = Get-ProcessCount "LWBridge.Desktop"
        lastWar = Get-ProcessCount "LastWar"
        launcher = Get-ProcessCount "LastWarLauncher"
        helper = Get-ProcessCount "LWBridge.OverviewHelper"
    }

    $packageSame = (($packageBefore | ConvertTo-Json -Depth 8 -Compress) -eq
                    ($packageAfter | ConvertTo-Json -Depth 8 -Compress))
    $ok = $packageSame -and
        $finalProcesses.desktop -eq 0 -and
        $finalProcesses.lastWar -eq 0 -and
        $finalProcesses.launcher -eq 0 -and
        $finalProcesses.helper -eq 0

    $result = [ordered]@{
        ok = [bool]$ok
        mode = "built-release-zero-argument-normal-user-restart-walkthrough"
        executableSha256 = (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash
        acceptanceApplicationArguments = @()
        acceptanceUsesViewFlag = $false
        acceptanceUsesCaptureOrProbeMode = $false
        input = [ordered]@{
            mechanism = "Win32 foreground mouse click"
            mapDataClient = [ordered]@{ x=94; y=229 }
            overviewClient = [ordered]@{ x=94; y=143 }
        }
        calibration = $calibration
        run1 = $run1
        run2 = $run2
        restartBoundary = [ordered]@{
            firstProcessClosedNormally = $true
            secondProcessIdDiffers = ($run1.processId -ne $run2.processId)
            presentationProfileNotIsolated = $true
        }
        autoLaunchSuppressed = $true
        acceptanceConfigSha256Unchanged = ($acceptanceConfigBefore -eq $acceptanceConfigAfter)
        acceptanceConfigBackupSha256Unchanged = ($acceptanceConfigBackupBefore -eq $acceptanceConfigBackupAfter)
        configSha256Unchanged = $null
        configBackupSha256Unchanged = $null
        packageIdentityUnchanged = [bool]$packageSame
        package = $packageAfter
        finalProcesses = $finalProcesses
        safety = [ordered]@{
            navigationOnly = $true
            mapScanInvoked = $false
            followInvoked = $false
            attackOrPlunder = $false
            claimOrCollect = $false
            messaging = $false
        }
    }
} finally {
    if ($hadConfig -and (Test-Path -LiteralPath $configOriginalCopy -PathType Leaf)) {
        Copy-Item -LiteralPath $configOriginalCopy -Destination $configPath -Force
    } elseif (-not $hadConfig -and (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        Remove-Item -LiteralPath $configPath -Force
    }
    if ($hadConfigBackup -and (Test-Path -LiteralPath $configBackupOriginalCopy -PathType Leaf)) {
        Copy-Item -LiteralPath $configBackupOriginalCopy -Destination $configBackupPath -Force
    } elseif (-not $hadConfigBackup -and (Test-Path -LiteralPath $configBackupPath -PathType Leaf)) {
        Remove-Item -LiteralPath $configBackupPath -Force
    }
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($null -eq $result) { throw "Normal-user restart walkthrough produced no result." }
$result.configSha256Unchanged = ($configBefore -eq (Get-OptionalSha256 $configPath))
$result.configBackupSha256Unchanged = ($configBackupBefore -eq (Get-OptionalSha256 $configBackupPath))
$result.ok = [bool]($result.ok -and $result.configSha256Unchanged -and $result.configBackupSha256Unchanged)
$result | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result | ConvertTo-Json -Depth 14
if (-not $result.ok) { exit 2 }
