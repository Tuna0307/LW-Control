# LWB317-RE-MAP-001 — Map Data end-to-end recovery goal

**State:** IN_PROGRESS
**Phase:** 2 — function recovery
**Project-lead authorization:** 2026-09-30

This is a deliberately large Goal-mode assignment. It is intended to require
multiple continuations and substantial independent reasoning.

## Goal

Recover the LWBridge 0.3.17 **Map Data function family** from exact reference
evidence deeply enough that the project has:

1. an evidence-backed frontend/host contract;
2. an evidence-backed persistence/query/export contract;
3. an evidence-backed scan lifecycle/state machine;
4. a clearly separated current-client compatibility map;
5. a tested local implementation of the recovered non-game control/data plane;
6. a precise list of remaining game/provider/native unknowns.

Do not stop after finding command names. Trace the family end-to-end as far as
the exact 0.3.17 artifact permits.

## Reference

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

Expected SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Read first

1. `AGENTS.md`
2. `task.md`
3. `docs/README.md`
4. `docs/PROJECT_LEAD.md`
5. `docs/AI_WORK_PROTOCOL.md`
6. `docs/strict-parity-recovery.md`
7. `docs/lwbridge-feature-ledger.md`
8. `docs/lwbridge-parity-matrix.md`
9. `docs/reviews/2026-09-29-LWB317-UI-002C-map-data-inventory.md`
10. `docs/LEGACY_0.3.1_INDEX.md`
11. this file

Inspect `git status --short` and current history before acting.

## Historical 0.3.1 policy

The repository contains extensive old Map research and tooling. Use it
aggressively as a hypothesis/search accelerator, but never promote a 0.3.1
contract to 0.3.17 fact without revalidation against 0.3.17.

For every reused historical claim, classify it as:

- unchanged in 0.3.17;
- changed in 0.3.17;
- absent in 0.3.17;
- still unknown.

## Scope

Recover the complete in-scope Map Data family visible or referenced by 0.3.17,
including where evidence exists:

- Manual Scan;
- Auto Scan / scheduling;
- scan start;
- scan resume/restart behavior;
- scan status/progress;
- scan stop/cancel;
- scan clear/reset;
- scan summary;
- scan options/content selection;
- Player City acquisition/data;
- Resource Point acquisition/data;
- Monster acquisition/data;
- Truck acquisition/data;
- Train acquisition/data;
- Secret Task acquisition/data;
- Ghost Ops acquisition/data;
- Treasure acquisition/data;
- saved-data tabs and counts;
- City search/filter/sort/pagination;
- Resource search/filter/sort/pagination;
- Monster search/filter/sort/pagination;
- Truck/Train/Secret Task/Ghost Ops/Treasure query behavior;
- Scheduled Plunder data surface where present;
- coordinate/map jump behavior;
- cross-server/server jump behavior and history where present;
- city export / Excel behavior;
- player marks/notes where present;
- Map-related persistent database/storage ownership;
- Map-related Tauri commands/events;
- frontend request/response schemas;
- error/cancel/terminal-state behavior;
- concurrency/ownership semantics visible in the 0.3.17 host.

Treat Map Data as one coherent product subsystem.

## Required trace depth

For each recovered function, trace as far as evidence permits:

`UI trigger -> frontend function -> Tauri invoke/event -> host handler -> state/storage -> provider/native boundary -> visible result`

Do not stop at a JavaScript command string if the native host can be recovered
further.

Where the trace reaches game-specific material that cannot yet be established,
stop at the exact boundary and record it.

## Static/native recovery requirements

Use appropriate static techniques against the exact 0.3.17 binary and recovered
frontend package, including as useful:

- JavaScript/component/locale analysis;
- Rust/Tauri command-string cross references;
- PE section/string/constant analysis;
- targeted disassembly/decompilation;
- database/schema/query string recovery;
- serializer/field-name recovery;
- event-name recovery;
- state-machine reconstruction;
- comparison against old 0.3.1 findings only after 0.3.17 evidence is located.

Create version-specific helper tools under:

`tools/lwbridge317/`

Do not edit old 0.3.1 evidence to make it appear current.

## Implementation requirement

Do not merely write research notes.

Create a **new 0.3.17 Map control/data-plane implementation** separate from the
legacy 0.3.1 reconstruction. The worker may extend
`src/LWBridge.UI-0.3.17` with a local service/adapter layer or create a new
clearly named 0.3.17 companion project if that gives cleaner boundaries.

The implementation must:

- model recovered Map command/request/result schemas;
- model recovered scan lifecycle states;
- model recovered persisted/queryable Map records;
- implement evidence-backed local query/filter/sort/pagination semantics;
- implement evidence-backed export formatting where recoverable;
- implement evidence-backed marks/history/options storage where recoverable;
- expose a clean boundary for the still-unimplemented game/provider adapter;
- never fabricate live game results;
- label unavailable provider-backed actions as unavailable rather than
  pretending success.

The existing Phase 1 Map UI may be wired to this local recovered control/data
plane where doing so is evidence-backed.

## Tests

Build a substantial deterministic test suite from exact recovered contracts.

At minimum cover:

- command argument validation;
- scan-state transitions;
- cancel/stop terminal behavior;
- resume/restart semantics where recovered;
- count/summary behavior;
- every recovered query filter family;
- sorting direction/defaults;
- pagination;
- unknown/null data fidelity;
- export row/filename behavior where recovered;
- marks/history/options persistence where recovered;
- malformed/corrupt persisted data behavior;
- ownership/concurrency edge cases where recoverable.

Do not claim live proof from deterministic/local tests.

## Current-client compatibility and live validation

After the 0.3.17 contract is stable, investigate the currently installed Last
War artifacts statically/headlessly first, then proceed to **bounded live Map
validation against the current Last War client**.

Live Last War interaction is explicitly authorized for this Goal because Map
scan correctness cannot be established from static analysis alone.

Before live interaction:

- inspect existing Last War/LWBridge processes;
- treat unexplained existing game sessions as owner activity;
- do not close, steal, repurpose or mutate an owner session;
- prefer a worker/assistant-owned game session for disruptive tests;
- record server/world/player context needed to interpret results;
- fail closed if session ownership is ambiguous.

The worker may launch/control an assistant-owned Last War session and perform
Map-only actions needed to validate the recovered subsystem, including:

- current map/world-state observation;
- coordinate navigation/jump;
- server jump when required by the recovered Map contract;
- manual scan start/status/progress/stop/clear/resume;
- auto-scan scheduling/status where safely testable;
- acquisition of city/resource/monster/truck/train/secret-task/ghost-ops/
  treasure data where available;
- query/filter/sort/pagination checks against acquired records;
- export/marks/history/options validation when they do not cause unrelated
  gameplay effects.

Do not send unrelated gameplay actions, marches, purchases, attacks, resource
spending, alliance actions or automation outside the Map subsystem.

Create a compatibility matrix covering:

- exact 0.3.17 expectation;
- old 0.3.1 historical implementation if relevant;
- current-client candidate source;
- unchanged/changed/missing status;
- confidence;
- what still requires live proof;
- what was actually live-proven in the current client.

## Live proof requirements

Where the current game/server state permits it, the Goal should not stop at
`IMPLEMENTED_NOT_VALIDATED`.

Attempt to live-prove at least:

1. connection/runtime readiness required by Map;
2. map/world metadata read;
3. coordinate navigation;
4. scan start;
5. progress/status updates;
6. safe stop/cancel;
7. clear/reset;
8. at least one completed scan/acquisition path that produces real records;
9. query/filter/sort/pagination over those real records;
10. restart/resume semantics when safely reproducible;
11. no orphaned worker/game process after cleanup.

If a particular content type has no live examples during the test window,
record that as `BLOCKED_BY_LIVE_STATE` / equivalent narrative rather than
fabricating proof.

## Explicit non-goals

- no login/account/licensing reconstruction;
- no auth/entitlement bypass;
- no credential/token/license/purchase protocol work;
- no unrelated Automation/Squads/City Layout function recovery;
- no broad redesign of the Phase 1 UI;
- no fabricated defaults or runtime data;
- no claim of one-for-one runtime visual parity;
- no promotion of old 0.3.1 evidence without 0.3.17 revalidation.

## Required durable outputs

Create/update:

1. `docs/reviews/2026-09-30-LWB317-RE-MAP-001-frontend-host-contract.md`
2. `docs/reviews/2026-09-30-LWB317-RE-MAP-002-scan-state-machine.md`
3. `docs/reviews/2026-09-30-LWB317-RE-MAP-003-storage-query-export.md`
4. `docs/reviews/2026-09-30-LWB317-COMPAT-MAP-001-current-client-map.md`
5. `evidence/lwbridge-0.3.17/map/` with machine-readable manifests/contracts
6. version-specific Map helper tools under `tools/lwbridge317/`
7. the new 0.3.17 Map control/data-plane implementation and tests
8. conservative updates to `docs/lwbridge-feature-ledger.md`
9. conservative function rows in `docs/lwbridge-parity-matrix.md`
10. `docs/reviews/2026-09-30-LWB317-RE-MAP-GOAL-handoff.md`

## Evidence states

Use:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

Never collapse static proof, local implementation and live proof into one
status.

## Git/checkpoint discipline

This is a long Goal. Use multiple coherent checkpoints.

After each major sub-deliverable:

1. finish durable evidence/review;
2. run applicable tests;
3. run `git diff --check`;
4. review `git status --short`;
5. commit coherently;
6. push `research/offline-controller`;
7. continue.

Do not wait for project-lead review between the sub-deliverables in this Goal.
Do not force-push.

## Goal completion criteria

This Goal is complete only when all of the following are true:

- the Map frontend/Tauri command surface is inventoried from 0.3.17;
- the Map scan lifecycle/state machine is recovered as far as the binary
  permits;
- Map persistence/query/export/marks/history/options contracts are recovered as
  far as evidence permits;
- old 0.3.1 Map findings have been explicitly revalidated or rejected where
  used;
- a local 0.3.17 Map control/data-plane implementation exists;
- deterministic tests pass;
- the Phase 1 Map UI is connected only to evidence-backed local behavior where
  implemented;
- the current-client compatibility matrix exists;
- every unrecovered provider/native boundary is listed precisely;
- bounded current-client Map live validation has been attempted;
- each claimed live Map behavior has durable evidence;
- no non-Map gameplay action was sent;
- repository checks pass;
- all coherent work is committed and pushed;
- the final handoff states exactly what is ready for the next live/provider
  Goal.

Do not stop because one interesting contract was recovered. Finish the complete
Map subsystem goal above or reach a documented hard blocker.

When complete, stop. Do not automatically begin another subsystem.
