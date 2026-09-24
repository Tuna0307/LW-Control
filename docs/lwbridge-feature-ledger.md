# LWBridge 0.3.1 parity feature ledger

**Current through:** `LWB-R8-014`, 2026-09-24.

This ledger now tracks parity with the original program, not whether our reconstruction happens to work.

## Whole-product surfaces

| Surface | Current parity state | Notes |
|---|---|---|
| Reference artifact | EXACT_BYTES authority | Hash reverified 2026-09-24 |
| Retained frontend assets | EXACT_BYTES-derived | Original chunks/styles/icons/locales recovered; transformed boundary and prior retained-scope product deviations still require cleanup |
| Auth/account flows | EXCLUDED — explicit owner directive | Login/Register/authentication/account management/activation/renewal/unbind/logout/entitlement/account-purpose UI/backend are intentionally outside retained scope |
| Overview | EQUIVALENT_REIMPLEMENTATION | Works, but original backend semantics still need exact audit |
| Automation | UNKNOWN backend parity | Original UI assets exist; whole feature contract not yet closed |
| Map Data | MIXED / not parity-complete | Large amount recovered, but acquisition internals and product customizations diverged |
| Squads / AFK | UNKNOWN backend parity | Original UI exists |
| City Layout | UI EXACT_BYTES / backend MISSING | Helper recovery preserved at `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`: UI chunk byte-identical; eight original wrappers present; all eight production handlers missing; draft/control-plane contracts recovered; protected planner/executor fenced |
| Hotkeys | PARTIAL | UI/preferences exercised; complete original handler/default audit still required |
| Mini-games | UNKNOWN backend parity | Original UI exists |
| Settings | UNKNOWN backend parity | Original UI exists |
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
| Scheduled Plunder | DEVIATION | Restore original retained feature set |
| Search/filter/sort/paging | PARTIAL EXACT_CONTRACT | R8-014 restores exact eight-kind admission, original filter ownership/query builder and generic keyword predicate; alternate-sort internals remain partial |
| Mark/Jump/Follow | PARTIAL | Tie every behavior to reference |
| Auto Scan | PARTIAL / possible deviation | Recover exact original routing/timing/failure semantics |

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
