# Checkpoint B — Ghost preparation versus execution

State: **COMPLETE — BT-02 VALID; preparation and terminal execution are separate capabilities.**

## Exact original pipeline

Original `map_dispatch_plunder_schedule` is `0x1336A9-0x1350DF`. The exact frontend already requires a decimal UUID plus positive `completionTime` and `plunderAt` before a Dispatch/Ghost row can be selected. The frontend schedule wrapper owns the optional random delay and only adds that bounded delay to the existing base `plunderAt`.

For Ghost rows the host then:

1. validates submitted scheduling fields;
2. invokes protected `prepareGhostPlunderTasks` at `0x133CA8`, bounded at 5,000 ms (`0x133CC1`);
3. requires the provider result to expose a `rows` collection (`0x133F57`);
4. matches/indexes prepared rows by UUID and fails missing/inconsistent preparation as `ghost scheduling data unavailable`;
5. re-validates/normalizes the prepared row;
6. persists durable identity `ghost:<uuid>` while row JSON retains the original UUID/taskKind;
7. only later does the independent worker arm the durable row and wait for a terminal result.

The exact later worker's arm/result correlation is therefore downstream of preparation and persistence. Nothing recovered in the preparation handler requires a terminal Ghost steal acknowledgement before a row can be prepared.

## Current clone pipeline

`MapActionControlPlane.ScheduleDispatchPlunderAsync` has the same stage separation: validate inputs -> call `PrepareGhostPlunderTasksAsync` for the Ghost subset -> index returned rows by UUID -> require prepared cardinality -> `NormalizeDispatchScheduleRow` -> `ScheduleDispatchPlunderRow` persistence.

The public current provider is still fenced, but its error rationale says terminal response identity is not source-proven. That rationale is **overbroad for preparation**: terminal correlation is a downstream execution gate.

## Field-by-field classification

| Field/rule | Exact original observable | Current evidence | Classification |
|---|---|---|---|
| `uuid` | Positive decimal task identity | Preserved by source row/helper | **EXACT_HOST_ADMISSION** |
| `ownerServer` | Ghost row requires positive value; host extracts and rejects non-positive at `0x1342E2-0x1342F1` | Current Ghost request serializes ownerServer | **EXACT_HOST_ADMISSION + CURRENT_OPERATION_INPUT** |
| `completionTime` | Positive; schedule cannot precede it | Current task field | **EXACT_HOST_ADMISSION** |
| `plunderAt` | Positive/base schedule time exists before preparation; frontend later adds random delay | Current v22 `CheckCanSteal` proves `(serverTime-completionTime)/1000 >= protectTime`; scan adapter derives `completionTime + protectTime*1000` | **SOURCE-BACKED BRIDGE ADAPTATION** |
| `taskExpireTime` | Zero/non-positive is permitted; if positive it must be after `plunderAt` (`0x134295-0x1342A5`) | RDL/task info exposes it. `ActGhostreconTaskInfo.__init` explicitly defaults it to `0`; ParseData copies the source value | **STRICT HELPER `>0` RULE IS CONSERVATIVE, NOT EXACT** |
| `stolenCount` | Used against positive max capacity | Current manager count derives from `stealList` | **SOURCE-BACKED BRIDGE ADAPTATION** from `stealListCount` |
| `maxStealCount` | Positive max gates full rows | Current task template reads `steal_maxtimes` | **SOURCE-BACKED BRIDGE ADAPTATION** from `stealMaxTimes` |
| `protectTime` | Not a host schedule field; host consumes `plunderAt` | Current template source input used to derive base plunder time | **CURRENT-SOURCE INPUT TO ADAPTATION** |
| Alias equality (`stolenCount==stealListCount`, `maxStealCount==stealMaxTimes`) | Not externally exposed by original host | Current strict helper checks consistency | **CURRENT CONSISTENCY GUARD**, not recovered protected-preparer semantics |

`b-current-ghost-field-bodies.txt` pins the current task/template instructions and decoded hashes. In particular, `ActGhostreconTaskInfo.__init` line 28 sets `taskExpireTime` to zero; this disconfirms treating positive expiry as a universal current domain invariant.

## What the protected preparer still owns

The exact body of `prepareGhostPlunderTasks` remains absent. The first genuine preparation gap is:

`current Ghost source row -> exact protected preparer returned-row transformation/rejection semantics beyond the host-visible postconditions`.

Unknowns include whether the protected preparer re-reads/hydrates any current task data, whether it recalculates any scheduling fields, what additional row members it returns, and its exact per-row rejection/error policy. No recovered source requires runtime hydration, but none proves the preparer is a pure identity transform either.

Returned-row ordering is not a terminal execution issue: the host matches prepared rows by UUID before persistence. Exact duplicate-UUID behavior inside the private preparer/host map construction is not promoted without a distinguishing source case.

## Downstream execution restriction

Ghost execution remains separately unavailable because the current terminal response correlation contract is unresolved. That is a valid **downstream capability gate**, not a proof that preparation semantics are unknowable or must be disabled by definition.

Preserving the current public preparation fence in this audit is required by assignment. The existing scheduled Ghost safety guard also remains untouched.

## Ready offline implementation candidate

A follow-up implementation task can independently split **preparation capability** from **execution capability** and evaluate wiring the existing source-backed one-to-one normalizer as a preparation-only current adaptation while keeping Ghost arm/result execution fenced.

Before doing so, that task should:

- relax the helper's unconditional positive-expiry rule to the exact host/current domain (`0` allowed; positive must exceed plunderAt), unless new source proves a stronger current requirement;
- retain positive ownerServer, UUID, completion/plunder timing and capacity guards;
- explicitly label the protect/counter aliases as bridge-owned current-v22 adaptation;
- decide product UX for durable Ghost rows whose downstream execution capability remains unavailable, without fabricating terminal success.
