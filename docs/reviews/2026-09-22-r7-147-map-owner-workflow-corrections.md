# R7-147 Map Data owner-workflow corrections

**Date:** 2026-09-22
**Parent revision:** `91d07d04ada09b1cfd143c6171a0d45c19760828`
**Scope:** the eight Map Data issues reported from direct owner testing before the planned external audit.

This checkpoint re-opened the Map Data assumptions instead of treating earlier acceptance as proof that the owner workflow was correct. The corrections below are implementation changes, not documentation-only status changes.

## Owner findings and disposition

| # | Owner finding | R7-147 disposition |
|---|---|---|
| 1 | Old/random server data remained after quitting LWBridge | **Corrected.** Published scan data is now session-scoped for the normal app. Normal startup clears leftover scan/index/run/Treasure-read state before Map Data is exposed, and normal teardown clears it again. Player marks, settings and plunder job/history state remain durable. |
| 2 | Saved-server filter had no **All** option | **Corrected.** Auto/saved-data browsing supports `serverId=0` as an aggregate published-data scope and shows **All** plus each current-session saved server. Each result row retains its real `serverId`. |
| 3 | **Run now** could not be used while recurring automatic scanning was disabled | **Corrected.** One-shot multi-server Run Now has its own persisted request marker. It does not silently enable recurring scheduling, and the marker clears at the terminal cycle. |
| 4 | A row from another scanned server did not navigate to that server before Jump | **Corrected.** Cross-server result navigation confirms `server_jump` to the row's server first, then performs coordinate Jump or moving-target Follow. |
| 5 | Manual Clear after Auto could fail, and Auto had no clear action | **Corrected.** Stopped Clear is a local-data operation and no longer requires browsed server == live server. Auto exposes **Clear Map Data** using `serverId=0` to clear all current-session scan data. Clear still fails closed while a scan is active. |
| 6 | Doom Walker should Follow instead of coordinate Jump | **Corrected.** Moving-target detection uses Follow for Truck/Railway and Doom Walker. Current ordinary Doom Walker identity is `configType=8`, `configSpecial=11`; the separate special-32 moving boss route is also treated as moving. |
| 7 | Train/Railway scan produced no rows | **Source defect corrected; fresh positive population unavailable.** Full-world AOI acquisition now performs one official read-only `LWTrainDataManager.TryGetTrainList(true)` refresh after coverage and merges `OnTrainListGet(allianceTrainList)` rows by exact march UUID. Current-v20 `TrainType.Truck=1` and `TrainType.Train=2` were re-verified from the installed RDL. Fresh probes on 2175, 2180, 2185, 2190, 2195, 2196 and 2204 all returned an authoritative empty Train list, so R7-147 does not claim a fresh positive Train row. Historical positive Railway/Follow evidence remains provenance only. |
| 8 | Manual Scan scans one server, so its server filter was misleading | **Corrected.** Manual Scan no longer shows the saved-server filter. Saved-server/All browsing belongs to the Auto/saved-data view. |

## Railway current-v20 source verification

Installed `Assembly-CSharp.rdl` SHA-256: `F8F12F40E16B1D839526A58B53BC4320EFA262906AF22D4EDBA29D9BA16857E1`. Metadata inspection returns `TrainType.Truck = 1` and `TrainType.Train = 2`.

The installed v20 Lua path used by the official Train UI was re-read before implementation. `LWTrainDataManager` owns `enemyTrains`, `TryGetTrainList(true)` refreshes the list, `OnTrainListGet` consumes `allianceTrainList`, and `TrainData` supplies march UUID / server / timing / route data. The production scanner now reproduces that read-only source once per Railway-containing full-world scan rather than relying only on whatever Train objects happen to be materialized in `WorldScene.MarchDataManager`.

The live 2204 Railway full scan completed 2,500/2,500 blocks before the positive-row assertion; its official Train-list result was `state=proven`, `refreshObserved=true`, `train_march_records=[]`. A separate current-v20 population probe then checked 2175/2180/2185/2190/2195/2196 through the same official list route and received zero rows on every server. This is a current population observation, not proof that Trains can never appear.

## Regression coverage

R7-147 adds/updates deterministic and browser coverage for:

- all-server (`serverId=0`) indexed search and option aggregation;
- session-wide Clear and preservation of durable marks/jobs;
- normal-window startup/teardown scan-data clearing contract;
- one-shot Run Now while recurring Auto is disabled;
- Manual Scan having no server filter;
- Auto **All** plus saved-server browsing;
- cross-server Jump ordering;
- ordinary Doom Walker Follow using exact march UUID;
- official Train-list refresh once per full Railway scan and exact source identity;
- missing optional name/reward option families normalize to empty arrays instead of blanking the Map page (caught by the full frontend interaction sweep on Zombie Boss/Truck);
- existing R7-131/R7-132/R7-136 persistence, scheduler, reconnect, Stop/Clear and restart behavior under the new contract.

## Audit status

The eight owner-reported workflow defects are addressed in code. A fresh **positive** current-v20 Railway row/Follow remains population-dependent; the current source path is verified and current sampled servers are empty. No state-changing gameplay action was used by this checkpoint.
