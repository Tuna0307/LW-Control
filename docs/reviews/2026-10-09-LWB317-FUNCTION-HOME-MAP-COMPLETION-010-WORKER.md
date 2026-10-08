# HOME-MAP-COMPLETION-010 worker report (2026-10-09, solo + one-time subagent allowance)

Branch `research/offline-controller`. Start checkpoint `ecd81884`. Status: **PARTIAL — not original A→A
acceptance, not lead acceptance.** Reference SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The owner permitted subagents for this assignment only; they were used for **read-only inventories and static
decodes into their own evidence directories**; every product change below was made and verified by the
coordinating worker. Subagent reports were treated as hypotheses and the load-bearing facts re-checked
(original frontend bytes, EXE strings, clone source).

## B — LEAD009R3-01 corrected (commit aea1f15d)

Defect: `RetireIncompleteAdoption` called the service-wide `StopLeaseTimer` before its ownership test, so a late old
adoption disabled the successor's lease timer (and bumped `leaseGeneration`, silencing in-flight renewals).
Fix: lease-timer owner = (session, challenge); stop/renew only for that exact owner; `StartLeaseTimer` refuses a
non-owner and a late orphan timer retires itself; adoption attempts carry a registration serial
(`LWBridgeControlPipeRegistry.Register` returns it; `Unregister(id, serial)`, `CancelLaunchBinding(id, serial)`), so a
late attempt can remove only its own registration/route. Evidence `evidence/.../COMPLETION-010/b-README.md`:
lead inverse probe (unchanged source) now 2/2 (`adoption-stop-start-fixed.json`; the negative stays immutable);
native `--overview-adoption-check` 26 assertions (6 new held success/fault/cancel × before/after-new-Start cases that
assert timer present **and lease renewal continues**, registry/state intact, no old-owner lease write, 1 launch/1 stop;
plus registration-serial isolation); mutation (unconditional stop restored) fails the new check.
Integrated R2-compatible sweep after B: **38/38 exit 0** (`sweep-b/`).

## D/E — original-contract differences corrected (commit 1029c03d), all with distinguishing checks

Verified against original bytes before editing (not just the inventory's claim):
* Auto cycle (original `index-BVfnK1wp.js` cycle effect, `Di()`): explicit servers scan even when the current
  server id is 0; only an empty resolved list fails (`MAP_AUTO_SCAN_SERVER_UNAVAILABLE`); return-home only if the
  original id is positive. The 45-minute wait throws `MAP_AUTO_SCAN_TIMEOUT` with **no map_scan_stop**; disabling Auto
  only clears the deadline and is observed between targets (no Auto Stop exists in the original; Manual Stop is the
  surface). The clone previously threw on id 0 and stopped the scan on timeout/disable. Five existing clone-only
  tests that asserted those stops were rewritten to the original semantics; new `ExplicitTargetsRunWhenCurrentServerIsUnknown`.
  `--map-auto-scan-campaign-check` passes.
* Host: `server_jump` missing/non-integer/out-of-range → `INVALID_SERVER_ID` "server ID must be an integer from 1 to 99999"
  (EXE string, RE5); `map_treasure_claim` now through `TreasureClaimContract` (missing scope → `INVALID_TREASURE_CLAIM_SCOPE`,
  bad server → original text); City export non-positive server → `MAP_EXPORT_FAILED` "city export server is unavailable"
  (RE3; EXE string); dispatch cancel text "server ID and task UUID are required" (0.3.17 EXE; handler attribution medium
  confidence — the old text was the 0.3.1 string); non-string `selectedTypes` entries ignored (original coercion UNKNOWN);
  scan-status events carry the last home/season/truck-match overlay. `--map317-native-boundary-check` (new
  `Completion010OriginalHostContractsAsync`) and Map-0.3.17 checks pass.

## C/F — static recovery (read-only evidence, not product behaviour)

* `c-finalizer/`: 0x1DDDE3 is the original multi-license **lease-release** future (POST /api/multi/leases/{id}/release);
  no record → Ok no-op; success removes record+files+map entry and emits an event, failure keeps them with a +60 s
  deadline; response `data` never read; all four callers discard the release error; launch failure wrapper 0x1D420D
  (27 call sites) clears state, unregisters, releases, unbinds profile (removes `recovery.json`) and the **launch error
  always wins**; double release on a lease-apply failure; the indirect callback at 0x1DEE65 is `Bytes::drop` (no lease
  semantics). Protected server bodies/timeouts remain UNKNOWN. Clone has no remote lease by design; the clone's
  post-publication failure edge (`!IsReady` → `BRIDGE_START_TIMEOUT`) leaves the owned game/journal/timer in place while
  the original always unregisters/unbinds — recorded as a mapping gap, **not changed** (no original equivalent for the
  restore journal).
* `c-handlers/`, `d-map-decode/`: see their reports (set_automation 0x12C34B, profile select 0x10B70B, root change
  0x188F12, game_recovery_status 0x154905, start in-flight 0x41B3BC, locale keys; Map start validation order,
  server-changed guard, query coercion, City export server derivation).
* Treasure/Ghost (F): nothing new could be enabled — the protected controller/response contract, claim scope/lucky
  ordering, Ghost terminal identity and fuid correlation inputs are still absent; public methods stay
  `GAME_PROVIDER_UNAVAILABLE`; Ghost-before-Dispatch and expiry fixes unchanged. Not a parity success.

## G — live verification: attempts, denials, counts

* Run 1 (2026-10-09 02:15–02:21 +08): task-owned isolated root, packaged Release app `1.0.0+aea1f15d`, **1 real game
  launch** (PID 65196) via the new profile's Auto Launch; canonical Home showed Connected/Game running
  (`live/run1-connected-zh-light.png`, zh-Hans/light). Windows-MCP: ControlStatus/DisplayInventory/Snapshot/Screenshot worked;
  the first Click returned transient CONTROL_PREEMPTED, and the retry was **denied by the Claude Code auto-mode
  classifier**. No workaround was attempted (no CDP, no other input path). The host was then closed gracefully
  (Process.CloseMainWindow); the game was not running afterwards and all three installed script hashes equalled the
  preflight (`live/run1-summary.md`); the intermediate journal removal was not attributed. Task root removed; 0 processes.
* The new bounded Home→City→Resource→query/export→reopen→Stop runner `Completion010LivePilot`
  (`--completion010-live-pilot`) is built and committed but its **launch was denied by the same classifier** and has
  never run. **No EN/light or genuine JA/dark canonical captures, no Home Stop click, no repair/restart adversity and no
  City/Resource pilot results exist from this assignment.** Real launches total: 1.

## Status

Home/Map remains PARTIAL. Exact remaining dependencies are in the obligation table
(`evidence/.../COMPLETION-010/obligations/obligation-table.md`, 47 Home + 86 Map rows) and continuation below.
