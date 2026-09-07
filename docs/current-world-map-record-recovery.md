# World Scan rich-record recovery and application boundary — 2026-09-06

This checkpoint turns the already-proven full-world transport into the record
contract consumed by the rebuilt desktop application. Evidence is intentionally
split between the original supplied LWControl artifact, the current game build,
and fields that remain dynamic or unavailable.

## Supplied artifact rechecked

The user-supplied `LWControl.zip` at
`C:\Users\chimw\OneDrive\Desktop\Github\LW\LWControl.zip` was re-hashed before
this implementation pass. Its SHA-256 remains
`064c8b5e08335d263a086e4bdf22a34be5b2ec777853a3b45b50d6315a65e59a`.
The extracted original scanner is `LWC2MapScanner.lua` version
`lwc2-map-scanner-108`, SHA-256
`da17f5b2b0054437b3ec777f89dda81db74533ecf61386536f41810eb14a52e2`.

The supplied LWControl tutorial also advertises Secret Task and Auto Radar as
separate product features from World Scan. Current-build static evidence now
shows that some radar/detect objects are also members of `WorldPointType`, so
World Scan must preserve those map objects when they occur in `WorldGetBlock`.
That does not by itself prove the original Secret Task or Auto Radar workflow;
their task selection/claim/automation behavior remains a separate feature until
its own scanner/runtime path is recovered.

## RECOVERED original LWControl behavior

The following behavior is present directly in `LWC2MapScanner.lua` v108 and is
mirrored by the clean-room full-scan extractor:

- point type `6` is `player_base`;
- point types `11`, `15`, `25`, and `35` are `alliance_building`;
- point types `4`, `5`, `22`, and `1003` are `monster`;
- point types `1`, `7`, and `26` are `resource_point`;
- unknown point codes remain `world_point` unless owner/alliance identity gives
  the original scanner enough evidence to classify them;
- resource metadata is queried through `GetResourcePointInfoByIndex`, then
  `GetResLevel`, `GetResType`, and `GetResPointType`;
- resource enum aliases are resolved through runtime `ResourceType` values,
  including Gold/Food/Wood, Metal-Iron-Stone, Oil-Petroleum, and seasonal
  resource aliases. Ambiguous cross-family aliases fail to `unknown`;
- world monsters are enumerated through `GetMarchesBossInfo`; their stable UUID
  is preferred for deduplication;
- the original distance scanner does not obtain monsters from the direct block
  cache alone. It moves the official world camera and runs the manager scan at
  every view, so `GetMarchesBossInfo` is refreshed as the viewport changes;
- `build_official_view_queue` partitions the requested AOI by the active visible
  block width/height and traverses rows in serpentine order. With the current
  observed normal-world viewport `LB={53,46}`, `RT={57,49}`, that is exactly
  `5 x 4` logical blocks per view and therefore `20 x 25 = 500` full-world
  views over the proven `100 x 100` AOI grid;
- each official view converts its target tile through `TileToWorld`, tries
  `SceneManager.World.Lookat`, falls back to `AutoLookat`, resets the official
  view-response flags, calls `WorldPointManager.StartViewRequest()` and
  `UpdateViewRequest(true)`, then waits for the correlated block response;
- the original default official response timeout is 4 seconds. Camera restore
  uses `AutoLookat` with the saved zoom and is accepted only when tile distance
  is at most 3 and zoom delta is at most 0.05;
- monster level/recommended-power enrichment comes from
  `MonsterTemplateManager.GetMonsterTemplate`. The original seasonal
  seven-digit-to-canonical template fallback is retained;
- the original record field families include player/alliance identity, level,
  power aliases, shield/protection end time, resource remaining/capacity aliases,
  gather timer/collector aliases, monster ID and recommended power.

The original managed desktop additionally confirms the intended rich-record
surface. `MapScanRecordEvidence` contains kind/name/player/alliance, level/power,
point ID and X/Y, shield fields, resource/gather fields, and monster recommended
power. Its `WorldIntelligenceStore` indexes the same data by server, kind,
identity, level, power, coordinate, shield, resource amount, and recommended
power. These are recovered-original product behaviors, not automatic proof that
every field is populated by the current build.

## PROVEN current-build facts

The current content-version-12 full-scan proof already established the transport
contract independently of the original program:

- normal world grid: `100 x 100` logical AOI blocks;
- complete coverage: `10,000 / 10,000` blocks;
- `65 / 65` bounded batches;
- recovered concurrency cap `8`, reached without exceeding it;
- post-response accumulation preserves records while the game's loaded point
  collection changes from area to area;
- the owned response hook and `isRecvViewPoints` manager flag are restored;
- the protected game files are restored to exact pre-run SHA-256 values.

Static recovery from the current `Assembly-CSharp.rdl` separately proves the
generated `Protobuf.WorldPointInfo` common payloads used by the richer extractor:

- `BuildInfo`: owner UID, UUID, build ID, level, state, alliance ID, protection
  end time, HP/state timestamps, name, alliance abbreviation and other build
  metadata;
- `RoadInfo`: owner/UUID, road state, HP and alliance ID;
- `CollectResourceInfo`: resource type, level, type and attach ID;
- `ResourceInfo`: resource ID, state and gather UUID;
- `ExplorePointInfo` / `SamplePointInfo`: owner, UUID and event ID;
- `GarbagePointInfo`: owner, UUID, event ID and end time.

The exact installed `Assembly-CSharp.rdl` SHA-256
`871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd`
also contains the current `WorldPointType` enum with 65 fields. Confirmed names
include the core `WorldMonster`, `WorldBoss`, `PlayerBuilding`, `WorldResource`,
`WORLD_ALLIANCE_CITY`, `WORLD_ALLIANCE_BUILD`, `WorldAllianceCollectResource`,
`INVASION_WORLD_MONSTER`, and `SIMPLE_WORLD_MONSTER` types. The same enum also
contains special/radar/detect map types including `EXPLORE_POINT`,
`SAMPLE_POINT`, `GARBAGE`, `DETECT_EVENT_PVE`, `RESCUE_POINT`, `HERO_DISPATCH`,
`TREASURE`, `WorldSuppliesPoint`, `RadarSeasonSnowSurvivor`,
`CAVE_EXPLORATION`, `RADAR_DOMINATOR_GUIDE`, `RADAR_DOMINATOR_CURE`,
`DETECT_RETRY_TASK`, `DETECT_DIG_GAME`, `DETECT_SUPPLIES_SEARCH`,
`DETECT_ALLIANCE_CITY_SCOUT_MONSTER`, `DETECT_LAST_STAND`, several treasure and
city/outpost types, and `ALLIANCE_BOSS_S0`. Their current numeric constants were
not promoted from field order; live `pointType` values remain the authority for
which codes actually occur in the direct stream.

`current_world_map_full_scan_probe.lua` now reads those current-build payloads
first. For example, player name/owner/alliance/level/protection prefer
`WorldPointInfo.BuildInfo`; resource type/level prefer
`CollectResourceInfo`; occupancy prefers `ResourceInfo.gatherUuid`. Only after
those exact current payloads are unavailable does the extractor use the
recovered-original alias families. Shield records include a source marker so the
current `BuildInfo.protectEndTime` path is distinguishable from an original
compatibility alias.

The current build also confirms the reason the previous full-block proof found
zero monsters even though the direct 10,000-block transport was complete:

- the last guarded camera-only acceptance run completed `10,000 / 10,000`
  direct blocks, `65 / 65` direct batches, `500 / 500` recovered camera views,
  500 camera moves, 500 official view refreshes, and 500 monster-cache reads;
  every view still reported zero marches and zero bosses, with maximum march
  count zero. It therefore terminated as `monster_coverage_incomplete` and did
  not emit a proven result. Camera restoration succeeded, the direct batch count
  remained 65, all protected runtime files restored to their exact pre-run
  hashes, and `LastWar.exe` was closed afterward. This is direct live evidence
  that camera traversal plus block refresh alone does not populate the current
  march cache;

- `SceneManager.World.GetMarchesBossInfo` reads the existing
  `WorldMarchDataManager.allMarches` cache; it does not fetch missing march data;
- `WorldMarchDataManager` contains view-scoped state including `viewBounds`,
  `viewRect`, `lastCameraViewRange`, and `cameraViewRect`, and `UpdateViewRect()`
  derives that state from the active camera/view;
- `MarchDataUpdater.DeltaUpdate()` (`0x06003307`) does not fetch march data. Its
  current IL iterates the existing normal-march array, updates movement and view
  intersection/visibility state, refreshes relation data, compacts the local
  array, and records update timing;
- `WorldMarchDataManager.HandleWorldGetRectMarchInfos()` (`0x0600327D`) is the
  current cache-populating response handler. It clears/rebuilds march-related
  collections, parses returned `marchInfos`, creates or updates `WorldMarch`
  records, mutates `allMarches` and its owner/my/to-me indexes, handles delayed
  destroys, and finishes through the march-refresh path;
- `WorldGetRectMarchInfosMessage.CSHandleResponse()` (`0x060019A5`) rejects an
  `errorCode`, obtains `SceneManager.MarchDataMgr`, and calls
  `HandleWorldGetRectMarchInfos()` on success. The probe's generic dispatch
  observer runs after the game's original dispatch delegate, so a response
  observed there is already past the cache-populating managed handler;
- the no-argument managed `WorldMarchDataManager.WorldGetMarchInfos`
  (`0x060032AC`) proves the current request construction. Its current IL obtains
  the player's main-world index, converts it with
  `TileCoord.IndexToTilePos(index, 1)`, creates a
  `WorldGetRectMarchInfosMessage.Request`, copies the resulting integer tile
  `x`/`y` into fields `0x04001084`/`0x04001085`, and calls `BaseMessage.Send()`;
- `WorldGetRectMarchInfosMessage.GetMsgId()` returns the same
  `world.get.march.infos` command family seen in the Lua/cross-server artifact,
  but this managed request/response path is materially different because the
  current `CSSetData` and `CSHandleResponse` participate in the normal game
  message pipeline;
- a guarded v7 run proved that 500/500 explicit Lua/cross-server
  `WorldGetMarchInfos` requests could receive 500/500 owned responses while
  `allMarches` remained empty at every capture. That transport is therefore
  rejected as the scanner cache-population mechanism for the current build;
- the v8 managed-rect experiment then proved 500/500 current
  `WorldGetRectMarchInfosMessage` sends and 500/500 correlated post-dispatch
  responses with zero foreign/unowned response ambiguity, but every
  post-handler `GetMarchesBossInfo` read still returned zero marches and zero
  bosses. It therefore ended `monster_coverage_incomplete` and restored the game
  exactly;
- a current call-graph recheck then corrected the working hypothesis:
  `WorldMarchDataManager.WorldGetMarchInfos()` has only scene-init/re-init and
  XLua-wrapper direct callers and is not called by camera movement;
  `UpdateViewRect()` is called by `WorldMarchDataManager.OnUpdate()`. Therefore
  repeatedly sending the managed rect request per camera view is not established
  as the current camera/mob loading path. The v8 request plumbing is proven, but
  it is rejected as the active World Scan discovery mechanism rather than being
  extended by guesswork;
- `WorldPointManager.UpdateLWAoi_Normal()` is now resolved one layer further at
  its current C# -> Lua boundary. It calls `GameEntry.get_Lua`; the return type
  statically decodes to `XLuaManager`. The method then loads
  `CSharpCallLuaInterface.UpdateViewRange`, pushes `x`/`y` from two
  `UnityEngine.Vector2Int` values, and invokes the unique compatible
  `XLuaManager.Call<T1,T2,T3,T4>(string,T1,T2,T3,T4)` overload
  (`0x06004444`). The current content-v12 Lua bytecode resolves the remainder:
  `CSharpCallLuaInterface.UpdateViewRange(minX,minY,maxX,maxY)` calls
  `DataCenter.SceneCameraManager:UpdateWorldCameraView(minX,minY,maxX,maxY)`.
  `SceneCameraManager.UpdateWorldCameraView` stores changed
  `worldCameraMinX/Y/MaxX/MaxY` values and broadcasts
  `EventId.WorldCameraViewChanged` with the four bounds. It does not itself send
  a network request;
- `WorldScene.SendViewRequest(center,lod,flag)` delegates to
  `WorldPointManager.SendViewRequest`. The normal request uses
  `GetViewLevelWorldInfoMessage`, whose message id is the current-build prefix
  `world.get.new`. Its `CSHandleResponse` ultimately calls
  `SceneInterface.HandleViewPointsReply`. `WorldGetBlockMessage.CSHandleResponse`
  calls the same point-reply boundary. This proves that the normal view response
  belongs to the world-point stream;
- the separate current push class `PushWorldMarchWorldGet` has the recognizable
  message-id prefix `push.world.march.world.get.new`. Its `CSHandleResponse`
  obtains `SceneManager.MarchDataMgr` and calls
  `WorldMarchDataManager.HandleWorldMarchGet`. That handler parses structures
  including `serverMarchArr`, `uuidSet`, and `marchInfos` and updates the march
  cache. The current build therefore has independently proven point and march
  streams;
- a later narrow IL pass resolves the static side of that relationship. The
  current `WorldMarchDataManager.HandleWorldMarchGetImpl` contains an explicit
  `AOI::Get` diagnostic referring to `marchinfo`, while
  `HandleWorldMarchGet` writes `_lastGetBlockMsgTime` and its implementation
  writes `_lastHandleBlockMsgTime`. `SetMarchOptLog` prints those two block-time
  fields. Combined with the proven push id above, this is current-build evidence
  that `push.world.march.world.get.new` is the march/monster side of the AOI
  get/block stream;
- the obvious Lua world-state module `Scene/LWSceneStateManager/SceneStateWorld`
  contains enter/exit world broadcasts and related managers but no view-request
  call. It is therefore not the missing Lua request caller;
- the completed direct `WorldGetBlock` sweep remains the proven point/special
  object transport. The v9 zero-monster result disproves treating that direct
  stream alone as sufficient current-build monster coverage.

### Current AOI initialization and request sequencing

The current `WorldPointManager` call graph narrows the startup sequencing
further:

- `StartViewRequest()` sets the manager's view-request-active flag and, when
  `_pendingInitViewRequestAfterMarchInfos` is already set, calls
  `RequestInitViewRequestAfterMarchInfos()`;
- `RequestInitViewRequestAfterMarchInfos()` first calls
  `SetFirstViewRequestFlag(true)`. If the view-request-active flag is not yet
  set it records `_pendingInitViewRequestAfterMarchInfos=true`; otherwise it
  clears that pending flag and calls `UpdateViewRequest(true)`;
- the two fields are statically named
  `_pendingInitViewRequestAfterMarchInfos` and `firstTimeReqAoi`. This proves the
  client deliberately coordinates initial AOI startup with a prior
  march-information initialization phase;
- `UpdateViewRequest(true)` calls `UpdateLWAoi(true)`, which reaches
  `UpdateLWAoi_Normal(true)` for the normal world and ultimately calls
  `SendAoiRequest(...)`;
- `SendAoiRequest` uses `WorldGetBlockMessage.Instance` and creates the
  current nested `Request` object. Its decoded fields are `bigMap`, `x`, `y`,
  `serverId`, `worldId`, `type`, `lod`, `index`, `blockSize`, `firstTime`,
  `leftBottom`, `rightTop`, and `battleFieldFirst`;
- `WorldGetBlockMessage.CSSetData` serializes those fields to the wire. The
  `firstTime` field is emitted as `clearUuidSet=true` when set, and
  `battleFieldFirst` is emitted as `force=true` when set. This means the normal
  game AOI path uses the same `WorldGetBlockMessage` transport as the scanner,
  with first-request and view-bound state supplied by the manager;
- the scanner already invokes the real `WorldPointManager.SendAoiRequest`
  through `WorldBlockSender`, passing the same eight public arguments, so these
  hidden request fields are populated by the game's own method. The v9
  zero-monster result therefore cannot be explained by a hand-built request
  object simply omitting `firstTime`/bounds fields;
- `WorldMarchDataManager.WorldGetMarchInfos()` is a distinct initialization
  operation that sends `WorldGetRectMarchInfosMessage`; managed callers include
  `WorldScene.CreateScene`, `CityScene.Init`, and manager reinitialization. This
  matches the earlier guarded experiment: it is proven as an initialization
  request, but not as the normal arbitrary-camera monster discovery mechanism.

The initialization order is also now proven from current IL rather than inferred:
`WorldScene.CreateScene` calls `WorldMarchDataManager.WorldGetMarchInfos()`
before `WorldScene.RequestInitViewRequestAfterMarchInfos()`. During the initial
march response, `WorldMarchDataManager.ParseDataWorldMarchGet` calls
`WorldScene.SetFirstViewRequestFlag(true)`. This makes the rect-march request an
initialization gate for first AOI startup, not a query to repeat at every camera
position.

The transport observer used by the probe is suitable for the march push as well
as point replies. In the current `BaseUtils.rdl`, all extension messages pass
through `MessageDispather.HandleExtensionMessage` to
`NetRawProxy.OnExtensionResponse`, which calls
`MessageFactoryProxy.DispatchResponse`. The latter invokes the same
`DispatchResponse1`/`DispatchResponse2` delegate fields that the probe wraps.
There is no separate `DispatchPush` field/method in this current proxy. This
provides a statically justified passive observation point for
`push.world.march.world.get.new`.

### PROVEN current-build raw march-push boundary (2026-09-06)

The later guarded manager-cache attempt (`lwcontrol-world-full-scan-probe-6`)
completed the point and camera transports again but disproved the assumption
that the post-dispatch `WorldMarchDataManager` cache remains populated long
enough for the scanner to sample it. The retained status recorded `10,000 /
10,000` logical blocks, `65 / 65` direct batches, peak direct concurrency `8`,
`15,499` accumulated point records, 500 camera moves, 500 official AOI view
requests, 500 observed march pushes, zero managed rect-march sends, and exact
camera/runtime restoration. Despite that, every
`allMarchUuids -> GetMarch` sample was empty. The run therefore correctly ended
`monster_coverage_incomplete`; it did not promote cache emptiness to proof that
the server sent no march data.

Current `Assembly-CSharp.rdl` resolves the earlier missing capture boundary:

- `WorldMarchDataManager.HandleWorldMarchGet` reads a `serverMarchArr` array.
  Each server object contains `serverId`, `uuidSet`, and `marchInfos`;
- `WorldMarchDataManager.ParseDataWorldMarchGet` iterates that `marchInfos`
  collection, reads each entry's `uuid`, obtains/creates a `WorldMarch`, and
  passes the raw SFS object into the normal WorldMarch update path;
- `WorldMarch.UpdateWorldMarch` delegates non-protobuf data to
  `WorldMarch.UpdateFromSFS`. The current parser reads spatial/identity fields
  including `uuid`, `targetPos`, `startPos`, `mainPointId`, `type`, `monsterId`,
  `worldId`, `server`, `targetServer`, `srcServer`, `eventId`, `eventUuid`,
  `bossId`, `curHp`, `maxHp`, and `running_hp`. It also reads event structures
  such as `allianceBossInfo`, `invasionBossInfo`, `strongholdBoss`, `zMBossInfo`,
  `allianceChallengeInfo`, `cityBattleS1MonsterInfo`, `bloodyQueenMonster`,
  `train`, and `busList`;
- current field metadata independently confirms `WorldMarch.type`,
  `WorldMarch.monsterId`, and `WorldMarch.monsterType` exist. `monsterType` is
  not a raw `UpdateFromSFS` key in the inspected parser, so the raw scanner does
  not invent it;
- current `WorldMarch.IsMonsterOrBoss` accepts march type `2` and type `33`
  directly before delegating other cases to the current `IsBoss` rules. This
  allows the raw payload to retain monster/boss candidates before the manager
  cache lifecycle can erase the sampling opportunity.

The probe now captures at
`PushWorldMarchWorldGet.serverMarchArr[*].marchInfos`. A non-empty push is
correlated to the current camera request by requiring its server to match and at
least one of `startPos`, `targetPos`, or `mainPointId` to fall inside the already
correlated `world.get.block` response envelope. An empty `marchInfos` snapshot
has no coordinate to compare, so its weaker evidence state is retained
explicitly as `request_window_matching_server_empty_marchInfos` rather than
being mislabeled spatial proof. Background/nonmatching pushes remain separate
diagnostics instead of satisfying the view.

This raw boundary also agrees with both supplied bots. Recovered original
`LWC2MapScanner.lua` enumerates `WorldScene.MarchDataManager.allMarchUuids`,
resolves each entry with `GetMarch`, and applies `IsMonster`/boss predicates.
The independent `lwbridge-0.3.1.exe` contains `map_scan.rs` plus hooks for
`WorldMarchDataManager.AddMarch`, `UpdateMarch`, `AddOrUpdateMarch`, and
`TryRemoveMarch`, and retains march/monster fields including `configId`,
`monsterType`, `maxHp`, `isMonster`, `requiresRally`, and `normalType`. The two
bots therefore independently support capturing monster state on the march
stream rather than expecting it in the persistent direct point stream.

### LIVE PROVEN raw march-push World Scan acceptance (2026-09-06)

The guarded acceptance candidate
`candidate-world-march-raw-20260906-2330` subsequently completed successfully.
Its package SHA-256 was
`63d5cbadab1b2446a003a92617d1cd4415dccce7754fb93e9ddadb8087bf36ad`
and its probe-source SHA-256 was
`48b74d95ae4a4d068c17831256513c0aa83084cfb998d0c0cde858b47d15cbdc`.
The retained runner result reports `live_contract_verified=true` and the probe
result state is `proven`.

Live transport/coverage evidence from that run:

- `10,000 / 10,000` logical blocks covered;
- `65 / 65` direct block batches completed, with peak direct concurrency `8`;
- `500 / 500` recovered camera moves completed;
- `500 / 500` official `StartViewRequest()` / `UpdateViewRequest(true)` AOI
  refreshes completed;
- `500 / 500` correlated `push.world.march.world.get.new` captures completed;
- zero managed rect-march requests and zero retries;
- `23,195` accumulated records, including `6,388` player bases, `7,989`
  resource points, `390` alliance buildings, `677` other world points, and
  `7,751` monster/boss records;
- all 500 monster views contained march records and monster/boss candidates;
  the maximum correlated march count in one view was `25`;
- monster records classified by the current-build `WorldMarch` rules were
  `6,944` ordinary monsters (`type=2`) plus `807` bosses: `746` type `3`, `58`
  type `15`, and `3` type `21`;
- the raw monster source is
  `PushWorldMarchWorldGet.serverMarchArr.marchInfos`;
- the direct point stream still contained zero monsters, independently
  confirming that current monster coverage belongs to the march stream;
- camera restoration succeeded with distance `0` and zoom delta
  `1.52587890625e-05`; the response hook, manager flag, world response flag,
  and march-hook state all restored successfully;
- the protected installed `LWScripts.data`, metadata, version, and BaseUtils
  hashes after the run exactly matched the pre-run hashes.

This is the first guarded live proof in this recovery work that the rebuilt
World Scan can combine complete persistent-map coverage with current
monster/boss capture using the game's real AOI/march stream.

The retained live monster set did **not** contain secondary-bot catalog ids
`2901011` (`小金`) or `2901012` (`大金`), and none of the inspected
`invasionBossInfo`, `zMBossInfo`, `cityBattleS1MonsterInfo`,
`bloodyQueenMonster`, `train`, or `busList` flags were present in this run.
Those event-only families therefore remain recovered/statically supported but
not live-seen in this particular acceptance. `allianceBossInfo` was present on
16 captured monster/boss records.

A later guarded v7 acceptance completed `10,000 / 10,000` logical blocks,
`65 / 65` direct batches, `15,681` accumulated direct point records, `500 / 500`
camera moves, `500 / 500` official block refreshes, `500 / 500` explicit
Lua/cross-server march requests, `500 / 500` owned march responses, and
`500 / 500` post-response captures with no timeout or response ambiguity. Every
capture still returned zero marches and zero bosses, so the run correctly ended
as `monster_coverage_incomplete`. Camera state and all hooks restored, the game
closed, and all protected file hashes matched the exact pre-run baseline. This
is the live evidence that motivated the managed rect-march correction above.

A later guarded acceptance attempt exposed a loader constraint before any scan
request was sent. The modified `DataCenter.Global.LuaEntry` successfully loaded
the already-present `DataCenter.Global.LuaEntry_original`, then failed at
`require("LWControlProbe")` with `module 'LWControlProbe' not found`. No probe
status/heartbeat was created, the runner terminated with `state=None`, closed
the game, and restored the four protected files to their exact expected hashes.
This proves that the newly appended `LWControlProbe.luac` name was not usable by
the current runtime loader in that candidate; it does not prove why the loader
rejected that name. The verified installed Daily Task runtime already has a
loadable `LWControlDailyTaskRuntime.luac` root module, so the full-scan candidate
now reuses that existing registered entry as a temporary carrier rather than
adding a new module name. The runner restores the original package afterward.

## Rebuilt application behavior

`CurrentWorldMapFullScanResult` remains the bounded importer for a retained live
result. The probe-side contract has now been corrected to the three-source
evidence: persistent points use the proven 10,000-block / 65-request direct scan,
while monster coverage uses the recovered 500-view camera traversal and the
current normal AOI path. It passively observes
`push.world.march.world.get.new`, captures
`serverMarchArr[*].marchInfos` before manager-cache retirement, and correlates
raw march positions against the matched current view envelope. It sends
**zero** `WorldGetRectMarchInfosMessage` requests during traversal.

The bounded network budget is therefore 65 direct block requests plus at most
500 official AOI view requests, for 565 total active requests, with zero retries
and zero managed rect-march sends. A proven hybrid result requires all 500
camera views, 500 official view requests, 500 correlated march AOI payloads, 500
raw-payload captures, at least one retained monster/boss record, successful
camera restoration, and the existing exact response-hook/manager/file
restoration checks. Direct monster `WorldPointInfo` records, when an event
exposes them, are retained as additional evidence but no longer substitute for
the march stream.

`CurrentWorldMapScanClient` uses the existing one-shot transaction rather than
inventing a second runtime protocol. A desktop **World Scan** click:

1. prepares and encrypted-round-trip verifies a fresh candidate from the exact
   installed current build;
2. runs the proven bounded full-scan runner;
3. lets that runner launch Last War, complete the 65-request direct
   `WorldGetBlock` phase, then traverse the recovered 500 AOI views while
   passively observing the current march push. It performs no rect-march send;
   before the monster phase it writes a retained direct diagnostic with all
   accumulated point records plus kind and numeric `pointType` histograms;
4. imports only the retained result inside the owned candidate directory;
5. validates the full transport/cleanup contract before displaying any rows;
6. displays the recovered rich fields and logs the retained evidence path.

The runner still refuses to begin when Last War is already running, preserving
the existing exact-backup/install/restore transaction boundary.

World Scan is still not labeled live-proven at this checkpoint. The next guarded
acceptance must demonstrate the new passive AOI march path with positive current
monster records alongside complete direct player/resource/alliance coverage,
zero retries, 500/500 correlated march pushes, and exact runtime restoration.

## UNKNOWN / deliberately not promoted to current-build fact

## RECOVERED secondary-bot evidence (`lwbridge-0.3.1.exe`)

Read-only string inspection of the user-supplied Rust/Tauri executable
`lwbridge-0.3.1.exe` (SHA-256
`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`)
provides an independent clue for the missing current monster path. This is
secondary implementation evidence, not proof that our current game build uses
the identical scan trigger.

- The executable contains a native world-capture component named
  `__XluaBridgeNativeWorldCapture` and a scan callback named
  `XluaBridgeMapScanTick`.
- Its required/optional native hook catalogue includes
  `WorldPointManager.AddPointInfo`, `WorldPointManager.RemovePointInfo`,
  `WorldTileInfo.RemovePointInfo`, `WorldPointManager.ParseWorldPointRemove`,
  `WorldPointManager.ParseWorldPointFoldUp`, `WorldMarchDataManager.AddMarch`,
  `WorldMarchDataManager.UpdateMarch`,
  `WorldMarchDataManager.AddOrUpdateMarch`,
  `WorldMarchDataManager.TryRemoveMarch`, and `WorldTroopManager.UpdateTroop`.
  The executable reports `native world capture hooks ready` only after its hook
  setup path succeeds.
- The same capture block resolves current-game classes/fields/methods including
  `WorldPointManager`, `WorldMarchDataManager`, `WorldMarch`, `pointIndex`,
  `mainIndex`, `pointType`, `GetResType`, `GetResLevel`,
  `GetWorldTreasureType`, `get_configId`, `GetMarchCurPosIndex`, `GetMaxHP`,
  `IsMonsterOrOrdinaryBoss`, `IsOrdinaryBoss`, and `IsNormalType`.
- Its serialized capture payload has separate `points`, `marches`,
  `pointRemovals`, and `marchRemovals` arrays. Recovered march/monster fields
  include `runtimeClass`, `configId`, `marchType`, `maxHp`, `isMonster`,
  `requiresRally`, and `normalType`.
- Its UI/command layer exposes `map_scan_start`, `map_scan_status`,
  `map_scan_stop`, `map_scan_clear`, `map_data_options`,
  `monster_catalog_options`, and `/tasks/monsterSweep`. Scan state strings
  include `scanRunId`, `totalBlocks`, `completedBlocks`, `readBlocks`,
  `failedBlocks`, `unreadBlocks`, `inflightBlocks`, `scanRate`,
  `nativePendingRecords`, `nativeDroppedRecords`, and `nativeCaptureReady`.
- deeper static inspection identifies the implementation source path
  `src\\services\\map_scan.rs`. Its scan state separately tracks direct block
  progress (`totalBlocks`, `readBlocks`, `unreadBlocks`, `failedBlocks`,
  `inflightBlocks`, `concurrency`) and native capture queues
  (`pendingPoints`, `pendingMarches`, point/march removals, dropped records,
  `nativeCaptureReady`). It also logs `direct map scan completed` before native
  capture diagnostics. This proves the secondary bot uses a hybrid direct-block
  plus native point/march capture architecture rather than expecting monsters
  to be present in the direct point response;
- targeted string inspection found a native capture/update loop containing
  `XluaBridgeNativeStart`, `__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN`,
  `__XluaBridgeNativeWorldCapture`, `XluaBridgeMapScanTick`,
  `XluaBridgeNativeUpdate`, and `XluaBridgePoll`. The executable does not contain
  literal `world.get.new`, `push.world.march.world.get.new`, `SendViewRequest`,
  `StartViewRequest`, `UpdateViewRequest`, or `WorldGetBlock` strings in the
  targeted scan. This is consistent with the secondary bot driving its scan via
  native hooks/runtime state rather than naming those managed request APIs in
  its Rust command layer; it does not by itself identify the missing trigger;
- two embedded default monster-catalog entries were recovered from the same
  executable: `monster-afk-default-small-gold` / `小金` with catalog key
  `catalog:7:9:2901011` and id `2901011`, and
  `monster-afk-default-big-gold` / `大金` with catalog key
  `catalog:7:10:2901012` and id `2901012`. These are recovered secondary-bot
  catalog facts, not yet current-game live-spawn proof.

Operator identification for the Zombie Invasion event (2026-09-06): `小金`
refers to the small invading zombies that appear in random waves, commonly as a
group of three with gold-coin visuals; `大金` refers to the Zombie Boss that can
spawn after an invading zombie is killed. This gameplay naming/behavior is
operator-supplied and is kept separate from binary proof. A narrow exact-byte
check of the current installed `Assembly-CSharp.rdl` did **not** find the
literal ids `2901011` / `2901012` or the Chinese names `小金` / `大金`; the same
check did confirm current-build zombie/event-related symbols including
`IsZombieBusTrain` and multiple `ZombieRush` / `ZombieBusTrain` strings. Those
symbols prove current zombie-event code exists, but they do not yet prove that
either runtime family is the implementation behind ids `2901011` / `2901012`.

This independently supports a two-stream design: persistent world points are
captured from `WorldPointManager`, while moving entities/monsters are captured
from `WorldMarchDataManager` mutations. It explains why a complete direct
`WorldGetBlock` sweep can legitimately contain players/resources/special points
yet still contain zero monsters. What remains to be recovered is the exact
current-build trigger/subscription that causes the server to deliver the march
updates over the desired scan area.

## PROVEN current-build event/radar/monster identifiers

The current `WorldPointType` enum values are now statically decoded from the
installed `Assembly-CSharp.rdl`; these values are constants from the current
build rather than declaration-order guesses. The v9 full direct scan observed
only types `6,7,11,13,15,17,21,25,30,31,44,46`. Consequently the entries below
that were not in that histogram are **statically identified but not live-seen in
that run**; absence during one event state is not treated as a failure.

| Value | Current enum name | Current evidence state |
| ---: | --- | --- |
| 4 | `WorldMonster` | static, not live-seen in v9 |
| 5 | `WorldBoss` | static, not live-seen in v9 |
| 12 | `MONSTER_REWARD` | static, not live-seen in v9 |
| 14 | `DETECT_EVENT_PVE` | static, not live-seen in v9 |
| 17 | `HERO_DISPATCH` | static + live-seen in v9 |
| 21 | `TREASURE` | static + live-seen in v9 |
| 22 | `INVASION_WORLD_MONSTER` | static, not live-seen in v9 |
| 28 | `RadarSeasonSnowSurvivor` | static, not live-seen in v9 |
| 29 | `GHOSTRECON_POINT` | static, not live-seen in v9 |
| 33 | `RADAR_DOMINATOR_GUIDE` | static, not live-seen in v9 |
| 34 | `RADAR_DOMINATOR_CURE` | static, not live-seen in v9 |
| 39 | `METEORITE_POINT` | static, not live-seen in v9 |
| 42 | `MONSETER_CHALLENGE_NEW_TREASURE` | static, not live-seen in v9 |
| 43 | `ACTIVITY_WORLD_TREASURE` | static, not live-seen in v9 |
| 44 | `DETECT_RETRY_TASK` | static + live-seen in v9 |
| 45 | `DETECT_DIG_GAME` | static, not live-seen in v9 |
| 46 | `TreasureChest` | static + live-seen in v9 |
| 47 | `RADAR_DOMINATOR_COCKATRICE_UNLOCK_1` | static, not live-seen in v9 |
| 48 | `RADAR_DOMINATOR_COCKATRICE_UNLOCK_2` | static, not live-seen in v9 |
| 49 | `DETECT_SUPPLIES_SEARCH` | static, not live-seen in v9 |
| 50 | `DETECT_ALLIANCE_CITY_SCOUT_MONSTER` | static, not live-seen in v9 |
| 54 | `DETECT_LAST_STAND` | static, not live-seen in v9 |
| 59 | `ALLIANCE_BOSS_S0` | static, not live-seen in v9 |
| 1001 | `SiegeTreasure` | static, not live-seen in v9 |
| 1003 | `SIMPLE_WORLD_MONSTER` | static, not live-seen in v9 |

The current managed `WorldMarch` type also contains dedicated classification
methods for event/moving families including `IsZombieBusTrain`, `IsMummyMarch`,
`IsDrillBase`, `IsDrillBaseTank`, `IsDrillBaseNewBossHugeSandWorm`,
`IsDrillBaseRoadHog`, `IsAisila`, `IsAlChallengeKirov`, `IsActBerserkBoss`,
`IsSandWorm`, `IsS4WanderBoss`, and `IsFixedBoss`. Its protobuf payload exposes
`MonsterInfo` and event-specific identifiers including `BloodyQueenMonsterUuid`
and `CityBattleS1RestBossUuid`. These names prove current-build recognition
paths exist, but they do not prove those event mobs are spawned at the current
time or provide their complete config catalog.

For acceptance and UI, monster/event support is therefore tracked as three
states: **Live verified**, **Statically identified**, and **Unknown**. When a
future march capture contains an unrecognized monster, retain its raw stable
identity and available `configId`, `marchType`, level/name/type fields so it can
be matched later without fabricating a label.

- Player power is part of the original scanner/product contract and now has a
  current-build-confirmed detail producer chain. Content-v12
  `UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac` implements
  `RequestWorldPointDetail(self)`: it resolves the current world/server,
  obtains the point via `SceneManager.World:GetPointInfo(pointId)`, and sends
  `MsgDefines.WorldGetDetail` with point id, server id, world id, current point
  type, and owner uid. `Net/Msgs/WorldGetDetailMessage.luac` handles a
  successful response by calling
  `DataCenter.WorldPointDetailManager:UpdateDetail(message)` and broadcasting
  `EventId.WorldPointDetail`. `WorldPointDetailManager` parses/stores a
  `WorldPointDetailData` by `pointId`, and `GetDetailByPointId` returns the
  cached detail. The current detail data has a real `power` field.
- Probe v8 ports the recovered original bounded enrichment policy onto that
  current route. Player bases with missing/zero power are checked against the
  detail cache, requested in batches of 48 through
  `UIWorldPointCtrl.RequestWorldPointDetail`, allowed 0.5 seconds for the cache
  update, and retried at most once. Resolved records receive
  `powerSource = DataCenter.WorldPointDetailManager.GetDetailByPointId`.
  Unresolved values remain null/unknown. Result/status diagnostics retain
  request, failure, cached-resolved, resolved, unresolved, and retry counts.
  This is current-build static producer proof plus recovered-original policy;
  the v8 live resolution rate is still unverified until the guarded acceptance
  is rerun.
- Resource remaining/capacity and exact gather-end time are recovered-original
  aliases/detail behavior; the common current protobuf payload proves resource
  identity/type/level/occupancy, not those amounts/timers. Missing values remain
  null/unknown.
- Monster template enrichment is recovered-original behavior. A missing current
  template or missing power row remains missing rather than being fabricated.
- The current static artifacts prove the managed rect-march request construction
  but not a camera-linked server coverage radius/spacing. v8 also disproved its
  use as the active per-camera discovery assumption. It remains historical
  reverse-engineering evidence rather than an active scan requirement.
- Current `WorldPointType` field names are statically proven, but exact numeric
  values for the newer special/radar/detect fields are not inferred from enum
  declaration order. The retained direct live histogram is the next authority.
- The reason a newly appended root-level `LWControlProbe.luac` module name was
  absent from `require()` during the guarded boot remains unknown. Only the
  observed loader failure and successful resolution of already-registered
  module names are promoted to fact.
- Season-specific extra `WorldPointInfo` payload bodies beyond the common
  statically recovered families are still not normalized.
- The larger 3000-tile map mode remains outside the proven normal-world
  `1000 x 1000` tile contract.

No default value is used to turn one of these UNKNOWN fields into positive game
state. The UI therefore shows blank/unknown data where the current capture does
not provide evidence.

## 2026-09-07 reference-bot UI, navigation, and camera findings

Confirmed from the recovered `LWControl.exe` UI bundle:

- World Map Intelligence is a dedicated data view with target-category filters,
  search, selectable rows, a detail pane, paging/library controls, and separate
  presentation for player bases, resources, monsters, alliance buildings, and
  other world points.
- Selecting a result exposes **Locate in Game**. Its command is `map_scan` with
  `mode=focus` plus point id, target UUID/server/kind/name and X/Y. The UI waits
  for verified game-camera coordinates before declaring success. This is a
  camera/map focus operation; it is not player/base relocation.
- The original UI includes a `Restoring original camera` scan stage. Therefore
  visible camera movement is part of at least one recovered original scanner
  path and is not unique to this reconstruction.

Confirmed independently from `lwbridge-0.3.1.exe`:

- it exposes `map_scan_start`, `map_scan_stop`, `map_scan_clear`,
  `map_coordinate_jump`, `map_march_follow`, `gotoWorldCoordinate`, and
  `gotoWorldMarch`;
- its stored map-data layer supports filtering by level, power, monster id/type,
  resource identity, distance, quality and several special-task fields;
- its lower-level capture contains `XluaBridgeMapScanTick` and native
  `WorldMarchDataManager.AddMarch/UpdateMarch/AddOrUpdateMarch/TryRemoveMarch`
  hooks. This is strong evidence for a native capture architecture that differs
  from the currently proven 500-view managed AOI route. The exact map-wide
  server trigger used by that native scanner is still UNKNOWN and is not
  fabricated from symbol names.

Product changes based on those findings:

- the desktop World Scan now has its own tab, category filter, text search,
  result table, raw/current point-type labels, selected-target details, and a
  Locate in Game action;
- after a successful scan the in-memory probe remains registered and accepts a
  bounded coordinate-focus command. It uses the already-proven current-build
  `TileToWorld` plus `Lookat`/`AutoLookat` route and requires observed camera
  coordinates within three tiles;
- the runner first attempts exact restoration of the three modified Lua package
  files while Last War remains running. It leaves the game open only when all
  protected before/after hashes match. Any sharing violation or hash mismatch
  falls back to the previously proven close-and-restore path.

The visible 500-view camera traversal remains part of the **PROVEN current
build** monster-discovery route. Replacing it with the `lwbridge`-style native
capture requires recovery of that bot's exact map-wide trigger/subscription; the
native hook names alone are insufficient evidence to remove the camera phase.

### Desktop packaged-LocalAppData correction (2026-09-07)

The desktop test at 00:21:07 reproduced a path-virtualization failure: all
installed Lua/runtime payload checks passed, but the Daily Task install manifest
was reported absent. The same manifest is present and exact under the real user
`AppData\Local\LWControl\runtime`; Windows packaged-app execution can expose a
`...\AppData\Local\Packages\<package>\LocalCache\Local` LocalAppData path to
child processes. World Scan now canonicalizes that packaged LocalCache path back
to the real user LocalAppData and explicitly passes the canonical value to the
Python runner/game process. Daily Task and bridge runtime defaults use the same
canonical resolver. Exact payload/manifest/hash gates remain unchanged.

## PROVEN desktop World Scan preflight incident (2026-09-07)

The desktop World Scan button reported `Loader markers are present but they are
not the exact verified Daily Task runtime` before creating its owned
`world-scan-ui-*` candidate directory. Read-only inspection immediately after
the failure proved the installed runtime itself was exact: both runtime entries
were present, generated payloads matched current source, the install manifest
was present, every protected-file hash matched `installedHashes`, and
`installed=true`. Running the same full-scan preparer directly then succeeded
and produced a round-trip-verified candidate without changing installed files.

This proves the observed button failure was a transient preflight observation;
it is not evidence of a damaged Daily Task runtime or a World Scan transport
failure. The preparer now repeats only the same exact Daily Task runtime
inspection for a bounded four attempts before refusing. It still requires all
payload, manifest, protected-hash, and preserved-official-entry proofs. A
persistent mismatch remains fail-closed and now reports which proof failed.

Guarded acceptance after that change also passed end to end. The patched
preparer accepted the exact Daily Task runtime and produced a verified candidate;
the runner then completed 10,000/10,000 logical blocks in 65/65 batches with
peak concurrency 8 and zero retries. The run accumulated 23,126 records:
6,388 player bases, 7,998 resource points, 7,734 monsters/bosses, 390 alliance
buildings, and 616 other world points. Monster discovery again came from
`PushWorldMarchWorldGet.serverMarchArr.marchInfos` with 500/500 official camera
views and 500/500 correlated march responses. Camera, response hooks, manager
flags, world response flags, and the protected files all restored successfully;
the before/after SHA-256 sets matched exactly.

## 2026-09-07 persistent World Scan runtime conversion

The game-closing behavior is now traced to the temporary package lifecycle, not
to World Scan completion itself. The old bounded runner had to restore
`LWScripts.data`, `LWScripts.txt`, and `version.txt` immediately after the scan.
A live restore attempt reproduced Windows sharing error 32 because the running
game held `LWScripts.data` open. The safe runner therefore closed Last War before
restoring the package.

World Scan has now been moved into the already-installed persistent runtime
package. The wrapper preserves the official `LuaEntry`, loads the Daily Task
runtime and `LWControlWorldScanRuntime`, registers both pumps, and leaves the
World Scan module idle until a bounded `world-map-scan-command.txt` `run_once`
command arrives. A completed scan remains loaded so the verified coordinate
focus command can be used without replacing game files again.

The installed combined package was verified after installation with all of the
following true: Daily Task runtime entry present, World Scan runtime entry
present, preserved official entry present, current source payloads matching,
manifest present, manifest hashes matching, and `installed=true`. The installed
package SHA-256 is
`881d801ff4174cc354af704616617efb86cae65a338616438f79fbe2663f2a8f`.
After launching the official Last War launcher, the live World Scan heartbeat
reported `persistent=true`, version `lwcontrol-world-full-scan-probe-7`, and
registration through `UpdateManager.AddUpdate`. This was the first persistent
loader proof. It has since been superseded by the probe-v9 live acceptance
evidence below.

Offline verification after the conversion passed 18 focused Python tests,
44/44 Core checks, Python compilation of the runtime installer/acceptance tools,
the Desktop Release build with zero warnings/errors, and `git diff --check`.
The World Scan Lua source still uses 190 top-level locals, below the retained
current runtime limit.

## 2026-09-07 lwbridge native world-capture architecture

Additional static recovery from `lwbridge-0.3.1.exe` confirms that its native
capture layer resolves IL2CPP metadata from `GameAssembly.dll` and installs
runtime method hooks rather than relying only on Lua-side manager snapshots.
The recovered hook targets include:

- `WorldPointManager.AddPointInfo`
- `WorldPointManager.RemovePointInfo`
- `WorldTileInfo.RemovePointInfo`
- `WorldPointManager.ParseWorldPointRemove`
- `WorldPointManager.ParseWorldPointFoldUp`
- `WorldMarchDataManager.AddMarch`
- `WorldMarchDataManager.UpdateMarch`
- `WorldMarchDataManager.AddOrUpdateMarch`
- `WorldMarchDataManager.TryRemoveMarch`
- `WorldTroopManager.UpdateTroop`

The same binary exposes native capture queues for points, marches, point
removals, and march removals plus ready/dropped/pending counters. Its
`src/services/map_scan.rs` strings prove a separate block-oriented scan service
with `normal` and `fast` modes, map dimensions/current coordinates, block totals,
read/unread/failed/in-flight counts, concurrency, scan rate, progress, native
pending-record counts, and a publishing phase. Recovered selectable data types
include `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, and
`treasure`.

This proves how lwbridge captures objects that enter or leave the game's native
world managers. It does **not** yet prove the request/trigger that makes all
off-screen monster/march objects enter those managers. No recovered evidence in
this pass establishes that `startMapScan` itself avoids camera/view movement.
Consequently the current product keeps the proven 500-view monster fallback and
restores the original camera afterward. Removing visible movement remains an
open reverse-engineering task until the exact lwbridge block/AOI trigger is
recovered and validated against the current Last War build.

## 2026-09-07 probe-v9 rich-detail acceptance

### PROVEN current-build detail normalization

Read-only inspection of content-v12
`DataCenter/WorldPointDetail/WorldPointDetailData.luac` now closes the resource
amount ambiguity. `WorldPointDetailData.ParseData` normalizes the incoming
resource fields as follows:

- `remainRes` is copied when present; otherwise incoming `reserve` is copied into
  `remainRes`;
- `initRes` is copied when present; otherwise incoming `initReserve` is copied
  into `initRes`;
- `expireTime` is copied separately. It is not treated as a gather-completion
  timestamp.

Probe v9 therefore reads resource remaining/capacity only from the current
`WorldPointDetailManager.GetDetailByPointId` cache after the normal
`UIWorldPointCtrl.RequestWorldPointDetail` request. Missing/non-positive detail
amounts remain unknown. The exact gather-end time is still **UNKNOWN** and is
not inferred from `expireTime`.

### PROVEN current-build loader/helper boundary

The persistent acceptance runner initially rejected an existing
`WorldBlockSender.dll` even when rebuilt from the same checked-in source. Two
independent .NET Framework `csc` builds proved the compiler output differs only
in compiler-generated identity fields: the PE COFF timestamp and the 16-byte
module MVID. The acceptance gate now derives those exact offsets from two fresh
verified builds, zeros only the four-byte timestamp and contiguous 16-byte MVID,
and requires the remainder of the staged/runtime assembly to hash identically.
For the accepted helper in this session:

- checked-in source SHA-256:
  `d53c20fe549e3beb3310c0f160963edf27d5e0d324bc2379226e5f87483025ae`;
- installed helper SHA-256:
  `f66f32524fbea60df7391ebc3fdc55b3c6be94d9eec5da20ed565c95417523bd`;
- normalized helper SHA-256:
  `2f97b609f633d533dbe69a7be6e17919fdaebedbfe63e09da788dec7b79da12e`;
- COFF timestamp offset `136`; MVID offset `4560`, size `16`.

This retains a fail-closed source/binary identity check without pretending the
legacy compiler is byte-deterministic.

The packaged-app LocalAppData correction is also now part of the persistent
acceptance path: heartbeat, command, result and helper files resolve through
`discover_paths()["runtime"]`, which points at the real
`%LOCALAPPDATA%\LWControl\runtime` rather than Codex's packaged LocalCache.

### PROVEN current-build v9 install

The v9 scanner source was installed only after closing `LastWar.exe`. The updater
created backup
`C:\Users\chimw\AppData\Local\LWControl\backups\loader-probe-20260907-034004-5c757556d75c497e95f0af6f78fcfc47`
and then verified the installed content-v12 package. All 18,765 entries remain
LENC encoded, plaintext count is zero, the preserved official `LuaEntry` remains
SHA-256
`50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137`,
and clean `BaseUtils.rdl` remains SHA-256
`b20865f42c9272e0fca2b6deb2e9142576b12dcf42f11ad1a8816ddd59b952d6`.
Installed protected hashes after the v9 update are:

- `LWScripts.data`:
  `4f79d46944db9b26fe9fadaa52b7c08a5f0bae76863d4dddbfcf5dc5f08416a4`;
- `LWScripts.txt`:
  `5a4f5ab19f1f9016058a6dbcdaceb4be05959c1f92d8ddb535c1c4e1d08a8eef`;
- `version.txt`:
  `6b51d431df5d7f141cbececcf79edf3dd861c3b4069f0b11661a3eefacbba918`;
- decoded v9 World Scan source:
  `a6dc2f194e51356a9519eb8372aa843a2a6c6ed324fa9f93574556e8a8a3d189`.

The official launcher then produced a fresh
`lwcontrol-world-full-scan-probe-9` heartbeat with `persistent=true` and
`registrationMethod=UpdateManager.AddUpdate`.

### PROVEN capped v9 live acceptance

The first v9 live acceptance used `powerTargetLimit=96` and
`resourceTargetLimit=96`. It completed the entire transport and monster path:
10,000/10,000 logical blocks, 65/65 batches, 500/500 camera views, exact camera
restore, response/manager/world-response restoration, unchanged protected
hashes, and a still-running game process. It accumulated 25,177 records:
6,394 player bases, 7,994 resource points, 7,875 monsters, 390 alliance
buildings and 2,524 other world points.

Both bounded detail samples were complete: player power resolved 96/96 and
resource amount/capacity resolved 96/96, with zero request failures, zero
retries and zero unresolved targets. This is live proof that the current
`UIWorldPointCtrl.RequestWorldPointDetail -> WorldPointDetailManager` route
populates both the player `power` field and the normalized resource amount
fields used by probe v9.

### PROVEN uncapped product-default v9 acceptance

A second acceptance omitted both target limits, exercising the product-default
uncapped enrichment path. It again completed 10,000/10,000 blocks, 65/65
batches and all 500 monster views, restored every owned hook/flag/camera state,
left the game running, and preserved all four protected hashes. It accumulated
25,208 records: 6,394 player bases, 7,987 resource points, 7,897 monsters, 390
alliance buildings and 2,540 other world points.

Player enrichment had 95 cache hits before requests, targeted 6,299 remaining
players, issued 6,563 detail requests including one bounded retry, and resolved
6,391 of the 6,394 player records. Three remained unknown after the single
allowed retry. Resource enrichment had 96 cache hits, targeted 7,891 remaining
resources, issued 7,926 requests including one bounded retry, and resolved
7,952 of 7,987 resource records. Thirty-five remained unknown. There were zero
request-call failures in both enrichers; unresolved values remain null/unknown
by design.

The 30,000 accumulated-record ceiling remains a fail-closed bound. It replaced
the earlier 25,000 ceiling only after a live run reached that old cap with
17,154 unique static records and 497/500 monster captures, proving the previous
limit was too small for normal current-world density rather than indicating a
duplication loop.

### Validation and remaining UNKNOWNs

After the v9 changes, focused World Scan Python tests pass 18/18, Core checks
pass 44/44, the Desktop Release build succeeds with zero warnings and zero
errors, and `git diff --check` reports no content errors.

Current UNKNOWNs remain explicit: exact gather-end time, the native lwbridge
map-wide request/trigger that could replace visible 500-view traversal, complete
season-specific point normalization, and the small set of player/resource
detail rows that do not resolve after one bounded retry. No default is used to
turn those unknowns into positive state.

The current workstation has one active monitor. Persistent World Scan itself is
protocol/runtime driven and does not depend on a second monitor; later mouse or
GUI automation must use the single active display rather than the older
dual-monitor assumptions from the Macro Clicker project.
