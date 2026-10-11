param(
    [Parameter(Mandatory=$true)][int]$HostPid,
    [Parameter(Mandatory=$true)][int]$GamePid,
    [Parameter(Mandatory=$true)][string]$Out,
    [Parameter(Mandatory=$true)][string]$StopFile
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $StopFile) { throw 'Pressure stop sentinel already exists' }
'utcTime,hostCpuSeconds,hostWorkingSetBytes,hostPrivateBytes,gameCpuSeconds,gameWorkingSetBytes,gamePrivateBytes' | Set-Content -LiteralPath $Out -Encoding UTF8
while (!(Test-Path -LiteralPath $StopFile)) {
    $a = Get-Process -Id $HostPid -ErrorAction SilentlyContinue
    $b = Get-Process -Id $GamePid -ErrorAction SilentlyContinue
    if ($a -and $b) {
        '{0},{1},{2},{3},{4},{5},{6}' -f (
            (Get-Date).ToUniversalTime().ToString('o'),
            [math]::Round($a.CPU,3), $a.WorkingSet64, $a.PrivateMemorySize64,
            [math]::Round($b.CPU,3), $b.WorkingSet64, $b.PrivateMemorySize64) |
                Add-Content -LiteralPath $Out -Encoding UTF8
    }
    Start-Sleep -Seconds 3
}
