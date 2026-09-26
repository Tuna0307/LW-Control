# R8-013 — restore original Map Scan Status / Stop shared-state contract

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** `map_scan_status`, shared scan-state lifecycle, successful completion, public Stop, and Clear/Stop reset separation.

## Authority

Primary authority:
- original `lwbridge-0.3.1.exe`, SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`;
- immutable original frontend assets under `evidence/lwbridge-0.3.1/frontend/assets`.

A secondary read-only evidence report supplied to the main researcher is SHA-256
`189EE292924E1702A760789E1EA4CB54FC2BF6690B689541ADD64568BE2B778C`.
Its claims used here were independently tied to original executable RVAs/strings.

## Status is the shared object

Original public `map_scan_status` calls the shared state producer directly
(`0x10ADF4 -> 0x0F7F47`) and returns that mutable object.
There is no second status-specific serializer.

Consequences:
- field presence is lifecycle-dependent;
- Stop/Clear mutate existing state rather than construct a DTO;
- frontend fallback fields are not merged into native/event state;
- exact key order is not established as a public compatibility promise.

## Recovered public fields and negative findings

Active Start directly writes the recovered run/state fields, including
`startedAt`, `lastError`, `resumeAvailable`, and the three native capture/queue fields.
World refresh additionally supplies `isInWorld`, positive `homeServerId`,
normalized season/truck-match arrays, and world/tile metadata.

Important negative findings:
- `retryCount` is frontend fallback only; no original native field literal was recovered;
- `scanStrategy` is rebuild-only public vocabulary;
- database/service `createdAt` / `updatedAt` are not proven public shared-state fields;
- `completedBlocks` and database `status` are not public aliases for `readBlocks` / `phase`.

R8-013 therefore removes those rebuild/database-derived public additions.

## Start-state lifecycle

World/server readiness and string `scanMode` validation occur before active
shared-state construction. Failed admission therefore must not synthesize a new
public `starting` or `error` scan state.

Accepted active state is constructed directly as `phase='scanning'` with:
- zero read/failed/inflight/native queue counts and zero progress/rate;
- `nativeCaptureReady=false`;
- current `startedAt`;
- `lastError=null`;
- `resumeAvailable=false`.
After protected acceptance the original writes `nativeCaptureReady=true` and publishes again.
The current-client compatibility engine models that as an explicit stored transition;
it no longer derives readiness from `isReading/phase`.

## World/server refresh

Recovered producer order is `getWorldMapState` then `getCurrentServerId`, 5 s each.
Positive current server writes `serverIdSource='live'`.
If an active scan owns another positive server, cleanup uses exact text
`current server changed during map scan` without switching the scan identity.

If current server is unavailable while reading, current scan identity is preserved.
If unavailable while idle, state falls to `serverId=0`, source `none`,
and zero tile dimensions; a positive home server is not substituted as current.

R8-013 adds a correlated read-only current-client `world-state` request.
It observes `SceneManager.IsInWorld`, current/home server, current map context,
and train-match servers without calling `ChangeToWorld`, jump, or capture paths.
Current-client `seasonServerIds` remains empty because no enumerable source is proven.

## Successful completion

Original success exposes `phase='publishing'`, then final shared-state writes:
- `isReading=false`;
- `phase='idle'`;
- `inflightBlocks=0`;
- `unreadBlocks=0`;
- `resumeAvailable=false`;
- progress 100% when total blocks are positive.

There is no recovered public `completed` phase.
## Public Stop

Stop admission is exact `isReading`, not phase.

Already idle:
- no protected/session requirement;
- no `stopMapScan`;
- no not-running error;
- common cleanup still runs and publishes status.

Active:
- protected `stopMapScan` is attempted only when the recovered bridge/session
  predicate is true;
- its exact request timeout is 5,000 ms;
- predicate false still proceeds to cleanup.

Exact public cleanup writes only:
`isReading=false`, `phase='idle'`, `inflightBlocks=0`,
`lastError=null`, `resumeAvailable=false`.

Stop preserves run ID, selected types, block counters other than inflight,
mode/concurrency, rate/progress, native queue fields, `nativeCaptureReady`,
`startedAt`, and server/world metadata. No original Map Scan `cancelling`
phase was recovered.

The compatibility engine has no original protected native scan session, so its active
Stop follows the evidenced predicate-false branch: cancel host acquisition, then apply
the exact public cleanup. No fake game-side `stopMapScan` primitive was invented.
## Clear remains a distinct broader reset

Clear is not Stop with deletion added.

After successful server-scoped deletion, the recovered reset writes:
- empty scan run ID;
- all eight original selected kinds;
- zero total/read/unread/failed/inflight counters;
- zero native pending/dropped counts;
- zero scan rate/progress;
- `startedAt=null`;
- `lastError=null`;
- `resumeAvailable=false`;
- final `phase='idle'`.

The recovered Clear reset does not write `scanMode`, `concurrency`, or
`nativeCaptureReady`; R8-013 preserves those existing values.
Refreshed server/world metadata is likewise not collapsed into the counter reset.

## Regression coverage

Deterministic coverage now proves:
- no native `retryCount` / `updatedAt` / `scanStrategy` emission;
- lifecycle-created native/start/error/resume fields;
- read-only current/home/world/tile refresh and normalized server arrays;
- unavailable current server does not become the home server;
- active current-server mismatch performs exact cleanup without identity switch;
- accepted Start preserves explicit native-ready transition;
- success publishes `publishing` then final `idle` with 100% progress;
- active Stop preserves partial counters/progress/native/start state;
- idle Stop publishes once and remains idempotent;
- Stop never exposes rebuild-only `cancelling`;
- Clear resets the recovered broad field set while preserving Fast mode/concurrency,
  native-ready state, and world dimensions;
- failed pre-active admission leaves the prior shared state untouched.

The current-client protocol regression also proves the status path writes only
`world-state.txt`; it does not invoke the mutating `world-ready.txt` route.

## Validation

Final validation passed:
- Release production build: 0 warnings / 0 errors;
- full deterministic checks: `ok=true`, `failures=[]`;
- `git diff --check` (line-ending notices only);
- R8-013 / corrected R8-012 / current-index JSON syntax;
- frontend generator reproducibility check;
- R8-008 Map Clear frontend contract regression;
- Python syntax validation of the Overview helpers;
- source-level current-client protocol regression.

A Lua interpreter is not installed on the validation machine. The optional full
Playwright frontend suite is also unavailable in this checkout because the local
Node environment has no `playwright` module. Neither limitation is used as evidence;
Lua/status transport is covered by deterministic/source checks and the generator /
targeted frontend checks above passed.

## Remaining evidence boundaries

R8-013 does not claim:
- a complete fixed process-start native field set or key order;
- exact tileX/tileY omission behavior in every world branch;
- complete stale-value removal semantics for home server;
- generic active protected-Stop error-envelope precedence;
- exact Stop/direct-completion race winner during the narrow publishing window;
- protected game-side acquisition/traversal/extraction internals;
- an enumerable current-client source for original `seasonServerIds`;
- the original Auto Scan scheduler/state machine;
- strict `map_search` parity;
- Scheduled Plunder;
- Login / Account / Authentication behavior.

The next Map parity checkpoint remains `map_search`.
