# FEATURE-RECOVERY-018 worker report

**Disposition: PARTIAL — not READY_FOR_LEAD_AUDIT.**

Branch: `codex/feature-recovery-018`; draft PR [#14](https://github.com/Tuna0307/LW-Control/pull/14) to `main`, deliberately unmerged. Target source: original **LWBridge 0.3.17 post-auth**, reference executable SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`. Work item: `docs/work-items/LWB317-FEATURE-RECOVERY-018.md`. Owner's sub-account authorization: `docs/OWNER_DIRECTION_SUB_ACCOUNT_ACTIONS_2026-10-17.md`.

This is a partial code and offline-native delivery, **not** proof that the three game-facing tabs now work. Every original feature requiring genuine current-game action or observation below is classified **NOT_POSSIBLE for acceptance in this delivery**, with the missing provider or explicit verification blocker identified. The native-only passes and historical preview fixtures are separately labeled and must not be promoted to WORKING under the work item's packaged-game proof rule.

## Original inventory and chronological checkpoints

| Stage | Source commit, pushed and verified against origin | Result |
|---|---|---|
| Inventory before implementation | `595d793f2c49648b1336501e8426018c7f146e8a` | `docs/FEATURE_RECOVERY_018_INVENTORY.md`: original 0.3.17 City Layout, Hotkeys and Mini Games control/action inventories; exact frontend-bundle/source locators and game-adaptation uncertainty |
| City Layout | `c1b35098f689d982d948096e41674ae2e62ba475` | Real native command transport for snapshot/draft/validation/apply/status/cancel; non-fabricated error when the current backend has no city provider; `docs/FEATURE_RECOVERY_018_CITY_LAYOUT.md` |
| Hotkeys | `131b5910a9e130d70ceebe8430ee411bc5c1dd3c` | Real profile-native fresh read/whole-object save, saved `treasureChestHint`, old-format migration, native regression test; `docs/FEATURE_RECOVERY_018_HOTKEYS.md` |
| Mini Games | `363581b5590357c1f8212ca5b43238a10711745e` | Original land and Food House command dispatch, real status polling or explicit unsupported state, packaged proof harness with exact session account admission and original-file restoration workflow; `docs/FEATURE_RECOVERY_018_MINI_GAMES.md` |

Only original frontend UI strings/calls are attributed to original **0.3.17**. Historical original **0.3.1** native city planning and hotkey defaults are recorded as corroboration, not evidence that the current game accepts those calls. Direct reference files: `research/offline-controller:evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js` and `HotkeyPanel-XA8idRHB.js`; branch `research/offline-controller` `docs/reviews/2026-09-29-LWB317-UI-002[E-G]-*-inventory.md` and historical City Layout contract reviews.

## Final per-feature behavior difference ledger

Here **NOT_POSSIBLE** means cannot be accepted as genuinely WORKING on this delivered, admitted current-client runtime. It does not mean that a future independently identified legitimate game API could never support the feature. A blocked packaged session is a separately tracked proof limitation. Native and deterministic test passes are not gameplay confirmations.

| Original City Layout feature | Acceptance outcome | Technical evidence / difference |
|---|---|---|
| Authoritative main-city snapshot, building list/levels/icons, regions/cell types | **NOT_POSSIBLE** | `city_layout_snapshot_get` has no current production handler; world Map city tiles do not provide the interior building/layout source. |
| Refresh, actual connection/error/stale state | **NOT_POSSIBLE** live; local error reporting available | Refresh actually retries original command; no game snapshot source, hence only genuine command failure can be shown. |
| Zoom, fit, selecting building, lasso, multi-select, group drag/footprints | **NOT_POSSIBLE** live; fixture UI PASS | UI code and old presentation checks pass, but cannot attach to an authoritative city grid. |
| Undo/redo, restore initial layout and changed-item inspector | **NOT_POSSIBLE** live; fixture UI PASS | Local history is real UI state; no current-game baseline or before/after relocation can be verified. |
| Road/flag/locked/occupancy and region/buffer placement rules | **NOT_POSSIBLE** live; partial local validation | Current-client map interior cell flags/zone/temporary buffer planner are not recovered. |
| Draft load, debounced save, revision conflict, discard | **NOT_POSSIBLE** packaged; native store PASS | `CityLayoutDraftCommandService` handles draft get/save/clear, optimistic revision; live page requires a legitimate snapshot before applying saved building placements. |
| Authoritative validation and planned temporary moves | **NOT_POSSIBLE** | No current `city_layout_validate` handler or server-backed swap/temporary-move planning. |
| Apply confirmation, actual building moves, progress/status, cancel | **NOT_POSSIBLE** | No `city_layout_apply_start/_status/_cancel` handlers or verified game move executor, asynchronous acknowledgments or cancellation semantics. |

| Original Hotkeys feature | Acceptance outcome | Technical evidence / difference |
|---|---|---|
| Save/load seven switch states plus attack speedup item/diamond options and G preference | **NOT_POSSIBLE** packaged; native store PASS | Actual profile config native `hotkey_config_get/save` passes offline load/save/reopen/migration checks; game session proof denied by pre-existing journal. |
| Q/W/E/R attacks and attack march speedup by item/diamonds | **NOT_POSSIBLE** | No game-scoped keyboard hook and validated cursor target→squad attack or speedup item/gem transaction consumer. |
| A/S/D/F recall squads | **NOT_POSSIBLE** | No working game-window hotkey capture to live matching-squad recall action/confirmation. |
| Space shield countdown overlay | **NOT_POSSIBLE** | No live shield countdown producer, overlay and key-down/up handler. |
| F6/F7/F8 8/12/24-hour shield consumption | **NOT_POSSIBLE** | Missing validated inventory shielding game action by duration and game-window key dispatcher. |
| Alt+1–4 apply equipment presets | **NOT_POSSIBLE** | Native `equipment_config_get/save` only persists presets; no verified live preset equip executor and shortcut hook. |
| F9 random and F10 alliance relocation | **NOT_POSSIBLE** | No game-window keyboard hook or client-native relocation item/target command and server confirmation. |

| Original Mini Games feature | Acceptance outcome | Technical evidence / difference |
|---|---|---|
| G key adds 5 soldiers per press during Frontline Breakthrough | **NOT_POSSIBLE** | Persists enabled preference; no battle-state source or validated reinforcement send/confirmation and G dispatcher. |
| Black Market chest Grand Prize marker after shuffle | **NOT_POSSIBLE** live; native preference PASS | `treasureChestHint` persists as optional native field with legacy-10-field compatibility; no shuffle identification or game-rendered marker. |
| Unlock exactly next one unopened Land Cell | **NOT_POSSIBLE** | UI now sends `monopoly_cell_open` and demands a real returned integer `cellId`; no native handler/current-client unlock provider exists. Fixture #17 cannot appear as production success. |
| Food House current-level solver, Start/Stop and confirmed level/progress/terminal states | **NOT_POSSIBLE** | UI sends `automation_start/stop` for `sheepGame` and polls `automation_status.tasks.sheepGame`. Native task registry omits sheepGame, start/stop handlers and current-level solver/confirmed move executor. Actual result correctly reports unsupported. |

## Tests and current packaged proof blocker

Passed before the final source-only documentation edits:

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run build`: **PASS**, production generated UI and its manifest/hashes.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: **PASS**, static, Home, Map mounted owner and UI fixture regression suites.
- `dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release -v quiet`: **PASS**, 0 errors, 0 warnings, including the opt-in packaged proof harness.
- `dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore`: **PASS**, entire desktop suite including `FEATURE018_HOTKEY_CONFIG_PASS` (native default, round-trip, reopen, legacy migration and bad-save rejection), Home and Map regressions.
- `python -m py_compile tools/map_014_lead_native/run_ui.py` and `git diff --check`: **PASS**.
- `dotnet publish ... -o artifacts/feature018/publish`: **PASS**; executable ProductVersion `1.0.0+363581b5590357c1f8212ca5b43238a10711745e` and `check-production-build.mjs`: **PASS**. This intermediate checkpoint executable was built only for the blocked live run; a final-commit-pinned archive is delivered separately.

**Live admission attempt, 2026-10-17 Singapore time:** `python tools/map_014_lead_native/run_ui.py feature018-tabs` terminated **before launching a game or app** at existing, unchanged `%LOCALAPPDATA%\LWBridgeRebuild\overview-bridge\recovery.json` with exact error `RuntimeError: Existing recovery journal requires independent ownership review`. Read-only journal metadata: SHA-256 `7FDFC54C09E1D6B384123C355CB55D7FA39928B8DB0A2D23637A6FAB00146C88`, modified `2026-10-16T17:49:40Z`, schema 1, a different `local-...` profile/session and historical game PID 31476. No `LastWar.exe`, `LastWarLauncher.exe` or `LWBridge.Desktop.exe` was running at the follow-up process check. The journal was not produced by FEATURE-RECOVERY-018. **It was not removed, modified or adopted.** No official updater, live game action, game script mutation, recovery operation or account switch was attempted. Because no authenticated own game session started, there is no FEATURE-RECOVERY-018 UID/name, 2212 session or before/after gameplay receipt to assert.

This preflight decision is binding for this run. Current packaged proof's source path is `src/LWBridge.Desktop/LWBridgeWindow.Feature018Proof.cs`; it checks the actual authenticated game UID and server 2212 before doing any UI action and returns settings to their original values, but **has not been executed** due to the foreign recovery journal. The existing owned game lifecycle is responsible for exact stop and script restoration *if* a future admitted session runs. No statement here asserts restoration of a game session that was never started.

## Final deliverables and lead follow-up

`tools/package_ui_release.ps1 -ReleaseLabel FEATURE-RECOVERY-018` creates one source-identified Windows review ZIP with a specific **PARTIAL** README, original feature limits, no research datasets/credentials and complete published app tree. Its generated `SOURCE-COMMIT.txt` and exe ProductVersion identify the **final** commit; ZIP path and independent SHA-256 are reported at handoff.

To close the current acceptance gaps, an authorized separate process must first investigate/resolve the **foreign** recovery journal and confirm clean ownership; then admit an authenticated, UID-verified server-2212 live session. Implement and game-verify the actual City interior/planner/mover, game-window keyboard action dispatch, Black Market marker, Land Cell unlock and Food House solver against current supported game methods before changing any feature verdict to WORKING. Re-run the isolated packaged UI path with before/after game receipts and exact installed original script restoration. Preserve draft PR status and seek lead audit only after that evidence exists.
