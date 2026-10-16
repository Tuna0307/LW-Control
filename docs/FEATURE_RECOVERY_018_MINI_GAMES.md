# FEATURE-RECOVERY-018 Mini Games checkpoint

Date: 2026-10-17. Status: **PARTIAL**. The original LWBridge 0.3.17 Mini Games panel has four features; the current live-game bridge does not implement three required game-action providers.

## Original-source evidence and adapted behavior

The retained original 0.3.17 `HotkeyPanel-XA8idRHB.js` (at `research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/`) establishes four features, the native Hotkey preference read/save calls, and the exact Land Cell and Food House action contract. `docs/reviews/2026-09-29-LWB317-UI-002G-mini-games-inventory.md` on that branch expands the UI status and safe one-cell/one-level semantics.

| Feature | Verdict | Current implementation / exact missing dependency |
|---|---|---|
| G: five frontline reinforcements each press after battle start | **NOT_POSSIBLE** as a live game action | The `frontlineReinforce` preference is **WORKING** with real per-profile native get/save. The current game hook has no proven G keyboard monitor and no active-battle reinforcement producer/acknowledgement. Saving enabled does not send soldiers. |
| Black Market: visually mark grand prize after chest shuffle | **NOT_POSSIBLE** as a live game overlay | Original 0.3.17 `treasureChestHint` switch is now **WORKING** as a persisted per-profile native boolean, including reopening/migration. The rebuild has no current live chess/chest shuffle prize-tracking source or game-side marker overlay; the switch alone does not mark anything. |
| Unlock next unclaimed Land Cell (exactly one per click) | **NOT_POSSIBLE** in this current production backend | Button now calls the original `monopoly_cell_open` native command and will report request success *only* if a real integer `cellId` is returned. The current `LWBridgeWindow` native command composition and `LWBridgeBackend` do not handle `monopoly_cell_open`. The original request therefore rejects visibly; no phantom #17 from fixture is shown. |
| Food House: open and solve current Season 4 level, stop, stage/progress/time and terminal states | **NOT_POSSIBLE** in this current production backend | Start/Stop now send original 0.3.17 `automation_start/automation_stop` with `taskName: "sheepGame"`. Active status polls genuine `automation_status.tasks.sheepGame` every 2 s and otherwise displays `unsupported_client`. The backend has no start/stop commands, `AutomationStatusCommandService.TaskNames` does not include sheepGame, and the in-game level/solver/confirmed-move executor is absent. The previous `previewSheepStatus` synthetic timing is isolated to preview fixtures. |

The original panel's `daily_limit`, `all_completed`, `activity_ended`, `ui_open`, `manual_or_state_conflict`, `solve_failed` and `unsupported_client` states are preserved in the presentation mapper. None of these states has been invented as current game proof. No paid resources or real-money purchases were invoked.

## Packaged verification

The opt-in genuine packaged WebView proof is gated behind `feature018-tabs` in `tools/map_014_lead_native/run_ui.py`. It reuses the existing isolated client-admission, exact process ownership, authenticated game bridge, no-updater policy, session-bound heartbeat and original Lua-byte restoration. Its `LWBridgeWindow.Feature018Proof.cs` path captures City Layout, Hotkeys and Mini Games native UI and actual backend errors; verifies account UID and server **2212** before any UI action; toggles/restores saved Hotkeys and chest-hint settings; and asserts that unsupported Land Cell / Food House actions cannot be reported as sent/completed. Proof files remain in ignored `artifacts/map014-takeover/native-ui-...`; no private UID is committed to the repository.

Verification outcome is recorded separately in the final worker report after the packaged run. A successful run proves the native preference/UI integration and negative guardrails; it **does not** prove a live Mini Games unlock or Food House solver.
