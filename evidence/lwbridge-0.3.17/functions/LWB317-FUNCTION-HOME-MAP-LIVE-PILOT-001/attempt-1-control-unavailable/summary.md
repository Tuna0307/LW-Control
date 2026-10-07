# Live attempt 1 — control bootstrap failure

Date: 2026-10-07
Disposition: FAILED_SAFE / RESTORED

Canonical Home launched the first assistant-owned session after a zero-process
preflight. Initial launcher PID 12896 and game PID 12572 were observed; launcher
settlement produced owned game PID 47332. The helper session was
`55c1d1c5764f429caf866932167a73e1`.

The in-game Overview wrapper executed far enough to write isolated-root heartbeat
with `connected=true`, `gameReady=true`, `loggedIn=true`, current
`serverId=2212`, and the live account/world observation, but `ready=false`
with `error=control_unavailable`. No Map scan was started.

The exact backup path was
`C:\Users\chimw\AppData\Local\Temp\LWB317-LIVE-PILOT-001-root\overview-bridge-backups\20261007-130015-b3a5e4c874f64e9bac37cf8a5220b13a`.
Its three restored files matched the preflight hashes exactly:

- LWScripts.data: `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`
- LWScripts.txt: `d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a`
- version.txt: `785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09`

The helper timed out, closed only the helper-owned game PID, restored the exact
backup and cleared the recovery journal. `helper-start-cleanup.json` reports
`ok=true` and `recoveryCleared=true`. No LastWar/launcher process remained.

Root cause: `ReadSharedRuntimeMetadata(control.txt)` needed the packaged .NET
reader before the control file could be reliably read, while the adapter path was
only learned from that same control file. This live-only bootstrap cycle did not
appear in offline tests. The correction propagates the exact packaged adapter path
through `LWBRIDGE_PIPE_ADAPTER_PATH` in the owned launcher environment and lets
the Lua shared-runtime reader load/validate that adapter before its first
control/lease read. Heartbeat diagnostics now retain the concrete read/bootstrap
error rather than collapsing every failure to `control_unavailable`.

No protected-original service, authentication dialog, gameplay action,
cross-server movement, claim, share, march, combat, plunder, spend, or recurring
Auto scan was used.
