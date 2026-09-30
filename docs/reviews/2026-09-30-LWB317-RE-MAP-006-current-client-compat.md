# LWB317-RE-MAP-006 — current Last War Map compatibility

Date: 2026-09-30

## Result

State: `IMPLEMENTED_NOT_VALIDATED` — current-v21 static/source compatibility is
proven and the production Map317 adapters are wired; fresh bounded live proof
remains required.

Fresh read-only identity/runtime gates on 2026-09-30 passed for the installed
Last War content-version 21 client. No game process was launched by this static
phase.

Evidence:

- `evidence/lwbridge-0.3.17/map/current-client-static.json` —
  `12DC35AB9675F0CC7242CEC0E252C9BE2C533C2354C945F11FBD2ED28F650C37`;
- `tools/lwbridge317/inspect_current_map_compat.py`;
- `python tools/check_current_client_compat.py` — PASS;
- `python tools/check_current_client_runtime_contract.py` — PASS.

## Installed identity

- script package file/content version: `3 / 21`;
- package entries: `18,734`;
- `LWScripts.data` SHA-256:
  `e888a4ae3faae6df501493e18653fbb92c2406487467ba34312b7c5a22e2caa5`;
- `LastWar.exe` SHA-256:
  `905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d`;
- `xlua.dll` SHA-256:
  `d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45`;
- `Assembly-CSharp.rdl` SHA-256:
  `bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.

The existing critical-anchor compatibility policy and the current world AOI
runtime-structure gate both pass.

## Compatibility matrix

| 0.3.17 requirement | current-v21 static source | state / shim | live proof |
|---|---|---|---|
| world readiness / map context | current `CurrentClientMapBlockSource.WorldReady` + v21 world runtime gate | compatible high-level context adapter | required |
| coordinate jump | current client route proves `SceneUtils.TileToWorld` + `GoToUtil.GotoWorldPos` | map `gotoWorldCoordinate` to current owned navigation result | required |
| march follow | current `WorldMarchDataManager.GetMarch/HasMarchUuid` and `GoToUtil.JumpToMarchByUuid` route | map `gotoWorldMarch` to current owned march-follow result | state-dependent |
| server jump | current RDL `CrossServerUtil` (`SilentLogin/Update/IsCrossing`) + owned current server-jump route | map `gotoServer`/authoritative arrival poll to current owned jump | required |
| Map acquisition | v21 AOI/runtime contract passes; current source owns full-world captures | current block-source adapter produces local records; no 0.3.17 wire grammar is invented | required completed scan |
| Dispatch steal / scheduled Dispatch | v21 `DispatchStealMessage` and Dispatch manager/UI modules are byte-identical to their recovered supported forms | concrete current-client arm/pending/result bridge with exact 0.3.17 durable worker boundary | entity/state dependent |
| Treasure inspection | v21 Supplies/Charge/UI/Radar claim-info decoded modules are byte-identical | current Treasure inspector is wired to the 0.3.17 cache/provider boundary | required if current Treasure rows exist |
| Treasure claim/status | low-level v21 Treasure/scout mechanics exist, but the exact high-level batch/status sender + authoritative result normalization is not source-proven | `GAME_PROVIDER_UNAVAILABLE`; fail closed, no fabricated counters/orchestration | `BLOCKED` until a source-proven provider chain exists |
| alliance Dispatch share | v21 Dispatch/share modules preserve the exact fresh-task/point/share sender path; `ChatMessageHelper.getAttachmentId == ShareEncode.Encode(param)` | current provider rereads `GetSingleTaskByUuid` + `World.GetPointInfo`, builds `Text_PointShare`, sends `ChatHeroDispatchShare`, and accepts success only from matching owned-session response with no `errorCode`; 5 s/row | entity/state dependent |
| plunder server day | v21 `UITimeManager.GetServerTime` + `GetTodayZero`; current `GetTodayZero = serverTime - ((serverTime + changeDeltaTime) % 86400000)` | source-only server time/day-zero provider, validated same-day; no local-clock fallback | required while live provider is connected |
| Truck scheduled execution | v21 Railway/Train/FakePVP/AttackTrain modules are byte-identical to recovered supported modules | concrete current-client arm/pending/result/clear bridge; scheduled `uuid == trainUuid`, result correlated by `jobId` | entity/state dependent |
| Ghost preparation | v21 snapshot/acquisition source can expose Ghost rows, but an exact current-v21 equivalent of protected `prepareGhostPlunderTasks` was not established | `GAME_PROVIDER_UNAVAILABLE`; fail closed rather than infer a scheduling payload | `BLOCKED` until a source-proven preparer exists |
| scheduled Dispatch/Truck local DB | independent of client after scheduling | exact 0.3.17 local SQLite + independent 100 ms worker contract | deterministic tests complete |

## Specific current source fingerprints

The Phase-2 inspector deliberately did not weaken old v19 package hash gates.
Instead, after the v21 identity gate passed, it decoded the exact current
entries and compared them independently. The following v21 decoded modules are
byte-identical to their previously recovered supported forms and retain all
required anchors:

- `Net/Msgs/DispatchTask/DispatchStealMessage.luac`;
- `DataCenter/ActivityListData/ActDispatchTaskDataManager.luac`;
- `UI/UIWorldPoint/Component/UIWorldPointBtn.luac`;
- `DataCenter/WorldPointDetail/WorldSuppliesPointData.luac`;
- `DataCenter/WorldPointDetail/WorldChargeData.luac`;
- `DataCenter/SeasonManager/Activity/SeasonSuppliesShareDataManager.luac`;
- `UI/UIWorldPoint/Controller/UIWorldPointCtrl.luac`;
- `DetectEventGetTreasureClaimInfo.luac`;
- `DetectEventTreasureClaimPlayerInfo.luac`.

Current RDL additionally proves `CrossServerUtil` and its XLua wrapper, plus
`WorldMarchDataManager` current methods including `HasMarchUuid`, `GetMarch`,
`GetMarchByTargetPos`, `GetAllMarchesByCS`, and the XLua wrappers needed by the
owned current-client navigation/Map lane.

The production Desktop path now uses one `Map317CommandService` per normal
profile. It owns the exact per-profile database, current block source, scan
provider, action provider and independent Dispatch/Truck workers. The older
`MapDataStore`/`ManualMapScanCommandService` path is not a second normal
production Map authority; it remains only in isolated legacy proof/replay
modes. `game_asset_image` and `map_train_list_coverage` are served directly by
the Map317 source-backed service and therefore do not require legacy Map
persistence.

Current-client compatibility is intentionally asymmetric where source proof is
incomplete. Treasure **inspection** is implemented, but Treasure claim/status
is not. Dispatch share/server-day/Dispatch scheduled execution/Truck scheduled
execution are implemented, but Ghost preparation is not. Those unavailable
provider methods throw current-style errors and never synthesize records,
claim counters, rewards or Ghost task payloads.

## Important boundary

Static compatibility proves that the required current-client structures and
specific action sources are present; it does not convert historical live
records into fresh Goal proof. The next phase must launch/use an assistant-owned
session, confirm ownership, and freshly observe every Map path the present game
state permits. Historical v19/v21 live reviews remain hypotheses/accelerators
only.
