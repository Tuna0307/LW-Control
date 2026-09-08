param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\.codex-live\lwbridge-desktop-captures'))
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\src\LWBridge.Desktop\bin\Release\net10.0-windows10.0.17763.0\LWBridge.Desktop.exe')).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
foreach ($view in @('overview', 'automation', 'map-data', 'march', 'city-layout', 'hotkeys', 'mini-games', 'advanced', 'settings')) {
    $capture = Join-Path $outputRoot "$view.png"
    $p = Start-Process -FilePath $exe -ArgumentList '--capture', ('"' + $capture + '"'), '--view', $view -WindowStyle Hidden -PassThru
    if (-not $p.WaitForExit(30000)) {
        Stop-Process -Id $p.Id
        throw "Desktop capture timed out: $view"
    }
    if ($p.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $capture)) { throw "Desktop capture failed: $view" }
    $report = Get-Content -LiteralPath ([IO.Path]::ChangeExtension($capture, '.json')) -Raw | ConvertFrom-Json
    if ($report.errors.Count -ne 0) { throw "UI error in ${view}: $($report.errors -join ', ')" }
    if ([string]::IsNullOrWhiteSpace($report.text)) { throw "Empty UI: $view" }
    if ($report.commands | Where-Object { $_ -match '^auth_' }) { throw "Unexpected auth request: $view" }
    Write-Output "Passed desktop capture: $view"
}
