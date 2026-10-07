# Milestone C — current-v22 producer/projection recovery

Current installed identity remains admitted under the current compatibility policy:
content version 22, exact current package/game/xLua/RDL identities recorded in A.
Fresh `check_current_client_runtime_contract.py` and
`inspect_current_map_compat.py` both pass.

## Producer mapping

| Kind | Current-v22 producer evidence | Current projection disposition |
| --- | --- | --- |
| City | `WorldPointManager.GetAllMainBaseList` and current `BuildPointInfo` fields/wrappers | retained City rows, owner identity, alliance/health/protection; sparse optionals preserved |
| Resource | `_pointInfos`, `GetResourcePointInfoByIndex`, `ResPointInfo.gatherMarchUuid/gatherUid/GetResLevel/GetResType`, GatherResource config | source-backed name/config/reserve/occupancy; unknown occupancy stays unknown |
| Monster | current `WorldMarch` monster/timing/zMBoss fields and methods plus current template manager | name/level/distance/shield only when source known; no generic timing substituted for shield |
| Truck | v22 `TrainData.luac` + `LWTrainDataManager.luac` | current goods/max loot/arrival metadata preserved; recovered special-UR cap applied; reward key remains declared rebuild adaptation |
| Railway | same v22 Train model/manager | direct row or `trainDataJson` supplies arrival/rob/protection; sparse values stay sparse |
| Dispatch | current `HeroDispatchMissionPointInfo` plus v22 `UIWorldPointBtn`/task config | exact current steal eligibility fields become plunderAt/stolen/max counts |
| Ghost | current `GhostreconPointInfo` plus current task/template Lua | completion/expiry/steal/config data retained; no action preparation invented |
| Treasure | current `TreasurePointInfo` / `WorldSuppliesPoint` and v22 status-data modules | ordinary/supplies scan state retained; protected claim/status action remains D |

The RDL reports in this checkpoint include xLua wrapper/member locators for the
current methods actually used by the probe. Train is a Lua-owned model, so its
proof comes from the exact decoded v22 `TrainData.luac` and
`LWTrainDataManager.luac`, not from a nonexistent C# `TrainData` type.

## Three deliberate non-mappings

1. The probe records `resourceTypeId`, but exact recovered public `MapQuery`
   does not expose `resourceType`. The old native SQL capability is not a license
   to add a new public filter.
2. Current `HeroDispatchMissionPointInfo` has `expiredTime`, but a whole-v22
   Dispatch/UI search shows eligibility uses completion/protection/steal count/cap,
   not `expiredTime` or `taskExpireTime`. Therefore the rebuild correctly does
   not reinterpret that field as the exact host's optional `taskExpireTime`.
3. Truck `currentGoods.key` remains a documented adaptation
   `reward:<type>:<itemId>`; the exact original producer for that key is still
   unrecovered and is not fabricated.

## C result

No source-backed projection/ingestion discrepancy was demonstrated. The current
projection preserves missing/null/sparse values and only enriches from verified
current fields. No product edit is justified by C.

Protected positive population and actual runtime field values remain future live
witnesses; this static milestone establishes field/API availability and projection
policy only.
