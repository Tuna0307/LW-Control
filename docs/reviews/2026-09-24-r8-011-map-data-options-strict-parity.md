# R8-011 — restore original map_data_options strict parity

**Date:** 2026-09-24
**Reference:** verified LWBridge 0.3.1 executable
**Scope:** public `map_data_options` source selection, option/count aggregation, and recovered result envelope.

## Authority

Primary authority is `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`, backed by:
- `evidence/lwbridge-implementation/2026-09-09-r6-map-option-source-selector.json`
- `evidence/lwbridge-implementation/2026-09-09-r6-map-option-response-assembly.json`

Request is exactly `{serverId}`.

Recovered native top-level response assembly order is:

`serverId, counts, alliances, names, dispatchLevels, noAllianceCount, rewardItems, treasureTypes, scanProgress`

The frontend-consumed option families are:
- `alliances[] = {name,count}`;
- `names.resource[]` and `names.monster[] = {key,count}`;
- numeric `dispatchLevels[]`;
- `rewardItems.truck[]` and `rewardItems.railway[] = {key,name,iconPath}`;
- `treasureTypes[] = {key,count,treasureType,suppliesType,treasureNameKey}`.

Counts contain exactly the eight original Map kinds:
`city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, `treasure`.

The rebuild-only `zombie_boss` option/count family and public `monsterLevels` field are not part of the original result.

## Recovered source-selection contract

Active staging is selected only when all of these are true:
- requested `serverId > 0`;
- shared scan state has `isReading=true`;
- shared scan-state `serverId` equals the requested server;
- raw `scanRunId` is a string with length greater than zero.

A whitespace-only run ID is nonempty; no trimming is evidenced.

The active source is `scan_records` scoped by exact server/run. Every fallback is `map_records` scoped by the requested server.

This removes the R7 rebuild shortcut that treated `serverId=0` as “aggregate every published server.” With no positive active-server scope, server zero remains a server-zero published scope; it does not select unrelated saved-server rows.

## Recovered aggregation behavior

R8-011 preserves the recovered original aggregation rules:
- City alliances group/count with `alliance_name COLLATE NOCASE, alliance_name`;
- NULL/empty alliance groups are omitted from `alliances[]` and accumulated into `noAllianceCount`;
- Resource/Monster names exclude empty keys and group/count with recovered NOCASE ordering;
- Dispatch levels are distinct integer values >=1 ordered ascending;
- Treasure rows preserve recovered ordinary/Supplies normalization and stable grouping/order;
- Truck/Railway reward items derive current goods and use the precise Unix-ms `arriveTs` cutoff;
- all eight counts come from the same selected source/scope as the options.

Published `scanProgress` selection remains the exact recovered query:

`SELECT id,server_id,selected_types,status,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs WHERE server_id=?1 AND status<>'discarded' ORDER BY updated_at DESC LIMIT 1`

With an active scope, the exact active run ID is selected instead.

## Deviations removed

Before R8-011 the rebuild could:
- expose `names.zombie_boss`;
- expose a ninth `counts.zombie_boss` key;
- expose rebuild-only `monsterLevels`;
- aggregate published rows across every saved/current-session server for `serverId=0`;
- assemble top-level fields in a non-reference order.

R8-011 removes those deviations from `map_data_options`.

The browser preview fixture is also aligned to the restored original envelope.

## Regression coverage

Deterministic/public-command coverage proves:
- exact recovered top-level property order;
- exact eight count keys in recovered order;
- only Resource/Monster name families;
- absence of `monsterLevels` and `zombie_boss` count/name families;
- published per-server option/count isolation;
- `serverId=0` does not aggregate unrelated saved servers;
- active matching `scanRunId` selects exact run-scoped `scan_records`;
- active scope does not leak published/wrong-run/wrong-server rows;
- alliance/no-alliance, dispatch, treasure, rewards and scan-progress selection remain covered.

## Validation

Passed on 2026-09-24:
- `git diff --check`
- Release C# build: **0 warnings / 0 errors**
- full deterministic checks: `ok=true`, `failures=[]`
- frontend generator reproducibility check
- R8-008 Clear frontend contract regression
- R7-147 owner-workflow browser regression
- R7-131 persistence / saved-server / Stop / Clear-race browser regression
- R7-156 Auto navigation / refresh / reconnect + jump-first browser regression
- R7-136 Auto app/process restart browser regression
- automatic scan-strategy UI browser regression

## Not claimed by R8-011

The complete original nested `scanProgress` serializer key set/order and exact absent-row representation remain only partially recovered. R8-011 continues to expose only the fields proven consumed by the original frontend: `id, serverId, status, createdAt, updatedAt, error`.

Also still separate:
- original Manual Scan eight-kind/`scanMode` public contract;
- `map_scan_status` / `map_scan_stop` serializer/state parity;
- `map_search` strict parity, including removal of rebuild-added Monster level-filter semantics;
- original Auto Scan state machine;
- Scheduled Plunder restoration;
- protected/original Map acquisition internals.
