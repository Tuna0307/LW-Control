# Live attempt 2 — shared-reader invocation boundary

Date: 2026-10-07
Disposition: FAILED_SAFE / RESTORED

A fresh canonical Home session was launched after attempt 1 had left zero selected
game/launcher processes. Home PID was 28952. The helper session was
`31f14ba6230b4e6b9ccc1066a8c2a9c5`; helper-owned game PID was 20832 and
helper launcher root PID was 43796.

The packaged helper and packaged Lua both contained the first adapter-bootstrap
correction. The real in-game wrapper again reached live authenticated state on
current server 2212 and wrote heartbeat under the isolated pilot root, but
`ready=false` with `error=unavailable`. No Map scan was started.

The helper timed out, closed only its owned game, restored the exact candidate
backup and cleared recovery. `helper-start-cleanup.json` reports
`ok=true`, `ownedGamePid=20832` and `recoveryCleared=true`. No LastWar
or launcher process remained. The installed script package was revalidated by the
subsequent current-client `check-only` run, which reported the exact supported
v22 identity and `installedFilesChanged=false`.

The live failure narrows the previously UNKNOWN boundary called out by the
OFFLINE-HOST-CONTRACT lead review: the packaged field is a .NET
`Func<string,string>`, and xLua must invoke its reflected `Invoke` member.
The correction now mirrors the already-supported reflected Connect delegate path:
it obtains `Invoke` through `safe_get`, calls it with the delegate instance,
and preserves concrete invocation/non-string failures in pipe diagnostics instead
of collapsing them to `unavailable`.

No protected-original service, authentication automation, updater action,
gameplay action, cross-server movement, recurring Auto scan, claim, share, march,
combat, plunder or spend occurred.
