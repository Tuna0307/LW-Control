# Current daily-task live capture — 2026-09-06

This checkpoint records the first successful current-game execution of the
version-1 daily-task read-only snapshot path. It is state-observation evidence,
not a reward-claim test.

## Sequence

1. The current official 18,686-entry content-version-12 script package passed the
   clean preflight contract.
2. A separate encrypted 18,688-entry candidate was built with the preserved
   official `LuaEntry`, the encrypted wrapper, and `LWControlProbe` containing the
   read-only snapshot builder.
3. The candidate was verified by decrypting its active wrapper/probe back to the
   exact generated Lua source before installation.
4. Last War was confirmed closed and exact backups of `LWScripts.data`,
   `LWScripts.txt`, `version.txt`, and `BaseUtils.rdl` were created.
5. Only the candidate script package/metadata/version files were installed;
   `BaseUtils.rdl` remained unchanged and `CommonUtils.IsDebug` remained false.
6. The official launcher started the game and the encrypted loader heartbeat was
   observed.
7. The first state build failed closed because `dailyBoxActive[1]` did not yet
   exist. The probe queued exactly one call to the game's recovered
   `DailyTaskManager:TryReqUpdateData()` path. Static bytecode proves that callback
   sends `MsgDefines.DailyQuestLs`, not a reward command.
8. The probe wrapped `DailyTaskManager.UpdateDailyTask` only to observe the
   response-driven state transition. The update was observed after the request,
   with 23 task records and five box thresholds populated.
9. Immediately after that update, the same in-game manager state was passed to the
   strict Lua snapshot builder. The builder independently cross-checked
   `GetCurValue()` and all five `GetBoxState()` results before writing JSON.
10. The game was closed and all four backed-up files were restored. SHA-256 values
    for every restored file exactly matched the pre-run values.

## Correlated live evidence

The repeatable runner produced this request/update/capture correlation:

```json
{
  "probeVersion": "lwcontrol-daily-state-probe-1",
  "state": "captured",
  "requestedAt": 1788634888,
  "updateObservedAt": 1788634893,
  "taskCount": 23,
  "boxCount": 5,
  "error": null
}
```

The corresponding snapshot was captured at
`2026-09-05T19:01:33Z` / `2026-09-06T03:01:33+08:00` and contained:

- schema version `1`, mode `state`;
- 23 daily-task records;
- symbolic task states only; numeric current-game `TaskState` values remain
  intentionally unknown;
- `currentPoint = 240`;
- received stages `[1, 2, 3, 4, 5]`;
- box activation thresholds `[40, 80, 120, 160, 200]`;
- all five exported box states `Received`.

An independent Python sum of template points for tasks symbolically marked
`Received` also produced `240`. The same live JSON was then read through
`JsonFiles.Read<CurrentDailyTaskSnapshot>()` and accepted by the C#
`CurrentDailyTaskSnapshot.Validate()` implementation with 23 tasks, current point
240, stages 1 through 5, and five box records.

These observed task completions and point totals are one account-state snapshot,
not universal template behavior. The recovered algorithms, field identities,
wire command, symbolic-state comparisons, and five box thresholds are the
technical evidence relevant to the reconstruction.

## Network boundary

The live state probe has exactly one bounded data-refresh path:
`DailyTaskManager:TryReqUpdateData()`. Current bytecode shows that this queues the
game's existing `DailyQuestLs` list request. The generated probe source contains
no `DailyQuestReward`, `DailyTaskReward`, `daily.quest.reward`, or
`daily.task.reward` action path. The bounded runs reported zero reward claims.

The request/result/state correlation is now:

`TryReqUpdateData` requested -> `UpdateDailyTask` observed -> 23 tasks / 5 boxes ->
snapshot built -> C# snapshot validator accepted.

## Repeatable command

Build the exact current probe candidate while the game is closed:

```powershell
python tools/install_loader_probe.py --prepare-dir "$env:LOCALAPPDATA\LWControl\candidate-daily" --json
python tools/install_loader_probe.py --verify-dir "$env:LOCALAPPDATA\LWControl\candidate-daily" --json
```

Run one bounded capture and automatically restore exact originals:

```powershell
python tools/run_daily_task_snapshot_probe.py --candidate-dir "$env:LOCALAPPDATA\LWControl\candidate-daily" --json
```

The runner refuses to start when Last War is already running, re-verifies the
exact candidate before installation, caps the live wait, creates an exact backup,
launches only through the official launcher, reads the heartbeat/status/refresh/
snapshot files, closes the game, restores from the backup in `finally`, and
requires SHA-256 equality after restoration.

## Proven vs unknown after this checkpoint

**PROVEN:** current encrypted Lua execution, one bounded `DailyQuestLs` refresh,
response-driven daily-task state population, symbolic 23-task snapshot capture,
five-box state capture, independent current-point derivation, C# validation, and
exact restore.

**UNKNOWN / not attempted:** numeric `TaskState` values, any reward-claim send,
claim-response correlation, or a post-claim state transition. Those remain outside
this read-only milestone.
