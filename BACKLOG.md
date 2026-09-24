# LWBridge strict parity backlog

**Current through:** `LWB-R8-013`, 2026-09-24.

This backlog supersedes the former “finish the reconstructed Home/Map implementation” queue. The target is the retained LWBridge 0.3.1 product scope one-for-one. Account/Login/Authentication and all related account-purpose activation, renewal, unbind, logout, entitlement, credential-persistence and account UI/backend surfaces are intentionally excluded by current owner direction.

## P0 — recover the original implementation

- [ ] **Park the evidence-limited `bridge-scripts.dat`/LWKE1 lane until genuinely new permitted evidence appears.** Earlier R8 work recovered substantial package/crypto structure, but the remaining field-map/AAD blocker must not be guessed, blindly re-searched, or expanded into Account/Login/Authentication recovery. Resume only for retained non-account runtime needs when new evidence exists.
- [ ] **Recover the exact host<->proxy command protocol.** Close `hello.ack`, readiness/heartbeat, request/result grammar, correlation, timeout/disconnect/write failure behavior, and script-dispatch ownership.
- [ ] **Build a complete original command/service inventory.** Enumerate every UI API call, Rust/Tauri command, service, script handler, launcher/proxy path, default, error code and persistent key in LWBridge 0.3.1.
- [ ] **Map each original command to the current Last War client.** Compatibility work may adapt internals but must preserve the recovered original observable contract.

## P0 — remove reconstruction drift

- [ ] **Remove/quarantine every rebuild-only product feature.** R7-151 Secret Task Quick Find is the known example; do not retain any addition not demonstrated in the reference.
- [x] **Restore original City Excel export.** R8-007 restores the reference API/UI, native save dialog, 200-row pagination, exact workbook/result contract and nine locale labels.
- [ ] **Restore every other retained original feature previously retired or customized.** This includes Scheduled Plunder surfaces and other retained reference behavior removed by earlier owner-specific rebuild decisions. Account/Login/Authentication is the explicit exception and must remain out of scope.
- [ ] **Eliminate performance-first Map substitutions that lack reference authority.** The wide-FOV/68-request work and similar current-game optimizations are research evidence only until proven equivalent to original LWBridge behavior.
- [ ] **Re-audit all frontend transforms.** Keep original chunks/assets byte-identical and reduce the generator/API boundary to only invisible compatibility plumbing.

## P0 — exact Map Data recovery

- [x] **Restore original `map_scan_clear` boundary.** R8-008 removes rebuild-only `serverId=0`/saved-server Clear, restores the exact current-live-server gate, server-scoped deletion and Manual-only Clear control.
- [x] **Restore original `server_jump` public success envelope.** R8-009 restores destination `serverId` alongside `previousServerId` and `changed`, while retaining the recovered validation/error contract.
- [x] **Restore original `map_summary` contract.** R8-010 restores the exact `{serverId, counts, scanState}` envelope, eight original count keys and shared-state active/published source selection; saved-server fallbacks are removed from this command.
- [x] **Restore original `map_data_options` contract.** R8-011 restores recovered top-level order, exactly eight count kinds, Resource/Monster name families, matching active-run staging and server-scoped published fallback; removes public `zombie_boss`, `monsterLevels` and the all-server fallback.
- [x] **Restore original Manual Scan public contract.** R8-012 restores exactly eight public kinds, Manual Normal/Fast UI/persistence, Normal=8/Fast=20, null/non-string default-to-Normal behavior and exact invalid-string mode error/precedence. R8-013 corrects `retryCount:2` to frontend-fallback-only status.
- [x] **Restore original `map_scan_status` / `map_scan_stop` shared-state contract.** R8-013 restores direct shared-state status, read-only world refresh, native/start lifecycle fields, publishing→idle completion, idempotent Stop and exact Stop/Clear reset separation without rebuild-only status fields.
- [ ] Restore original `map_search` eight-kind filter ownership; remove rebuild-added Monster/Resource level-filter semantics.
- [ ] Roll Auto Scan back to the exact original frontend scheduler/state machine.
- [ ] Restore original Scheduled Plunder control plane and UI without inventing protected robbery execution internals.
- [ ] Recover original Map Scan script/host algorithm for City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure.
- [ ] Recover remaining protected Normal/Fast internals: mode-specific retries/pacing beyond proven public concurrency 8/20, block/AOI semantics, completeness rules and failure/resume logic. (`retryCount:2` is frontend fallback only, not recovered native status.)
- [ ] Recover original multi-server behavior instead of designing from LW Atlas or our own assumptions.
- [ ] Reconcile original acquisition with current-client Last War APIs/managers without changing product semantics.
- [ ] Re-run identity-level comparisons, not only counts, for every map category.

## P1 — whole-program parity

- [ ] Automation — recover every category, handler, schedule, state transition and error path.
- [ ] Squads / AFK — recover every task/equipment/preset/runtime action.
- [ ] City Layout — helper recovery is complete and preserved at `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`: the original UI chunk is byte-identical, all eight frontend wrappers remain, and all eight production backend handlers are missing. Implement the recovered draft persistence and host control plane later; keep the protected planner/executor fenced.
- [ ] Hotkeys — recover every command, default and persistence rule.
- [ ] Mini-games — recover every visible and conditional function.
- [ ] Settings — recover every retained setting, update, feedback and persistence behavior; do not restore account/authentication-purpose settings or UI.
- [ ] Home / Overview — re-audit working reconstructed lifecycle against the original host/launcher/proxy contract instead of treating current functionality as final.

## P1 — parity validation

- [ ] Expand `docs/lwbridge-parity-matrix.md` until every reference feature/function has a row.
- [ ] Compare reference and rebuild UI states, labels, controls, errors, defaults and state transitions.
- [ ] Compare original and rebuild request/result payloads where observable.
- [ ] Preserve exact recovered asset hashes.
- [ ] Live-prove the final parity implementation against the current Last War client.

## Release exit rule

A feature is not complete merely because the rebuild works. Final release requires no required retained reference feature classified as `DEVIATION` or `UNKNOWN`, and the retained product must function end-to-end.
