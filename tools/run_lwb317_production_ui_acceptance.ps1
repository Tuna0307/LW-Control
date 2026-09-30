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
    $OutputPath = Join-Path $repoRoot "evidence\lwbridge-0.3.17\map\LWB317-MAP-UI-PRODUCTIONIZE-001\normal-launch-live-acceptance.json"
}
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$evidenceRoot = Split-Path -Parent $OutputPath
$screenshotRoot = Join-Path $evidenceRoot "screenshots"
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null
New-Item -ItemType Directory -Force -Path $screenshotRoot | Out-Null

function Get-ProcessCount([string]$Name) {
    return @(Get-Process -Name $Name -ErrorAction SilentlyContinue).Count
}

function Get-OptionalSha256([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    return $null
}

function Get-PackageIdentity {
    $root = Join-Path $env:USERPROFILE "AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts"
    $path = Join-Path $root "LWScripts.data"
    return [ordered]@{
        path = $path
        sha256 = Get-OptionalSha256 $path
        length = if (Test-Path -LiteralPath $path -PathType Leaf) { (Get-Item -LiteralPath $path).Length } else { $null }
    }
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return ([Net.IPEndPoint]$listener.LocalEndpoint).Port }
    finally { $listener.Stop() }
}

$preflightPath = Join-Path $evidenceRoot "current-client-v22-preflight.json"
$postflightPath = Join-Path $evidenceRoot "current-client-v22-postflight.json"
$cleanupPath = Join-Path $evidenceRoot "cleanup.json"
$identityPath = Join-Path (Split-Path -Parent $ExecutablePath) "ProductionUi\lwbridge-ui-build.json"
if (-not (Test-Path -LiteralPath $identityPath -PathType Leaf)) {
    throw "Packaged canonical UI identity not found: $identityPath"
}

$initialProcesses = [ordered]@{
    desktop = Get-ProcessCount "LWBridge.Desktop"
    lastWar = Get-ProcessCount "LastWar"
    launcher = Get-ProcessCount "LastWarLauncher"
    helper = Get-ProcessCount "LWBridge.OverviewHelper"
}
if ($initialProcesses.desktop -ne 0 -or $initialProcesses.lastWar -ne 0 -or
    $initialProcesses.launcher -ne 0 -or $initialProcesses.helper -ne 0) {
    throw "Production UI acceptance requires exclusive ownership and no pre-existing LWBridge/Last War/helper processes."
}

python (Join-Path $repoRoot "tools\lwbridge317\inspect_current_map_compat.py") `
    --require-content-version 22 `
    --finding-id LWB317-MAP-UI-PRODUCTIONIZE-001-PREFLIGHT `
    --output $preflightPath
if ($LASTEXITCODE -ne 0) { throw "Current-client v22 preflight failed with exit code $LASTEXITCODE" }
$preflight = Get-Content -Raw -LiteralPath $preflightPath | ConvertFrom-Json
$officialSha = "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22"
if ($preflight.identity.observed.contentVersion -ne 22 -or
    [string]$preflight.identity.observed.packageSha256 -ne $officialSha) {
    throw "Current-client preflight is not the official validated v22 package."
}

$packageBefore = Get-PackageIdentity
$oldBrowserArgs = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$oldNodePath = $env:NODE_PATH
$port = Get-FreeTcpPort
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$port"
if ([string]::IsNullOrWhiteSpace($env:NODE_PATH)) {
    $env:NODE_PATH = (& npm.cmd root -g).Trim()
}
$app = $null
$nodeSucceeded = $false
try {
    # Intentionally no application arguments: this is the production frontend-selection acceptance.
    $app = Start-Process -FilePath $ExecutablePath -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $targets = $null
    do {
        if ($app.HasExited) { throw "LWBridge exited before the normal WebView became ready (exit $($app.ExitCode))." }
        try {
            $targets = Invoke-RestMethod -Uri "http://127.0.0.1:$port/json" -TimeoutSec 1
            if ($targets) { break }
        } catch { Start-Sleep -Milliseconds 150 }
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $targets) { throw "WebView2 DevTools endpoint did not appear on port $port" }

    & node (Join-Path $repoRoot "tools\check_lwb317_production_ui_acceptance.cjs") `
        --port $port --output $OutputPath --screenshots $screenshotRoot
    if ($LASTEXITCODE -ne 0) { throw "Production UI normal-launch verifier failed with exit code $LASTEXITCODE" }
    $nodeSucceeded = $true
} finally {
    if ($app -and -not $app.HasExited) {
        [void]$app.CloseMainWindow()
        if (-not $app.WaitForExit(15000)) {
            Stop-Process -Id $app.Id -Force
            throw "LWBridge did not exit after normal CloseMainWindow."
        }
    }
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $oldBrowserArgs
    $env:NODE_PATH = $oldNodePath
}

Start-Sleep -Seconds 2
python (Join-Path $repoRoot "tools\lwbridge317\inspect_current_map_compat.py") `
    --require-content-version 22 `
    --finding-id LWB317-MAP-UI-PRODUCTIONIZE-001-POSTFLIGHT `
    --output $postflightPath
if ($LASTEXITCODE -ne 0) { throw "Current-client v22 postflight failed with exit code $LASTEXITCODE" }
$postflight = Get-Content -Raw -LiteralPath $postflightPath | ConvertFrom-Json
$packageAfter = Get-PackageIdentity

$stateRoot = Join-Path $env:LOCALAPPDATA "LWBridgeRebuild"
$recoveryJournals = @(Get-ChildItem -LiteralPath $stateRoot -Filter "recovery.json" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
$temporaryMapFiles = @(Get-ChildItem -LiteralPath $stateRoot -File -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^map-data\.db-(wal|shm)$' } |
    Select-Object -ExpandProperty FullName)
$finalProcesses = [ordered]@{
    desktop = Get-ProcessCount "LWBridge.Desktop"
    lastWar = Get-ProcessCount "LastWar"
    launcher = Get-ProcessCount "LastWarLauncher"
    helper = Get-ProcessCount "LWBridge.OverviewHelper"
}
$packageRestored = [string]$postflight.identity.observed.packageSha256 -eq $officialSha -and
    $packageAfter.sha256 -eq $packageBefore.sha256
$ok = $nodeSucceeded -and $packageRestored -and
    $finalProcesses.desktop -eq 0 -and $finalProcesses.lastWar -eq 0 -and
    $finalProcesses.launcher -eq 0 -and $finalProcesses.helper -eq 0 -and
    $recoveryJournals.Count -eq 0 -and $temporaryMapFiles.Count -eq 0
$cleanup = [ordered]@{
    schemaVersion = 1
    ok = [bool]$ok
    assistantOwnedProcessesTerminated = ($finalProcesses.desktop -eq 0 -and $finalProcesses.lastWar -eq 0 -and $finalProcesses.launcher -eq 0 -and $finalProcesses.helper -eq 0)
    finalProcesses = $finalProcesses
    officialPackageSha256 = $officialSha
    packageBefore = $packageBefore
    packageAfter = $packageAfter
    packageRestored = [bool]$packageRestored
    contentVersion = [int]$postflight.identity.observed.contentVersion
    recoveryJournals = $recoveryJournals
    temporaryMapWalShm = $temporaryMapFiles
    canonicalUiBuildIdentitySha256 = Get-OptionalSha256 $identityPath
}
$cleanup | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $cleanupPath -Encoding UTF8
$cleanup | ConvertTo-Json -Depth 10
if (-not $ok) { exit 2 }
