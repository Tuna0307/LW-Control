# Project manager checkpoint — review 3 + PM3 follow-up

Reviewed: 2026-09-08 at implementation commit `04237c2` on `research/offline-controller`; the worktree was clean before this review. This is the current audit and next-work guide. The [review 2 narrative and subsequent recovery notes](reviews/2026-09-08-review-2-and-followups.md) are preserved as historical evidence, not current instructions.

## Decision

**Accept the PM2 fixes, substantial native-host work, additional R5 recovery and the limited R6 persisted-search implementation. Both requested pages still need real-game completion.** PM2-01–04 now pass independently. Do not spend another batch fixing those old defects or repeating the recovered launch-envelope/ABI investigations.

PM3-01 and PM3-02 are now closed by the 2026-09-08 preference checkpoint. Rejected saves are visible in the login-free single-profile flow, failed rapid saves reconcile to the last confirmed persisted value, and the real native-host `ok` gate includes those requirements. Continue the main R5 launch-contract and R6 map-query/export tracks; this does not make the Overview lifecycle live-proven.

The user's standing tool permission is now explicit in [AGENTS.md](../AGENTS.md), section 3. Install/configure appropriate reverse-engineering tools when needed; missing Ghidra or MCP integration is a setup task, not a final research blocker. Attribute an actual denial to the rejecting environment and its stated reason. Never describe it as permission the user withheld.

## What changed since the previous management review

| Commit / work | Accepted result | Remaining boundary |
|---|---|---|
| `6b7efca` PM2 fixes | Backend partial saves preserve fresh fields; missing-primary backup recovery preserves identity; incompatible primaries are not overwritten; late request disposal is fixed | All four independent reproducer cases now pass. This closes the specified PM2 defects, not all possible persistence/lifetime cases. |
| `e48fc73` native document lifetime | Backend/storage work runs off the UI thread; real WebView reload rotates session/request/subscription ownership; stale session/publication rejected | Current config/storage/diagnostic paths are tested. Real lifecycle/scan workers and durable runtime diagnostics do not exist yet. |
| `50b824d` native interactions + PM3 follow-up | Actual React single-save rollback, ordered successful rapid saves, duplicate IDs, picker busy/cancel/invalid, navigation rejection, late close suppression and startup suppression | PM3 follow-up adds visible save-error feedback, confirmed-value rollback, both-fail/mixed rapid-save cases and successful recovery in the same real WebView host. Lifecycle-worker integration remains open. |
| `6fdc983` / `LWB-R5-001` | Outer-host `LaunchEnvelope` producer, field value sources, JSON serialization and handoff recovered with exact locators | `descriptorJson`, `launchProof`, `gameLaunchTicket` names/handoff are known; complete representation, producer semantics, validation and child input decoding remain open. |
| `6978d58` / `LWB-R5-002` | Exact export-table fingerprint algorithm and secure/plain selector recovered; current installed xLua again matches secure in this audit | Static/current-file compatibility correlation, not proof of successful injection, owned process, handshake or heartbeat. |
| `LWB-R5-003` | Child `LaunchEnvelope` parser, descriptor expiry/shape gates, two-segment proof signature/timing validation and `LWLT1`/`LWLT2` ticket grammar recovered | Static-only. Host proof/ticket producers, remaining descriptor semantics, ticket ownership/consumption and exact child argument construction are still open; lifecycle remains fail-closed. |
| `LWB-R5-004` | Outer `LeaseActivationResponse.launchProof` propagation, exact `primary_official` / `cached_reusable` / `independent_official` ticket-source labels, `ticket_missing` transition and cached launcher-report ticket/expiry fields recovered | Static-only. Exact response/ticket producers and signing inputs, `LWLT2` extra-field meaning, ownership/consumption, helper `0x14033C51B` semantics and child argument construction remain open; lifecycle remains fail-closed. |
| `04237c2` / `LWB-R6-003` | Persisted `map_search` returns rows/total with LIMIT/OFFSET, `updatedAt` order, stable `record_key` tie-break and city marks/markedOnly | Other filters/sorts reject `MAP_QUERY_UNRECOVERED`; options/summary/export and authoritative ingestion remain unfinished. A small offline query slice is not the full Map Data page. |

## Current functional boundary

- **Overview:** installation detection, preference storage, native RPC and substantial UI/host behavior exist. `profile_instance_start` still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; stop has no owned instance. Reconcile is a status read; reconnect is a stored preference without a real recovery worker. Bridge status/pending/recovery are not authoritative live services.
- **Map Data:** schema, explicit-key upsert/read/count, persisted marks, transactional scoped clear and the default search slice exist. Clear preserves marks and other servers. Options and city export remain unavailable, summary is still unavailable/zero, and no production scan/capture/scheduler runs. Unknown native keys and normalization must be recovered before claiming a complete typed index.
- **Shared controls:** no verified real server travel, runtime localization, meaningful pending count or full live event/diagnostic stream. Existing themes/languages/navigation remain visual/host work, not proof of connected game features.

## PM3-01 — Save error visibility in the single-profile UI

**Status:** IMPLEMENTED/OFFLINE-TESTED; closed R2/R4 requirement. This is rebuild UI behavior, not a claim about the original executable.

**Sources/locators:** `WebUi/local-providers.js`, `Xt`, `asProfileError`, `profile-save-error`; `LWBridgeWindow.RunHostProbeAsync`, rollback phase and final `ok` conjunction. Exact source hashes, commands and host results are in [PM3 fix evidence](../evidence/lwbridge-implementation/2026-09-08-pm3-preference-fix.json).

**Reproduction/result:** Build and run the executable with `--host-probe <isolated-output.json>`. The real React switch rolls back to persisted state and now reports `preferenceRollbackErrorVisible=true` with `Expected isolated preference-save failure.`. The host aggregate gate explicitly requires that visible error and reports `ok:true`. The alert is absent during normal operation, preserving the steady recovered layout.

**Validation/limits:** Provider/host tests use isolated configuration and perform no live-game commands. This closes preference feedback only; startup launch/reconnect still require R5 lifecycle services.

## PM3-02 — Rapid failed-save reconciliation

**Status:** IMPLEMENTED/OFFLINE-TESTED; closed R2/R4 correctness requirement.

**Source/locator:** `WebUi/local-providers.js`, `Xt.setAutoLaunchGame`, `autoLaunchCommitted`, `autoLaunchSaveRevision` and the serialized save chain; `tools/check_lwbridge_preference_provider.cjs`; `LWBridgeWindow.RunHostProbeAsync` preference failure/recovery phases.

**Reproduction:** Start with persisted/UI false; toggle true and then false before either save finishes; reject both serialized saves. Run from repository root:

```powershell
node evidence/lwbridge-implementation/pm-review-3-repro/check-overlap-failures.cjs
```

**Observed after fix:** the historical reproducer now reports submitted values `[true,false]`, committed false, final UI false and `passed:true`. The maintained provider matrix passes both-fail, first-fail/second-success, first-success/second-fail, both-success and recovery. The real WebView probe independently passes both-fail/mixed cases, restores the confirmed first-success value when the second save fails, and clears the error after a later successful save. Native saves remain serialized with observed maximum concurrency `1`.

**Validation/limits:** Exact results are in [PM3 fix evidence](../evidence/lwbridge-implementation/2026-09-08-pm3-preference-fix.json). This is isolated rebuild validation; it does not prove game launch, bridge readiness or reconnect behavior.

## Next AI work in order

1. **R5 recovery/implementation critical path:** start from `LWB-R5-001/002/003/004` and the existing inspectors/evidence. `LWB-R5-004` now establishes outer `LeaseActivationResponse.launchProof` propagation, the three ticket-source labels, `ticket_missing` transition and cached launcher-report ticket/expiry gate. Continue into the exact response/ticket producers and signing inputs, remaining descriptor semantics, `LWLT2` extra-field meaning, ticket ownership/consumption state machine, helper `0x14033C51B` semantics and exact child argument construction/quoting. Do not infer those helper semantics from names or control flow alone. Once those contracts are supported, implement owned start/status/stop, exact ABI choice, fresh bridge identity/heartbeat, startup/reconnect/repair and failure cleanup. Verify current artifacts before using recovered offsets/values.
2. **R6 parallel deliverable:** recover/implement remaining predicates and sort expressions, `map_data_options`, actual counts/summary and full filtered Excel export. Start from the implemented default query, not an empty store. Test real frontend query envelopes across all eight categories; an omitted value, false, zero and empty string must not be treated as equivalent without evidence. Maintain truthful unsupported errors.
3. **R6 integrity:** recover native per-kind keys/types/update/removal semantics; implement versioned schema/migration, consistent query snapshots and staging-to-committed publication. A process-local store lock does not make separate count/page SQL reads a cross-connection snapshot. Add scoped clear generation protection and reject late run/server/profile results. Do not invent successful scan publication from nonzero row counts.
4. **R7–R9:** after prerequisites, connect bounded then full scans, durable automatic server cycles and all conditional actions/jobs. Require authoritative current-client outcomes and record specific world/target availability limits. Preserve the 47 full acceptance cases.
5. **Regression coverage:** the deterministic preference matrix and isolated native host matrix now run in Windows CI. Current-build/reference recovery inspectors remain compile-checked there because their runtime inputs are not present in game-independent CI; add artifact-backed CI execution only when those prerequisites are explicitly supplied.
6. **Checkpoint delivery:** document new findings immediately, update `BACKLOG.md`/feature ledger, run applicable checks and commit/push the coherent checkpoint. Verify the remote commit; do not leave status documents beginning with superseded blockers.

## Verification performed in review 3

Current results, source hashes, limitations and reproduction commands are in [the review 3 record](../evidence/lwbridge-implementation/2026-09-08-pm-review-3.json).

- Recovered frontend hash/generation check and Release build passed, zero warnings/errors.
- Standard deterministic backend suite passed, including the optional read-only installed-client diagnostic; real configuration comparison passed and no game/launcher was running in that snapshot.
- The independent PM2 reproducer now reports all four `passed:true` outcomes.
- Node missing-native and transport-boundary harnesses passed.
- The isolated native WebView probe passes its strengthened gate: actual reload/session/ownership, cancellation, duplicate/error, picker/rollback/ordered save, visible save errors, both-fail/mixed rapid-save reconciliation, successful recovery and close paths ran. Real user config bytes were unchanged; no live game commands ran.
- The three new static inspectors reran successfully against the verified reference/recovered frontend and installed xLua. This confirms reproducibility of the saved contracts and current-file selector result; it does not complete R5 launch semantics.
- The maintained PM3 provider matrix passes all four rapid-save outcome combinations plus recovery; the earlier audit reproducer now returns `passed:true` against the fixed provider.
- Fresh fixture/browser comparison results are recorded in the evidence record. Populated production map states and successful game lifecycle remain unverified.

## Tools, authorization and genuine limits

This review used existing .NET, Node, Python, WebView2 and the existing PE/disassembly inspectors; it did not need a new analyzer installation. `Get-Command` resolved Python/Node/.NET. Java/Ghidra/headless/LLVM/7z names were not found on the inspected PATH, which does **not** establish that those tools are absent elsewhere. The next AI must discover/install what its specific investigation needs, rather than relying on this limited inventory.

The user's permission is not a missing prerequisite for project-related tool setup. Distinguish setup errors, unrecovered contracts, unavailable live targets and actual platform restrictions in every blocker report. Record exact errors and pursue permitted alternatives/independent work. Do not infer that broad user consent changes an external platform's restrictions or claim a denied action succeeded.

No game launch, injection, travel, scan, claim or message delivery was performed in this management review. Completed cleanup and the login-free recovered UI are preserved. Hand off [AGENTS.md](../AGENTS.md), [task.md](../task.md) and [BACKLOG.md](../BACKLOG.md); they retain one clear rule/requirement/checklist structure.
