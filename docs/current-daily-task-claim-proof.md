# Current daily-task bounded claim proof contract

This checkpoint defines the first action-proof boundary after the successful
read-only daily-task capture. It still sends no reward command. A later live test
must fail closed unless a fresh snapshot proves an exact target is claimable and a
later snapshot proves the expected state change.

## Current package evidence

The installed content-version-12 package was re-inspected on 2026-09-06.

- `UI/UIMainTask/Component/UIBox.luac`, prototype `0.10`, checks
  `TaskState.CanReceive`, uses `isSend` to suppress a repeat click, records the
  current stage, and calls
  `SFSNetwork.SendMessage(MsgDefines.DailyQuestReward, param.index)`.
- `Net/Msgs/DailyQuestRewardMessage.luac`, prototype `0.0`, serializes that
  argument through `PutInt("stage", param)`.
- `Net/Msgs/Alliance/AllianceDailyTaskRewardMessage.luac`, prototype `0.0`,
  serializes the task identity through `PutUtfString("taskId", taskId)`.
- `DataCenter/DailyTaskData/DailyTaskManager.luac`, prototype `0.11`, rejects a
  non-null `errorCode`; on success it processes `stageArr` through
  `SetCurReward` and broadcasts `EventId.DailyQuestReward`.
- The same manager, prototype `0.27`, rejects a non-null `errorCode`; on success
  it processes `taskInfo` through `UpdateOneDailyTaskInfo` and broadcasts the
  task-success event recovered earlier.

The newer quest-list UI also contains a `DailyQuestReward` send with stage `-1`.
Its exact semantics remain unclassified. The bounded proof excludes `-1` and
permits only an explicit stage `1..5` or an explicit task ID already present in a
validated snapshot.

## Offline selector

`CurrentDailyTaskClaimProof` consumes a fresh, validated
`CurrentDailyTaskSnapshot` and emits candidates only for state explicitly marked
`CanReceive` by the game-derived snapshot:

- a daily chest candidate carries one exact stage from `1..5`;
- an individual daily-task candidate carries one exact task ID;
- received or incomplete targets are never candidates;
- stale, future, malformed, or internally inconsistent snapshots are rejected by
  the existing snapshot validator before selection.

Candidates are ordered by chest stage first, then task ID. `SelectOne()` therefore
chooses at most one exact target for a future bounded test.

## Post-action proof rule

The offline effect verifier does not treat a send return as success. Given a
candidate plus validated before/after snapshots:

- a chest succeeds only when the exact stage was `CanReceive` before, absent from
  `receivedStages` before, `Received` afterward, and present in
  `receivedStages` afterward;
- a task succeeds only when the exact task ID was `CanReceive` before and
  `Received` afterward;
- the candidate must have been derived from the same pre-action capture ID;
- the post-action snapshot may not precede the pre-action snapshot.

This is the state-effect half of the eventual live proof. A future live runner
must also observe the matching success/error handler for the one in-flight request
before accepting the state transition.

## Current live account state

The successful 2026-09-06 capture had all five chest stages already `Received`.
Its 23 tasks contained only `Received` and `NoComplete` states; no task was
`CanReceive`. That captured state therefore contains zero eligible claim targets
and must result in zero reward sends.

## Remaining live milestone

When a future fresh snapshot contains an explicit `CanReceive` target, the first
live claim proof can be bounded to exactly one request. It must record the chosen
target and pre-state, send exactly one explicit-stage or explicit-task request,
observe the corresponding success/error handler, capture a fresh post-state, and
accept success only if response correlation and the state-effect rule both match.
Exact game-file backup/restore rules from the read-only runner still apply.
