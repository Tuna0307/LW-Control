# FEATURE-RECOVERY-018 Hotkeys checkpoint

Date: 2026-10-17. Status: PARTIAL.

## Source-backed recovery

Original 0.3.17 frontend file at research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/HotkeyPanel-XA8idRHB.js includes an on-mount hotkey_config_get read and, for each switch or checkbox, an optimistic UI change followed by fresh hotkey_config_get and a whole-object hotkey_config_save. The restored Hotkeys UI now calls those real commands through the selected profile's native command bridge with rollback and visible errors. It continues saving per-profile settings while the game is disconnected, as the original did. It uses the actual saved booleans instead of a fictitious initial configuration.

Original seven cards and all nine configurable Hotkeys fields: Q/W/E/R attack, attack speedup by item, attack speedup by diamond, A/S/D/F recall, Space shield status, F6/F7/F8 use shield, Alt+1–4 equipment presets, F9 random relocate, F10 alliance relocate. The Mini Games G frontline reinforcement preference shares this config, and the observed original 0.3.17 Mini Games Black Market chest-hint switch has a further treasureChestHint boolean. This was absent in the 0.3.1 ten-key model. The current backend now saves the optional 0.3.17 field with the other ten keys and remains compatible with ten-field older config payloads.

## Verdicts

| Feature | Status | Proof or exact limitation |
|---|---|---|
| Toggle and persist all Hotkeys and G preference booleans, read/reopen saved settings, rollback on failure | NOT_POSSIBLE as accepted packaged feature (native config PASS) | Feature018HotkeyConfigChecks and desktop full suite passed native get/save, reopen, migration and malformed-save checks. Genuine packaged proof was denied by pre-existing foreign recovery journal. |
| Black Market hint preference persistence | NOT_POSSIBLE as accepted packaged feature (native config PASS) | The added treasureChestHint property round-trips in native HotkeyConfigCommandService; there is no packaged proof or live chest marking. |
| Q/W/E/R attack and optional march speedups | NOT_POSSIBLE as live hotkeys in this checkpoint | There is no current-game keyboard capture/attack-target selection or confirmed march-speedup action consumer for this Hotkeys config. |
| A/S/D/F squad recall | NOT_POSSIBLE | No current-game keyboard capture/selected-squad recall binding; merely persisting the toggle does not dispatch a recall. |
| Space shield countdown | NOT_POSSIBLE | No current-game keyboard capture or game-sourced shield countdown overlay. |
| F6/F7/F8 shield item use | NOT_POSSIBLE | No current-client keyboard mapping to a validated shield item-use/confirmation producer. |
| Alt+1–4 apply equipment presets | NOT_POSSIBLE | Existing equipment presets UI provides preview/config semantics only; no verified current-client command mapping shortcut to applied equipment changes. |
| F9/F10 relocations | NOT_POSSIBLE | Current Hotkey backend does not expose validated relocation items/game commands; enabling a preference does not send them. |
| G frontline reinforcement action | NOT_POSSIBLE | No recognized current in-battle reinforcement producer or G key binding. |

The original Hotkeys preference UI is now native and persists, but there is still no genuine in-game key-action proof. The action-specific NOT_POSSIBLE statuses must remain visible in the final PARTIAL report.

Validation: npm.cmd --prefix src/LWBridge.UI-0.3.17 run build PASS; run check PASS; dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release PASS (0 warnings/errors); dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore PASS, including FEATURE018_HOTKEY_CONFIG_PASS. None of those tests claims live hotkey actuation.

## Continuation: exact-game foreground capture and native input gate

The earlier native-config-only limitation is superseded **at the keyboard infrastructure level**. `Feature018HotkeyCapture` maps all original shortcuts and checks exact foreground Last War process PID, executable path and process creation time without global hooks, injected keystrokes or intercepting other applications. `Feature018HotkeyActions` checks authenticated selected profile, game UID, server, session and fresh persisted settings. `Feature018HotkeyNativeProviders` calls the native command service; submitted network messages are not accepted as completed gameplay. No unsafe spend opt-in silently falls through.

The v26 game uses Unity UI `InputField`, `TMP_InputField` and `TMP_InputFieldEx`; a Windows HWND focus test cannot tell whether text is being typed into the game. The game-side `feature018_input_focus` read-only handler queries the Unity EventSystem and rejects selected text inputs/unknown state. The native monitor polls it through the same authenticated owner-bound bridge and accepts **only a fresh positive** for at most 300 ms, rechecked by the game handler at action dispatch. The current StatusManager shield timer path is recovered read-only. Other hotkey actions remain unsupported until their current client operation/eligibility/ack schema is verified; in particular the ItemUse shield source signature alone does not establish safe item duration selection, meteorite warning or spend confirmation.

Offline native tests `FEATURE018_HOTKEY_CAPTURE_PASS`, `FEATURE018_EXCHANGE_OWNER_CORRELATION_PASS` and `FEATURE018_HOTKEY_GAME_LUA53_OFFLINE_PASS` cover mapping, text-focus fallbacks, owner changes, opt-in spend fences and game manager observation. Genuine current-game text-focus availability and gameplay action confirmation remain blocked by the foreign recovery journal, so **NOT_POSSIBLE for packaged WORKING acceptance** at this checkpoint.

### 2026-10-17 follow-up source-backed squad recall and chest setting effect

`tools/feature018_hotkey_actions.lua` now wraps the original native Hotkeys action handler for current-client recall. Source evidence: `MarchUtil.OnBackHome`, `ArmyFormationDataManager.GetOneArmyInfoByIndex`, `WorldMarchDataManager.GetOwnerFormationMarch/GetMarch`, `WorldMarchChangeMessage.HandleMessage`. The managed `GetOwnerMarches` method ignores two owner filtering arguments; accordingly the adapted handler **independently** compares the live player UID, selected formation UUID and fresh march UUID/owner before submitting a single return-home request. A request is only **Submitted** until a later genuine native `BACK_HOME` target is reobserved; pending identity changes prevent retries. Normal game confirmation setting `slide_left_return` must be enabled before calling the source-native return method; no confirmation UI is bypassed. Offline `FEATURE018_HOTKEY_ACTIONS_LUA53_OFFLINE_PASS` is source backed, not a live march.

The saved `treasureChestHint` preference now also feeds a scoped, 1500-ms background native monitor. It reads the actual current Black Market winning-chest position only after `SwitchEnd`, invokes `mini_chest_mark` once per settled shuffle, verifies the source-native effect appeared, and unmarks when disabled or when the winning position changes. Lease loss leaves cleanup to the game module's exact in-memory hook restoration and does not touch other sessions. `FEATURE018_CHEST_MARKER_MONITOR_OFFLINE_PASS` checks successful/malformed/pre-shuffle/foreign receipts, but there is no actual game visual screenshot. The game-window key capture still requires actual Last War foreground and game-side clear text focus; background-only automated control cannot steal focus to fake a key press.
