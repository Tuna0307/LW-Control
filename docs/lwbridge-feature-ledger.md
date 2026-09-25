# LWBridge 0.3.1 parity feature ledger

**Current through:** `LWB-R8-036`, 2026-09-25.

This ledger now tracks parity with the original program, not whether our reconstruction happens to work.

## Whole-product surfaces

| Surface | Current parity state | Notes |
|---|---|---|
| Reference artifact | EXACT_BYTES authority | Hash reverified 2026-09-24 |
| Retained frontend assets | EXACT_BYTES-derived | Original chunks/styles/icons/locales recovered; transformed boundary and prior retained-scope product deviations still require cleanup |
| Auth/account flows | EXCLUDED — explicit owner directive | Login/Register/authentication/account management/activation/renewal/unbind/logout/entitlement/account-purpose UI/backend are intentionally outside retained scope |
| Overview | EQUIVALENT_REIMPLEMENTATION / PARTIAL profile registry | R8-029 replaces the synthetic `profile_list` object with an evidence-backed controller registry for the normal retained single-profile path; R8-030 restores exact `profile_note_set`; R8-031 restores `profile_reorder`; R8-032 restores the fixed `profile_primary_set` guard and primary uniqueness index; R8-034 restores `profile_select` guards, selected-profile persistence, and best-effort verified game-window focus. Profile enable/create/delete and exact launcher lifecycle still need recovery |
| Automation | UI EXACT_BYTES / PARTIAL backend | R8-026 restores Resource Automation config/status/event; R8-027 restores generic `automation_status`; R8-033 restores exact host-local red-packet/treasure claim-delay validation plus mirrored scheduler/chat persistence. Generic configure/inspect/start/stop, Resource Run Now, Trade Station provider execution, and other live actions remain incomplete/provider-gated |
| Map Data | MIXED / not parity-complete | Large amount recovered; R8-036 restores native `server_jump_history_get/set/import` normalization, legacy migration, and per-profile `map-data.db/app_settings` ownership. Acquisition internals and some product customizations still diverge |
| Squads / AFK | UI EXACT_BYTES / PARTIAL backend | R8-023 restores equipment config persistence; R8-024 restores Monster AFK config validation/persistence/status reload; R8-025 restores Alliance Garrison config validation, `tasks.allianceGarrison` persistence and status reload. Live AFK/garrison execution and equipment apply remain incomplete/protected |
| City Layout | UI EXACT_BYTES / PARTIAL backend | R8-018 restores per-profile `profile_state` draft persistence and `city_layout_draft_get/save/clear`; five gameplay-facing commands remain missing/protected. Original UI chunk and all eight wrappers remain preserved; planner/executor stays fenced |
| Hotkeys | UI EXACT_BYTES / PARTIAL backend | R8-019 restores exact ten-field config/defaults, `hotkey_config_get/save`, native `INVALID_REQUEST` / `STATE_UNAVAILABLE` vocabulary and per-profile runtime config persistence; keyboard/game-action execution remains protected/unimplemented |
| Mini-games | UNKNOWN backend parity | Original UI exists |
| Settings | PARTIAL backend / original UI contract recovered | R8-020 restores visual-metrics persistence; R8-021 restores updater idle status; R8-022 restores feedback result/progress schemas and blank-`exportId` failure; R8-028 restores native `profile_settings_save` revisioned `settings` persistence for the current local profile; R8-035 restores exact `set_window_theme` DWM attributes/colors and semantic errors. Full feedback archive, updater check/download/open, and full multi-profile settings/registry behavior remain incomplete |
| Advanced | EXACT_CONTRACT visibility | Original normal build hard-hides the page |

## Map Data parity

| Feature | Parity state | Required work |
|---|---|---|
| Original scan command envelope | EXACT_CONTRACT | Preserve |
| Original selected-type allowlist | EXACT_CONTRACT | R8-012 restores exactly eight public Manual kinds; preserve |
| Manual `map_scan_start` mode/default/error contract | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-012 restores Manual UI/persistence, Normal=8/Fast=20, null/non-string default-to-Normal and exact invalid-string error/precedence; R8-013 corrects `retryCount:2` to frontend fallback only |
| Normal/Fast concurrency 8/20 | EXACT_CONTRACT | Preserve; protected pacing/retry differences remain separate |
| City | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Resource | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Monster | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Truck | POSSIBLE DEVIATION | Prove direct Train-list mapping against original |
| Railway | POSSIBLE DEVIATION | Prove direct Train-list mapping against original |
| Dispatch | DEVIATION/UNKNOWN | Remove wide-FOV/Quick-Find assumptions; recover original |
| Ghost | EQUIVALENT_REIMPLEMENTATION | Recover original |
| Treasure | PARTIAL EXACT_CONTRACT | Protected orchestration still unknown |
| City Excel export | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-007 restored API/UI/dialog/paging/workbook/result contract |
| `map_scan_clear` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-008 restored positive current-live-server admission, server-scoped deletion, player-mark preservation and Manual-only Clear |
| `server_jump` public result | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-009 restored destination `serverId` alongside `previousServerId` and `changed`; protected travel internals remain separate |
| `map_summary` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-010 restored exact three-field envelope, eight original kinds and shared-state active/published count source selection |
| `map_data_options` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-011 restored recovered top-level order, eight original count kinds, Resource/Monster name families and exact active-run vs server-scoped published selection; rebuild-only `zombie_boss`/`monsterLevels` removed |
| Manual Scan public contract | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-012 restores exactly eight kinds and the recovered Normal/Fast request/default/error/UI contract; Auto Scan and protected acquisition remain separate |
| `map_scan_status` / `map_scan_stop` shared state | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-013 restores direct shared-state status, lifecycle-dependent native fields, read-only world refresh, publishing→idle completion, idempotent Stop and exact Stop/Clear reset separation; uncommon protected Stop error envelopes remain partial |
| Scheduled Plunder control plane/UI | EXACT_BYTES-derived frontend + EXACT_CONTRACT/EQUIVALENT persistence | R8-016 restores five list/schedule/cancel commands, three tables/indexes, two events and original tab/API/locales; protected robbery execution and exact Truck job-ID entropy remain unclaimed |
| Search/filter/sort/paging | PARTIAL EXACT_CONTRACT | R8-014 restores exact eight-kind admission, original filter ownership/query builder and generic keyword predicate. R8-017 gates unrecovered Monster distance, City shield and Railway quality instead of executing R7 guesses; complete native multi-sort assembly remains partial |
| Mark/Jump/Follow | PARTIAL | Tie every behavior to reference |
| Auto Scan frontend scheduler/control plane | EXACT_BYTES-derived + EQUIVALENT public-command wiring | R8-015 restores immutable 0.3.1 config/sanitizer, scheduler state/effect and Auto card byte-for-byte; protected acquisition/travel internals remain separate |

## Original runtime architecture

| Component | Parity state |
|---|---|
| Rust/Tauri host structure | PARTIAL EXACT_CONTRACT |
| profile launcher | PARTIAL EXACT_CONTRACT |
| multi-hook | PARTIAL EXACT_CONTRACT |
| secure/plain xLua proxies | PARTIAL EXACT_CONTRACT |
| bridge pipe framing | PARTIAL EXACT_CONTRACT |
| `bridge-scripts.dat` plaintext | PARTIAL EXACT_CONTRACT / evidence-limited | Substantial package/crypto structure is historically recovered; remaining LWKE1 field-map/AAD work is parked until genuinely new permitted evidence and must not expand into Account/Login/Authentication research |
| protected script handlers | UNKNOWN / retained non-account scope only |

## Completion interpretation

The earlier R7 feature ledger used PASS/LIVE-PROVEN for a reconstructed product scope. Those claims remain valid observations but do not establish one-to-one parity.

For detailed row-by-row status, use `docs/lwbridge-parity-matrix.md`.
