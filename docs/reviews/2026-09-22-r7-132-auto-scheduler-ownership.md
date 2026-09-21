# LWB-R7-132 — Auto Scan single-owner navigation/reconnect closure

**Date:** 2026-09-22
**Scope:** close the remaining R8 Auto Scan navigation/Refresh Status/reconnect duplicate-cycle and confirmed-travel ownership gap.
**Overall status:** **IMPLEMENTED/OFFLINE-TESTED** against the generated shipped frontend in real headless Microsoft Edge. No official-game state-changing action was performed.

## Base and source identity

R7-132 starts from pushed R7-131 revision `542119c0c54ed6c6e12571207b4e196376a795a8` on `research/offline-controller`.

Current R7-132 source identities:
- `tools/build_lwbridge_frontend.py` SHA-256 `1D316B66C92294B57B5BC510402DA054367C6BF5678908AF96FC8F5617D23DA9`
- generated `src/LWBridge.Desktop/WebUi/assets/index-sfL2sT3K.js` SHA-256 `67742732E126B677D8D1DADD01E862B94BDC253C77CB7B1B292337B49DB828F9`
- `tools/check_scan_strategy_auto.cjs` SHA-256 `6D1B2C7867B90B2DB8C6EE791D17F215885A6DED16C10E690BA1253D646414F7`
- `tools/check_map_auto_r7132.cjs` SHA-256 `4C578C8BBDD776ED112719C67285F047DACB393852F5771C49D4BEE5909B7F5A`

Local browser acceptance used Playwright 1.63.0 from the existing temporary verification installation and Microsoft Edge 153.0.4234.32. Repository CI remains pinned to Playwright 1.62.1 and now runs the R7-132 regression in the established browser-verification job.

## Finding R7-132-F1 — connection-state effect teardown could re-admit one due Auto cycle

**IMPLEMENTED/OFFLINE-TESTED.**

Failure-first browser reproduction used the real generated top-level scheduler with a contract-faithful preview backend:
1. Auto Scan was enabled with `nextRunAt=0`.
2. Target 2212 started once.
3. While that scan was active, the fixture changed the authoritative Home transport state to disconnected and invoked the shipped **Refresh Status** path.
4. The first scan was then completed while disconnected.
5. The transport was restored and **Refresh Status** invoked again.
6. After the scheduler polling window, two `map_scan_start` calls existed for the same due cycle.

The pre-fix generated scheduler effect depended on `[selectedProfileId, P]`, where `P` is the current connected state. Disconnect tore down the effect while its cycle was active. Its cleanup set the old closure's cancellation flag, so the old `finally` did not persist a new `nextRunAt`. Reconnect then created a new effect with the same still-due persisted configuration, which admitted the cycle again.

R7-132 keeps the scheduler effect owned only by profile. Current connectivity is mirrored through `autoOnlineRef` and consulted at cycle admission and target boundaries without recreating the effect.

## Finding R7-132-F2 — target ownership is rechecked after confirmed travel

**IMPLEMENTED/OFFLINE-TESTED.**

R7-132 checks scheduler cancellation, Auto enabled state and `autoOnlineRef.current`:
- before each configured target;
- immediately after `server_jump` resolves and before `map_scan_start`.

This closes the race where connectivity can disappear while a server jump is awaiting authoritative arrival confirmation. The travel request may finish, but the target scan is not admitted after online ownership has been lost.

## Browser acceptance matrix

The permanent `tools/check_map_auto_r7132.cjs` drives three scenarios.

### Navigation + repeated Refresh Status
- Configured targets: 2212, 2213.
- First target starts once.
- UI navigates away from Map Data to Overview.
- **Refresh Status** is invoked three times.
- UI navigates back to Map Data.
- First scan completes.
- Second target's `server_jump` is deliberately held pending.
- No second `map_scan_start` is allowed before jump confirmation.
- After confirmation, exactly one second scan starts.
- Final target order is exactly 2212 then 2213.
- No duplicate cycle appears.
- `nextRunAt` advances to the future.

### Disconnect at target boundary + reconnect
- First target starts once.
- Transport changes to disconnected through the shipped Refresh Status path.
- First scan completes while disconnected.
- No second-target travel is admitted.
- The interrupted cycle advances `nextRunAt` to the future.
- Transport reconnects through Refresh Status.
- After another scheduler polling interval, total `map_scan_start` count remains exactly one.
- The interrupted target list is not retroactively resumed.

### Disconnect while server jump is pending
- First target completes.
- Jump to target 2213 begins and is held pending.
- Transport changes to disconnected before jump confirmation.
- Jump is then allowed to return.
- The jump request is permitted to finish, but no second `map_scan_start` occurs.
- The cycle advances to its next future deadline.

These scenarios additionally verify the generated bundle retains the profile-only scheduler dependency and the pre-target/post-jump online guards.

## Regression integration

R7-132 strengthens existing source-contract tests rather than weakening them:
- `tools/check_scan_strategy_auto.cjs` now requires the online-ref pre-target and post-jump guards.
- `tests/LWBridge.Desktop.Checks/Program.cs` updates the R7-130 failure-isolation and core Auto scheduler source guards to require the same stronger ownership semantics.
- `.github/workflows/csharp.yml` now runs `node tools/check_map_auto_r7132.cjs`.

The existing R7-130 per-target failure isolation remains intact: a target-local jump/scan failure is still caught and the configured cycle continues while the scheduler remains enabled and online.

## Validation

Clean detached R7-132 worktree:
- canonical frontend generation/check — pass;
- generated index syntax — pass;
- `tools/check_scan_strategy_auto.cjs` — pass;
- `tools/check_map_data_r7131.cjs` — pass;
- `tools/check_map_auto_r7132.cjs` — pass;
- interaction-only frontend browser suite — 4 checks pass;
- Release build — **0 warnings / 0 errors**;
- all six deterministic groups — true, `failures=[]`;
- full frontend browser suite — **36 checks pass**;
- City Export removal browser regression — pass;
- visual comparison — 32 pairs, 28 strict + 4 intentional Map Data override pairs; after isolated recapture of one transient Hotkeys rendering pixel, all 28 strict pairs are pixel-identical with `strictMaxMae=0` and `strictMaxHighDeltaRate=0`. The four existing intentional Map Data deltas remain within their prior bounds.

The same R7-132 generated bundle and permanent browser check were then ported to the main worktree without replacing pre-existing diagnostic WIP, and the main worktree regenerated byte-for-byte and reran `check_map_auto_r7132.cjs` successfully.

## Acceptance boundary

R7-132 closes the outstanding R8 navigation/Refresh Status/reconnect duplicate-cycle and pre-confirmed-travel ownership item at generated-frontend/browser scope. Full desktop/game restart persistence and unattended deadline execution were already LIVE-PROVEN by R7-039 and are not re-claimed here.

This checkpoint does not change the remaining product gates:
- Ghost positive-row live proof remains population/date dependent.
- Supplies positive-row proof remains population dependent.
- explicitly authorized state-changing Truck/Dispatch/Alliance/Treasure acceptance remains open.
- simultaneous real multi-account UI population remains a separate final evidence gap.
- final normal-user built-executable restart/walkthrough remains open.
