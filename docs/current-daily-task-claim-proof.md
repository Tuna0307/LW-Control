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
- `Net/Msgs/DailyQuestRewardMessage.luac`, prototype `0.1`, dynamically forwards
  its decoded message to
  `DataCenter.DailyTaskManager:DailyQuestRewardMessageHandle(t)`.
- `Net/Msgs/Alliance/AllianceDailyTaskRewardMessage.luac`, prototype `0.1`,
  dynamically forwards its decoded message to
  `DataCenter.DailyTaskManager:DailyTaskRewardMessageHandle(t)`.
- `Net/Msgs/Alliance/PushDailyQuestMessage.luac` is a separate current-build
  update path. On success it iterates `message.dailyQuest`, calls
  `UpdateOneDailyTaskInfo` for each entry, and broadcasts
  `EventId.DailyQuestSuccess`.
- `Net/Config/MsgMap.luac` maps `DailyTaskReward` to
  `Net.Msgs.Alliance.AllianceDailyTaskRewardMessage` and `PushDailyQuest` to
  `Net.Msgs.Alliance.PushDailyQuestMessage`. The recovered `SFSNetwork` receive
  path instantiates the mapped type and calls its `HandleMessage(t)` method.
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

The authoritative acceptance condition is a fresh post-action daily-task state
that proves the exact selected target changed to `Received`. Direct reward and
push handlers remain useful correlation evidence. An observed response error or
handler failure blocks success, but a missing/non-echo response field does not
override a fresh authoritative state transition; the live chest proof below is the
current-build evidence for that rule.

## 2026-09-06 post-reset bounded live attempt

After the user reported that the daily server state had reset, a fresh read-only
capture at `2026-09-06T04:08:01Z` contained three explicit task candidates:
`101`, `102`, and `119`, all symbolically `CanReceive`. `currentPoint` was `0`,
`receivedStages` was empty, and no chest was claimable.

Candidate `a36` implemented the one-request boundary above. Its exact encrypted
package SHA-256 was
`8858136a4ebbbc96e64f9be47ebaf62a1ebb6cacbcb807aa5db016a0d93ca927`.
The live runner refreshed the task manager once, built a fresh pre-action snapshot
at `2026-09-06T04:12:15Z`, selected task `101`, and sent exactly one explicit
`DailyTaskReward("101")` request. It issued no retry and no second reward request.

The injected wrapper around `DailyTaskRewardMessageHandle` did not observe the
matching handler before the bounded timeout, so the strict response-correlation
half of this proof remains unproven. The runner then closed the game and restored
the exact original files.

A separate fresh read-only capture at `2026-09-06T04:14:28Z` proved a real state
change after that one request: task `101` was now `Received`, satisfying the
state-effect half of the contract. Tasks `102` and `119` were also `Received`,
raising `currentPoint` to `60`; chest stage `1` consequently became
`CanReceive`. The reason those two additional task records changed after the
single request is not yet classified and must not be inferred from this run.

The detailed live evidence is recorded in
[`current-daily-task-claim-live-capture.md`](current-daily-task-claim-live-capture.md)
and [`current-daily-task-claim-live-evidence.json`](current-daily-task-claim-live-evidence.json).

## 2026-09-06 explicit chest-stage proof

After the task-101 test, a fresh read-only capture at `2026-09-06T04:35:28Z`
showed `currentPoint = 60`, `receivedStages = []`, and chest stage `1` as the
only `CanReceive` chest. Tasks `101`, `102`, and `119` were already `Received`.

Candidate `a38` then selected exactly chest stage `1` and sent exactly one
`DailyQuestReward(1)` request with no retry. The current-build
`DailyQuestRewardMessageHandle` wrapper was observed live and completed without a
handler exception or `errorCode`. Its response `stageArr`, however, was empty.
The immediate manager snapshot taken from that response path still showed stage
`1` as `CanReceive` with `receivedStages = []`.

A separate authoritative read-only `DailyQuestLs` refresh at
`2026-09-06T04:38:18Z` then showed `receivedStages = [1]` and chest stage `1` as
`Received`, while `currentPoint` remained `60`. This proves the exact owned
`DailyQuestReward(1)` request had the intended server-state effect. It also proves
that, for the current build, a successful direct chest response does **not** need
to echo the claimed stage in `stageArr` and must not be used as the sole effect
verifier.

The claim probe has therefore been revised to version
`lwcontrol-daily-claim-probe-3`. It still allows at most one explicit reward send,
but now records response/push evidence and verifies the exact target through one
fresh post-claim daily-task list refresh. An observed response error or handler
failure still fails closed. Missing or non-correlating response payload fields no
longer override a fresh authoritative state transition.

## Remaining research boundary

Both explicit current-build claim target types now have a live one-send state
effect proof: task `101` transitioned `CanReceive -> Received`, and chest stage
`1` transitioned `CanReceive -> Received`. The remaining protocol research is to
capture the exact task-response or `PushDailyQuest` payload that explains why
tasks `102` and `119` also changed after the task-101 request, and to classify the
special `DailyQuestReward(-1)` path. Those unknowns are not used by the bounded
claimer.

The revised same-run post-refresh verifier has passed its source checks and Lua
parse, but a fresh end-to-end live execution of probe version 3 still requires a
new `CanReceive` target. The current server state after the chest proof has no
eligible target beyond the already received stage `1`; the selector must therefore
send zero reward requests until a new target becomes claimable.

## Persistent runtime and desktop integration checkpoint

The claim contract is now implemented as a persistent clean-room runtime rather
than only a bounded research probe. Candidate `a42` is content version `12`, has
18,688 entries (official 18,686 plus preserved `LuaEntry` and the Daily Task
runtime), and has package SHA-256
`afc145b9614cc81697e7079723a688ee26ccd9c6a3aa569ecf31e341bc60c8f6`.
Its encrypted wrapper/runtime payloads round-trip exactly with the current xLua
key/nonce derivation.

The runtime registers through `UpdateManager.AddUpdate`, waits for the real Daily
Task managers after login, and processes one single-use `run_once` command at a
time. Each run performs a fresh `DailyQuestLs`; a target is selected only from that
fresh symbolic state. Each reward target is sent once, followed by another fresh
list request. Success requires the exact target's `CanReceive -> Received`
transition. The runtime then settles the confirmed attempt and re-detects from the
new state before sending any next target. Maximum claims per run is bounded to
`1..20`. An observed response/handler error fails the active run. The special
`DailyQuestReward(-1)` path is absent.

Live registration smoke proved `UpdateManager.AddUpdate` with no command present,
therefore zero command creation and zero reward sends. A fresh independent capture
at `2026-09-06T04:56:48Z` then proved the account had zero eligible targets:
tasks `101`, `102`, and `119` were `Received`, all other tasks `NoComplete`, stage
`1` `Received`, stages `2..5` `NoComplete`, and `currentPoint=60`.

That state allowed a safe end-to-end command proof. The same
`CurrentDailyTaskRuntimeClient` used by the desktop submitted `run_once` with
maximum `1`. At `2026-09-06T04:57:43Z` the runtime returned `completed /
no_eligible_target`, `RefreshSendCount=1`, `RewardSendCount=0`,
`ConfirmedClaims=0`, and a fresh validated final snapshot. After persistent
installation, a normal game launch repeated the proof at
`2026-09-06T04:59:18Z` with the same one-refresh/zero-reward result and a fresh
runtime heartbeat.

The installer was also exercised end to end. It restored the exact official
hashes on uninstall, then reinstalled `a42` and reproduced the exact runtime
hashes. `BaseUtils.rdl` remained unchanged throughout. The final installed data
hash is `afc145b9614cc81697e7079723a688ee26ccd9c6a3aa569ecf31e341bc60c8f6`;
the reversible install manifest and exact backup are kept under
`%LOCALAPPDATA%\LWControl`.

## 2026-09-06 persistent positive-path completion

A later naturally claimable state closed the last persistent-runtime validation
gap. The account had `currentPoint=80`, activity chest stage `2` claimable, and an
individual daily task still claimable. The same installed `a42` runtime and the
same `CurrentDailyTaskRuntimeClient` used by the desktop were exercised with two
separate `run_once` commands, each bounded to `maximumClaims=1`.

At `2026-09-06T05:26:29Z`, command
`daily-20260906052628-1ae66dc66bde49f09e9337c354d10a9d` sent exactly one reward
request and claimed `DailyQuestStage` `2`. The runtime reported
`RewardSendCount=1`, `ConfirmedClaims=1`, `RefreshSendCount=2`, then returned a
fresh authoritative snapshot with `receivedStages=[1,2]` and stage `2` in
`Received`. No retry or second reward send occurred in that command.

At `2026-09-06T05:26:41Z`, command
`daily-20260906052640-bd591dd9a9684156a0a8aaef037f1c4a` sent exactly one reward
request and claimed `DailyTask` ID `105`. It again reported
`RewardSendCount=1`, `ConfirmedClaims=1`, and `RefreshSendCount=2`. The final
authoritative snapshot showed task `105` in `Received` and `currentPoint=90`.
No retry or second reward send occurred in that command.

Immediately before these positive runs, a fresh normal game process had exposed a
stale runtime heartbeat even though the installed package still matched the
verified `a42` bytes. The client correctly refused to send while the heartbeat was
stale. A controlled uninstall restored the exact recorded official hashes, a
no-command smoke proved `a42` still registered through `UpdateManager.AddUpdate`,
and a clean persistent reinstall then produced a fresh heartbeat on a normal
launcher start. The transient cause of the earlier missed startup is not yet
classified, so heartbeat freshness remains a mandatory fail-closed dispatch gate.

The structured evidence for this completion checkpoint is recorded in
[`current-daily-task-runtime-positive-evidence.json`](current-daily-task-runtime-positive-evidence.json).

**Implementation status:** the current-build protocol, target selection,
authoritative verifier, persistent scheduler, command/result bridge, C# client,
bilingual desktop action, reversible installer, zero-target path, positive chest
path, and positive individual-task path are now implemented and live-proven
through the persistent runtime. The remaining `PushDailyQuest` attribution and
special `DailyQuestReward(-1)` semantics are research unknowns that the bounded
claimer does not use.
