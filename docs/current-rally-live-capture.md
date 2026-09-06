# Current Rally live capture and refresh evidence

Status: **READ-ONLY MANAGER CAPTURE AND ONE-REQUEST LIST REFRESH PROVEN**

This checkpoint records the current-build evidence used to connect the recovered
LW Control Auto Join Rally selector to the installed Last War client. The live
runs performed no Rally join and no reward claim. The one network action in a24
was the game's own Rally-list refresh request recovered from current code.

## Reverse-engineering route

The original `LWC2AutoJoinRally.lua` v49 remains the behavioral reference. Its
background path reads `AllianceWarDataManager.GetAllianceWarIdList`, resolves
each row with `GetAllianceWarDataByUuid`, calls `CheckJoinAllianceWar`, and reads
formations through `ArmyFormationDataManager.GetCurFormationList`. When the
manager candidate list is empty, the original implementation refreshes the
Alliance War list once and re-reads manager state before treating the empty list
as final.

Current content-version-12 bytecode independently confirms the refresh contract:

- `MsgDefines.GetAllianceWarList` is `alliance.team.ls`;
- `Net/Msgs/Alliance/GetAllianceWarListMessage.luac` creates the request from
  target server plus `LuaEntry.Player:GetCurWorldId()`;
- its handler calls `DataCenter.AllianceWarDataManager:InitAllianceWarList(teams)`
  and broadcasts the Alliance War update event on a successful response;
- `UIAllianceWarMainTableCtrl` sends exactly
  `SFSNetwork.SendMessage(MsgDefines.GetAllianceWarList, LuaEntry.Player:GetCurServerId())`.

That current UI call removed the remaining target-server parameter ambiguity.

## a22: scheduling failure, not manager-contract failure

The first read-only candidate (`a22`) loaded, but its snapshot timed out while
reporting that player identity was not loaded. Screen inspection showed the
normal client had already reached the logged-in city. Reverse engineering then
showed the probe only pumped during early `LuaEntry` callbacks and stopped before
the stable logged-in manager state was available.

The fix reused the recurring registration path already proven by the World Map
probe: `UpdateManager.AddUpdate`, with
`GameEntry.Timer.RegisterTimerRepeat` retained as the fallback. No manager field
or Rally eligibility behavior was changed to make a23 succeed.

Prepared a22 package identities:

- `LWScripts.data` SHA-256:
  `3ec127309cf17b89bd6772a8aa6a595186a12cc3da03c8402a2cb80536110e8c`
- `LWScripts.txt` SHA-256:
  `20197b848187707df3f4be677e01bd8628e9bd081546e972633d7def35c01fcd`

## a23: live read-only manager snapshot

Candidate a23 registered through `UpdateManager.AddUpdate` and completed a
stable live snapshot. It made **0 explicit network sends**, **0 Rally joins**,
and **0 reward claims**.

At `2026-09-05T22:07:53Z` the snapshot contained three current formations. All
three were free, each had state `0`, and each reported stamina `25`. The player
was in an alliance. The manager contained zero observed Rally rows at that
moment.

The zero Rally count from a23 alone was not treated as authoritative because the
recovered original implementation refreshes an empty manager list before making
that conclusion.

Prepared a23 identities:

- `LWScripts.data` SHA-256:
  `bbba2d05efffd76820e128e175bc693dc1a0652b90d0e6edc84785aabd22fef3`
- `LWScripts.txt` SHA-256:
  `c7a117e174f4c804454483d23592235ba609c4728d83c83b43fd8af484079260`
- `version.txt` SHA-256:
  `6b51d431df5d7f141cbececcf79edf3dd861c3b4069f0b11661a3eefacbba918`
- snapshot builder source SHA-256:
  `2bf9497333aa7d112e43829ed27a277bce62e394bf636f7b7e7e08bcfd402728`
- preparer source SHA-256:
  `6a09edb7314386612213f508dd7706b627f29447a8db6b4496f33eb9709fc1d5`

## a24: exactly one correlated current-protocol refresh

Candidate a24 was separated from a23 so the earlier zero-network read-only proof
remained intact. It used the current UI refresh call once and captured the
post-handler manager state.

The live result was:

- protocol: `alliance.team.ls`;
- target server: `2212`;
- current world ID: `0`;
- owned sends: `1`;
- foreign same-protocol sends: `0`;
- current message-handler observations: `1`;
- response contained the `teams` field;
- `exactlyOneOwnedSend=true`, `noRetry=true`, and
  `listRefreshCorrelated=true`;
- pre-refresh observed Rally count: `0`;
- post-refresh observed Rally count: `0`;
- Rally join actions: `0`;
- reward claim actions: `0`.

The post-refresh capture ID was `live-sync-1788646379-post`, captured at
`2026-09-05T22:12:59Z`. The three formations were:

| Index | Formation UUID | Free | State | Stamina |
| ---: | --- | --- | --- | ---: |
| 1 | `1349056539444945940` | true | `0` | 26 |
| 2 | `1349056695082984539` | true | `0` | 26 |
| 3 | `1356530504375510135` | true | `0` | 26 |

For this capture, **no active Rallies after refresh is authoritative**: one
owned refresh request was correlated to one current handler invocation, the
manager was read again after that handler, and the empty state persisted.

Prepared a24 identities:

- `LWScripts.data` SHA-256:
  `99b8e564a979f8136db89500133214952eeaa4e77e516b260cc6e629c1541cbc`
- `LWScripts.txt` SHA-256:
  `4854c0bd0b4b5d0225d3d83b9489c65cb546ec32a7ae30f000279d3a38d3fa15`
- `version.txt` SHA-256:
  `6b51d431df5d7f141cbececcf79edf3dd861c3b4069f0b11661a3eefacbba918`
- sync template source SHA-256:
  `ea7b01db1dbfeb435f370605ae391cbf8e144bbb9ffad1782b2dcf02ce1880ff`
- sync preparer source SHA-256:
  `8e4b2e5451a72a820c4cb4e4cef7001d1a5a0658164270c5cbeef6b376854c79`

The official installed files were restored after both successful candidates.
The recorded pre/post SHA-256 sets matched exactly, including the protected
`BaseUtils.rdl`. `BaseUtils.rdl` and `CommonUtils.IsDebug` were not modified.

## Clean-room integration added

`src/LWControl.Core/CurrentRallySnapshot.cs` now provides a strict importer for
the current read-only/sync snapshot schema. Schema v3 validates manager-derived
counts, formation identities, the recovered numeric `AllianceTeamType`, the
server-populated target level/skin fields, the normal UI Rally-routing tuple,
and the exact one-request refresh evidence.
Formation indexes and stamina can be converted to the already recovered
`RecoveredRallySquad` planner model.

The importer only drives `RecoveredAutoJoinRallyPlanner` when the current
snapshot proves an authoritative empty list after refresh. This means the a24
state deterministically produces
`auto_join_rally_no_new_joinable_rally` with three free squads and no join
action.

A non-empty current Rally snapshot is intentionally not converted into a
`RecoveredRallyCandidate` yet. Current-build timing, join eligibility, enum
classification, and UI join routing are now statically recovered. A non-empty
current capture is still required to verify actual server values for the
branch-specific target metadata before planner mapping is enabled.

## Evidence boundary

## Post-a24 current-build static recovery

Further read-only bytecode analysis of current content version 12 removed two
more places where reusing the old v49 field meanings would have been unsafe.

### Current Rally time fields

`DataCenter/AllianceData/AllianceWarDataManager.luac` exposes
`GetAllianceWarDurationSec(data, curTime)`. Its current behavior is:

1. require the leader march to be in `MarchStatus.STATION` or
   `MarchStatus.WAIT_RALLY`; otherwise return `-1`;
2. convert `curTime`, `data.waitTime`, and `data.marchTime` from milliseconds to
   integer seconds;
3. while current time is before `waitTime`, return `waitTime - currentTime`;
4. after that and while current time is before `marchTime`, return
   `marchTime - currentTime`;
5. otherwise return `-1`.

The current Alliance War UI independently confirms the clock domain. Its timer
code obtains `UITimeManager:GetInstance():GetServerTime()` and compares that
value directly with `data.waitTime` and `data.marchTime`. Therefore, in the
current build, the raw `waitTime` and `marchTime` fields are absolute server-time
values in milliseconds.

This differs materially from recovered LW Control v49, whose selector copied the
raw `waitTime` to `remaining_seconds` and raw `marchTime` to
`estimated_march_seconds`. The clean-room current adapter must not copy those old
assumptions forward.

The read-only Rally snapshot builder now records:

- the raw `waitTime` and `marchTime` values;
- `serverTimeMs` from `UITimeManager:GetServerTime()`; and
- `remainingSeconds` calculated by the current manager's own
  `GetAllianceWarDurationSec`, with an explicit source tag.

This recovers the Rally phase countdown. The raw `data.marchTime` must not be
reused as a selected formation's travel duration.

### Current manager join eligibility

Read-only inspection of content-version-12
`AllianceWarDataManager.CheckJoinAllianceWarByWarData` (prototype `0.27`, source
lines 489-537) recovers the full `(canJoin, isLeader, inTeam, state)` state
machine:

1. nil data -> `(false, false, false, 1)`;
2. current player owns the leader march -> `(false, true, false, 2)`;
3. `table.count(memberList) + 1 >= assemblyMarchMax` ->
   `(false, false, false, 3)`;
4. `targetUid` is the current player -> `(false, false, true, 4)`;
5. current epidemic-battlefield enemy restriction ->
   `(false, false, false, 5)`;
6. normal-world player alliance does not match `attackAllianceId` ->
   `(false, false, false, 6)`;
7. `CheckAllianceWarData(data, curTime)` fails -> `(false, false, false, 7)`;
8. `CheckRallyWaitStateTimeoutValid(data, curTime)` fails ->
   `(false, false, false, 8)`;
9. otherwise scan `memberList`; return `(!inTeam, false, inTeam, 9)`.

`CheckAllianceWarData` (prototype `0.49`, lines 815-817) is exactly a positive
duration check: `GetAllianceWarDurationSec(data, curTime) > 0`.
`CheckRallyWaitStateTimeoutValid` (prototype `0.50`, lines 820-832) accepts a
truthy `data` with a missing/legacy `waitTime` below `9527`; otherwise it
requires `curTime <= data.waitTime`.

There is no selected-formation travel-time calculation in this manager
eligibility function. `CurrentRallySnapshot.Validate` now checks the recovered
state tuple and, for rows claiming `canJoin=true`, the positive manager duration
and wait-deadline evidence. The historical LW Control v49 planner remains a
separate model and keeps its own recovered timing gates.

### Current formation travel-time behavior

The remaining travel-time question was traced through both current formation UI
implementations:

- `UIFormationSelectListV2Ctrl.OnAtkClick` (prototype `0.22`) and the matching
  `UIFormationSelectListNewCtrl` path call `GetTimeFormCurPosToTarPos(uuid)` for
  `JOIN_RALLY + RALLY_FOR_BOSS`;
- they compare that value with
  `DataConfig:TryGetNum("assembly_monster_toplimit", "k3") * 60`;
- when the formation travel time exceeds the configured threshold, the UI shows
  localized warning `110204`;
- the warning's confirm closure (`0.22.2`) calls
  `ChangeMarchByType(uuid)`, so the threshold is **not a hard rejection**;
- the later `OnCheckTime` path (`0.23`) applies the same threshold and warning;
  its confirm closure (`0.23.6`) calls `OnCreateClick(uuid)`, again allowing
  continuation;
- `CheckCanBattle` (`0.27`) in both V2 and New formation UIs contains neither
  `JOIN_RALLY` nor `GetTimeFormCurPosToTarPos`, so it does not add a hidden
  travel-time Rally gate;
- `OnCreateClick` does not recalculate formation travel time before the recovered
  `ChangeMarchByType` / `MarchUtil.OnJoinRally` handoff.

Therefore current selected-formation travel time is a UI warning/confirmation
input for boss Rally joins, not part of the current manager's joinability tuple.
The clean-room current adapter must not reject an otherwise joinable Rally solely
because this warning threshold is exceeded. UI-parity warning behavior can be
implemented separately if needed.

### Current official join call chain

`UIAllianceWarMainTableCtrl.OnJoinClick` gives the richer exact current main-table
UI call for an Alliance War Rally. It maps Alliance team types to the Rally target
type:

- `ATTACK_BOSS` -> `MarchTargetType.RALLY_FOR_BOSS`;
- `ATTACK_BUILDING` -> `MarchTargetType.RALLY_FOR_BUILDING`;
- `ATTACK_AL_CITY` -> `MarchTargetType.RALLY_FOR_ALLIANCE_CITY`;
- `ATTACK_CITY` -> `MarchTargetType.RALLY_FOR_CITY`;
- `ATTACK_EPIDEMIC_CITY` -> `MarchTargetType.RALLY_EPIDEMIC_CITY`;
- `ATTACK_CITY_STRONGHOLD` -> `MarchTargetType.RALLY_CITY_STRONGHOLD`.

It then calls:

`MarchUtil.OnClickStartMarch(MarchTargetType.JOIN_RALLY,
data.leaderMarch.startId, uuid, -1, 1, targetType, data.server, data.worldId,
monsterSpecialType)`.

The same call is repeated by the shield-break confirmation closure. Current
`AllianceWarInfo.ParseData` copies incoming `message.server` and
`message.worldId` into the `data.server` and `data.worldId` values used by this
call. `AllianceWarInfo` initializes both to zero before parsing.

Current `Util/MarchUtil.luac` confirms the public `OnClickStartMarch` signature
has ten parameters:

`targetType, pointIndex, uuid, index, backHome, rallyType, targetServerId,
targetWorldId, monsterSpecialType, ignoreNotice`.

More importantly, current `MarchUtil.OnJoinRally` is a dedicated five-parameter
helper:

`selfMarchUuid, rallyType, targetUuid, targetPointId, curStamina`.

It gets the stamina cost using `GetCostStaminaByTargetType(JOIN_RALLY,
rallyType)`, refuses the action when current stamina is insufficient, and then
calls exactly:

`MarchUtil.StartMarch(MarchTargetType.JOIN_RALLY, targetPointId, targetUuid,
-1, selfMarchUuid)`.

Current `MarchUtil.StartMarch` itself has twelve parameters:

`targetType, targetPoint, targetUuid, timeIndex, mUuid, fUuid, autoBackHome,
dataObj, pos, targetServer, desTimeIndex, extraParam`.

These are static current-build contracts only. No join was executed while
recovering them.

### Current target identity and type semantics

The next read-only bytecode pass removed the remaining generic target-field
assumptions from the snapshot adapter.

`Global/EnumType.luac` lines 4715-4730 define the current numeric
`AllianceTeamType` values directly:

| Value | Current enum name |
| ---: | --- |
| 0 | `ATTACK_BOSS` |
| 1 | `ATTACK_BUILDING` |
| 2 | `ATTACK_CITY` |
| 3 | `ATTACK_AL_CITY` |
| 4 | `ATTACK_ALLIANCE_THRONE` |
| 5 | `ATTACK_DRAGON_BUILDING` |
| 6 | `ATTACK_SERVER_THRONE` |
| 7 | `ATTACK_AL_CENTER` |
| 8 | `ATTACK_CITY_STRONGHOLD` |
| 10 | `ATTACK_EPIDEMIC_BUILDING` |
| 11 | `ATTACK_EPIDEMIC_CITY` |
| 12 | `ATTACK_OUTPOST` |
| 13 | `ATTACK_ZWL` |

Value `9` is not assigned in this current enum and is therefore not given an
invented meaning. Snapshot schema v4 records both the raw integer and the exact
recovered enum name and refuses unmapped values.

`DataCenter/AllianceData/AllianceWarInfo.luac` proves that current `targetUuid`
and `targetUid` are separate fields, and also proves that `targetBaseSkinId` and
`targetLevel` are real current model fields. The constructor initializes both
numeric fields to `0`; `ParseData` copies `message.targetBaseSkinId` and
`message.targetLevel` into the same `AllianceWarInfo` object when those incoming
fields are present. There is still no evidence for the old `data.level`
fallback, so that guessed fallback remains excluded.

The producer chain is now recovered end to end. `GetAllianceWarListMessage`
passes response `t.teams` to `AllianceWarDataManager:InitAllianceWarList`;
`InitAllianceWarList` walks each team through `UpdateOneAllianceWarList`;
`UpdateOneAllianceWarList` constructs `AllianceWarInfo` and calls
`ParseData(team)`. `UIAllianceWarMainTableCtrl` then obtains that same model with
`GetAllianceWarDataByUuid(uuid)`. Its `ATTACK_CITY` and
`ATTACK_EPIDEMIC_CITY` branches read `data.targetBaseSkinId`, and if an icon is
still missing they read `data.targetLevel` for
`BuildTemplateManager:GetBuildingLevelTemplate(FUN_BUILD_MAIN, targetLevel)`.

Target display metadata is branch-specific in current
`UIAllianceWarDetailCtrl.GetWarItemData`:

- `ATTACK_BOSS` resolves a monster template with
  `MonsterTemplateManager:GetMonsterTemplate(data.targetUid)` and gets the
  displayed level/name inputs from that template;
- `ATTACK_AL_CITY` resolves an alliance-city template by
  `data.targetContentId`, using its template level and fallback name;
- `ATTACK_CITY` and `ATTACK_EPIDEMIC_CITY` use the server-populated
  `targetBaseSkinId`/`targetLevel` fields for icon selection.

The normal Rally join identity is different again: `OnJoinClick` passes the
**Alliance War Rally `uuid`** as the join UUID and `leaderMarch.startId` as the
join point. It does not pass `targetUid` as the Rally join UUID. The main-table
path also forwards `data.server` and `data.worldId`. Snapshot schema v4 therefore
records `joinTargetUuid = rally.uuid`, `joinTargetPointId = leaderMarch.startId`,
the matching server/world route, all six proven Rally-type mappings, and explicit
source tags. A row that claims current-manager `canJoin=true` for one of those
six supported normal Rally types is also rejected unless `leaderMarch.startId`
is positive.

The main-table controller accepts `monsterSpecialType` as a separate argument.
The classic `AllianceWarItem` calls the controller with only the Rally UUID, so
that argument is absent on that path. `LWAllianceWarItem` calls it with
`self.dataInfo.monsterSpecialType`. `LWAllianceWarItem.RefreshData` obtains that
row from `UIAllianceWarMainTableCtrl.GetWarItemData(uuid, true)`. For
`ATTACK_BOSS`, `GetWarItemData` looks up
`MonsterTemplateManager:GetMonsterTemplate(data.targetUid)` and copies
`template.special` into `monsterSpecialType`; the other target branches leave
the field unset. Schema v4 mirrors that derivation directly from the current
manager row's `targetUid` and records an explicit source tag.

The a27/a28 encrypted offline candidates below are the last schema-v2 artifacts
and are retained as historical evidence; schema-v3 candidates are rebuilt after
the corrected target-field contract is validated:

- a27 read-only snapshot candidate: package SHA-256
  `f61df176a4a030a29ab2119134463c2b983ac024de1695fd00766adf19597d74`,
  probe-source SHA-256
  `b17e12127f61054803c237014562e28b1da29261766ca462001378de0a515cec`;
- a28 one-refresh sync candidate: package SHA-256
  `d7069c45e29cacace6e971eeaf8c2d60cf82001b2170f7fca0516372720a1f18`,
  embedded-probe SHA-256
  `ec18138248dac66e0089d3d5c62b8df5b0b4d8a8f113c200c36341439254583d`.

Both report `serialized_round_trip_verified=true` and
`changed_installed_files=false`. They are offline candidates only; neither was
installed or run against the game during this pass.

After the `targetLevel` producer trace corrected the schema, fresh schema-v3
offline candidates were prepared and independently verified without changing
the installed game files:

- a29 read-only snapshot candidate: package SHA-256
  `564901c298c743dd821dfd8b9035a211a4e7b45ba762d106b207e90ce41ee8ca`,
  probe-source SHA-256
  `bb13f815da3e62663428569642d96c67a3ac8f2b2effdfebb521d6bd9cafe86d`,
  builder-source SHA-256
  `ce169dfc90d4ebfe7dc8e910db9992ad1dedc12c513c665578e2f94484ab066b`;
- a30 one-refresh sync candidate: package SHA-256
  `adf005f48bb6766038a6d7ddd8a26751fe0b43780d18e78a2fd103b95d237860`,
  embedded-probe SHA-256
  `e6b6cb863c0ba804fd5f4397913722a0482fbc9f2cd58f658490a13b3d552fb1`,
  builder-source SHA-256
  `ce169dfc90d4ebfe7dc8e910db9992ad1dedc12c513c665578e2f94484ab066b`.

Both schema-v3 candidates report `serialized_round_trip_verified=true` and
`changed_installed_files=false`. They are offline artifacts only and were not
installed or executed against Last War.

The main-table join trace then exposed a material schema-v3 omission. Current
`UIAllianceWarMainTableCtrl.OnJoinClick` handles six Rally target types rather
than the four visible in the detail-controller branch, and forwards the Rally's
server/world route. Schema v4 corrects that contract. The a29/a30 schema-v3
packages above remain historical evidence and are not treated as current
candidates.

Initial schema-v4 route-corrected offline candidates were prepared and
independently verified before the later `monsterSpecialType` producer trace:

- a31 read-only snapshot candidate: package SHA-256
  `3046414cc764c10ce994edef1b3c4188475b1267111d6945d4e5405128764aab`,
  probe-source SHA-256
  `76d401a241237295ced93677389ed55262fbffa52ff0bef64b272ffe5b113530`,
  builder-source SHA-256
  `8675ee67d4842c8f3b0bd1dcd21e21e159e620e46a7df30684027bf496edb924`;
- a32 one-refresh sync candidate: package SHA-256
  `4efc161edfc350549df7c6f3b908bcf851a15f20f508f1b818a2b8b1e112c700`,
  embedded-probe SHA-256
  `677dd3019f30ddf48a43059db51d74b5e03c3c4ec4e17c5458e7188176a36cd7`,
  builder-source SHA-256
  `8675ee67d4842c8f3b0bd1dcd21e21e159e620e46a7df30684027bf496edb924`.

Both a31/a32 report `serialized_round_trip_verified=true` and
`changed_installed_files=false`. They are retained as superseded preliminary v4
evidence and were never installed or executed against Last War.

After `LWAllianceWarItem.dataInfo` was traced through `GetWarItemData` and the
boss-only `MonsterTemplateManager(targetUid).special` derivation was added to
schema v4, final offline candidates were prepared and independently verified:

- a33 read-only snapshot candidate: package SHA-256
  `7def3ad0a0653670944ec727cb49a2bc7517fc9a8e30dd2fa7d7f185a2ddd928`,
  probe-source SHA-256
  `b117187a12f1a71d3b2d93a2547380df8c32bed5583886874f064bcae555b39b`,
  builder-source SHA-256
  `b6ce0019165ca9e4d5f7c3dd627ec021983b76c068c866746cb5ef4b4c09b2f4`;
- a34 one-refresh sync candidate: package SHA-256
  `2a8e8a5227dff29185a1fd3a13341ad8e97495aa02b61ea21e09cf99f239a001`,
  embedded-probe SHA-256
  `840e1ea7fb48f5c75133e011d4708cf21677eda97ce5814c1d6ceba1d39890fe`,
  builder-source SHA-256
  `b6ce0019165ca9e4d5f7c3dd627ec021983b76c068c866746cb5ef4b4c09b2f4`.

Both final schema-v4 candidates report `serialized_round_trip_verified=true`.
They were first retained as offline artifacts, then re-verified against the
exact official content-version-12 baseline and executed in bounded live runs on
2026-09-06 after the full-world scan milestone was completed.

### Fresh schema-v4 live verification

The final a33 read-only snapshot was executed at `2026-09-06T06:58:04Z` with
zero explicit network sends, zero Rally joins, and zero reward claims. It
captured three free formations and `observedRallyCount=0`. The runner restored
the exact official protected-file hashes afterward.

Because a manager-only zero is not authoritative under the recovered original
behavior, the final a34 sync candidate was then executed. It sent exactly one
owned `alliance.team.ls` request to server `2212`, observed exactly one matching
handler invocation with `teams` present, performed no retry, and captured the
post-handler manager state at `2026-09-06T06:59:04Z`. The result remained:

- `preSyncObservedRallyCount=0`;
- `observedRallyCount=0`;
- `joinableRallyCount=0`;
- `joinedRallyCount=0`;
- `formationCount=3`, all three formations free;
- `explicitNetworkSends=1`;
- `joinActions=0` and reward-claim actions `0`;
- response correlation: `handlerCount=1`, `responseTeamsPresent=true`,
  `exactlyOneOwnedSend=true`, `noForeignSameProtocolSend=true`, `noRetry=true`;
- exact protected-file pre/post SHA-256 equality.

This fresh a34 result independently confirms that the current server state was
authoritatively empty at that capture. It does not supply the still-missing
non-empty current Rally row needed to verify server values for the recovered
target/countdown mapping.

### PROVEN current-build facts

- The current global manager paths used by the probe are
  `DataCenter.AllianceWarDataManager` and
  `DataCenter.ArmyFormationDataManager`.
- Recurring probe execution works through `UpdateManager.AddUpdate`.
- The current Rally-list refresh protocol and target-server call are recovered
  from current bytecode and were accepted live in both a24 and the final a34
  schema-v4 verification.
- a24 and the fresh a34 run each independently prove a correlated empty Rally
  list after refresh for their respective captures.
- Three free formations and their UUID/index/stamina values were captured live.
- Current raw `waitTime`/`marchTime` are absolute millisecond timestamps, and
  `GetAllianceWarDurationSec` is the authoritative current phase-countdown
  conversion.
- `CheckJoinAllianceWarByWarData` state codes 1-9 and their exact boolean tuples
  are recovered from current bytecode, including its duration and wait-deadline
  gates.
- Current manager eligibility contains no formation travel-time gate.
- For `JOIN_RALLY + RALLY_FOR_BOSS`, both current formation UIs use calculated
  formation travel time only for a confirmable `110204` warning based on
  `assembly_monster_toplimit/k3`.
- The normal current main-table Rally UI call and the dedicated
  `MarchUtil.OnJoinRally` helper signatures are recovered from
  content-version-12 bytecode.
- The complete current numeric `AllianceTeamType` table used here is recovered;
  value `9` is intentionally unmapped because the current enum does not assign it.
- Current `AllianceWarInfo` has distinct `targetUuid` and `targetUid` fields,
  plus real `targetBaseSkinId` and `targetLevel` fields copied from the incoming
  Rally message.
- Boss display metadata comes from `MonsterTemplateManager(targetUid)`, while
  Alliance-city metadata comes from its template and city/epidemic-city icon
  fallback can use the server-provided `targetLevel`.
- The normal Rally join transport uses the Alliance War `uuid` plus
  `leaderMarch.startId` and forwards `data.server` plus `data.worldId`; it does
  not use `targetUid` as the join UUID.
- The main-table target-type map additionally proves `ATTACK_AL_CITY ->
  RALLY_FOR_ALLIANCE_CITY` and `ATTACK_CITY_STRONGHOLD ->
  RALLY_CITY_STRONGHOLD`; the other currently enumerated types have no mapping
  in this function and remain empty in the snapshot contract.
- Snapshot schema v4 now enforces those recovered enum/routing contracts and
  records `targetBaseSkinId`/`targetLevel` with exact `AllianceWarInfo.ParseData`
  provenance, plus the boss-only `MonsterTemplateManager(targetUid).special`
  derivation used for `monsterSpecialType`, while continuing to exclude the
  unproven `data.level` fallback.

### RECOVERED original LW Control behavior

- Empty manager state is refreshed once before the original background path
  concludes that there is no candidate.
- Candidate selection comes from Alliance War manager records and formation
  selection comes from current formation state.
- Join execution remains the separate `MarchUtil.StartMarch(JOIN_RALLY, ...)`
  path with strict post-state verification and no retry after ambiguous commit.

### UNKNOWN / not yet implemented

- UI-parity details for presenting/automatically handling the current
  `110204` boss-Rally travel-time warning are not implemented; this is no longer
  an eligibility unknown.
- Current live observation of a non-empty Rally row to verify the recovered
  countdown, enum value, distinct target fields, and branch-specific metadata
  against actual server data.
- Current live Rally join transport and correlated success proof.
- Multi-Rally ordering behavior under current live data.

Those items should be solved by further source/bytecode recovery first, then a
bounded live observation when a real Rally is present.
