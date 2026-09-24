# R8-010 — restore original map_summary strict parity

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** public `map_summary` state/count source selection and result envelope.

## Authority

Primary authority is `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`.

Recovered original success result is exactly:

`{serverId, counts, scanState}`

`counts` contains exactly the eight original Map kinds:
`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`.

The rebuild-only `zombie_boss` key is not part of this public result.

## Recovered source-selection contract

`map_summary` first consumes the shared scan-state producer and takes `serverId` from that state.

Counts use active staging only when all of these are true:
- `isReading=true`;
- current/requested server matches the scan-state server;
- `scanRunId` is a raw string with length greater than zero.

That active source is the exact run/server scope in `scan_records`. Otherwise counts come from published `map_records` for the shared-state server. A whitespace-only run ID remains nonempty; no trim is applied.

State/count failures propagate. The original contract does not fabricate a server-zero summary and does not select saved data as current state.

## Deviations removed

Before R8-010 the backend could:
- add non-reference `savedServerIds` to the result;
- select `LiveResourceProbeCommandService.CurrentServerId` when shared scan state was unavailable;
- select `firstLiveResultServerId`;
- choose one or more saved profile servers and synthesize `serverIdSource=saved_profile_index`;
- expose counts from `MapScanContract.AllTypes`, including rebuild-only `zombie_boss`.

R8-010 removes those branches from `map_summary`.

Persisted saved-server browsing data still exists elsewhere in the rebuild for separate Auto Scan / result-browsing parity work. This checkpoint does not silently delete that data; it only stops presenting it as original `map_summary` state.

## Regression coverage

`MapSummaryParityChecks` proves:
- exact three-field result envelope;
- exactly eight original count keys;
- no `zombie_boss` count key;
- idle state reads published `map_records`;
- active state with nonempty `scanRunId` reads run-scoped `scan_records`;
- the supplied shared `scanState` is preserved;
- changing shared state to a server with no persisted rows returns that server with zero counts rather than falling back to another saved server;
- shared-state errors propagate unchanged.

Older saved-reopen proofs were corrected to test persisted browsing/search data directly or to provide an explicit test scan state. They no longer claim that persisted data is current `map_summary` state.

## Validation

Passed on 2026-09-24:
- `git diff --check`
- Release C# build: 0 warnings, 0 errors
- full deterministic checks DLL: `ok=true`, `failures=[]`
- frontend generator reproducibility check
- R8-008 Clear frontend contract regression
- R7-147 owner-workflow browser regression
- R7-131 persistence / saved-server / Stop / Clear-race browser regression
- R7-156 Auto navigation / refresh / reconnect browser regression
- R7-136 Auto restart browser regression

## Not claimed by R8-010

The shared `scanState` serializer itself still has rebuild-specific fields and requires separate parity review.

Also still separate:
- `map_data_options` exact result/selector contract;
- original Auto Scan state machine;
- Scheduled Plunder restoration;
- Normal/Fast remaining semantics;
- protected/original Map acquisition internals.
