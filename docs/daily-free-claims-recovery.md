# Daily Free Claims recovery notes

## What was analyzed

This pass continued static analysis of the supplied `LWControl.exe`. The executable
was not launched. The game was not opened or controlled, and no command was queued
to the Last War bridge.

The supplied executable is a .NET single-file bundle. Direct ILSpy decompilation of
the outer PE fails because the native host has no managed metadata. Parsing the .NET
bundle header recovered a version 6.0 bundle with 269 entries. The bundle header is
at byte 102,861,360 and the bundle ID is `HYOrSR4iwOur`.

The application-specific managed payloads include:

| Assembly | Uncompressed bytes |
| --- | ---: |
| `LastWarControl.Auth.dll` | 110,592 |
| `LastWarControl.Bridge.dll` | 24,576 |
| `LastWarControl.Core.dll` | 380,928 |
| `LastWarControl.Game.dll` | 4,894,720 |
| `LWControl.dll` | 43,364,352 |
| `LWControl.r2r.dll` | 72,130,560 |

Temporary extraction and decompilation copies were kept outside this repository.
The original binaries and recovered source are not committed here.
Exact source and extracted-artifact hashes are recorded in
[`daily-free-claims-evidence.json`](daily-free-claims-evidence.json). ILSpy
11.0.0.9375 was installed only into a temporary analysis directory for this pass;
no pre-existing ILSpy, dnSpy, or dotPeek command-line installation was found in
the common user/PATH locations checked.

## Recovered daily-claim architecture

`LastWarControl.Core.dll` contains a complete Daily Free Claims feature family:
options and validation, a planner, source ownership, an adapter registry, seven
source-specific adapters, eligibility checking, execution models, result
verification, and recovery policy.

The seven adapters and their recovered priorities are:

| Adapter ID | Priority | Resource/page locks |
| --- | ---: | --- |
| `daily_task_chest` | 900 | home + daily task page |
| `weekly_task_chest` | 890 | home + weekly task page |
| `vip_daily_reward` | 700 | home + VIP page |
| `store_daily_free_pack` | 690 | home + store page |
| `login_reward` | 650 | home + login event page |
| `tavern_free_recruit` | 600 | home + tavern page |
| `campaign_idle_reward` | 500 | home + campaign event page |

These seven sources are owned by `daily_free_claims`. Mail attachments, alliance
gifts, camp-armored rewards, and general world rewards are explicitly owned by
other feature modules, preventing two automation features from claiming the same
source.

## Recovered configuration contract

The original configuration is disabled by default, while all seven categories are
enabled inside the feature configuration. The maximum claims per run defaults to
20 and is constrained to 1 through 20.

The v1 contract is deliberately free-only. Premium currency, tickets, and
advertisement claims are all forced off. The desktop host serializes the feature
as a `run_once` command by default and explicitly sets `background=false`.

The embedded game module also rejects `start` mode; background Daily Free Claims
is not supported by this build. It accepts `state` and `probe` as read-only modes,
`stop` as a control operation, and `run_once` for execution.

## Runtime eligibility and ordering

The embedded `LWC2DailyFreeClaims` runtime is the closest recovered evidence to
the code that actually drives the game, so the new clean-room preview policy uses
its behavior as the parity target.

A source must be in the `Claimable` state. Its numeric currency cost must be zero
and the runtime must not mark it as having a currency cost. It is then considered
free when either a positive remaining-free-claims count is present, or the source
is explicitly marked free with a button semantic of `free` or `claim`.

The runtime candidate order is:

1. Task chests first when task-chest preference is enabled.
2. Adapter priority, descending.
3. Adapter ID, ordinal.
4. Source key, ordinal.

The managed C# planner in the reference application contains an additional expiry
ordering rule. The desktop host also forwards `prefer_expiring_rewards` to the
game. However, the embedded Daily Free Claims Lua planner in this supplied build
does not read expiry while sorting candidates. The two layers therefore disagree.
For runtime parity, this reconstruction currently follows the embedded runtime and
records the managed expiry option without using it to order preview candidates.

The local reconstruction still performs its own imported-observation identity,
freshness, duplicate, and expiry checks. Those are local fail-closed safeguards for
untrusted JSON observations, not claims that the original runtime uses the same
checks at that layer.

## Recovered execution and verification contract

The recovered runtime does not blindly click a list of rewards. It executes one
`claim_once` step, associates it with a claim-attempt ID, then re-reads state before
continuing. A live recreation should therefore re-detect after each claim instead
of executing a stale multi-item preview as a batch.

The recovered verification path correlates command ID, feature ID, adapter ID,
source key, reward ID, claim-attempt ID, capture ID, capture time, and server time.
It only accepts a claim effect when fresh post-state proves at least one matching
transition, such as a Claimable-to-AlreadyClaimed state change, a decrease in free
claim count, disappearance from the refreshed list, or a matching server reward
receipt.

The managed verifier additionally requires high-confidence authoritative evidence
from the server state manager or server reward receipt. A positive action return by
itself is not treated as successful completion.

The runtime recovery policy retries a page/route problem at most once. Paid/free
condition failures are blocked. Correlation, replay, stale-state, or other
unconfirmed outcomes become Unknown State instead of being retried as a new claim.

## Recovered local bridge transport

`LastWarControl.Bridge.dll` shows a file-command transport rooted at
`%LOCALAPPDATA%\LastWarControl` with these subdirectories:

- `commands\pending`
- `commands\processing`
- `commands\results`
- `commands\ledger`
- `commands\cancelled`
- `runtime`

Command envelopes use schema version 1 and contain command ID, feature ID,
request time, and string arguments. The pending queue has 64 named slots. The host
writes a temporary file and atomically moves it into a free slot. Command IDs are
single-use across pending, processing, result, cancelled, and ledger state.

Bridge health also checks for a running `LastWar` process and a fresh
`runtime\lua-heartbeat.json`. The recovered host uses a 15-second heartbeat
freshness window with a five-second future-clock tolerance.

This is enough to describe the transport format and build an offline adapter, but
it is not evidence that the supplied game-side bridge is currently installed,
compatible with the user's Last War build, or safe to invoke. No queue files were
created during this analysis.

## Current-machine bridge compatibility observation

A read-only inspection on 2026-09-05 found Last War running and an existing
`%LOCALAPPDATA%\LastWarControl` tree. The stored heartbeat identifies Daily Free
Claims as `lwc2-daily-free-claims-20`, exactly matching the embedded module recovered
from the supplied executable. However, the heartbeat file was last written on
2026-09-02 and predates the currently running Last War process, so it fails the
recovered 15-second heartbeat/correlation gate and must be treated as stale.

The current `LWScripts.data` is a valid LWLF container at content version 12 with
18,686 entries. Read-only parsing found the official `DataCenter/Global/LuaEntry.luac`
but did not find `LWC2DailyFreeClaims` or the bridge's preserved
`LuaEntry_original` entry. The current script container is therefore no longer the
previously injected bridge container.

The current `BaseUtils.rdl` has now been resolved further without modifying it. Its
`RGMD` metadata identifies `CommonUtils.IsDebug` as MethodDef RID 133 at RVA
`0x4BD0`, which maps to file offset `0x2DD0`. The body begins `08 16 2A`, matching
the supplied installer's accepted constant-Boolean method contract and currently
returning false. The previous hard-coded RVA `0x333C` / file offset `0x153C` simply
points elsewhere in the updated file. See [BaseUtils.rdl loader recovery](baseutils-rdl-recovery.md)
for the metadata evidence and read-only inspector.

The reconstruction does not attempt a game-file mutation from this evidence. The
C# app now includes a read-only bridge inspector that checks game
presence, heartbeat age, process-start correlation, pending queue count, and Daily
Free Claims version without creating a command file.

## Reconstruction changes from this pass

The C# preview planner now mirrors the recovered runtime in the areas proven above:

- default run limit is 20 and validation rejects larger values;
- all seven categories are selected by default while the master feature remains off;
- imported observations model the recovered source status and free-condition fields;
- runtime free eligibility uses Claimable status, zero-cost evidence, remaining-free
  count or the exact runtime free semantics;
- fixed adapter priorities and runtime ordering replace the earlier invented expiry
  ordering;
- preview output is labeled `preview-only/recovered-runtime-policy`;
- a read-only local bridge inspector reproduces the recovered 15-second heartbeat,
  five-second future tolerance, and process-start correlation checks.

The application still sends zero game actions. Live work should begin with a
read-only `state`/`probe` adapter and current bridge-heartbeat compatibility check,
then add one bounded claim only after the read/result correlation path is verified
against the current game installation.

## Current-game Lua daily-task protocol recovery

The recovered LWLF-v3 decoder made it possible to inspect the currently installed
content-version-12 Lua bytecode directly. This is independent current-game
evidence, rather than behavior inferred only from the supplied controller binary.
No network message was sent during this analysis.

`tools/inspect_lua53_bytecode.py` parses the game's custom Lua 5.3 format byte `1`
and retains prototype source lines, local names, constants, and instructions. The
current archive's `Net/Config/MsgDefines.luac` proves these protocol mappings:

| Symbol | Wire command |
| --- | --- |
| `DailyQuestLs` | `daily.quest.ls` |
| `DailyQuestReward` | `daily.quest.reward` |
| `DailyTaskReward` | `daily.task.reward` |
| `PushDailyQuest` | `push.daily.quest` |
| `PushTaskComplete` | `push.task.complete` |

The daily-task manager's `TryReqUpdateData` sends exactly:

`SFSNetwork.SendMessage(MsgDefines.DailyQuestLs)`

Current UI code proves two separate claim request contracts.

Daily goal/chest rewards use `DailyQuestReward`. `UI/UIMainTask/Component/UIBox`
sets the manager's current reward to `param.index` and sends:

`SFSNetwork.SendMessage(MsgDefines.DailyQuestReward, param.index)`

`Net/Msgs/DailyQuestRewardMessage:OnCreate(param)` serializes that argument as:

`sfsObj:PutInt("stage", param)`

`UI/UILWQuest/UILWQuestList/Component/UILWDailyTaskGoalItem` first checks
`TaskState.CanReceive`, requires a valid index, guards repeat sending with
`isSend`, and also contains a current path that sends `DailyQuestReward` with
`-1`. The exact semantic meaning of `-1` is not yet classified beyond being a
special value used by current UI code.

Individual daily tasks use `DailyTaskReward`. Both the old/main-task UI and the
current quest-list UI send the task identity directly, for example:

`SFSNetwork.SendMessage(MsgDefines.DailyTaskReward, info.id)`

`Net/Config/MsgMap.luac` maps this command to
`Net.Msgs.Alliance.AllianceDailyTaskRewardMessage`. Despite that historical class
namespace/name, its current `OnCreate(taskId)` serializes the argument as:

`sfsObj:PutUtfString("taskId", taskId)`

and its response handler dispatches to
`DataCenter.DailyTaskManager:DailyTaskRewardMessageHandle(t)`.

The response side gives the evidence needed for fail-closed verification. All
three recovered manager handlers first test `message.errorCode`; a non-null error
is displayed and does not enter the success-state path. On success:

- `DailyQuestLsMessageHandle` calls `UpdateDailyTask(message)` and broadcasts
  `EventId.DailyQuestLs`;
- `DailyQuestRewardMessageHandle` adds rewards/resources, applies every value in
  `message.stageArr` through `SetCurReward`, and broadcasts
  `EventId.DailyQuestReward`;
- `DailyTaskRewardMessageHandle` adds rewards/resources, updates every item in
  `message.taskInfo` through `UpdateOneDailyTaskInfo`, and broadcasts
  `EventId.DailyQuestSuccess`.

The current `DataCenter/DailyTaskData/DailyTaskInfo.luac` also proves the local
state shape used by those task updates. A fresh object initializes these fields:

| Field | Initial value |
| --- | ---: |
| `id` | `0` |
| `num` | `0` |
| `totalNum` | `0` |
| `totalTimes` | `0` |
| `state` | `0` |
| `reward` | empty table |

`DailyTaskInfo:UpdateInfo(message)` treats all six fields as partial-update
fields: it only overwrites a field when the corresponding message value is not
`nil`. A non-null `reward` is normalized through
`DataCenter.RewardManager:ReturnRewardParamForView(message.reward)` before being
stored. `DailyTaskManager:UpdateOneDailyTaskInfo(message)` keys lookup by
`message.id`, creates a new `DailyTaskInfo` when no record exists, calls
`UpdateInfo`, and stores newly created records in `dailyQuestTasks` by task id.
This gives the read-only bridge a concrete current-build task-state schema without
requiring any claim request.

The manager's daily goal/chest state is now also recovered. `UpdateDailyTask`
clears the current state on each non-null update, sets `curReward` to `0`, then
replaces it with `message.curReward` when that field is present. The same update
rebuilds `dailyBoxActive` from each `message.rewardList` item whose `point` is
present by appending that point with `table.insert`, while reward payloads are
normalized through `RewardManager:ReturnRewardParamForView`. `message.dailyQuest`,
when present, is iterated into `UpdateOneDailyTaskInfo`.

`SetCurReward(value)` does exactly one operation: `table.insert(self.curReward,
value)`. The current `GetBoxState(index, curPoint)` contract is therefore:

1. if `index` occurs anywhere in `curReward`, return `TaskState.Received`;
2. otherwise look up `dailyBoxActive[index]`; when it exists and is less than or
   equal to `curPoint`, return `TaskState.CanReceive`;
3. otherwise return `TaskState.NoComplete`.

`GetCurValue()` is also recovered exactly. It starts `result` at zero and iterates
`dailyQuestTasks`. For a task whose `state` equals `TaskState.Received`, it asks
`DailyTaskTemplateManager:GetQuestTemplate(k)` for the template keyed by that
task-table key. If the template exists, it adds `template.point` to `result`.
Other task states and missing templates contribute nothing. The returned sum is
the `curPoint` consumed by `GetBoxState`.

`IsAllBoxRewardReceived()` obtains the current point value and calls that state
function for exactly five indices, `1` through `5`; it returns false on the first
non-`Received` box and true only when all five are received. This means a future
read-only bridge does not need to infer chest state from UI pixels: the minimum
state needed to reproduce the game's decision is the received-stage list,
per-index activation threshold, plus task state/template-point pairs from which
the current point can be reproduced. The C# core now contains
`CurrentDailyTaskState`, a symbolic interpreter for this exact contract including
`GetCurValue`. It does not assume the numeric values of the game's `TaskState`
enum and sends no action. The live in-game read-only probe avoids exporting an
unknown numeric enum entirely by comparing against `TaskState.Received` inside
Lua and exporting the derived symbolic state/current point.

The reconstruction now also has a strict version-1 JSON snapshot boundary for
the live probe. It carries task IDs, symbolic task states, template points,
the exported and independently re-derived current point, received chest-stage
indices, all five activation thresholds and symbolic chest states, plus capture
and heartbeat metadata. The C# validator rejects duplicate task IDs, unsupported
schema/mode values, numeric or unknown task-state values, malformed stage/box
indices, invalid point ranges, stale/future timestamps, and any mismatch between
the exported current point/chest state and the recovered manager algorithm. See
[`current-daily-task-snapshot.md`](current-daily-task-snapshot.md). These input
limits and freshness checks are local fail-closed safeguards; the point and chest
derivation rules are the current-game behavior recovered above.

On 2026-09-06 the separate encrypted loader-heartbeat candidate was accepted by
the current game in a bounded launch. `LastWar.exe` started through the official
launcher and produced a fresh `lwcontrol-loader-probe-1` heartbeat. No gameplay
message was sent. The game was then closed, the original script package and
`BaseUtils.rdl` were restored, and every backed-up file matched its original
SHA-256. This proved current-build encrypted Lua payload execution at the loader
heartbeat level and enabled the daily-task live-capture milestone below.

That next milestone is now complete. The first state-only live attempt reached
the real `DailyTaskManager`, `DailyTaskTemplateManager`, and global `TaskState`,
but failed closed because `dailyBoxActive[1]` was still absent. Static inspection
of prototype `0.4`/`0.4.0` then confirmed that `TryReqUpdateData()` appends a
callback whose body calls `SFSNetwork.SendMessage(MsgDefines.DailyQuestLs)`.
Prototype `0.9` confirms that a successful `DailyQuestLsMessageHandle` calls
`UpdateDailyTask(message)` before broadcasting `EventId.DailyQuestLs`.

The bounded probe was therefore extended with one and only one call to the game's
own `TryReqUpdateData()` refresh plus read-only instrumentation around
`UpdateDailyTask`. In the repeatable 2026-09-06 run the request was recorded at
Unix time `1788634888`, `UpdateDailyTask` was observed at `1788634893`, and the
manager then contained 23 tasks and five box thresholds. The same update produced
a version-1 snapshot with `currentPoint=240`, received stages `1..5`, thresholds
`40,80,120,160,200`, and all five box states `Received`. Independent point
summation produced 240 and the C# `CurrentDailyTaskSnapshot.Validate()` path
accepted the live JSON. The runner then closed the game and restored all backed-up
files with exact SHA-256 equality. Full evidence and the repeatable command are in
[`current-daily-task-live-capture.md`](current-daily-task-live-capture.md).

No `DailyQuestReward` or `DailyTaskReward` action was sent. Numeric `TaskState`
values remain deliberately unknown; the live probe compares the game's symbolic
enum members directly.

For repeatable static analysis, `tools/inspect_lua53_bytecode.py` now accepts an
exact `--prototype` path (for example `0.10`). In that mode it emits the complete
instruction and constant body for only that prototype, reducing unrelated output
while preserving the same read-only package decode path.

`SFSNetwork.SendMessage` resolves each command through `Net.Config.MsgMap`, builds
the mapped message object, converts it to binary, and calls the managed network
layer's `SendLuaMessage`. These facts are sufficient to specify the current daily
task read/request/response contract for reconstruction. They do not constitute a
live claim test, and the desktop controller still sends zero game actions.
