# Current daily-task read-only snapshot contract

This document defines the JSON boundary between a future current-game Lua state
probe and the C# reconstruction. It is an offline contract only. No current-game
daily-task probe has produced this file yet. The separate encrypted loader
heartbeat candidate was accepted by the current game on 2026-09-06, but this
daily-task state payload has not yet been installed or executed live. No network
or reward-claim command is authorized by this work.

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

## What the future Lua probe must read

The minimum current-game data source remains:

- `DataCenter.DailyTaskManager.dailyQuestTasks` for task identity/state;
- `DataCenter.DailyTaskTemplateManager:GetQuestTemplate(taskId)` for each task's
  point value when a template exists;
- `DataCenter.DailyTaskManager.curReward` for received stage indices;
- `DataCenter.DailyTaskManager.dailyBoxActive` for activation thresholds;
- the manager's current task-state enum symbols for symbolic comparisons.

The probe should derive the symbolic task and chest states inside Lua, write only
the snapshot and heartbeat metadata, and send zero `SFSNetwork` messages. Generic
encrypted loader execution is now proven by the 2026-09-06 heartbeat acceptance;
live execution of this daily-task snapshot payload remains `UNKNOWN` until a fresh
snapshot is actually produced by the current game.

An offline draft of the state builder now lives at
[`tools/current_daily_task_snapshot_probe.lua`](../tools/current_daily_task_snapshot_probe.lua).
It performs no file I/O and contains no network send path. Instead it accepts the
manager, template-manager, and task-state objects as arguments and returns a plain
Lua snapshot table. Before returning, it cross-checks `GetCurValue()` against an
independent task/template sum and `GetBoxState()` against the recovered five-box
derivation. This draft is deliberately not wired into `install_loader_probe.py`;
current-game loading of any injected payload remains unproven.

## Validation at this checkpoint

The offline implementation was checked with the repository's existing validation
paths: all 21 C# core checks pass, all 43 Python tests pass, the Release desktop
build completes with zero warnings and zero errors, and the desktop smoke test
exits successfully. The Lua draft parses successfully with the installed Python
`luaparser` package, and a static token check finds no `SFSNetwork`, `SendMessage`,
`SendLuaMessage`, `io.open`, or `os.execute` path in that draft. These checks prove
only the offline contract and source consistency. Loader heartbeat execution is
now proven separately; this snapshot payload itself remains unproven live.
