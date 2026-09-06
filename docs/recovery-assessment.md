# LWControl managed-code recovery assessment

This follow-up advances the initial string-based assessment by parsing the
application's bundle directory and the CLI metadata of two embedded assemblies.
No uploaded code was executed. Missing developer documentation does not block
this kind of static analysis.

## Confirmed bundle structure

The .NET bundle signature occurs at byte 8,351,608 of `LWControl.exe`. Its header
pointer resolves to byte 102,861,360. The directory declares bundle version 6.0
and 269 entries. Parsing those entries terminates at byte 102,876,940, exactly
the end of the supplied executable. Entry offsets and stored lengths were
checked against the containing file's bounds.

The directory contains 264 entries classified as assemblies, three native
entries, one runtime-configuration entry, and one dependency-configuration entry.
Bundle format version 6 is distinct from the application's .NET runtime version.

Application-specific assembly entries include:

| Assembly | Declared uncompressed bytes |
| --- | ---: |
| `LWControl.dll` | 43,364,352 |
| `LWControl.r2r.dll` | 72,130,560 |
| `LastWarControl.Core.dll` | 380,928 |
| `LastWarControl.Game.dll` | 4,894,720 |
| `LastWarControl.Bridge.dll` | 24,576 |
| `LastWarControl.Auth.dll` | 110,592 |

Only `LWControl.dll` and `LastWarControl.Core.dll` were copied out for further
metadata inspection. Their output lengths match their bundle entries, and both
have PE and CLI metadata headers. These local analysis copies are not published
in this repository. The original hash remains recorded in `evidence.json`.

This supersedes the initial report's uncertainty about whether the application
is a .NET bundle: its directory is now parsed, not merely inferred from strings.

## Metadata and method-body findings

| Assembly | Type definitions | Method definitions | Methods with IL body RVAs |
| --- | ---: | ---: | ---: |
| `LWControl.dll` | 389 | 3,765 | 3,752 |
| `LastWarControl.Core.dll` | 256 | 2,510 | 2,471 |

Both assemblies contain standard metadata streams: `#~`, `#Strings`, `#US`,
`#GUID`, and `#Blob`. The counts above come from the TypeDef and MethodDef tables,
not from matching printable strings. Every nonzero method-body RVA in these
two assemblies maps to a recognized tiny or fat IL header form. Counts include
property accessors, constructors, and compiler-generated code; they are not
counts of distinct user-facing features.

These checks establish retained metadata and candidate IL bodies. They do not
validate every instruction, resolve every reference, prove successful C#
decompilation, or establish that the reconstructed code compiles. The original
source files, comments, project structure, and tests have not been recovered.

## Feature structure is now visible in type metadata

The core assembly defines separate components for:

- Daily free claims: options, eligibility checks, planners, adapters, verification,
  and recovery policy.
- Radar: configuration, task/state models, planning, startup recovery, and verification.
- Rally participation: configuration, candidate/team state, planning, and verification.
- Troop promotion: barracks/inventory models, options, planning, verification, and recovery.
- Shared automation: scheduling, feature running, resource locks, and runtime/configuration stores.

Daily claims is the selected first candidate for a bounded feature study. Its
`DailyFreeClaimsFeature` type declares `Detect`, `CanRun`, `BuildPlan`,
`ExecuteAsync`, `Verify`, `Recover`, and `Stop`. Related types include
`DailyFreeClaimsPlanner`, `FreeEligibilityVerifier`, and `FreeClaimVerifier`.

Named adapter types cover VIP daily rewards, store free packs, daily and weekly
task chests, login rewards, tavern free recruitment, and campaign idle rewards.
This establishes a feature decomposition. It does not yet reveal their exact
eligibility rules or demonstrate a live action.

## Implication for the reconstruction

LWControl remains the preferred reference because it demonstrably retains named
managed types and method bodies. Comparable source-level recoverability has not
been established for the Rust lwbridge executable. This is a reasoned choice,
not a guarantee of full feature parity or an estimate of completion time.

The next technical work is to recover and understand a bounded set of daily-claim
decision methods, identify their inputs and outputs, and derive behavioral tests
from the actual logic. The current Python example remains a synthetic prototype;
it must not be presented as recovered behavior.

An SDK is not necessary to continue static study of ordinary feature logic,
configuration formats, and application structure. Live integration remains
unimplemented and requires separate investigation and Windows testing. The
metadata findings do not establish that a complete, working bot can be rebuilt
without additional dependencies or runtime observations. This assessment does
not implement game injection, license bypass, or protection circumvention.

## 2026-09-06 world-map scanner checkpoint

The current installed game now provides a stronger reconstruction boundary than
the earlier targeted-search work. Static inspection of the current
`Assembly-CSharp.rdl` proves that `WorldPointManager` owns structured loaded
world-point state and drives an AOI/block request pipeline through
`WorldGetBlockMessage`, `HandleViewPointsReply`, `ParseWorldGetBlock`, and
`AddPointInfo`. The current generated `Protobuf.WorldPointInfo` also exposes
structured point identity/routing fields and typed payloads.

This moves the World Map Scan feature beyond loaded-state discovery: the bulk
source is identified, the read-only snapshot contract is implemented and proven
live, and bounded multi-block, serial multi-batch, plus a two-inflight
concurrent wave are now proven end to end. The detailed
evidence, PROVEN/UNKNOWN boundary, request fields, and schema are recorded in
[`current-world-map-snapshot.md`](current-world-map-snapshot.md) and
[`current-world-map-static-evidence.json`](current-world-map-static-evidence.json).
No game launch, network send, or installed-file modification was used for this
checkpoint.

The user-provided original `LWControl.zip` has now yielded the embedded
`LWC2MapScanner.lua` v108 and `LWC2AutoJoinRally.lua` v49 source resources plus
their managed Core/Game contracts. This resolves the loaded-point enumeration
uncertainty: the original scanner reflects `WorldPointManager._pointInfos` and
enumerates structured values. It also proves that original Auto Join Rally uses
an `AllianceWarDataManager` candidate selector and `MarchUtil.StartMarch` with
strict post-state verification rather than merely toggling the game's native
alliance auto-rally switch. The full evidence boundary is recorded in
[`original-lwcontrol-map-rally-recovery.md`](original-lwcontrol-map-rally-recovery.md)
and [`original-lwcontrol-map-rally-evidence.json`](original-lwcontrol-map-rally-evidence.json).
A clean-room offline reconstruction of the proven v49 candidate/squad gates is
implemented in `src/LWControl.Core/RecoveredAutoJoinRally.cs`.

The dedicated current-build world-map probe first proved the loaded-state boundary
live. Reverse engineering of the current `UIMainChangeScene`/`SceneUtils`
bytecode showed that the current UI uses a Lua `UIButton:SetOnClick` callback and
that the canonical City-to-World routine is `SceneUtils.ChangeToWorld`; the
older raw Unity `Button.onClick:Invoke()` assumption did not change the current
scene. Candidate a13 used the current routine once, observed authoritative
`CS.SceneManager.CurrSceneID == SceneManagerSceneID.World`, resolved
`CS.SceneManager.World.PointManager`, waited for `_pointInfos` to stabilize at
86 records, and emitted a schema-v1 snapshot. Exact installed hashes were
restored afterward. The evidence and remaining wider-AOI boundary are recorded
in [`current-world-map-live-capture.md`](current-world-map-live-capture.md) and
[`current-world-map-live-evidence.json`](current-world-map-live-evidence.json).

Candidate a20 then proved the next transport boundary. With the visible AOI at
blocks X `53..57`, Y `46..49`, it selected adjacent logical block `(58,47)` and
used the original scanner's recovered minimum 5-by-4 padded transport envelope,
X `56..60`, Y `46..49`. A small managed bridge supplied the exact current C#
argument types to `WorldPointManager.SendAoiRequest`. One request produced one
correlated `world.get.block` response whose authoritative nested
`serverPointArr` bounds covered exactly that padded envelope. The manager then
contained 13 identities not present before the request. There were no retries or
camera moves, the response hook and touched manager flag were restored, and the
installed game/script hashes were restored exactly afterward.

The recovered scanner policy is now implemented offline in
`src/LWControl.Core/RecoveredWorldMapScan.cs`. The clean-room planner mirrors
the original centered odd scan window, row-major block IDs, 160-index native
batch limit, minimum 5-by-4 transport padding, serpentine batch order, and
logical-block response coverage accumulation. Its checks reproduce the proven
a20 request geometry and verify that partial response coverage does not become a
successful batch.

A bounded three-logical-block candidate (`a21`, package SHA-256
`3969bfefb53a2c4da757f184ca6a891e4b4c20d836b0150640233e2488c466ad`)
was then executed live. One padded request covered all three requested logical
blocks through a correlated authoritative `world.get.block` envelope, with zero
retry/camera activity and exact post-run hash restoration.

Candidate `a22` then exercised the recovered 160-index split with the smallest
odd square above that ceiling: 13 by 13 = 169 logical blocks. The planner
produced 156 + 13 logical blocks. Batch 1 reached 156/156 correlated coverage
before batch 2 was sent; batch 2 reached 13/13. The final result was 169/169,
exactly two sends, zero retries, zero camera moves, restored hook/manager state,
and exact installed-file hash restoration. Package SHA-256 was
`6e8abb9bbbedb7292d959f6006ddb9db04370c8e5a7e6e94e2af935f9b08defd`.

Candidate `w23` then exercised the recovered serial-first/concurrent scheduling
shape using a 19-by-19 logical window. The recovered split was 152 + 152 + 57.
Batch 1 completed 152/152 first. Batches 2 and 3 were then both sent before the
first concurrent response arrived: send completion events were 2 and 4, the
wave completed at event 5, and responses arrived at events 6 and 7. The final
result was 361/361, peak in-flight width 2, exactly three sends, zero retries,
zero camera moves, and exact hook/manager/file restoration. Package SHA-256 was
`c67b7c71dc1e422e46d86f5a54ecba330c6c1a369f4419d37ee7aa679acd33fd`.

The remaining scanner work is broader orchestration and full-world completion
proof. The wider original concurrency policy, retry/camera fallback, and
terminal full-world coverage remain unproven.

## 2026-09-06 Auto Join Rally current-build checkpoint

The current-build Rally boundary has advanced from static recovery to a live
read-only manager capture plus one exact list refresh. The first candidate a22
exposed a scheduling problem: the probe only pumped during early Lua startup and
therefore timed out before stable player identity. Reverse engineering of the
already successful World Map runtime path led to recurring registration through
`UpdateManager.AddUpdate`; a23 then captured three free current formations and
an empty Alliance War manager with zero sends, zero joins, and zero claims.

The original LW Control source showed that an empty manager list is refreshed
before it is treated as final. Current content-version-12 bytecode independently
confirmed `MsgDefines.GetAllianceWarList == "alliance.team.ls"`, the current
message handler's `InitAllianceWarList(teams)` update, and the UI's exact
`SFSNetwork.SendMessage(MsgDefines.GetAllianceWarList,
LuaEntry.Player:GetCurServerId())` call. Candidate a24 used that recovered call
exactly once. One owned send correlated to one current handler response with a
`teams` field; the post-handler manager remained empty, with no retry, no Rally
join, and no reward claim. For that capture, zero active Rallies is therefore an
authoritative refreshed result rather than a stale-manager assumption.

The live evidence and candidate hashes are recorded in
[`current-rally-live-capture.md`](current-rally-live-capture.md) and
[`current-rally-live-evidence.json`](current-rally-live-evidence.json). A strict
clean-room importer now validates these snapshots and maps the directly proven
formation index/free/stamina fields into `RecoveredRallySquad`. It drives the
offline selector only for the authoritative empty-after-refresh case, producing
`auto_join_rally_no_new_joinable_rally`. Non-empty current Rally candidate
mapping remains intentionally blocked until a real Rally verifies the current
target-field/category combinations instead of guessing them.

Subsequent content-version-12 bytecode recovery narrowed that block further.
Current `AllianceWarDataManager.GetAllianceWarDurationSec` proves that raw
`waitTime` and `marchTime` are absolute server-time millisecond values; the game
compares them directly with `UITimeManager:GetServerTime()` and converts them to
seconds only when producing a phase countdown. This is incompatible with LW
Control v49's old direct use of those raw fields as planner durations. The
read-only snapshot builder now records the current manager-derived
`remainingSeconds` instead of carrying that old assumption forward.

Current `UIAllianceWarMainTableCtrl.OnJoinClick` and `Util/MarchUtil.luac` also
recover the richer normal join call chain. The main-table UI invokes
`OnClickStartMarch(JOIN_RALLY, leaderMarch.startId, uuid, -1, 1, targetType,
data.server, data.worldId, monsterSpecialType)`, and the dedicated
`MarchUtil.OnJoinRally(selfMarchUuid, rallyType, targetUuid, targetPointId,
curStamina)` checks the current JOIN_RALLY stamina cost before calling
`StartMarch(JOIN_RALLY, targetPointId, targetUuid, -1, selfMarchUuid)`. No live
join was performed.

The same reverse-engineering pass fully recovered
`AllianceWarDataManager.CheckJoinAllianceWarByWarData`: states 1-8 are explicit
rejection/leader/own-target cases, while state 9 returns `canJoin = not inTeam`.
Its only timing gates are positive `GetAllianceWarDurationSec` and the current
wait deadline (with the build's `<9527` legacy/sentinel handling). There is no
selected-formation travel-time gate in manager eligibility.

Both current formation selection variants were then traced through
`OnAtkClick`, `OnCheckTime`, `CheckCanBattle`, and their confirm closures. For
`JOIN_RALLY + RALLY_FOR_BOSS`, the game calculates selected-formation travel
time and compares it with `assembly_monster_toplimit/k3 * 60`. Exceeding that
threshold shows warning `110204`, but confirmation continues to
`ChangeMarchByType`/`OnCreateClick`; `CheckCanBattle` contains no hidden Rally
travel-time check. The current adapter therefore must not invent that threshold
as a hard joinability rule. The historical v49 planner remains unchanged and
separate.

The next reverse-engineering pass resolved the current target-field structure
far enough to remove the generic guesses from the adapter. `Global/EnumType.luac`
defines `AllianceTeamType` as `ATTACK_BOSS=0`, `ATTACK_BUILDING=1`,
`ATTACK_CITY=2`, `ATTACK_AL_CITY=3`, `ATTACK_ALLIANCE_THRONE=4`,
`ATTACK_DRAGON_BUILDING=5`, `ATTACK_SERVER_THRONE=6`, `ATTACK_AL_CENTER=7`,
`ATTACK_CITY_STRONGHOLD=8`, `ATTACK_EPIDEMIC_BUILDING=10`,
`ATTACK_EPIDEMIC_CITY=11`, `ATTACK_OUTPOST=12`, and `ATTACK_ZWL=13`; the
current enum assigns no value 9. `AllianceWarInfo` stores `targetUuid` and
`targetUid` independently. It also initializes `targetBaseSkinId` and
`targetLevel` to zero and `ParseData` copies the corresponding incoming message
fields when present. The list-response chain is now traced from `t.teams`
through `InitAllianceWarList` and `UpdateOneAllianceWarList` into that
`AllianceWarInfo.ParseData` call; `UIAllianceWarMainTableCtrl` later fetches the
same object by Rally UUID and reads these fields for city/epidemic-city icon
selection.

`UIAllianceWarDetailCtrl.GetWarItemData` proves that boss display level/name are
resolved from `MonsterTemplateManager:GetMonsterTemplate(targetUid)`, while an
`ATTACK_AL_CITY` uses `WorldCity(targetContentId)` for its level and fallback
name. The main-table `OnJoinClick` Rally branch uses the Alliance War `uuid` and
`leaderMarch.startId` as the join identity/point and assigns Rally target types
for boss, building, alliance-city, city, epidemic-city, and city-stronghold. It
also forwards `data.server` and `data.worldId`; `AllianceWarInfo.ParseData`
copies those values from `message.server` and `message.worldId`. The classic
table item omits `monsterSpecialType`, while `LWAllianceWarItem` forwards
`dataInfo.monsterSpecialType`. Its `RefreshData` gets that row from
`UIAllianceWarMainTableCtrl.GetWarItemData(uuid, true)`, whose boss branch
derives the value from `MonsterTemplateManager.GetMonsterTemplate(targetUid).special`;
non-boss branches leave it unset.
`CurrentRallySnapshot` and the read-only probe are now schema v5 so the six-type
map, server/world route, boss-only monster-special derivation, template-resolved
boss name/level, leader-inclusive member count, and optional localized boss
display name are validated explicitly. The proven
`targetBaseSkinId`/`targetLevel` fields remain recorded with
`AllianceWarInfo.ParseData` provenance, while the old guessed `data.level`
fallback remains excluded.
Fresh schema-v3 offline candidates were then prepared and encrypted round-trip
verified without touching the installed files: a29 read-only package SHA-256
`564901c298c743dd821dfd8b9035a211a4e7b45ba762d106b207e90ce41ee8ca`
and a30 one-refresh package SHA-256
`adf005f48bb6766038a6d7ddd8a26751fe0b43780d18e78a2fd103b95d237860`.
Those packages are now historical schema-v3 evidence. After correcting the
main-table map and server/world route, preliminary schema-v4 a31/a32 were
prepared and encrypted round-trip verified offline: a31 package SHA-256
`3046414cc764c10ce994edef1b3c4188475b1267111d6945d4e5405128764aab`
and a32 package SHA-256
`4efc161edfc350549df7c6f3b908bcf851a15f20f508f1b818a2b8b1e112c700`.
The subsequent `monsterSpecialType` producer trace superseded those preliminary
artifacts. Final schema-v4 a33/a34 were then prepared and round-trip verified:
a33 package SHA-256
`7def3ad0a0653670944ec727cb49a2bc7517fc9a8e30dd2fa7d7f185a2ddd928`
and a34 package SHA-256
`2a8e8a5227dff29185a1fd3a13341ad8e97495aa02b61ea21e09cf99f239a001`.
All four v4 builds reported `changed_installed_files=false`; they are retained as
historical schema-v4 evidence.

A real current joinable boss Rally was subsequently captured read-only. Its raw
row had `targetUid=38`, empty `targetName`, `targetLevel=0`, `joinState=9`, and an
empty `memberList`; the current monster template resolved name value `300602`
and level `30`. Current manager bytecode proves occupancy is
`table.count(memberList)+1`, so that live row has one participant including the
leader. Current UI bytecode also proves boss display text comes from
`CS.GameEntry.Localization:GetString(monster.name)`, although the exact returned
localized string was not retained in that capture.

The schema-v5 adapter now converts non-empty joinable snapshots into the
recovered planner. It deliberately maps target taxonomy to `Unknown` until an
authoritative structured current world-point type is correlated to the exact
Rally target, and it leaves planner `MarchSeconds` unknown because current raw
`marchTime` is an absolute timestamp. This removes the previous non-empty
conversion block without inventing `WorldBoss` or travel-time semantics.

## 2026-09-06 Daily task bounded claim and bilingual desktop checkpoint

The daily reset supplied the first fresh current-build claimable state. A
read-only capture at `2026-09-06T04:08:01Z` showed tasks `101`, `102`, and `119`
as `CanReceive`, with zero daily points and no claimable chest. A bounded
content-version-12 candidate then selected task `101` and sent exactly one
`DailyTaskReward("101")` request with no retry. The expected injected
`DailyTaskRewardMessageHandle` wrapper did not observe the response before the
timeout, so strict request/response correlation remains UNKNOWN.

A separate read-only refresh after restoration nevertheless proved the selected
state effect: task `101` changed from `CanReceive` to `Received`. Tasks `102` and
`119` were also `Received`, producing `currentPoint=60` and making chest stage 1
`CanReceive`. The reconstruction does not yet attribute those additional task
changes to a particular response shape. Full evidence and exact hashes are in
[`current-daily-task-claim-live-capture.md`](current-daily-task-claim-live-capture.md)
and [`current-daily-task-claim-live-evidence.json`](current-daily-task-claim-live-evidence.json).
The game was closed and the official 18,686-entry script package plus unchanged
`BaseUtils.rdl` were restored after the tests.

A second bounded action then exercised the explicit daily chest path. Fresh state
at `2026-09-06T04:35:28Z` showed `currentPoint=60`, no received stages, and chest
stage `1` as `CanReceive`. Candidate `a38` sent exactly one
`DailyQuestReward(1)` request. The current `DailyQuestRewardMessageHandle` was
observed live with no `errorCode` and no handler exception, but its `stageArr` was
empty and the manager still showed stage `1` as `CanReceive` inside that immediate
response path. A separate authoritative refresh at `2026-09-06T04:38:18Z` then
showed `receivedStages=[1]` and stage `1` as `Received`, proving the one-shot chest
effect while disproving the assumption that the direct response must contain the
claimed stage.

Current bytecode inspection also recovered the separate `PushDailyQuest` message
path: it iterates `message.dailyQuest` and applies every entry through
`DailyTaskManager:UpdateOneDailyTaskInfo`. This is a concrete candidate for the
multi-task update seen after task `101`, but no matching live push payload has yet
been captured, so that attribution remains UNKNOWN.

The daily claim probe is now version `lwcontrol-daily-claim-probe-3`. It retains
the one-reward-send/no-retry gate and records reward/push responses, but completion
is based on one fresh post-claim daily-task list refresh proving the exact selected
target changed to `Received`. Focused Python checks are `11/11` and the generated
Lua source parses successfully. A fresh end-to-end v3 action run still requires a
new current `CanReceive` target; the existing stage `1` is already received.

The Daily Task work has now moved from probe-only into a persistent clean-room
runtime and the C# desktop. `lwcontrol-daily-task-runtime-1` uses the already
proven `UpdateManager.AddUpdate` scheduler, current Daily Task list/reward message
paths, one in-flight target at a time, fresh-state verification, and re-detection
after each confirmed claim. `CurrentDailyTaskRuntimeClient` gates commands on a
fresh runtime heartbeat, uses single-use IDs, and rejects a completed result unless
its final authoritative snapshot still proves every reported target `Received`.
The UI exposes this in both English and Simplified Chinese.

Runtime candidate `a42`, content version `12`, package SHA-256
`afc145b9614cc81697e7079723a688ee26ccd9c6a3aa569ecf31e341bc60c8f6`,
passed encrypted round-trip verification and live registration through
`UpdateManager.AddUpdate`. A read-only snapshot at `2026-09-06T04:56:48Z`
proved there were no current `CanReceive` targets. The C# client then completed a
real `run_once` through the runtime with exactly one list refresh, zero reward
sends, and a validated `no_eligible_target` final snapshot. The persistently
installed runtime repeated that result after a normal game launch. The reversible
installer then restored every official protected hash exactly and reinstalled the
same verified runtime hashes. The 2026-09-06 completion rerun passes 21/21 focused
Daily Task, installer, and LENC Python tests plus 37/37 C# core checks, with
zero-warning desktop/CLI builds and a passing Windows Forms smoke.

The Daily Task implementation is now complete through the persistent runtime as
well. A naturally claimable state on 2026-09-06 allowed two independent
`maximumClaims=1` runs through installed `a42`: the first claimed activity chest
stage `2` with one reward send and a fresh post-state showing
`receivedStages=[1,2]`; the second claimed daily task `105` with one reward send
and a fresh post-state showing task `105` `Received` and `currentPoint=90`. Both
runs had `ConfirmedClaims=1`, `RefreshSendCount=2`, and no retry.

A transient stale-heartbeat launch was also observed before the successful runs.
The installed bytes still matched `a42`, and the C# gate sent no command while the
heartbeat was stale. Exact uninstall, a zero-command registration smoke, and a
clean reinstall restored normal `UpdateManager.AddUpdate` heartbeats. The startup
cause remains unclassified, so a fresh runtime heartbeat remains required before
every live dispatch.

English and Simplified Chinese are now explicit desktop product languages.
`LWControl.Desktop` has a runtime language selector, Chinese/English labels,
buttons, reward-category names, status text, and grid headers through a dedicated
`UiText` layer, while protocol identities and eligibility logic remain
language-independent. The desktop project builds successfully with this change.
The remaining localization polish is persistence of the selected UI language and
translation of secondary diagnostic/error text that still originates from the
core or runtime.

Generated encrypted candidate packages and temporary live-probe bundles live
under `.codex-live/`. They are local research/build artifacts rather than product
source. The directory is now ignored by Git so those large generated files no
longer clutter repository status; existing local contents were preserved.

## Reference for bundle entry layout

The .NET runtime's own
[FileEntry implementation](https://github.com/dotnet/runtime/blob/main/src/installer/managed/Microsoft.NET.HostModel/Bundle/FileEntry.cs)
documents and writes bundle-entry offsets, sizes, compression lengths, types,
and relative names. Artifact-specific counts and addresses above come from the
uploaded executable, not from that external source.
