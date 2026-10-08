# BACKGROUND-CONTINUATION-001 — checks and clean-state proof

Date: 2026-10-08. Starting branch HEAD:
`3634daa30a036eaaa567553dea44e2aed65e4f71`.
All work is static/inert or read-only. No new native operation was performed.

## New archived inverse checker

- `python -m unittest discover -s tests -p 'background_pilot_readiness_checks.py' -v`
  — **5/5 PASS** (baseline archived diagnosis, inverse shifted Resource
  timestamp, positive-only pipe snapshot, mismatched PID, inverted run).
- `python tools/lwbridge317/check_background_pilot_readiness.py`
  — **PASS**, output retained as `archived-readiness.json`.
  It is not proof of host pipe acceptance or provider execution.

## Accepted local safety fences rerun

- `C:/Users/chimw/.codex/tmp/lwb317-recovery003-lua/Scripts/python.exe tests/home_runtime_file_ownership_checks.py`
  — **5/5 PASS**.
- `C:/Users/chimw/.codex/tmp/lwb317-recovery003-lua/Scripts/python.exe tests/home_runtime_lease_lua_checks.py`
  — **6/6 PASS**, lupa 2.8 / Lua 5.4.

## Fresh read-only installed file/process recheck

At this continuation, `Get-FileHash -Algorithm SHA256` of the installed
`C:/Users/chimw/AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts/`
triplet matched the lead's verified originals:

| File | Installed SHA-256 | Exact original match |
| --- | --- | --- |
| `LWScripts.data` | `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22` | YES |
| `LWScripts.txt` | `d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a` | YES |
| `version.txt` | `785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09` | YES |

`Get-CimInstance Win32_Process` filtered on
`LastWar.exe`, `LastWarLauncher.exe`,
`LWBridge.Desktop.exe` returned **0** instances.
The prior lead checkpoint separately verified restored backup manifest and
absent recovery journal; this continuation did not mutate the installed
files or open a game process. Existing backup/restore evidence is retained,
not recreated.

## Focused .NET production tests

Executed from repository root with:
`dotnet run --no-restore --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release -- <case>`.

Cases exercised:
`--map317-native-boundary-check`,
`--overview-bridge-isolated-handshake-check`,
`--overview-bridge-host-transport-check`,
`--overview-status-transport-check`,
`--production-root-isolation-check`.

See appended final results. These isolate the actual production
control planes and do not imply any original Lua, live UI, or Resource
acquisition proof.

## Final .NET results (all finished exit 0)

Executed all five cases listed above, each returning `"ok": true`:

1. `--map317-native-boundary-check` — PASS, actual Map317 native boundary harness.
2. `--overview-bridge-isolated-handshake-check` — PASS; framed hello ACK,
   OS PID/canonical path/build/token admission, inverse PID and no-hello
   rejection. This was an isolated Windows test, not an attempt-5 witness.
3. `--overview-bridge-host-transport-check` — PASS; host listener,
   connected route, correlated commands and pending-call drain on shutdown
   through the production pipe host.
4. `--overview-status-transport-check` — PASS; pending/result, blocked
   functions, timeout and disconnected codes through production status transport.
5. `--production-root-isolation-check` — PASS; default owner root stayed
   separate from the isolated temporary test root, A/B/A profile re-selection,
   real current-client provider composition with `autoSchedulerStarted=false`
   and `bridgeTransportStarted=false`.

Command sequence finished with `ALL FOCUSED PRODUCTION CHECKS PASSED`;
process exited **0**. Compilation/test runs were headless and did not
launch the game. No product code, frontend assets or native provider
implementation was changed, so frontend/package rebuild is not required
for this evidence-only checkpoint.

`git diff --check` was run before staging. Final staged verification,
commit/push and direct remote identity are recorded in Git.
