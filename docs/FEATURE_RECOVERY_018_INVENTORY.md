# FEATURE-RECOVERY-018 — original-feature inventory (before implementation)

Date: 2026-10-17. Branch: `codex/feature-recovery-018` at the initial inventory checkpoint. This is an **original-feature inventory**, not proof of live functionality. Individual features receive a `WORKING` or `NOT_POSSIBLE` verdict only after investigation and genuine current-client verification.

## Provenance and classification

The original reference is post-auth **LWBridge 0.3.17**, executable SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783` (`AGENTS.md:490-510`). Exact original frontend evidence is retained on `research/offline-controller` at:

- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js` (19,322 bytes, SHA-256 `C1B83A0B6524EF9AF510A114DF9679DAE0260D34B633659EB1A5FFB2AC5CF49C`), `HotkeyPanel-XA8idRHB.js` (7,801 bytes, SHA-256 `9BD7C11768693705391060D4F79119B5EA4A5229E042758165D3AD751A961BE6`), `api-ClPPi2JT.js`, `en-BisSXcTB.js` and `index-rIL9Fpht.css`;
- `docs/reviews/2026-09-29-LWB317-UI-002E-city-layout-inventory.md`, `002F-hotkeys-inventory.md`, and `002G-mini-games-inventory.md` on that branch; machine-readable page inventories in `evidence/lwbridge-0.3.17/ui/pages/`;
- older **0.3.1** native reverse-engineering (`docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`, `2026-09-25-r8-018-city-layout-draft-persistence.md`, `2026-09-25-r8-019-hotkey-config-persistence.md`, `2026-09-26-r8-050-city-layout-snapshot-fence.md`, `2026-09-26-r8-052-city-layout-apply-status-fence.md`, `2026-09-26-r8-070-city-layout-live-fence.md`), used as corroboration **only**, not silently attributed to 0.3.17;
- current production `src/LWBridge.UI-0.3.17/src/{CityLayoutPage,HotkeyPages,previewRemainingPagesContracts,backendBridge}.js*`, `src/LWBridge.Desktop/{CityLayoutDraftCommandService,HotkeyConfigCommandService,LWBridgeWindow}.cs`.

`RECOVERED` means original 0.3.17 frontend shows the control and described behavior; `CURRENT_ADAPTATION` means a current-client integration must be engineered and tested; `OWN_DESIGN` means a future behavior is independently designed (not to be represented as original); `UNKNOWN` means original game-side implementation, live game availability or effective state has not been recovered. No original commercial authorization or entitlement bypass is in scope.

## City Layout

| Original feature / workflow | Original evidence | Classification | Current behavior at inventory |
|---|---|---|---|
| Read real city snapshot, statistics, first main-city region, grid and building icons/names/levels | 002E inventory §Page structure; original `CityLayoutPanel` / native contract §3 | RECOVERED UI / UNKNOWN game producer | Only fixture population, ordinary live route has no snapshot (`CityLayoutPage.jsx:18-24,289-290`). CURRENT_ADAPTATION needed. |
| Refresh snapshot, present connection/error/not-in-city/stale warnings | 002E inventory §Page structure/state; `CityLayoutPanel` | RECOVERED UI | Refresh clears UI error only; no bridge operation. |
| Zoom out/in, 24-pixel initial grid, step 4, range 4–72, Fit canvas | 002E inventory §Defaults; original `CityLayoutPanel` | RECOVERED | Local UI gestures in fixture only. |
| Hover/select, box selection, Shift additive, Ctrl toggle, drag individual/multiple buildings preserving relative offsets, Esc clear | 002E inventory §Controls; `CityLayoutPage.jsx:88-222` | RECOVERED | Implemented only over fixture cells; live snapshot missing. |
| Undo/redo (Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y), restore initial layout | 002E inventory; original `CityLayoutPanel` | RECOVERED | Local preview history only. |
| Available/road/flag-only/locked cell states, footprint/movable/placement inspector, pending changes/conflict indicator | 002E inventory §Controls, original `CityLayoutPanel` | RECOVERED | Fixture visualization and local `cityLayoutIssues`; native zone/buffer validation unknown. |
| Validate placements and show `{valid,issues,totalMoves,temporaryMoves}` with immovable/outside/road/flag/locked/overlap, plus zone/missing/buffer game issues | Original 0.3.17 visual strings, earlier 0.3.1 native contract §§5–6 | RECOVERED UI / UNKNOWN 0.3.17 native planning | Local fixture validator; no production `city_layout_validate`. CURRENT_ADAPTATION required for game confirmation. |
| Save draft (500 ms debounce), reload/revision conflict/discard stale draft | 002E inventory; 0.3.1 native contract §7 (corroboration) | RECOVERED workflow | `CityLayoutDraftCommandService` implements selected-profile optimistic persistence; UI currently does not call it. |
| Apply to game, explicit confirmation, execution-plan/temp moves, progress/status polling, cancel remaining moves, terminal refresh; failure stops without automatic rollback | 002E inventory §Validation/application; 0.3.1 native contract §§8–18 (corroboration) | RECOVERED UI / UNKNOWN current executor | No real start/status/cancel, confirmation currently only local fixture state. Requires proven current game move commands. |

Original eight command names (0.3.17 exact frontend bundle): `city_layout_snapshot_get`, `city_layout_validate`, `city_layout_apply_start`, `city_layout_apply_status`, `city_layout_apply_cancel`, `city_layout_draft_get`, `city_layout_draft_save`, `city_layout_draft_clear`. Original calls submit changed placements as `{uuid,targetPointId}`; the historical native 0.3.1 snapshot/status calls had 10,000/5,000 ms deadlines and auth-state-derived arguments, which must **not** be recreated by inventing credentials or bypassing checks. Current supported xLua/game methods, including city snapshot, move ordering and cancellation, remain UNKNOWN pending current-client source research. The snapshot, validator, apply and polling cannot be called working merely because draft persistence exists.

## Hotkeys

| Original shortcut | Original UI behavior and default | Classification | Current behavior at inventory |
|---|---|---|---|
| Q / W / E / R | Attack target with squad keys; attack enabled by default | RECOVERED | Persisted toggle only; keyboard and game dispatch absent. |
| Attack speedup options | Consume speedup items if enabled; separately allow diamond purchase, both **off** by original native default | RECOVERED native 0.3.1 default, 0.3.17 UI labels | Persisted booleans, no execution. |
| A / S / D / F | Recall squad; on by default | RECOVERED | Persisted toggle only. |
| Space | Show shield countdown; on by default | RECOVERED | Persisted toggle only; no game overlay. |
| F6 / F7 / F8 | Use matching shield immediately, consuming an item; on by default (warning in UI) | RECOVERED | Persisted toggle only. |
| Alt + 1–4 | Apply equipment presets; on by default | RECOVERED | Persisted toggle only. |
| F9 | Random relocation, may consume item; **off** by default | RECOVERED | Persisted toggle only. |
| F10 | Alliance relocation, may consume item; **off** by default | RECOVERED | Persisted toggle only. |

All cards have enabled/disabled switches, display fixed bindings and explanatory warnings; settings are per profile and should continue to work when game disconnects (`002F-hotkeys-inventory.md`, original `HotkeyPanel`, original `hotkey_config_get/save`). Original 0.3.1 native defaults additionally make `shieldUse=true` and `frontlineReinforce=true` (`2026-09-25-r8-019-hotkey-config-persistence.md`). `HotkeyConfigCommandService` persists the ten booleans, but `HotkeyPages.jsx:75-105` presently initializes a preview object, merges changes in memory and never invokes native get/save. The keyboard intercept scope (game window active), key-repeat suppression, focus safety, source game methods and game-side validation are UNKNOWN. Current registration/action logic requires CURRENT_ADAPTATION with native proof, not keystroke simulation presented as success.

## Mini Games

| Original control / workflow | Original evidence | Classification | Current behavior at inventory |
|---|---|---|---|
| G Frontline reinforcement toggle | After a Frontline Breakthrough battle begins, add five soldiers each press; original native default **on** | Original `HotkeyPanel` near byte `0x457`, 002G inventory | Local preview switch, no game action. CURRENT_ADAPTATION; event/battle availability UNKNOWN. |
| Black Market Chests: Mark grand prize chest | After shuffle, visually mark prize chest; toggle with `Grand prize` marker string | Original `HotkeyPanel`, 002G inventory | `treasureChestHint` preview-only. Not in original ten-key persisted native Hotkey object; actual persistence/marker transport UNKNOWN. |
| Unlock Land Cell, one click/one next unopened cell | Displays `Unlock request sent for cell #{id}` or failure; no claim of completed unlock from request alone | Original `HotkeyPanel`, 002G inventory; original API `monopoly_cell_open` is related but game-side association requires confirmation | Preview Promise and fixed cell #17 only, no bridge action (`HotkeyPages.jsx:114-128`). CURRENT_ADAPTATION. |
| Food House Auto Clear | Open current in-game Season 4 Food House board; visibly solve/execute **current level only**, without items or entering next level; Start/Stop | 002G inventory, original `HotkeyPanel` | Entirely simulated (`HotkeyPages.jsx:130-142`), no solver/provider. CURRENT_ADAPTATION; seasonal availability UNKNOWN. |
| Food House running/current level, confirmed move progress, elapsed time, solving/executing/completed, daily limit, all complete, event ended, UI open/conflict, manual conflict, solve failure, unsupported client | 002G inventory and 0.3.17 original locale/status strings | RECOVERED UI / UNKNOWN native schema | All result states and timers from `previewSheepStatus`, no live measurements. |

The exact original 0.3.17 mini-games page contains these four feature areas (including the G shortcut). Its original executable/game bridge payload, Black Market marker source and Food House solving logic have not yet been shown to work on the admitted current game. Historical fixture tests, screenshots and existing UI parity do not constitute gameplay proof.

## 2026-10-17 continuation: source-backed current-client implementation inventory

The first inventory above was committed **before** any implementation. Subsequent read-only inspection of the admitted current v26 original Lua package has now identified actual game producers and provides additional evidence beyond the earlier C# RDL inspection. These are implementation-source findings, not current account gameplay observations. The installed package remains controlled by an unresolved **foreign** recovery journal; no new live session was admitted.

| Feature | Source-backed current-game interface identified | Implemented / remaining |
|---|---|---|
| City snapshot, grid, occupancy and move legality | `DataCenter/BuildManager/BuildManager.luac` `GetBuildingDataByUuid/allBuilding/FreeBuildingReplaceNewHandle`; `BuildingDate.GetRangedPoints`; `BuildingUtils.CanMoveBuild/IsInFlagPosition/IsCanPutDownByPoint`; `CityZoneManager.CanPutBuildByPoint`; `LandLockManager.CanBuildByPointId`; `Util/SceneUtils.GetIsInCity`; CLR `CityScene/SceneInterface.IsSelfRoad` | Source-backed bounded snapshot/planning candidate and exact scoped UI/native bridge. Current-game shape and grid access still require a real-session observation. |
| City move apply and server acknowledgement | `UI/UIPlaceBuild/View/UIPlaceBuildView.luac:425–435` sends `SFSNetwork.SendMessage(MsgDefines.FreeBuildingReplaceNew,{buildUuid,pointId})`; `Net/Msgs/FreeBuildingReplaceNewMessage.luac` encodes a long UUID/int target; BuildManager handler applies successful replies | One-at-a-time move sender, subsequent BuildManager-point confirmation, timeout/no retransmit and cancel-before-next implemented offline; not live verified. |
| Hotkeys exact game-window input and owner-scoped recall | `Feature018HotkeyCapture` checks foreground PID/exe/start-time and v26 Unity text-input focus; `MarchUtil.OnBackHome`, `WorldMarchDataManager.GetOwnerFormationMarch/GetMarch`, `ArmyFormationDataManager.GetOneArmyInfoByIndex` | All bindings and text-input gates implemented; native A/S/D/F owner+formation+march-verified return-home sender and later BACK_HOME observation implemented as source-backed candidate. Attack-on-cursor, equip and relocation remain unsupported. |
| Shield timer | `DataCenter/ItemManager/StatusManager.luac` `GetShieldStatusIds`, `GetAllStatusItem`, `GetBuffTimeInfo`, `UITimeManager:GetServerTime()` | Read-only native MAIN_SHIELD timing candidate implemented, exact owner and server clock gated; in-game overlay has no live proof. |
| Shield consumption | `UI/UILWCityShield/Component/CityShieldPage.luac:243–249` sends `MsgDefines.ItemUse` with `{uuid=item.uuid,num=1}` | Correct item duration, ownership, meteorite warning and server ACK still unknown, so no item is consumed. |
| Land unlocking | `LandLockBubbleManager.OnClickBubble:156–164`, `LandLockManager.GetCurrentToBeUnlockedId`, `PushUnlockLandMessage.HandleMessage` | Source-backed one-click guarded `MsgDefines.UnlockUserLand` send, then independently observed unlocked/finished state; server confirmation absent in this session. |
| Food House current level, board and bounded solver | `LWSheepDataManager.GetCurrentLevel/CheckHasGame`, `LWSheepEngine`, `LWUISheepGameView.SeasonSheepOpHandle`, `LWUISheepCardItem.OnClickCard`, `GSheep.Move`, `LWSheepMoveMessage`, `SeasonCitySheepMoveMessage` | `feature018_sheep_solver.lua` is now implemented with source-bytecode-verified complete 3-match plan, exact current-level fence, 7-tray and 2×2 occupancy, one in-flight native UI click and server-ACK gating; bounded offline tests pass. No live packaged current-level success/stop proof; fails closed on unknown/oversized boards. |
| Black Market best chest and visual mark | `UITreasureChestView.GetBigRewardBoxIndex`, item `GetCurPosition`, `ShowSwitchEffect`, `HideSwitchEffect` and `switchState=SwitchEnd` | Source-backed native mark/unmark and owner-bound background preference poll, guarded against pre-shuffle/foreign receipts, with cleanup on view destruction/lease loss; no live visual proof. |
| Frontline G reinforcement | Original hotkey UI establishes +5 soldiers per G. Current v26 `ParkourBattleLogic.AddMember` delegates 4 unspecified parameters to a team and `FrontBreakSundayChallengeResultMessage` sends end-of-battle score, not per-key reinforcement | Only native preference and key intention recognized; +5 reinforcement lacks a source-verified live battle precondition, four parameters and server-confirmed action. It remains disabled rather than faking a local spawn. |

Controlled offline source tests include actual decompiled original v26 Lua bytecode for city move serialization and Mini Games methods, no game actions. The required final WORKING versus NOT_POSSIBLE certification remains subject to authenticated packaged gameplay, presently blocked by the foreign journal. Details in `docs/FEATURE_RECOVERY_018_FOREIGN_JOURNAL_AUDIT.md` and the continuation worker report.

## Current-client recovery gates

For each action: identify the current game class/method and admissible bridge route; capture account name/UID/server **before** live actions; show before/after game state or native result and packaged-application UI result; distinguish action queued from confirmed execution; preserve game Chinese/background-only; exact cleanup and restoration. On a missing feature, retain the original control with an explicit `NOT_POSSIBLE` reason and the evidence showing the dependency, rather than silently removing it or claiming a preview positive. Unverified items in this first inventory are **pending**, never `WORKING`.
