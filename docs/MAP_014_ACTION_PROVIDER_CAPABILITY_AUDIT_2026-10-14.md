# MAP-014 Map action provider capability audit — 2026-10-14

Scope: read-only review of the 0.3.17 action catalogue, current production
`CurrentClientMap317ActionProvider`, its owned `CurrentClientMapBlockSource`
probes, controller, worker, and the existing source-locator receipts. No official
client was started, no action command was issued, and no owner account was changed.
This audit does **not** upgrade any historical action evidence to a current-v26
positive. Whole Map remains **PARTIAL**.

## Action catalogue and actual result boundary

| Control / provider method | Current production capability | Source/result gate |
| --- | --- | --- |
| `map_coordinate_jump`, `map_march_follow`, `server_jump` | Real native navigation adapter | Owned session and server check; march follows UUID rather than cached moving coordinates. Existing native navigation receipts are separate from this offline audit. |
| `map_treasure_state_refresh`, `_all` / `InspectTreasureStatesAsync` | Read-only inspection | Batches of at most 100, request/session/profile/game PID correlation, per-request distinct Treasure UUID and stable viewer/alliance before cache update. Inspection does not produce rewards. |
| `map_treasure_claim_status` / `TreasureClaimStatusAsync` | **Inspection-only status**, not aggregate claim quota | Control plane calls `GetCurrentServerIdAsync` plus `InspectTreasureStatesAsync` over current candidate records; returns `mode:inspection_only`, `claimExecutionAvailable:false` and `null` quota/reset/cooldown. It does **not** call the provider's `GetTreasureClaimStatusAsync`, which remains explicitly unavailable. |
| `map_treasure_claim_preview` | Local read-only single/boxes/season preview | Re-inspection and scope/eligibility checks return `mode:preview_only`, `execute:false`, `claimExecutionAvailable:false`; no game claim transport. Lucky-slot priority remains unknown when absent. |
| `map_treasure_claim` / `ClaimTreasuresAsync` | **Unavailable** | Controller validates server/scope and reads candidates, then current provider fails `GAME_PROVIDER_UNAVAILABLE` before a claim send. The recovered old-v19 `detect.event.claim.treasure` helper models one `PutLong` UUID + `PutInt` target server and error/reward shape, but is not a validated current-v26 session-correlated claim/terminal or an aggregate boxes/season scheduler. |
| `map_dispatch_share_alliance` | Native share adapter; owner-only live action | Exact task/server/coordinate/config input, `ChatHeroDispatchShare` response and owned-session/task matching. Batch counts successful vs failed rows; no live action was taken here. |
| `map_ghost_plunder_prepare` / `PrepareGhostPlunderTasksAsync` | **Preparation only** | v22 source projection requires `GhostreconPointInfo`, point type 29, owner server, completion/protection, expiry and counter agreement; at most 200 unique rows. Current guard additionally requires Ghost UUID to fit positive signed 64-bit `PutLong` and protection arithmetic not to overflow. It creates no scheduled job. |
| `map_dispatch_plunder_schedule`, `_list`, `_cancel`, `_clear`, `_retry` | Durable Dispatch job controls; live plunder owner-only | Stored jobs and a source-backed Dispatch arm/terminal; exact request ID, current session and task result. New Ghost schedule is rejected `GHOST_PLUNDER_PROVIDER_UNAVAILABLE`. A legacy stored Ghost job cannot enter Dispatch transport. |
| `ArmGhostPlunderAsync`, `DrainGhostPlunderResultsAsync` | **Unavailable** | Valid Ghost arm fails `GHOST_PLUNDER_PROVIDER_UNAVAILABLE`; result drain is empty. Controlled `IGhostPlunderProvider` worker correlation requires kind, server, owner server, UUID, attempt ID and launch session. Such an interface/test is not a live game producer. |
| `map_truck_plunder_schedule`, `_list`, `_cancel`, `_clear`, `_retry` | Durable Truck job controls; native quick-rob terminal adapter; live plunder owner-only | Native result validation requires exact owned session/request/target and authoritative battle/reward fields. Pending handles can be cleared. |
| `map_plunder_jobs_list` / server-day provider | Local job lists and source-backed day-start lookup | Live server time and day start must agree; cached prior day may be reused only on connection-unavailable. No fabricated daily quota. |
| `map_player_mark_set`, server jump history get/set/import | Local MapStore operations | SQLite/local history; no game claim or plunder action. |

`map_scan_start/status/stop/clear`, `map_search`, `map_summary`, `map_data_options`,
`map_saved_servers`, `map_city_export`, image lookup and train list coverage
belong to the scan/browse/export action *catalogue* but are not Treasure/Ghost
terminal providers. Their acceptance lives in MAP-014's separate acquisition,
UI and packaging gates.

## Exact unavailable edges

**Ghost:** A historical v22 `GhostReconSteal` request serializes `uuid` and
`ownerServer`; its `HandleMessage` does not consume a returned task UUID and
SFSNetwork creates a separate incoming message instance. The admitted bridge
explicitly rejects Ghost inside the Dispatch runtime
(`tools/current_overview_bridge.lua`, `dispatch_plunder_runtime.begin`). Current
v26 has **no verified dedicated Ghost send path with an authoritative terminal
containing an identity that can be bound to the exact server, owner server,
task UUID, attempt and owned session**. A send acknowledgement or unrelated
Dispatch result cannot be promoted to plunder success. Owner-only positive
Ghost population and a legitimate native request/result witness are needed
before implementing or enabling this adapter. The prepared row does not grant
execution authority.

**Treasure:** A v24 locator identifies
`Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage` as a known-item
claim-info reader; the existing current inspector can prove read-only states.
Neither establishes current-v26 aggregate daily claim remaining, reset,
cooldown or a claim send + same-owner/same-target terminal claimed-state and
reward/inventory delta for single, boxes and season. The v19 direct helper is
historical protocol knowledge, not sufficient evidence to blind-send on v26.
Until those terminal semantics are shown in a permitted owner-only witness,
the production claim provider must remain unavailable. Do not infer a Season 4
requirement; reported seasonal eligibility is unverified.

## Controlled inverse checks and scope

`Map006ActionSafetyChecks` now directly invokes the **production** current
provider with a game-session accessor and command writer that throw if accessed.
Treasure claim status/claim and valid Ghost arm are expected to fail with their
explicit unavailable codes **without entering any owned session**, and Ghost
drain must produce no synthetic terminal. Native `PutLong` overflow UUIDs,
duplicate Ghost arm IDs and protection-timing arithmetic overflow fail
`INVALID_REQUEST` during preparation/admission. The existing
`Map007ActionProviderChecks` and `Map007GhostCorrelationChecks` exercise the
real control plane and SQLite with inert fake game outcomes, including
cross-transport/stale result inverses; these do not establish live action
support.

Evidence locations: `src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs`,
`CurrentClientMapBlockSource.TreasureState.cs`, `CurrentClientMapBlockSource.MapActions.cs`,
`TreasureClaimContract.cs`, `src/LWBridge.Map-0.3.17/MapActionControlPlane.cs`,
`MapPlunderWorker.cs`, `docs/MAP_007_ACTION_PROVIDER_HANDOFF.md`,
`docs/proof/MAP-009-OFFLINE-NATIVE-SOURCE-LOCATORS-2026-10-12.json`.
