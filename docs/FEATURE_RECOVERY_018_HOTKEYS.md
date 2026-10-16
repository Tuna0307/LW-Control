# FEATURE-RECOVERY-018 Hotkeys checkpoint

Date: 2026-10-17. Status: PARTIAL.

## Source-backed recovery

Original 0.3.17 frontend file at research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js includes an on-mount hotkey_config_get read and, for each switch or checkbox, an optimistic UI change followed by fresh hotkey_config_get and a whole-object hotkey_config_save. The restored Hotkeys UI now calls those real commands through the selected profile's native command bridge with rollback and visible errors. It continues saving per-profile settings while the game is disconnected, as the original did. It uses the actual saved booleans instead of a fictitious initial configuration.

Original seven cards and all nine configurable Hotkeys fields: Q/W/E/R attack, attack speedup by item, attack speedup by diamond, A/S/D/F recall, Space shield status, F6/F7/F8 use shield, Alt+1–4 equipment presets, F9 random relocate, F10 alliance relocate. The Mini Games G frontline reinforcement preference shares this config, and the observed original 0.3.17 Mini Games Black Market chest-hint switch has a further treasureChestHint boolean. This was absent in the 0.3.1 ten-key model. The current backend now saves the optional 0.3.17 field with the other ten keys and remains compatible with ten-field older config payloads.

## Verdicts

| Feature | Status | Proof or exact limitation |
|---|---|---|
| Toggle and persist all Hotkeys and G preference booleans, read/reopen saved settings, rollback on failure | WORKING for native config | Feature018HotkeyConfigChecks and desktop full suite passed, including native get/save, read-after-reopen, migrated ten-field config, invalid-save rejection. |
| Black Market hint preference persistence | WORKING for native config | The added treasureChestHint property round-trips under the real native HotkeyConfigCommandService; effect in the game is a separate unimplemented feature. |
| Q/W/E/R attack and optional march speedups | NOT_POSSIBLE as live hotkeys in this checkpoint | There is no current-game keyboard capture/attack-target selection or confirmed march-speedup action consumer for this Hotkeys config. |
| A/S/D/F squad recall | NOT_POSSIBLE | No current-game keyboard capture/selected-squad recall binding; merely persisting the toggle does not dispatch a recall. |
| Space shield countdown | NOT_POSSIBLE | No current-game keyboard capture or game-sourced shield countdown overlay. |
| F6/F7/F8 shield item use | NOT_POSSIBLE | No current-client keyboard mapping to a validated shield item-use/confirmation producer. |
| Alt+1–4 apply equipment presets | NOT_POSSIBLE | Existing equipment presets UI provides preview/config semantics only; no verified current-client command mapping shortcut to applied equipment changes. |
| F9/F10 relocations | NOT_POSSIBLE | Current Hotkey backend does not expose validated relocation items/game commands; enabling a preference does not send them. |
| G frontline reinforcement action | NOT_POSSIBLE | No recognized current in-battle reinforcement producer or G key binding. |

The original Hotkeys preference UI is now native and persists, but there is still no genuine in-game key-action proof. The action-specific NOT_POSSIBLE statuses must remain visible in the final PARTIAL report.

Validation: npm.cmd --prefix src/LWBridge.UI-0.3.17 run build PASS; run check PASS; dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release PASS (0 warnings/errors); dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore PASS, including FEATURE018_HOTKEY_CONFIG_PASS. None of those tests claims live hotkey actuation.
