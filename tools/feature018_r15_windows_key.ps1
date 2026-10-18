param(
    [Parameter(Mandatory=$true)][string]$RunDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('Q','W','A','S')][string]$Key
)
$ErrorActionPreference = 'Stop'
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$admission = Get-Content -LiteralPath (Join-Path $run 'feature018-account-admission.json') -Raw | ConvertFrom-Json
$heartbeatPath = Join-Path $run 'overview-bridge/heartbeat.json'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Feature018R15PhysicalKey {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
function Assert-ExactGame {
    $heartbeat = Get-Content -LiteralPath $heartbeatPath -Raw | ConvertFrom-Json
    if ($admission.uid -ne '4799432499002212' -or $admission.serverId -ne 2212 -or
        $heartbeat.gameUid -ne $admission.uid -or $heartbeat.serverId -ne $admission.serverId -or
        $heartbeat.gamePid -ne $admission.gamePid -or $heartbeat.sessionId -ne $admission.sessionId -or
        !$heartbeat.loggedIn -or !$heartbeat.connected -or
        [Math]::Abs([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - $heartbeat.updatedAt) -gt 5) {
        throw 'Exact R15 sub-account/session is unavailable; no key sent'
    }
    $hwnd = [Feature018R15PhysicalKey]::GetForegroundWindow()
    [uint32]$gameWindowPid = 0
    [void][Feature018R15PhysicalKey]::GetWindowThreadProcessId($hwnd, [ref]$gameWindowPid)
    if ($gameWindowPid -ne $admission.gamePid) { throw 'Task-owned game is not foreground; no key sent' }
    foreach ($modifier in @(16,17,18,91,92)) {
        if (([Feature018R15PhysicalKey]::GetAsyncKeyState($modifier) -band 0x8000) -ne 0) {
            throw 'Modifier held; no key sent'
        }
    }
    return $hwnd.ToInt64()
}
$window = Assert-ExactGame
$virtualKey = [byte][char]$Key
if (([Feature018R15PhysicalKey]::GetAsyncKeyState($virtualKey) -band 0x8000) -ne 0) {
    throw 'Requested key already held; no key sent'
}
$started = [DateTimeOffset]::UtcNow
try {
    [Feature018R15PhysicalKey]::keybd_event($virtualKey, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
} finally {
    [Feature018R15PhysicalKey]::keybd_event($virtualKey, 0, 2, [UIntPtr]::Zero)
}
[pscustomobject]@{ key=$Key; gamePid=$admission.gamePid; hwnd=$window;
    uid=$admission.uid; serverId=$admission.serverId; keyDownUtc=$started;
    keyUpUtc=[DateTimeOffset]::UtcNow; input='Windows virtual key via windows-mcp PowerShell';
    gameAcknowledgementProven=$false } | ConvertTo-Json -Compress
