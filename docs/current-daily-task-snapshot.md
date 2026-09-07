# Current daily-task read-only snapshot contract

This document defines the JSON boundary between the current-game Lua state probe
and the C# reconstruction. The contract was exercised successfully in a bounded
current-game run on 2026-09-06. The probe produced a version-1 state snapshot that
passed the C# validator after one bounded `DailyQuestLs` data refresh. No reward-
claim command was sent. See
[`current-daily-task-live-capture.md`](current-daily-task-live-capture.md).

The contract intentionally exports symbolic task states. The current game's
numeric `TaskState` values are still unknown and are not guessed here. A future
Lua probe should compare values against `TaskState.NoComplete`,
`TaskState.CanReceive`, and `TaskState.Received` inside the game, then write the
matching symbolic name.

## Schema version 1

The machine-readable schema is
[`current-daily-task-snapshot.schema.json`](current-daily-task-snapshot.schema.json).
The required top-level fields are:

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Must be exactly `1`. |
| `mode` | Must be exactly `state`; action-shaped snapshots are rejected. |
| `captureId` | Unique identity supplied by the read-only probe. |
| `capturedAt` | Timestamp for the state capture. |
| `heartbeat` | Probe version plus a fresh heartbeat timestamp. |
| `tasks` | Task identity, symbolic state, and optional template point. |
| `currentPoint` | Point total exported by the probe and independently re-derived in C#. |
| `receivedStages` | Received chest-stage indices. Only indices `1` through `5` are accepted. |
| `boxes` | Exactly five chest records: index, activation threshold, and symbolic derived state. |

Example:

```json
{
  "schemaVersion": 1,
  "mode": "state",
  "captureId": "capture-example-1",
  "capturedAt": "2026-09-06T02:30:00+08:00",
  "heartbeat": {
    "probeVersion": "lwcontrol-daily-state-probe-1",
    "observedAt": "2026-09-06T02:30:00+08:00"
  },
  "tasks": [
    {
      "taskId": "example-task-a",
      "state": "Received",
      "templatePoint": 30
    },
    {
      "taskId": "example-task-b",
      "state": "CanReceive",
      "templatePoint": 40
    }
  ],
  "currentPoint": 30,
  "receivedStages": [1],
  "boxes": [
    { "index": 1, "activationPoint": 10, "state": "Received" },
    { "index": 2, "activationPoint": 20, "state": "CanReceive" },
    { "index": 3, "activationPoint": 30, "state": "CanReceive" },
    { "index": 4, "activationPoint": 40, "state": "NoComplete" },
    { "index": 5, "activationPoint": 50, "state": "NoComplete" }
  ]
}
```

## Game-derived invariants

These checks come directly from the current content-version-12 daily-task logic
already recovered in `DailyTaskManager`:

- `currentPoint` is independently re-derived by summing `templatePoint` only for
  tasks whose symbolic state is `Received`; a missing template contributes zero.
- a chest is `Received` when its index occurs in `receivedStages`;
- otherwise it is `CanReceive` when its activation threshold is less than or
  equal to `currentPoint`;
- otherwise it is `NoComplete`;
- the complete daily chest set is exactly indices `1` through `5`.

The C# model in `CurrentDailyTaskSnapshot.cs` rejects a snapshot when the exported
`currentPoint` or any exported chest state disagrees with those derived values.

## Local fail-closed guards

The following are reconstruction-side input-safety rules. They are not claims
that the game itself enforces the same limits:

- unknown JSON properties are rejected;
- task and chest states must be symbolic enum names, never integers;
- there may be at most 1,000 task entries;
- task IDs must be non-empty, at most 200 characters, and unique by ordinal text;
- template points and chest thresholds must be between `0` and `1,000,000`;
- `currentPoint` must be between `0` and `1,000,000,000`;
- `receivedStages` may contain each index `1` through `5` at most once;
- the five chest records must contain each index `1` through `5` exactly once;
- the default freshness window is 15 seconds with a five-second future-clock
  tolerance, matching the separately recovered bridge heartbeat policy;
- schema versions other than `1` and modes other than `state` are rejected.

These guards deliberately fail closed. A malformed or internally inconsistent
snapshot is unusable for claim eligibility and must not be converted into an
action request.

## What the live Lua probe reads

The minimum current-game data source remains:

- `DataCenter.DailyTaskManager.dailyQuestTasks` for task identity/state;
- `DataCenter.DailyTaskTemplateManager:GetQuestTemplate(taskId)` for each task's
  point value when a template exists;
- `DataCenter.DailyTaskManager.curReward` for received stage indices;
- `DataCenter.DailyTaskManager.dailyBoxActive` for activation thresholds;
- the manager's current task-state enum symbols for symbolic comparisons.

The probe derives the symbolic task and chest states inside Lua and writes the
snapshot/heartbeat metadata. If the daily-task manager has not yet populated
`dailyBoxActive`, the outer loader probe may queue exactly one call to the game's
own `DailyTaskManager:TryReqUpdateData()` path. Recovered bytecode proves that path
queues `MsgDefines.DailyQuestLs`. It is a list refresh, not a reward command.

The state builder lives at
[`tools/current_daily_task_snapshot_probe.lua`](../tools/current_daily_task_snapshot_probe.lua).
It performs no file I/O and contains no network send path. Instead it accepts the
manager, template-manager, and task-state objects as arguments and returns a plain
Lua snapshot table. Before returning, it cross-checks `GetCurValue()` against an
independent task/template sum and `GetBoxState()` against the recovered five-box
derivation. `install_loader_probe.py` now embeds this builder into the exact
encrypted probe source, while `run_daily_task_snapshot_probe.py` performs the
bounded install/launch/read/restore workflow.

## Validation at this checkpoint

The builder still parses successfully with `luaparser`, and its standalone source
contains no file I/O or network send path. The generated outer probe was also
checked to contain no `DailyQuestReward`, `DailyTaskReward`, `daily.quest.reward`,
or `daily.task.reward` token. In the live run the list refresh was requested once,
`UpdateDailyTask` was then observed with 23 tasks and five box thresholds, and a
fresh snapshot was emitted. The live snapshot independently derived to 240 points
and was accepted by `CurrentDailyTaskSnapshot.Validate()` in the C# core. Exact
script-package/BaseUtils restoration was verified by SHA-256 equality.
