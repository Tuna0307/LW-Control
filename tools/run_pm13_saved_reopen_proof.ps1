param(
    [Parameter(Mandatory=$true)][string]$ProofPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$AppPath = '',
    [int]$Port = 0
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($AppPath)) {
    $AppPath = Join-Path $PSScriptRoot '..\src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe'
}
$ProofPath = [IO.Path]::GetFullPath($ProofPath)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$AppPath = [IO.Path]::GetFullPath($AppPath)
if (!(Test-Path $ProofPath)) { throw "Proof file not found: $ProofPath" }
if (!(Test-Path $AppPath)) { throw "Release app not found: $AppPath" }
if ($Port -eq 0) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $Port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port; $listener.Stop()
}
$screenshotPath = [IO.Path]::Combine(
    [IO.Path]::GetDirectoryName($OutputPath),
    [IO.Path]::GetFileNameWithoutExtension($OutputPath) + '-reopen.png')
$oldBrowserArgs = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$oldNodePath = $env:NODE_PATH
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$Port"
$app = $null
try {
    $app = Start-Process -FilePath $AppPath -ArgumentList '--view','map-data','--language','en' -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    $targets = $null
    do {
        try {
            $targets = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json" -TimeoutSec 1
            if ($targets) { break }
        } catch { Start-Sleep -Milliseconds 150 }
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$targets) { throw "WebView2 DevTools endpoint did not appear on port $Port" }

    if (!(Test-Path env:NODE_PATH) -or [string]::IsNullOrWhiteSpace($env:NODE_PATH)) {
        $env:NODE_PATH = (& npm.cmd root -g).Trim()
    }
    & node (Join-Path $PSScriptRoot 'check_pm13_saved_reopen.cjs') `
        --port $Port --proof $ProofPath --output $OutputPath --screenshot $screenshotPath
    if ($LASTEXITCODE -ne 0) { throw "Saved reopen verifier failed with exit code $LASTEXITCODE" }
} finally {
    if ($app -and !$app.HasExited) {
        $app.Refresh(); [void]$app.CloseMainWindow(); [void]$app.WaitForExit(5000)
    }
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $oldBrowserArgs
    $env:NODE_PATH = $oldNodePath
}
