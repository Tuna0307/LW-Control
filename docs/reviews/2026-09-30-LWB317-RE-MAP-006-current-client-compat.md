# LWB317-RE-MAP-006 — current Last War Map compatibility

Date: 2026-09-30

## Result

State: `IMPLEMENTED_NOT_VALIDATED` — current-v22 static/source compatibility is
proven and the production Map317 adapters are wired; fresh bounded live proof
remains required.

The v21 baseline below remains the original Phase-2 compatibility checkpoint.
`LWB317-COMPAT-MAP-V22-001` has now revalidated the same Map implementation
against installed content version 22 without changing production Map code. See
`docs/reviews/2026-09-30-LWB317-COMPAT-MAP-V22-001.md` and
`evidence/lwbridge-0.3.17/map/current-client-v22-static.json`.

Evidence:

- `evidence/lwbridge-0.3.17/map/current-client-static.json` —
  `12DC35AB9675F0CC7242CEC0E252C9BE2C533C2354C945F11FBD2ED28F650C37`;
- `tools/lwbridge317/inspect_current_map_compat.py`;
- `python tools/check_current_client_compat.py` — PASS;
- `python tools/check_current_client_runtime_contract.py` — PASS.

## Installed identity

### Current v22 revalidation

- normalized underlying script package file/content version: `3 / 22`;
- normalized entry count: `18,741`;
- normalized `LWScripts.data` SHA-256:
  `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`;
- all 22 tracked Map-critical decoded modules are byte-identical to their
  supported forms and retain their required anchors;
- `LastWar.exe`, `xlua.dll`, and `Assembly-CSharp.rdl` remain byte-identical to
  the v21 supported binaries;
- the installed package is the exact repository-owned Overview wrapper, so the
  v22 inspector normalizes only its preserved official `LuaEntry` and packaged
  probe in memory; the installed files are not rewritten.

### v21 baseline

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

| 0.3.17 requirement | current-v22 static source | state / shim | live proof |
|---|---|---|---|
| world readiness / map context | current `CurrentClientMapBlockSource.WorldReady` + unchanged v22 world runtime structures | compatible high-level context adapter | required |
| coordinate jump | current client route proves `SceneUtils.TileToWorld` + `GoToUtil.GotoWorldPos` | map `gotoWorldCoordinate` to current owned navigation result | required |
| march follow | current `WorldMarchDataManager.GetMarch/HasMarchUuid` and `GoToUtil.JumpToMarchByUuid` route | map `gotoWorldMarch` to current owned march-follow result | state-dependent |
| server jump | current RDL `CrossServerUtil` (`SilentLogin/Update/IsCrossing`) + owned current server-jump route | map `gotoServer`/authoritative arrival poll to current owned jump | required |
| Map acquisition | v22 RDL AOI/runtime contract is unchanged; current source owns full-world captures | current block-source adapter produces local records; no 0.3.17 wire grammar is invented | required completed scan |
| Dispatch steal / scheduled Dispatch | v22 `DispatchStealMessage` and Dispatch manager/UI modules are byte-identical to their recovered supported forms | concrete current-client arm/pending/result bridge with exact 0.3.17 durable worker boundary | entity/state dependent |
| Treasure inspection | v22 Supplies/Charge/UI/Radar claim-info decoded modules are byte-identical | current Treasure inspector is wired to the 0.3.17 cache/provider boundary | required if current Treasure rows exist |
| Treasure claim/status | v22 adds no source proof for the exact high-level batch/status sender + authoritative result normalization | `GAME_PROVIDER_UNAVAILABLE`; fail closed, no fabricated counters/orchestration | `BLOCKED` until a source-proven provider chain exists |
| alliance Dispatch share | v22 Dispatch/share modules preserve the exact fresh-task/point/share sender path | current provider rereads `GetSingleTaskByUuid` + `World.GetPointInfo`, builds `Text_PointShare`, sends `ChatHeroDispatchShare`, and accepts success only from matching owned-session response with no `errorCode`; 5 s/row | entity/state dependent |
| plunder server day | v22 preserves the supported runtime/binary identity for `UITimeManager.GetServerTime` + `GetTodayZero` | source-only server time/day-zero provider; no local-clock fallback | required while live provider is connected |
| Truck scheduled execution | v22 Railway/Train/FakePVP/AttackTrain modules are byte-identical to recovered supported modules | concrete current-client arm/pending/result/clear bridge; scheduled `uuid == trainUuid`, result correlated by `jobId` | entity/state dependent |
| Ghost preparation | v22 adds no exact current-client equivalent of protected `prepareGhostPlunderTasks` | `GAME_PROVIDER_UNAVAILABLE`; fail closed rather than infer a scheduling payload | `BLOCKED` until a source-proven preparer exists |
| scheduled Dispatch/Truck local DB | independent of client after scheduling | exact 0.3.17 local SQLite + independent 100 ms worker contract | deterministic tests complete |

## Specific current source fingerprints

The Phase-2 inspector deliberately did not weaken old v19 package hash gates.
Instead, the current inspector decodes exact current entries and compares them
independently. The v22 revalidation expands the tracked set to 22 decoded Map
modules, including the complete Dispatch-share chain; every tracked module is
byte-identical to its supported form and retains all required anchors. The
following original v21 subset remains representative:

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

Static compatibility proves that the required v22 current-client structures and
specific action sources are present; it does not convert historical live
records into fresh Goal proof. The next phase must launch/use an assistant-owned
session, confirm ownership, and freshly observe every Map path the present game
state permits. Historical v19/v21 live reviews remain hypotheses/accelerators
only.
