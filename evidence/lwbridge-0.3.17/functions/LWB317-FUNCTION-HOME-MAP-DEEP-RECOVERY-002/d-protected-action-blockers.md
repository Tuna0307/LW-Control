# Milestone D — Treasure/Ghost protected provider disposition

D recovered substantially more current-v22 action material but **did not establish
the complete protected adapter contracts required to implement the three currently
fail-closed provider methods**.

## Treasure status

Current v22 contains
`DetectEventGetTreasureClaimInfoMessage`: a single-target request with treasure
`uuid`, an info-type integer, `targetServer`, an `errorCode` result path and
the `GetDetectEventTreasureClaimInfo` handler. Its data objects expose
`remainNum`, claim-player state and reward information.

The exact 0.3.17 host, however, calls a no-argument high-level
`getTreasureClaimStatus` provider. Static evidence does not establish which
current targets must be queried, how multiple single-target responses are
aggregated, or the exact returned provider object. A synthetic aggregate would be
an invented schema.

## Treasure claim

Current v22 contains a direct single-target
`DetectEventClaimTreasureMessage` with `uuid`, `targetServer`,
`worldTreasureType`, `errorCode`, reward and treasure-data result constants.
A push route correlates `uuid` and `bUuid`. The client also contains scout
primitives (`LaunchScout`, `SCOUT_SUPPLIES`, `StartMarch`).

The exact 0.3.17 provider contract is broader: it receives the host-selected
records plus `claimScope`, optional target UUID and lucky prioritization and
returns authoritative aggregate counters separating direct queueing, scout
queueing/dispatch, claimed/skipped/failure cases. The current static material does
not establish that orchestration, scout formation ownership/correlation, or
terminal aggregation.

The accepted `current_live_resource_probe.lua` Treasure-state lane is deliberately
read-only. Existing deterministic checks require it to contain no
`DetectEventClaimTreasure`, `LaunchScout`, march, reward-fetch or claim calls.
That lane cannot be repurposed.

## Ghost preparation

Current v22 exposes `GhostReconStealMessage`, current task/template fields,
`GetTaskInfoByUUid`, `GetTaskTemplate`, `CheckCanSteal`, steal counters and
server/task timing. These support the already source-backed scheduled execution
path.

The exact 0.3.17 scheduler nevertheless calls
`prepareGhostPlunderTasks(rows)` **before persistence**. Static 0.3.17 recovery
has not established its returned-row transformation/validation schema, and current
v22 has no equivalent high-level method. Reconstructing it from
`CheckCanSteal` would invent semantics.

## Ownership/result boundary

All implemented current actions use an owned session plus request ID and
authoritative result envelope. A blind current-game message send would lose the
accepted profile/session/challenge/PID admission, cancellation, replacement
fencing and ambiguity handling. This campaign does not weaken those protections.

## D result

- `GetTreasureClaimStatusAsync`: remains fail-closed.
- `ClaimTreasuresAsync`: remains fail-closed.
- `PrepareGhostPlunderTasksAsync`: remains fail-closed.
- no product change.

This is a concrete evidenced blocker, not a generic “needs live testing” label.
Independent E/F/G branches continue as required.
