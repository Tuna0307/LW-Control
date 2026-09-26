# R8-073 — fence Equipment Preset Apply / Resource Automation Run / Trade Station Configure

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1

R8-073 closes three remaining retained frontend action boundaries without synthesizing owner-excluded authorization state, unrecovered shared config ownership, or protected live action planner behavior.

## equipment_preset_apply

Exact handler:

- `0x140142659-0x1401437DB`

Immutable frontend payload:

- `{presetId,squadIndex,operationId,profileId?}`

Native first awaits the shared authorization-state future and resolves the profile runtime.

The handler reads `presetId` from the persisted `equipmentPresets` family. Missing/invalid preset uses exact:

- code: `INVALID_REQUEST`
- message: `invalid equipment preset`

The dedicated apply helper `0x1400E103B-0x1400E2DB5` then owns the connected execution path:

- validates `squadIndex`;
- fetches live squads through `getSquads` with a 5,000 ms deadline;
- builds equipment requests using `equips` / `equipUuid`;
- carries the caller `operationId`;
- emits `bridge://equipment-apply-progress` with recovered vocabulary including `phase`, `applying`, `heroName`, and `heroUuid`;
- invokes `applyHeroEquipment` with a 5,000 ms deadline;
- exposes provider rejection vocabulary `equipment_request_rejected`.

The exact full preset-to-hero/equipment binding planner and every multi-hero partial/rejection aggregation branch remain protected/incomplete. Public normal output is converted through the shared generic JSON converter.

No route is added because original authorization admission is owner-excluded and the exact multi-step live planner/result aggregation is not fully closed.

## resource_automation_run

Exact handler:

- `0x14015C3B8-0x14015CD66`

Immutable frontend payload:

- `{task,profileId?}`

The handler awaits shared authorization state, resolves the profile runtime, and constructs the same native request-key family containing `task`, `options`, `premium`, and `admin`; the manual-run source vocabulary is exact `manual`. The authorization-derived `premium/admin` projection is owner-excluded.

The already verified native task parser has exactly:

- `buildingResources` → native action `collectResources`;
- `armedTruckReward` → native action `collectArmedTruckIdleReward`.

Any other task uses:

- code: `INVALID_REQUEST`
- message prefix: `unknown resource automation task: `

Dedicated resource runner helper:

- `0x1400DF9B9-0x1400E079A`

Recovered live boundaries include:

- `STATE_UNAVAILABLE / resource state is unavailable`;
- `AUTOMATION_BUSY / another resource task is already running`;
- connected game-route requirement;
- descriptor-driven native resource action call with exact 10,000 ms deadline;
- Resource Automation status ownership and `bridge://resource-automation-status` publication;
- success/failure status/log vocabulary, including `lastError`.

The exact action descriptor/provider payload uses authorization-derived request material and is not reconstructed. Public normal output is passed through the shared generic JSON converter.

## trade_station_configure

Exact handler:

- `0x140138AB2-0x14013A2E8`

Immutable frontend payload:

- `{config,profileId?}`

Native awaits authorization state, resolves the profile runtime, then normalizes Trade Station config through helper `0x1403BE9A1-0x1403BEE50`.

Recovered retained fields:

- `enabled`
- `crossServerEnabled`
- `selectedItemIds`
- `selectedCurrencyIds`

When enabling without required selections, exact validation is:

- `INVALID_REQUEST / select at least one trade station currency`
- `INVALID_REQUEST / select at least one trade station good before enabling`

Trade Station also belongs to shared config-state ownership under `trade_station` / `config.json`. The shared state lane has exact:

- `STATE_UNAVAILABLE / config state is unavailable`

Connected execution calls:

- `configureTradeStation`
- exact deadline: 5,000 ms
- no handler retry loop.

The handler contains two branch-local provider call sites to the same method and routes normal output through the shared generic JSON converter. Exact branch-selection/merge atomicity across provider execution and shared-config persistence is not fully claimed.

No route is added because exact behavior depends on both owner-excluded authorization admission and the unrecovered shared config migration/merge/write owner.

## Inventory effect

R8-073 moves these commands from genuinely unclosed to audited/fenced:

- `equipment_preset_apply`
- `resource_automation_run`
- `trade_station_configure`

The 33 unrouted retained frontend commands now split into:

- **31 audited/fenced**
- **2 genuinely unclosed**

The remaining two are:

- `map_dispatch_share_alliance`
- `map_treasure_claim`

Evidence: `evidence/lwbridge-implementation/2026-09-26-r8-073-final-provider-actions-fence.json`.
