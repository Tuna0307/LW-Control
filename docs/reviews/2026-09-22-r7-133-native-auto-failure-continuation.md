# LWB-R7-133 — Native Auto travel-failure continuation proof

**Date:** 2026-09-22
**Scope:** close the remaining native travel-failure evidence gap for Auto Scan without driving a state-changing gameplay action.
**Status:** **LIVE-PROVEN backend continuation + browser-proven scheduler handling**.

## Base

R7-133 starts from pushed R7-132 revision `b16020e564b67b0736c99c3a7cfe7d23326f159b` on `research/offline-controller`.

The production scheduler code is unchanged. R7-133 adds only a bounded live proof harness:
`tests/LWBridge.Desktop.Checks/LiveAutoNativeFailureContinuationProof.cs`
and one opt-in command-line entry point:
`--live-auto-native-failure-continuation`.

## Live native failure

The proof launched one owned current-client session on home server 2212 and targeted server 2148, which prior read-only exploration had shown as outside the currently reachable travel range.

The public `server_jump({serverId:2148})` call failed through the real current-v20 game path with:
- code: `SERVER_JUMP_FAILED`
- message: `server_jump_precheck_failed`
- visited target: none
- authoritative post-failure server: 2212

The proof then issued a same-server public jump to 2212 and required the normal no-op result, demonstrating that the failed native travel attempt did not corrupt server context or the owned bridge session.

## Same-session continuation

Without restarting LWBridge or the game, the harness immediately executed the next valid Auto target path through the same public scan service:
- selected type: `zombie_boss`
- caller scan mode: omitted
- backend-selected mode: `fast`
- strategy: `current_fast_zombie_boss_lod2_v1`
- concurrency: 20
- total/read blocks: 2500 / 2500
- failed/unread blocks: 0 / 0
- wall time: 5.5303191 s
- current population rows: 0

Zero Zombie Boss rows is accepted here because this checkpoint proves failure isolation and continuation, not positive population. The exact scan lifecycle and coverage gate completed successfully.

## Relationship to the real Auto scheduler

R7-133 does not claim that the React/WebView scheduler itself was live-driven through the 2148 failure. That would require native GUI/DOM control not available through the authorized remote connector.

Instead the evidence is deliberately split:
- R7-132 real-browser regression proves the shipped scheduler catches one target's jump/scan failure, continues only while still enabled/online, and does not duplicate cycles across navigation/refresh/reconnect.
- R7-133 live-proves that the public native travel command can produce the exact real `server_jump_precheck_failed` failure and that the same owned service/session remains healthy enough to run the next valid 2500-block scan immediately afterward.

Together these close the previously documented gap between synthetic scheduler failure handling and a real native travel failure, without claiming an unobserved live UI click sequence.
## Cleanup and restoration

After the proof:
- `LastWar=0`
- launcher count = 0
- Overview helper count = 0
- `LWScripts.data` SHA-256 = `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`
- `LWScripts.txt` SHA-256 = `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`
- `version.txt` SHA-256 = `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`

These match the original restored-package hashes used by the current project handoff.

## Safety boundary

No Treasure claim, Supplies scout, Truck robbery, Dispatch plunder, Alliance share, message, spend, collection, or other state-changing gameplay action was executed.

The only live operations were the already-established owned lifecycle, one rejected server-jump precheck, one same-server no-op, and a read-only map scan.

## Remaining limits

The final normal-user built-executable walkthrough is still open because the available remote connector has no mouse/screen interaction primitive. Ghost and Supplies positive-row gates remain population-dependent. Authorization-gated gameplay actions remain open.

Treasure protected-loader recovery remains blocked by the preserved SB-79 restriction and was not replayed or rerouted.
