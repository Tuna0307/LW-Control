# FEATURE-RECOVERY-018 Mini Games checkpoint

Date: 2026-10-17. Status: **PARTIAL**. The original LWBridge 0.3.17 Mini Games panel has four features; the current live-game bridge does not implement three required game-action providers.

## Original-source evidence and adapted behavior

The retained original 0.3.17 `HotkeyPanel-XA8idRHB.js` (at `research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`) establishes four features, the native Hotkey preference read/save calls, and the exact Land Cell and Food House action contract. `docs/reviews/2026-09-29-LWB317-UI-002G-mini-games-inventory.md` on that branch expands the UI status and safe one-cell/one-level semantics.

| Feature | Verdict | Current implementation / exact missing dependency |
|---|---|---|
| G: five frontline reinforcements each press after battle start | **NOT_POSSIBLE** as a live game action | The `frontlineReinforce` preference passes real per-profile native get/save tests, but lacks packaged proof. The current game hook has no proven G keyboard monitor and no active-battle reinforcement producer/acknowledgement. Saving enabled does not send soldiers. |
| Black Market: visually mark grand prize after chest shuffle | **NOT_POSSIBLE** as a live game overlay | Original 0.3.17 `treasureChestHint` switch now passes persisted per-profile native tests, including reopen/migration, but lacks packaged proof. The rebuild has no current chest shuffle prize-tracking source or game-side marker overlay; the switch alone does not mark anything. |
| Unlock next unclaimed Land Cell (exactly one per click) | **NOT_POSSIBLE** in this current production backend | Button now calls the original `monopoly_cell_open` native command and will report request success *only* if a real integer `cellId` is returned. The current `LWBridgeWindow` native command composition and `LWBridgeBackend` do not handle `monopoly_cell_open`. The original request therefore rejects visibly; no phantom #17 from fixture is shown. |
| Food House: open and solve current Season 4 level, stop, stage/progress/time and terminal states | **NOT_POSSIBLE** in this current production backend | Start/Stop now send original 0.3.17 `automation_start/automation_stop` with `taskName: "sheepGame"`. Active status polls genuine `automation_status.tasks.sheepGame` every 2 s and otherwise displays `unsupported_client`. The backend has no start/stop commands, `AutomationStatusCommandService.TaskNames` does not include sheepGame, and the in-game level/solver/confirmed-move executor is absent. The previous `previewSheepStatus` synthetic timing is isolated to preview fixtures. |

The original panel's `daily_limit`, `all_completed`, `activity_ended`, `ui_open`, `manual_or_state_conflict`, `solve_failed` and `unsupported_client` states are preserved in the presentation mapper. None of these states has been invented as current game proof. No paid resources or real-money purchases were invoked.

## Packaged verification

The opt-in genuine packaged WebView proof is gated behind `feature018-tabs` in `tools/map_014_lead_native/run_ui.py`. It reuses the existing isolated client-admission, exact process ownership, authenticated game bridge, no-updater policy, session-bound heartbeat and original Lua-byte restoration. Its `LWBridgeWindow.Feature018Proof.cs` path captures City Layout, Hotkeys and Mini Games native UI and actual backend errors; verifies account UID and server **2212** before any UI action; toggles/restores saved Hotkeys and chest-hint settings; and asserts that unsupported Land Cell / Food House actions cannot be reported as sent/completed. Proof files remain in ignored `artifacts/map014-takeover/native-ui-...`; no private UID is committed to the repository.

Verification outcome is recorded separately in the final worker report after the packaged run. A successful run proves the native preference/UI integration and negative guardrails; it **does not** prove a live Mini Games unlock or Food House solver.

## Continuation: source-backed v26 Mini Games actions and game status

The earlier unhandled native command verdicts are now historical. The new owner-bound game exchange and `tools/feature018_mini_game.lua` register source-backed `monopoly_cell_open`, `mini_land_status`, `mini_sheep_status`, `mini_sheep_board`, `mini_chest_position`, `mini_chest_mark` and `mini_chest_unmark`. Native Land Lock unlock sends the exact one-cell `MsgDefines.UnlockUserLand` from current v26 `LandLockBubbleManager.OnClickBubble` only after confirming Locked state, no pending PVE/unpaid condition, valid current next cell and a source-native eligible game condition. It returns only **request sent** until a later server-driven `LandLockManager` state confirms Unlocked/Finished; the frontend separately polls and displays confirmed completion.

Food House can now report actual current game level and visible playable board data from `LWSheepDataManager`/`LWSheepEngine`, with no fabricated duration or solver. The auto solver and native G +5 Frontline reinforcements still require a source-verified safe game-state executor. The Black Market marker reads the source-native best reward, confirms the shuffle's `SwitchEnd` phase, follows the exact winning chest physical position and uses `UITreasureChestView.ShowSwitchEffect`/`HideSwitchEffect`. The feature maintains ownership of its visible effect and restores only its own in-memory view hooks/effects on game lease loss; the frontend never claims a marker was shown until `switchEffect.activeSelf` is observed.

Seven inert original-v26 Lua bytecode method tests pass, including network serialization and chest show/hide. Current-client UI module visibility and real account receipts **have not been tested** because the foreign-owner journal blocks an authenticated packaged game launch. Therefore each game-changing Mini feature remains **NOT_POSSIBLE for live acceptance in this session**, despite the newly implemented guarded candidate paths.
