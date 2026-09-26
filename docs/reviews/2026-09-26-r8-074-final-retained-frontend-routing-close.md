# R8-074 — close final retained frontend routing gaps: Dispatch Alliance Share + Treasure Claim

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the two final genuinely-unclosed retained frontend command contracts without bypassing owner-excluded authorization-state admission or inventing protected game-side execution.

## Result

R8-074 moves the last two retained frontend commands from genuinely unclosed to audited/fenced:

- `map_dispatch_share_alliance`
- `map_treasure_claim`

The R8-067 retained frontend inventory is therefore now fully classified:

- 94 retained frontend API commands;
- 61 commands with a specific production route;
- 33 commands without a specific production route;
- **33/33 unrouted commands audited/fenced**;
- **0 genuinely-unclosed retained frontend routing commands**.

This closes only the retained **frontend command-routing inventory**. It does not close the complete native/service/script/launcher/proxy inventory, original Map acquisition internals, protected provider implementations, or whole-product one-to-one parity.

## map_dispatch_share_alliance

### Exact frontend payload

The immutable wrapper sends:

`{ rows: [{uuid,serverId,x,y,cfgId,ownerName,allianceAbbr}, ...], profileId? }`

Only those seven row fields are projected from the selected frontend rows.

### Exact native handler/admission

Handler:

- `0x140163EF2-0x1401651C5`

Native awaits the shared authorization-state future `0x1400DC79E` before the retained live action path, then resolves the selected profile runtime through `0x1402AE43C`.

Authorization-state implementation remains owner-excluded.

### Exact validation

The complete row list must contain 1 through 200 rows.

Invalid count:

- code: `INVALID_REQUEST`
- message: `select between 1 and 200 dispatch tasks`

Every row must have:

- nonempty decimal-string `uuid`;
- integer-like positive `serverId`;
- integer-like positive `x`;
- integer-like positive `y`;
- integer-like positive `cfgId`.

`ownerName` and `allianceAbbr` are optional display metadata and do not gate admission.

Any invalid row fails the whole input before sharing begins:

- code: `INVALID_REQUEST`
- message: `selected dispatch task cannot be shared`

The recovered integer-like conversion accepts JSON numbers or signed decimal numeric strings; finite floating input truncates toward zero; invalid/missing/overflow values normalize to zero and fail the positive check.

### Provider loop and result

Only after the entire input validates does native execute rows sequentially.

For each row:

- provider: `shareDispatchTaskToAlliance`
- deadline: **5,000 ms**
- no parallel fan-out;
- normal provider JSON is converted through the shared generic JSON converter.

A row counts as successful only when the provider object contains exact boolean `shared=true`.

The host aggregates the exact public result:

- `shared`
- `failed`
- `sharedUuids`
- `failedUuids`

The retained frontend removes `sharedUuids` from current selection and reports partial/full success from these aggregate counts.

Message construction, alliance-chat/network authorization, and protected game-side delivery below `shareDispatchTaskToAlliance` remain protected.

## map_treasure_claim

### Exact frontend payload

The immutable wrapper sends:

`{serverId,claimScope,prioritizeLuckySlots,targetUuid,profileId?}`

Public claim scopes are exactly:

- `boxes`
- `season`
- `single`

`single` requires a nonempty target UUID.

`prioritizeLuckySlots` defaults to **true** when absent; explicit false is preserved.

### Exact native handler/admission

Handler:

- `0x1401686E4-0x14016A011`

Native first awaits the shared authorization-state future `0x1400DC79E`, then resolves the selected profile runtime through `0x1402AE43C`.

This authorization admission precedes the protected claim action and cannot be bypassed for strict parity.

### Server validation and live preflight

Server ID domain is 1 through 99999.

Recovered server errors include:

- `INVALID_SERVER_ID / server ID is required` for the missing-value branch;
- `INVALID_SERVER_ID / server ID must be an integer from 1 to 99999` for invalid range/type normalization.

Native performs a live current-server preflight through:

- provider: `getCurrentServerId`
- deadline: **5,000 ms**

Unavailable current server:

- code: `SERVER_UNAVAILABLE`
- message: `current server id unavailable`

When the live game server differs from the requested map-data server:

- code: `SERVER_MISMATCH`
- message: `current game server does not match map data server`

Invalid scope or missing required single target uses:

- code: `INVALID_TREASURE_CLAIM_SCOPE`
- message: `treasure claim scope is invalid`

### Exact candidate ownership

Before protected claim execution native queries Treasure candidates from the profile map-data database. The recovered query selects `data_json` from `map_records` where:

- `kind='treasure'`;
- `server_id` equals the requested server;
- supported supplies types are 1, 3, or 4, or ordinary type 0 with `complete=1`;
- UUID is present, trimmed nonempty, and not `0`;
- expiration is absent/nonpositive or greater than current time;
- results are ordered by `point_index ASC`.

This is the dedicated Treasure claim-candidate query; the separate Supplies-only query is not equivalent.

### Protected claim bridge and immediate result

Native sends one protected call:

- provider: `claimTreasures`
- deadline: **5,000 ms**
- request fields:
  - `serverId`
  - `records`
  - `claimScope`
  - `targetUuid`
  - `prioritizeLuckySlots`

After generic provider JSON conversion, the host projects the immediate result vocabulary:

- `eligible`
- `queued`
- `skipped`
- `directQueued`
- `scoutQueued`
- `scoutDispatched`
- `claimed`
- `noScoutSkipped`
- `otherAllianceSkipped`
- `failed`

The frontend treats `queued` as admission/queue count, not authoritative completion. If `queued > 0`, it polls `map_treasure_claim_status` every 1,000 ms for up to 1,800 iterations and stops early when optional `batch.state` is no longer exact `running`.

Scope-specific ordering/filtering below the host candidate set, lucky-slot prioritization, duplicate suppression, scout-slot selection/reservation, and the protected batch executor remain unrecovered/protected.

## Why no runtime routes are added

Both final action handlers retain mandatory owner-excluded authorization-state admission before state-changing provider execution.

For Dispatch Share, bypassing admission would permit alliance-message delivery in states where the original command cannot proceed.

For Treasure Claim, bypassing admission would permit claim/scout orchestration in states where the original fails before the protected executor.

Implementing either route without that original admission would be observable non-reference behavior. R8-074 therefore closes their contracts as **audited/fenced** rather than enabling approximate live actions.

## Inventory classification after R8-074

Retained frontend routing layer:

- specifically routed: 61
- unrouted but audited/fenced: 33
- genuinely unclosed: **0**

Important remaining project work is outside this now-closed routing-inventory bucket, including:

- native-only handlers that have no recovered frontend literal;
- exact host↔proxy protocol/readiness/failure semantics;
- secure/plain xLua proxy and script-dispatch ownership;
- original Map acquisition/travel/action internals;
- shared config migration/normalization ownership;
- retained provider implementations fenced on excluded authorization state;
- removal of reconstruction drift and final reference-vs-rebuild validation.

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-074-final-retained-frontend-routing-close.json`.
