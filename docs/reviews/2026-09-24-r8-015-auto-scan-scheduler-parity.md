# R8-015 — restore original Auto Scan scheduler/state machine

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 frontend/executable contract
**Scope:** Auto Scan persisted configuration, Run Now semantics, scheduler admission/effect ownership, multi-server cycle flow, Normal/Fast ownership, and Auto panel controls.

## Authority

Primary authority:
- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- immutable `evidence/lwbridge-0.3.1/frontend/assets/index-sfL2sT3K.js`;
- immutable `evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js`;
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md` for the shared `map_scan_start`, status, and `server_jump` public boundaries.

R8-015 restores the final generated Auto helper block, scheduler-owned state segment, scheduler effect, and Auto card directly from those immutable frontend bytes. `tools/check_map_auto_r8015.cjs` compares those blocks byte-for-byte.

## Persisted Auto configuration

Original default Auto state is:
- `enabled=false`;
- `intervalMinutes=60`;
- `serverIds=[]`;
- `selectedTypes=[truck, railway, dispatch, ghost, treasure]`;
- `scanMode=fast`;
- `returnToOriginalServer=true`;
- `nextRunAt=0`.

Normalization clamps interval to 20..1440 minutes, normalizes at most 20 valid server IDs, filters selected types through the original eight-kind allowlist with the original Auto-type fallback, accepts exact `normal` and otherwise normalizes Auto mode to `fast`, defaults return-to-origin true, and clamps `nextRunAt` to a nonnegative integer.

Configuration remains stored under exact key `lwbridge.mapAutoScan.<profileId>`.

## Enable / Run Now / scheduler admission

The original config update helper owns deadlines:
- transition disabled -> enabled: `nextRunAt=Date.now()`;
- disabled state: `nextRunAt=0`;
- otherwise preserve normalized configuration.

The Auto panel Run Now button is disabled when offline, Auto is disabled, an Auto cycle is already running, or a map scan is active. Clicking it only writes `nextRunAt=Date.now()`; there is no independent one-shot request field.

Exact scheduler due predicate is:
`enabled && online && !mapScanActive && !autoCycleRunning && now >= nextRunAt`.

The scheduler effect is owned by `[selectedProfileId, connected]`, invokes immediately, then polls every 5,000 ms. A connection-state change therefore tears down/recreates the effect exactly as original; R7's reconnect-stable `autoOnlineRef` owner policy is removed.

## Cycle execution

At admission the scheduler marks Auto running, reads current scan status, and uses that state's `serverId` as the original server. Target servers are the configured list when nonempty, otherwise the current positive server.

For each target, in order:
1. stop the loop if the effect has been cleaned up or Auto was disabled;
2. call `server_jump(target)`;
3. log switched/already-current result;
4. call `map_scan_start({selectedTypes, scanMode, resume:false})`;
5. poll `map_scan_status` every 2 seconds until `isReading=false`, with a 2,700,000 ms (45 minute) deadline;
6. log terminal `lastError` when present, otherwise log completion.

A terminal status with `lastError` is logged and the target loop proceeds. By contrast, thrown jump/start/wait failures are not caught per target in the original; they escape to the single outer cycle catch and therefore stop later targets for that cycle. R7's per-target exception isolation and synthetic completed/failed cycle summary are removed.

## Finalization and UI

If the effect is still live and `returnToOriginalServer=true` with a positive original server, the original attempts `server_jump(originalServer)` in `finally` and logs success/failure. When the effect is still live, it always advances the persisted next deadline with `now + intervalMinutes * 60000`, clears Auto-running state, and refreshes Map summary.

The original has no restart-cycle marker, no restart recovery routine, no independent one-shot field, and no dedicated Auto Stop button. Disabling Auto changes the persisted scheduler state; the target loop observes `Je.current.enabled` between targets. The ordinary Manual Stop control remains the recovered map-scan Stop surface.

The restored Auto card is byte-identical to immutable 0.3.1 and includes:
- Enable automatic scanning checkbox/status;
- target-server editor/chips;
- 20..1440 minute interval input;
- Normal/Fast select bound to `S.scanMode`;
- original eight-kind scan type choices with the final-item disable guard;
- Return after Auto Scan checkbox;
- Run Now button that requires Auto enabled and sets `nextRunAt=Date.now()`;
- navigation notice and next-run display.

## Removed R7 owner policies

R8-015 removes from final generated behavior:
- Auto `scanMode` removal / backend-selected speed policy;
- per-target exception isolation and synthetic completed/failed summary;
- `autoOnlineRef` reconnect-stable scheduler ownership;
- `lwbridge.mapAutoScanCycle.*` restart markers and restart recovery;
- `runOnceRequestedAt` independent one-shot scheduling;
- `autoCycleRequested` mid-cycle ownership helper;
- dedicated Auto Stop control;
- `liveServerId` fallback inserted into the top-level current-server prop for this scheduler workflow.

The historical transforms remain in the generator source as evidence, followed by the explicit R8-015 immutable-byte rollback.

## Validation

Completed before checkpoint packaging:
- `tools/check_map_auto_r8015.cjs`: PASS, including byte-for-byte block comparison against immutable 0.3.1;
- frontend generator reproducibility: PASS;
- full deterministic checks: `ok=true`, `failures=[]`.

Final Release build, whitespace/JSON integrity, staged-diff check, and clean-tree commit verification are recorded in the machine-readable evidence after the final run.

## Remaining boundaries

R8-015 restores the original **frontend Auto scheduler/control state machine** and its calls into already-recovered public Map commands. It does not claim:
- original protected map acquisition/traversal/extraction internals;
- remaining protected Normal/Fast pacing/retry implementation beyond public mode/concurrency;
- full `server_jump` protected travel implementation;
- Scheduled Plunder;
- unresolved `map_search` alternate-sort internals;
- Login / Account / Authentication behavior.

The next retained Map checkpoint is Scheduled Plunder control-plane/UI restoration, while protected scan internals remain a separate evidence-bound lane.
