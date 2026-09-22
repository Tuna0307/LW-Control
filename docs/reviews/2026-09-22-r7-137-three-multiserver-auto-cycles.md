# LWB-R7-137 — Three full live multi-server Auto cycles

**Date:** 2026-09-22
**Base revision:** `615a92c7fc4c13a3143336513020dcc1baba5b70`
**Scope:** close D02 by proving at least three real multi-server scan/return cycles through the production public service boundaries, while composing next-run scheduling from the already-live scheduler proof instead of overstating the React timer path.

## Proof harness

`tests/LWBridge.Desktop.Checks/LiveAutoThreeMultiServerCyclesProof.cs`

Opt-in entry point:

`--live-auto-three-multiserver-cycles`

Committed-checkout harness SHA-256:

`4573FB1390D22920DFAE279E5B738E187B5BA2464782EBD0D4B514E6D7095EE1`

The harness owns one game lifecycle session and one file-backed `MapDataStore` for the entire proof. It does not recreate state between scan legs.

Default proof parameters:

- cycle count: 3
- home server: authoritative current server
- target server: 2213 when home is not 2213
- configured order: home -> target
- selected type: `zombie_boss`
- caller scan mode: omitted
- expected backend-selected strategy: `current_fast_zombie_boss_lod2_v1`
- expected concurrency: 20

Each cycle:

1. confirms authoritative current context on home;
2. runs a full Zombie Boss scan on home;
3. invokes public `server_jump({serverId:2213})`;
4. independently confirms the authoritative current-client context is 2213;
5. runs a full Zombie Boss scan on 2213;
6. invokes public `server_jump({serverId:2212})`;
7. independently confirms authoritative return to 2212.

Every scan leg must expose a unique `scanRunId` and finish 2,500/2,500 with zero failed/unread blocks.

## Failure-first harness correction

The first live attempt exited before the multi-cycle assertion because the proof incorrectly assumed public `server_jump` returned a `serverId` field.

Production was correct. The recovered public contract is:

`{ previousServerId, changed }`

Destination truth comes from the current-client context after travel.

The harness was corrected to validate the real public envelope and then independently read authoritative current context. The failed attempt still ran owned cleanup:

- LastWar = 0
- launcher = 0
- Overview helper = 0
- all three original Lua package hashes restored exactly

No production code change resulted from this failed proof attempt.

## Final live result

Three full cycles passed in one owned session.

Common scan contract across all six legs:

- mode: `fast`
- strategy: `current_fast_zombie_boss_lod2_v1`
- concurrency: 20
- total blocks: 2,500
- read blocks: 2,500
- failed blocks: 0
- unread blocks: 0
- all six `scanRunId` values unique

### Cycle 1

- 2212 run `5833ca0d2eca447db3cc70e8f4098993` — 5.5855145 s
- 2213 run `6e13479021724594acbc8083dfa98d73` — 3.3633848 s
- return 2213 -> 2212: `changed=true`

### Cycle 2

- 2212 run `6eb6256d2ede421cb9e399d91f4e8290` — 0.6071676 s
- 2213 run `938fb190c54a4fe7b77ecca899783ff0` — 0.3923223 s
- return 2213 -> 2212: `changed=true`

### Cycle 3

- 2212 run `b65a8bd1439246a0b3285af8aee820e1` — 2.4993524 s
- 2213 run `a6289117d07947a1b6c9c7a543f5035b` — 0.2775415 s
- return 2213 -> 2212: `changed=true`

Aggregate:

- cycles: 3
- scan legs: 6
- total scan-leg wall time: 14.3025321 s
- process runtime including lifecycle/travel: 55.77 s
- return-to-origin after every cycle: yes
- final authoritative server: 2212

The current population contained zero Zombie Boss rows during these legs. That does not weaken D02 because this acceptance case concerns cycle ownership, travel, completion, restoration and scheduling—not positive Zombie Boss population.

## Scheduling composition

R7-137 does not claim that the React timer itself initiated all three live cycles.

D02 is closed by explicit evidence composition:

- **R7-137 LIVE:** three real 2212/2213 cycles, six completed scans, authoritative server confirmation, return to origin after each cycle.
- **R7-039 LIVE:** persisted future Auto scheduling across full desktop/game restart and automatic `nextRunAt` advancement.
- **R7-132 BROWSER:** one top-level scheduler owner, confirmed-travel ordering, no duplicate cycle across navigation/Refresh Status/reconnect.
- **R7-136 BROWSER/OFFLINE:** app/process restart does not resume an interrupted target list and advances future scheduling only after recovery.

Therefore D02 is recorded as **PASS_CURRENT_PLUS_HISTORICAL**, not as a new claim that the current React timer personally drove all three live cycles.

## Validation and cleanup

- Release build: 0 warnings / 0 errors
- all six deterministic groups: true, `failures=[]`
- final LastWar processes: 0
- final launcher processes: 0
- final Overview helper processes: 0
- restored `LWScripts.data` SHA-256: `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`
- restored `LWScripts.txt` SHA-256: `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`
- restored `version.txt` SHA-256: `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`

No robbery, plunder, claim, Alliance share, spend or collection action was performed.

## Acceptance effect

**D02 -> PASS_CURRENT_PLUS_HISTORICAL**

Remaining independent release gates are population-/authorization-/availability-dependent or human-GUI acceptance:

- Ghost positive-row proof;
- Supplies positive-row proof;
- explicitly authorized Truck/Dispatch/Alliance/Treasure live actions;
- simultaneous real multi-account UI population;
- final human normal-user built-executable walkthrough.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-three-multiserver-auto-cycles.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7137.json`
