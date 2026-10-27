# Live testing rules

These rules come from the owner and apply to every human or AI working on the real game.
If a newer owner instruction conflicts with them, the newer instruction wins. Record it here.

## Accounts

| Account | UID / server | Allowed |
|---|---|---|
| **Test sub-account** | UID `4799432499002212`, server 2212 | Normal in-game actions for testing: marches, attacks (see below), claims, items, in-game currency/gems, relocation, City/Equipment changes, mini-games. |
| **Owner's main account** | also on server 2212 | **Read-only.** Map scans and passive observation only. One-off exceptions need the owner's explicit approval for that test only (example: on 2026-10-26 the owner approved up to four gathering sends/recalls on main, for one test). |

- **Lock every action to the exact UID, not the server.** Main and the test account share
  server 2212. Check the UID before every action; on mismatch, stop.
- Earlier test accounts (`3133546747002289`/2289, server 2288, `1183884595002296`/2296) are retired.
  Do not act on them unless the owner selects one again.

## Never

- Real-money purchases or entering payment details.
- Changing login, password, binding or email, or deleting an account.
- **Pressing or testing Unlock Land Cell**, which breaks an account whose tutorial is unfinished.
- Disabling, patching, hooking or evading the anti-cheat (ACE), or bypassing login/licensing.
- Attacking players who are in **any alliance**, or the owner's main account. Only guildless
  players, monsters and empty resource tiles are allowed targets. If a target's alliance
  can't be read, don't send.
- Applying for official positions 10002/10003/10004.
- Chat or mail to other players, unless the feature under test requires it (keep it minimal
  and neutral).
- Changing the game language. It stays Chinese; only the app UI language may change.
- Taking control of the owner's windows (mouse, global keyboard, focus, minimize, rearrange).
  Live work runs in the background.

## Always

- Start and stop only processes **your own run** created (exact PID + path + start time). Never
  stop an owner session.
- Back up the three original game script files before injecting. Restore them byte-for-byte after
  the game has exited, then confirm no recovery journal or game process remains.
- Never replay an uncertain send (no automatic retry of a request that may have reached the server).
- Never adopt marches, squads or sessions that your run did not create.
- Keep raw receipts, which include UIDs, march IDs and screenshots, in git-ignored `artifacts/`.
  Commit only sanitized summaries.
- Report honestly. "Feature unavailable today" (no event, no target, no idle squad) is not a pass.
  Controlled tests are not live evidence.
- If a tool call or action is denied, don't retry or reroute it. Do other work and report the denial.

## Harness

`tools/live-harness/` holds the scripts that drove the real packaged app (Windows only):

- `run_ui.py <scenario>`: starts an isolated copy of the packaged UI (`LWBRIDGE_MAP014_UI_EXE` or
  `artifacts/map014-takeover/publish/`) against the live game and runs a proof scenario (Map kinds,
  Auto, Clear, `feature018-tabs`, …). It uses the app's `--*-proof` switches. It expects some
  earlier trusted receipts under `artifacts/` on the original dev machine, so read it before
  reusing it elsewhere.
- `run_owned.py <scenario>`: native-only scan/benchmark runs through `Native.csproj`/`Program.cs`.
- `feature018_live_queue.py`: sends one operation to an already running isolated proof session.
  It refuses every account except the current test account.
- `feature018_live.py` (+ `feature018_live_performance.py`, `feature018_live_recall_cleanup.py`):
  one background packaged-UI run of the Automation/Squads services on the test account, with
  timing receipts and a read-only check that earlier gathering trips came home.
- `tabs_checkpoint.py`: the all-tabs sweep (`feature018-tabs` with `LWBRIDGE_R29_SWEEP_PROOF`),
  sampling memory/CPU with `process_sampler.py` and `measure_phases.py`.
- `home_stability_*.py`: Home connection soak, fault-injection and route-cycle campaigns, and
  their summary.
- `live_ui_deploy.py`: hot-swaps a rebuilt UI into a running test copy (UI only).
- `map_015_package_ui.py`, `map_017_audit.py`, `audit_*.py`, `compare_*.py`: packaged Map proof
  and receipt auditing.

## Proof switches

The packaged app only runs proof code when a switch or variable asks for it. Driver = the script
in `tools/` that sets it. "None" means no script in the repository drives it any more; those are
candidates for removal (STATUS.md follow-ups).

| Switch / variable | Driver |
|---|---|
| `--isolated-root`, `--view`, `--language`, `--theme`, `--capture` | `run_ui.py` and `home_stability_*.py`; `--view` and `--capture` also `release/check_packaged_home.ps1` |
| `--normal-ui-live-map-proof` | `run_ui.py` |
| `LWBRIDGE_MAP014_*`, `LWBRIDGE_MAP016_*` (except `NO_GAME_UI_PROOF`), `LWBRIDGE_MAP017_UI_MODE`, `LWBRIDGE_STARTUP_HOME_PROOF`, `LWBRIDGE_FEATURE018_UI_PROOF`, `LWBRIDGE_FEATURE018_R2_PROOF` | `run_ui.py` (`LWBRIDGE_MAP014_NEW_KINDS_UI` via `map_015_package_ui.py`) |
| `LWBRIDGE_FEATURE018_STAGED_PROOF`, `_BACKGROUND_ONLY`, `_PROOF_UID`, `_PROOF_SERVER`, `LWBRIDGE_R29_PERF_PROOF` | `feature018_live.py`, `tabs_checkpoint.py` |
| `LWBRIDGE_R29_SWEEP_PROOF` | `tabs_checkpoint.py` |
| `LWBRIDGE_HOME004_ISOLATED_ROUTE_FAULT` | `home_stability_faults.py`, `home_stability_route_cycles.py` |
| `LWBRIDGE_HOME004_RECOVERY_TRACE`, `LWBRIDGE_HOME_STABILITY_TRACE`, `LWBRIDGE_HOME004_F06_DISPATCH_TRACE` | None (manual trace switches) |
| `--city-reopen-proof`, `--first-live-result`, `--home-map-campaign-*`, `--host-probe`, `--live-city-proof`, `--live-probe`, `--live-resource-proof`, `--map-ui-integration-proof`, `--map010-native-names-proof`, `--normal-ui-live-resource-proof`, `--owner-evidence`, `--startup-state-proof`, `--ui-root` | None |
| `LWBRIDGE_CHAT_CATALOG_*`, `LWBRIDGE_FEATURE018_R22_*`, `LWBRIDGE_FEATURE018_R3_*_OPT_IN`, `LWBRIDGE_MAP016_NO_GAME_UI_PROOF`, `LWBRIDGE_R30_*`, `LWBRIDGE_STARTUP_GATHER_PROOF` | None |

`LWBRIDGE_BUILD_ID`, `LWBRIDGE_INSTANCE_ID`, `LWBRIDGE_PIPE_TOKEN`, `LWBRIDGE_PROFILE_ID` and
`LWBRIDGE_REBUILD_DATA_ROOT` are not proof switches: the app sets them for the game it launches.

Live runs need the owner's go-ahead for the session. The owner authorized task-owned app/game
start/stop for assigned work; that does not cover owner sessions.
