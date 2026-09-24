# LWBridge current backlog

**Current through:** `LWB-R7-153`, 2026-09-24.

This file now tracks only current/actionable work. Historical completed backlog detail is preserved in `docs/reviews/`, subject ledgers, evidence files, and Git history; it is intentionally not repeated here.

## P0 — current external/live gates

- [ ] **Ghost positive-row acceptance (B03/B13/B14/C01):** R7-152 current-v21 recheck completed 2,500/2,500 clean Ghost-only scans on 2212, 2175, 2180, 2185, and 2207; all five had zero authentic Ghost rows. Keep population-gated and rerun the unchanged strict proof only when authentic Ghost rows exist.
- [ ] **Supplies positive-row acceptance (B13/B14/C01):** R7-153 current-v21 strict recheck completed clean 2,500/2,500 Treasure-family scans on 2212, 2175, 2180, 2185, 2207, and 2213; all six had zero authentic `WorldSuppliesPoint` rows. Keep population-gated and rerun the unchanged strict proof only when authentic Supplies population exists.
- [ ] **Simultaneous real multi-account UI population:** deterministic multi-profile UI and real single-session transport are proven separately; integrated several-active-account population remains unavailable.
- [ ] **Final integrated release acceptance:** perform when the external population/action/account gates are ready or explicitly waived/retired by the owner.

## P0 — protected/authorization-gated actions

- [ ] **Treasure protected executor (E01/E02):** `map_treasure_claim` remains intentionally unrouted. Protected scope filtering/order, lucky-slot scheduling, scout-slot reservation, and exact terminal batch enumeration remain `UNKNOWN/BLOCKED` behind preserved SB-79. Do not replay/reroute SB-79 or invent semantics.
- [ ] **Alliance-share live delivery (E06):** payload/validation is offline-tested. Do not send a real alliance message without explicit messaging authorization.

## Completed ordinary Home / Map work

- [x] Home A01-A12 ordinary acceptance is closed at documented current/historical scopes.
- [x] Shared Manual Scan engine and backend strategy selection.
- [x] Player City completeness/effective HP, Resource, Monster/Doom Walker, Zombie Boss, Truck, Railway, Dispatch, ordinary Treasure.
- [x] Exact full-world coverage, transactional publication, saved-server/multi-server persistence.
- [x] Manual Stop, bridge-loss fail-fast, app-restart safe rejection/reconciliation.
- [x] Search/filter/sort/paging, result persistence, Clear race, mark/unmark/relocation, Jump/Follow and moving-target failures.
- [x] Auto Scan ordered targets, failure isolation, return origin, scheduler persistence, reconnect/navigation ownership, restart handling, three consecutive multi-server cycles.
- [x] Normal Release responsiveness and zero-argument normal-user Overview/Map restart navigation.
- [x] R7-151 Last War v21 update-safe restore -> official update -> strict final validation -> injection order.
- [x] R7-151 Dispatch/Secret Task v21 acceleration: exact 68-request full scan plus read-only native Quick Find before Auto full scan.
- [x] City Excel export retired by owner and removed from product scope.
