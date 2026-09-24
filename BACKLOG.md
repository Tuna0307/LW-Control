# LWBridge strict parity backlog

**Current through:** `LWB-R8-005`, 2026-09-24.

This backlog supersedes the former “finish the reconstructed Home/Map implementation” queue. The target is now the whole LWBridge 0.3.1 program, one-for-one.

## P0 — recover the original implementation

- [ ] **Recover `bridge-scripts.dat` plaintext and container contents.** R8-003 closes the encrypted LWBP2 package/AES side; R8-004 closes `LWKE1` envelope framing/auth transport; R8-005 closes exact client ECDH public-key and launch-nonce encoding. Recover the decoded server envelope agreement/encrypted-key fields, derive the 32-byte package key through the already-proven ECDH/TRUNCATE path, then decrypt and hash-preserve the original scripts.
- [ ] **Recover the exact host<->proxy command protocol.** Close `hello.ack`, readiness/heartbeat, request/result grammar, correlation, timeout/disconnect/write failure behavior, and script-dispatch ownership.
- [ ] **Build a complete original command/service inventory.** Enumerate every UI API call, Rust/Tauri command, service, script handler, launcher/proxy path, default, error code and persistent key in LWBridge 0.3.1.
- [ ] **Map each original command to the current Last War client.** Compatibility work may adapt internals but must preserve the recovered original observable contract.

## P0 — remove reconstruction drift

- [ ] **Remove/quarantine every rebuild-only product feature.** R7-151 Secret Task Quick Find is the known example; do not retain any addition not demonstrated in the reference.
- [ ] **Restore every original feature previously retired or customized.** This includes original auth/account presentation, City Excel export, Scheduled Plunder surfaces, and any other reference behavior removed by earlier owner-specific rebuild decisions.
- [ ] **Eliminate performance-first Map substitutions that lack reference authority.** The wide-FOV/68-request work and similar current-game optimizations are research evidence only until proven equivalent to original LWBridge behavior.
- [ ] **Re-audit all frontend transforms.** Keep original chunks/assets byte-identical and reduce the generator/API boundary to only invisible compatibility plumbing.

## P0 — exact Map Data recovery

- [ ] Recover original Map Scan script/host algorithm for City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure.
- [ ] Recover original Normal/Fast mode behavior, retries, pacing, concurrency, block/AOI semantics, completeness rules and failure/resume logic.
- [ ] Recover original multi-server behavior instead of designing from LW Atlas or our own assumptions.
- [ ] Reconcile original acquisition with current-client Last War APIs/managers without changing product semantics.
- [ ] Re-run identity-level comparisons, not only counts, for every map category.

## P1 — whole-program parity

- [ ] Automation — recover every category, handler, schedule, state transition and error path.
- [ ] Squads / AFK — recover every task/equipment/preset/runtime action.
- [ ] City Layout — recover connected editor/data behavior.
- [ ] Hotkeys — recover every command, default and persistence rule.
- [ ] Mini-games — recover every visible and conditional function.
- [ ] Settings — recover every setting, update, feedback, account and persistence behavior.
- [ ] Home / Overview — re-audit working reconstructed lifecycle against the original host/launcher/proxy contract instead of treating current functionality as final.

## P1 — parity validation

- [ ] Expand `docs/lwbridge-parity-matrix.md` until every reference feature/function has a row.
- [ ] Compare reference and rebuild UI states, labels, controls, errors, defaults and state transitions.
- [ ] Compare original and rebuild request/result payloads where observable.
- [ ] Preserve exact recovered asset hashes.
- [ ] Live-prove the final parity implementation against the current Last War client.

## Release exit rule

A feature is not complete merely because the rebuild works. Final release requires no required reference feature classified as `DEVIATION` or `UNKNOWN`, and the resulting program must function end-to-end.
