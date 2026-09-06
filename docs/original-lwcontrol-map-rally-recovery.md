# Original LW Control map-scan and rally recovery

Status: **PARTIAL RECONSTRUCTION WITH SOURCE-LEVEL EVIDENCE**

This checkpoint records static findings recovered from the user-provided
`LWControl.zip`. No original executable or embedded Lua module was executed.
Temporary extracted binaries and resources are analysis artifacts and are not
part of this repository.

## Artifact identity

- `LWControl.zip` SHA-256: `064c8b5e08335d263a086e4bdf22a34be5b2ec777853a3b45b50d6315a65e59a`.
- `LWControl.exe` is a native x64 .NET single-file host, size 102,876,940 bytes,
  SHA-256 `205b3e4adf59fceee30088d30ae1e4d02831c38c8934660a4fbd421a6bf39e15`.
- The host contains 269 recoverable bundle entries, including
  `LastWarControl.Core.dll`, `LastWarControl.Game.dll`, `LWControl.dll`, and the
  ReadyToRun image.
- `LastWarControl.Core.dll` SHA-256:
  `3715b00c3b49d59bc0edee5ed206a4e57ca279bff8511b8ae0721a2df67b7b5d`.
- `LastWarControl.Game.dll` SHA-256:
  `c2bcd754ff24c2351a7e5cee30b6b9eae2de6d23ee8e54d86968f11d360dac24`.
- `LWControl.dll` SHA-256:
  `fda82201c90800431cbb46826679eff5f1bce913b59a99675cbfeeadb35d0827`.

`LastWarControl.Game.dll` contains plaintext embedded Lua resources:

- `LWC2MapScanner.lua`, version `lwc2-map-scanner-108`, SHA-256
  `da17f5b2b0054437b3ec777f89dda81db74533ecf61386536f41810eb14a52e2`.
- `LWC2AutoJoinRally.lua`, version `lwc2-auto-join-rally-49`, SHA-256
  `a8dd8b8d07dd114cc8ab210ccca4163b31b1aadb5e1e107a817120dbd9c58b44`.
- `LWC2AutoJoinRallyFeature.lua`, SHA-256
  `a6118dbe9a1165b8d391c07c6e6226f703d7c5e926baa7d9b24f4b5899c3d379`.

## World Map Scan: proven behavior

The original scanner confirms that structured `WorldPointManager` data is the
primary source. Loaded-point enumeration uses reflection. For private/protected
names such as `_pointInfos`, it resolves the field through
`GetType():GetField(name, Instance|Public|NonPublic)` and calls `GetValue`.
Its collection walker supports `.Values`, indexed `Count`/`Length`,
`GetEnumerator`/`MoveNext`/`Current`, and Lua tables. This resolves the previous
loaded-point enumeration blocker.

The canonical source is the active world scene and its point manager. The
scanner reads `_pointInfos`, `alliancePointsInfos`, `aoiAssistanceInfos`, and
optional cached bases from `GetAllMainBaseList`. Resource records can be
enriched with `GetResourcePointInfoByIndex(pointId)`. Coordinates use the game's
own `SceneUtils.IndexToTilePos` and `SceneUtils.WorldToTileIndex` helpers.

The wider-area path uses the native AOI/block protocol:

- block identity is `y * _lwAoiBlockCount + x`;
- native request lists are capped at 160 block indexes;
- native concurrency is capped at 20 and defaults to 8 requested workers;
- normal block scans default to an odd 5-by-5 edge and 25,000 records;
- the direct path invokes private `WorldPointManager.SendAoiRequest(...)`;
- a response hook is mandatory before the scan proceeds;
- world size 1000 is supported, while 3000 is explicitly
  `big_map_mode_unresolved`;
- native-batch transport has an official-camera fallback.

The value 160 is a native request-size ceiling, not world-coverage policy.

## Auto Join Rally: proven behavior

The original bot has its own candidate model, selector, executor, and verifier.
The recovered rally sources contain no calls to `StartAllianceAutoRally`,
`StopAllianceAutoRally`, `GetAllianceAutoRallyInfo`, or `GetAutoRallyInfo`.

Candidates come from `AllianceWarDataManager.GetAllianceWarIdList`,
`GetAllianceWarDataByUuid`, and `CheckJoinAllianceWar`. Formations come from
`ArmyFormationDataManager.GetCurFormationList` and are cross-checked against the
world march manager.

Before sending a join, the bot temporarily intercepts
`MarchUtil.OnClickStartMarch`, constructs the game's own
`UIAllianceWarDetailCtrl`, calls `OnJoinClick`, captures the parameters the
official UI would use, then restores the hook. The capture stage performs no
join send. The captured target must be `MarchTargetType.JOIN_RALLY`, match the
selected rally UUID, and contain a positive target point.

The active v49 background gates, in order, are member-count confirmation and
minimum; target allow/block checks; leader checks; member whitelist/blacklist;
target-level range with optional whitelist bypass; remaining-time range; march
time plus safety buffer; and maximum march time. Squads use preferred order,
idle-squad reservation, allowed-squad filtering, and optional exact per-team
monster-name/level schemes.

The join itself calls `MarchUtil.StartMarch` with wire wait-time index `-1`.
Hooks record the exact create request and server/world handlers. Success requires
exactly one owned request, exactly one new correlated player march, the selected
formation becoming bound, target/team correlation, an alliance-membership or
launched-rally transition, and a changed state digest. A send/response alone is
not success. Ambiguous post-send results become `commit_uncertain` and
`no_retry`.

Several configuration fields are transported by Lua v49 but are not referenced
by its active background selector: `preferred_leader_ids`, `maximum_distance`,
the `prefer_*` scoring flags, `target_type_priorities`,
`retry_when_rally_expires_or_fills`, and `allow_stamina_items`. They must remain
separate from proven live-selector behavior until another path proves otherwise.

## Reconstruction added at this checkpoint

`src/LWControl.Core/RecoveredAutoJoinRally.cs` is a clean-room offline
reconstruction of the proven v49 candidate and squad gates. It performs no game
or network I/O and keeps live execution/verification as a separate future
boundary.

`tools/current_world_map_snapshot_probe.lua` implements the first read-only
current-game loaded-state adapter from the same recovered reflection contract.
It only enumerates already-loaded `_pointInfos`, bounds the result to 50,000
points, checks collection-count consistency and duplicate identities, and has no
AOI/view/network send path. Runtime acceptance remains pending.

The current Rally adapter has also advanced to schema v4 after direct bytecode
recovery of `Global.EnumType.AllianceTeamType`, `AllianceWarInfo`, and
the richer `UIAllianceWarMainTableCtrl.OnJoinClick`. It now distinguishes
`targetUuid` from `targetUid`,
records the proven `targetBaseSkinId` and `targetLevel` fields copied by
`AllianceWarInfo.ParseData`, and records the normal join target as the Alliance
War Rally `uuid` plus `leaderMarch.startId` and the server/world route copied by
`AllianceWarInfo.ParseData`. The main-table mapping covers boss, building,
alliance-city, city, epidemic-city, and city-stronghold. This current-build
schema remains separate from the historical v49 planner fields.

## Still unresolved

- live non-empty current Rally target-field/category/countdown verification;
- UI-parity handling of the current `JOIN_RALLY + RALLY_FOR_BOSS` travel-time
  warning (the threshold is statically proven to be confirmable, not a hard
  current-manager eligibility gate);
- current-build correlated live Rally join proof;
- native `autoRallyInfo.index` semantics, which the recovered LW Control v49
  custom join path does not use.

The current Rally manager/formation capture and one-request empty-list refresh
are now proven live, including a fresh final schema-v4 a33/a34 verification on
2026-09-06. The a34 refresh sent exactly one owned `alliance.team.ls` request,
observed one correlated handler, and authoritatively confirmed zero active
Rallies for that capture. See
[`current-rally-live-capture.md`](current-rally-live-capture.md) and
[`current-rally-live-evidence.json`](current-rally-live-evidence.json).
Static current-build recovery has also established that raw `waitTime` and
`marchTime` are absolute millisecond timestamps, plus the exact current normal
Rally UI call and `MarchUtil.OnJoinRally` -> `StartMarch` handoff. The remaining
manager eligibility state machine is now recovered as states 1-9. Both current
formation-selection UI variants calculate selected-formation travel time for
`JOIN_RALLY + RALLY_FOR_BOSS`, but exceeding
`assembly_monster_toplimit/k3 * 60` only raises confirmable warning `110204`;
confirmation proceeds. This current behavior remains separate from the original
v49 planner's recovered hard remaining-time/march-time gate. The same pass
recovered current `AllianceTeamType` values
`0..8,10..13` (with no assigned value 9), proved separate `targetUuid` and
`targetUid` fields, and proved that `AllianceWarInfo.ParseData` copies the real
`targetBaseSkinId` and `targetLevel` message fields. The main-table city and
epidemic-city branches consume those fields for icon selection. The normal
main-table UI join transports the Rally `uuid`, `leaderMarch.startId`,
`data.server`, and `data.worldId`, with the optional LW item also forwarding its
`dataInfo.monsterSpecialType` value. That LW row is produced by
`UIAllianceWarMainTableCtrl.GetWarItemData`; for boss Rallies it derives the
value from `MonsterTemplateManager.GetMonsterTemplate(targetUid).special`, while
non-boss branches leave it unset.
