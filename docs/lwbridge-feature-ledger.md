# LWBridge 0.3.1 parity feature ledger

**Current through:** `LWB-R8-007`, 2026-09-24.

This ledger now tracks parity with the original program, not whether our reconstruction happens to work.

## Whole-product surfaces

| Surface | Current parity state | Notes |
|---|---|---|
| Reference artifact | EXACT_BYTES authority | Hash reverified 2026-09-24 |
| Post-login frontend assets | EXACT_BYTES-derived | Original chunks/styles/icons/locales recovered; transformed boundary and prior product deviations still require cleanup |
| Auth/account flows | DEVIATION | Intentionally removed in the rebuild; must be restored for one-to-one parity |
| Overview | EQUIVALENT_REIMPLEMENTATION | Works, but original backend semantics still need exact audit |
| Automation | UNKNOWN backend parity | Original UI assets exist; whole feature contract not yet closed |
| Map Data | MIXED / not parity-complete | Large amount recovered, but acquisition internals and product customizations diverged |
| Squads / AFK | UNKNOWN backend parity | Original UI exists |
| City Layout | UNKNOWN backend parity | Original UI exists |
| Hotkeys | PARTIAL | UI/preferences exercised; complete original handler/default audit still required |
| Mini-games | UNKNOWN backend parity | Original UI exists |
| Settings | UNKNOWN backend parity | Original UI exists |
| Advanced | EXACT_CONTRACT visibility | Original normal build hard-hides the page |

## Map Data parity

| Feature | Parity state | Required work |
|---|---|---|
| Original scan command envelope | EXACT_CONTRACT | Preserve |
| Original selected-type allowlist | EXACT_CONTRACT | Preserve |
| Normal/Fast concurrency 8/20 | EXACT_CONTRACT | Recover remaining mode semantics |
| City | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Resource | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Monster | EQUIVALENT_REIMPLEMENTATION | Recover original acquisition algorithm |
| Truck | POSSIBLE DEVIATION | Prove direct Train-list mapping against original |
| Railway | POSSIBLE DEVIATION | Prove direct Train-list mapping against original |
| Dispatch | DEVIATION/UNKNOWN | Remove wide-FOV/Quick-Find assumptions; recover original |
| Ghost | EQUIVALENT_REIMPLEMENTATION | Recover original |
| Treasure | PARTIAL EXACT_CONTRACT | Protected orchestration still unknown |
| City Excel export | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-007 restored API/UI/dialog/paging/workbook/result contract |
| Scheduled Plunder | DEVIATION | Restore original feature set |
| Search/filter/sort/paging | PARTIAL EXACT_CONTRACT | Finish exact branch/default/error audit |
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
| `bridge-scripts.dat` plaintext | PARTIAL EXACT_CONTRACT / P0 ? R8-003 recovers encrypted LWBP2/AES ownership; R8-004 recovers `LWKE1` envelope framing/auth binding; R8-005 recovers exact client login material; R8-006 proves the exact opaque-consumer output pointer used as the 32-byte package key; decoded server agreement material and plaintext still pending |
| protected script handlers | UNKNOWN / P0 |

## Completion interpretation

The earlier R7 feature ledger used PASS/LIVE-PROVEN for a reconstructed product scope. Those claims remain valid observations but do not establish one-to-one parity.

For detailed row-by-row status, use `docs/lwbridge-parity-matrix.md`.
