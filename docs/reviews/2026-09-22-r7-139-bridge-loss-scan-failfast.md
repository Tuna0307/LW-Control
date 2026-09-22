# LWB-R7-139 — Definitive bridge loss fails an active scan immediately

**Date:** 2026-09-22
**Base revision:** `0bb8e738e99aa79c12cad3c8c907aa7aa1f9e35f`
**Scope:** close B07 with a production failure-boundary repair plus deliberate live bridge-loss acceptance.

## Failure-first live result

A real Fast Monster scan was started on authoritative server 2212. While the scan was in `phase=scanning`, the exact owned game instance was stopped through the lifecycle service.

Before the repair, the scan did fail safely and did not publish, but the engine treated definitive connection loss as an ordinary per-block capture failure. The visible terminal error degraded to:

`direct map scan contains failed batches`

The failed proof process took **65.92 s**. The source emitted a retry diagnostic after the owned bridge disappeared:

`FAST_FULL_WORLD_RETRY ... attempt=1/3 error=The current-client fast City batch did not return a correlated result.`

This exposed two problems:

1. the recovered connection-loss reason was masked by generic `INCOMPLETE_SCAN`;
2. the engine continued through block-failure handling after the exact owned session was gone.

## Production repair

`src/LWBridge.Desktop/MapScanEngine.cs`

Committed-checkout SHA-256: `256D6DCDAE171F9BFB419AFA43BF4A06CB60E96BC4B3044016D8E91DB7DD27B0`

Live-proof harness committed-checkout SHA-256: `E65E9BF967770D34BE6B30F2410AE96D24B7D9EA8F3782B338420944E14275C5`

The block-capture retry loop now treats exactly:

- code: `GAME_CONNECTION_UNAVAILABLE`
- message contract: `game connection unavailable`

as **run-terminal**.

That exception is rethrown immediately to the engine's existing terminal failure path. The run is durably failed with the original connection error.

The repair does **not** remove ordinary retry behavior:

- transient I/O/data errors still use bounded per-block retries;
- exhausted ordinary block errors still checkpoint a block failure and produce `INCOMPLETE_SCAN`;
- transient `overview_session_unavailable` remains handled inside the current-client source by its existing bounded same-session retry path;
- user cancellation still uses the Stop path rather than failure.

## Deterministic regression

`MapScanEngineChecks.DefinitiveConnectionLossFailsRunImmediately` proves:

- one source call only;
- sink sequence exactly `begin -> fail`;
- no block retry;
- no block-failure checkpoint;
- no publication;
- the exact recovered `GAME_CONNECTION_UNAVAILABLE / game connection unavailable` exception reaches the caller.

Existing generic retry/failure tests remain unchanged and passing.

The Home-close-during-scan regression was updated because its synthetic source already throws the exact recovered connection-loss contract. Its stronger expected result is now:

- first successful checkpoint remains;
- lost-session block is not retried/checkpointed as a synthetic failure;
- terminal phase is `error`;
- `failedBlocks=0`;
- `lastError=game connection unavailable`;
- durable run status is `failed` with the same error;
- prior published City data remains unchanged;
- intentional Home Close does not auto-restart the game.

## Repaired live result

The same real bridge-loss scenario was repeated after the repair.

Observed result:

- server: 2212
- scan mode: `fast`
- strategy: `current_fast_full_world_v2`
- run ID: `e451352c69074cdd9d474fe3a5616486`
- total blocks at loss: 2,500
- read blocks at loss: 0
- failed blocks at loss: 0
- durable checkpoints: 0
- final phase: `error`
- final isReading: false
- final error: `game connection unavailable`
- durable run status: `failed`
- prior published Monster dataset preserved: yes
- resumeAvailable: false
- scan wall time to terminal result: **18.3244281 s**
- whole proof runtime including lifecycle startup/cleanup: **48.70 s**

The source still logged one in-flight acquisition retry message before the lifecycle health check observed the exact owned session had disappeared. The resulting `GAME_CONNECTION_UNAVAILABLE` then crossed the engine boundary immediately; no generic failed-block fanout occurred.

## Cleanup

After the live proof:

- LastWar processes: 0
- LastWarLauncher processes: 0
- Overview helper processes: 0
- `LWScripts.data` SHA-256: `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`
- `LWScripts.txt` SHA-256: `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`
- `version.txt` SHA-256: `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`

No plunder, robbery, claim, share, spend, collection or other gameplay-consuming action was performed.

## Validation

Fresh candidate validation:

- Release build: **0 warnings / 0 errors**
- all six deterministic groups: true
- `failures=[]`
- final deterministic gameRunning: false
- final deterministic launcherRunning: false
- live B07 proof: PASS
- package restoration: exact

## Acceptance effect

**B07 -> PASS_LIVE**

A deliberate loss of the exact owned game/bridge session during a real active scan now produces a truthful terminal connection error, durable failed run and no partial publication.

Remaining read-only technical acceptance gaps:

- B11 — native add/update/remove/movement transition matrix;
- C04 — mark/unmark -> rescan -> restart -> relocate;
- C07 — vanished/replaced-target failure branches.

Population-, authorization-, multi-account- and final human-GUI-dependent gates remain separate.

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-22-r7-bridge-loss-scan-failfast.json`
