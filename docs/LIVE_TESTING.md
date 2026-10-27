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
  Its default refuses every account except the approved test pairs.
- `live_ui_deploy.py`: hot-swaps a rebuilt UI into a running test copy (UI only).
- `map_015_package_ui.py`, `map_017_audit.py`: packaged Map proof and receipt auditing.

Live runs need the owner's go-ahead for the session. The owner authorized task-owned app/game
start/stop for assigned work; that does not cover owner sessions.
