# Project manager checkpoint — review 3

Reviewed: 2026-09-08 at implementation commit `04237c2` on `research/offline-controller`; the worktree was clean before this review. This is the current audit and next-work guide. The [review 2 narrative and subsequent recovery notes](reviews/2026-09-08-review-2-and-followups.md) are preserved as historical evidence, not current instructions.

## Decision

**Accept the PM2 fixes, substantial native-host work, additional R5 recovery and the limited R6 persisted-search implementation. Both requested pages still need real-game completion.** PM2-01–04 now pass independently. Do not spend another batch fixing those old defects or repeating the recovered launch-envelope/ABI investigations.

Two preference requirements remain open: PM3-01 visible save errors and PM3-02 rollback after multiple failed saves. Address these narrowly while continuing the main R5 launch-contract and R6 map-query/export tracks. Full R4 acceptance must not be inferred from a probe whose overall `ok:true` omits a required visible-error check.

The user's standing tool permission is now explicit in [AGENTS.md](../AGENTS.md), section 3. Install/configure appropriate reverse-engineering tools when needed; missing Ghidra or MCP integration is a setup task, not a final research blocker. Attribute an actual denial to the rejecting environment and its stated reason. Never describe it as permission the user withheld.

## What changed since the previous management review

| Commit / work | Accepted result | Remaining boundary |
|---|---|---|
| `6b7efca` PM2 fixes | Backend partial saves preserve fresh fields; missing-primary backup recovery preserves identity; incompatible primaries are not overwritten; late request disposal is fixed | All four independent reproducer cases now pass. This closes the specified PM2 defects, not all possible persistence/lifetime cases. |
| `e48fc73` native document lifetime | Backend/storage work runs off the UI thread; real WebView reload rotates session/request/subscription ownership; stale session/publication rejected | Current config/storage/diagnostic paths are tested. Real lifecycle/scan workers and durable runtime diagnostics do not exist yet. |
| `50b824d` native interactions | Actual React single-save rollback, ordered successful rapid saves, duplicate IDs, picker busy/cancel/invalid, navigation rejection, late close suppression and startup suppression | Error visibility is false; rapid *failed* saves were not covered and expose PM3-02. Most host scenarios pass, but the blanket R4-complete wording is too broad. |
| `6fdc983` / `LWB-R5-001` | Outer-host `LaunchEnvelope` producer, field value sources, JSON serialization and handoff recovered with exact locators | `descriptorJson`, `launchProof`, `gameLaunchTicket` names/handoff are known; complete representation, producer semantics, validation and child input decoding remain open. |
| `6978d58` / `LWB-R5-002` | Exact export-table fingerprint algorithm and secure/plain selector recovered; current installed xLua again matches secure in this audit | Static/current-file compatibility correlation, not proof of successful injection, owned process, handshake or heartbeat. |
| `04237c2` / `LWB-R6-003` | Persisted `map_search` returns rows/total with LIMIT/OFFSET, `updatedAt` order, stable `record_key` tie-break and city marks/markedOnly | Other filters/sorts reject `MAP_QUERY_UNRECOVERED`; options/summary/export and authoritative ingestion remain unfinished. A small offline query slice is not the full Map Data page. |

## Current functional boundary

- **Overview:** installation detection, preference storage, native RPC and substantial UI/host behavior exist. `profile_instance_start` still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; stop has no owned instance. Reconcile is a status read; reconnect is a stored preference without a real recovery worker. Bridge status/pending/recovery are not authoritative live services.
- **Map Data:** schema, explicit-key upsert/read/count, persisted marks, transactional scoped clear and the default search slice exist. Clear preserves marks and other servers. Options and city export remain unavailable, summary is still unavailable/zero, and no production scan/capture/scheduler runs. Unknown native keys and normalization must be recovered before claiming a complete typed index.
- **Shared controls:** no verified real server travel, runtime localization, meaningful pending count or full live event/diagnostic stream. Existing themes/languages/navigation remain visual/host work, not proof of connected game features.

## PM3-01 — Save error is hidden in the single-profile UI

**Status:** IMPLEMENTED/OFFLINE-TESTED observation; open R2/R4 requirement, revalidated from the earlier documented `R4-PREF-02` limitation. This is rebuild UI behavior, not a claim about the original executable.

**Sources/locators:** `LWBridgeWindow.RunHostProbeAsync`, preference phase and final `ok` conjunction; `WebUi/local-providers.js`, `asProfileError` and failed-save catch; the recovered single-profile layout. Exact reviewed file hashes and raw selected probe results are in [review 3 evidence](../evidence/lwbridge-implementation/2026-09-08-pm-review-3.json).

**Reproduction/result:** Run the current executable with `--host-probe <isolated-output.json>`. The real React switch rolls back after a rejected save, but `preferenceRollbackErrorVisible=false` and `preferenceRollbackErrorText` is empty. The probe still returns `ok:true` because its final conjunction checks switch rollback, not error visibility. Historical R4 evidence already acknowledges the hidden profile-error pane.

**Next action/exit:** Surface the failure in the normal login-free single-profile flow using the existing visual language, without restoring login. Assert a visible useful error, correct switch value and retained persisted state in the real host test; make error visibility part of the pass gate. Preserve normal-state visual fidelity. Do not label R4 complete while this required feedback is absent.

## PM3-02 — Two failed rapid saves restore an uncommitted switch value

**Status:** IMPLEMENTED/OFFLINE-TESTED defect reproduced by executing the current provider in an isolated Node VM with deterministic React-hook storage. Native WebView reproduction remains the next validation step.

**Source/locator:** `WebUi/local-providers.js`, `Xt.setAutoLaunchGame`, `previous`, `autoLaunchSaveRevision`, the serialized save chain and failure rollback. The revision check protects newer drafts from older failures, but the last failure restores the value captured from an optimistic render rather than the last successfully committed value.

**Reproduction:** Start with persisted/UI false; toggle true and then false before either save finishes; reject both serialized saves. Run from repository root:

```powershell
node evidence/lwbridge-implementation/pm-review-3-repro/check-overlap-failures.cjs
```

**Observed:** submitted values `[true,false]`; committed value remains false; final provider/UI value becomes true; `passed:false`. The reproducer reports JSON and intentionally exits zero when the report is produced; inspect `passed`, not just the process exit code. It uses the real provider source, isolated hooks/promises and no user config/game operations.

**Next action/exit:** Track the last confirmed persisted value separately from the latest optimistic draft. Reconcile the latest relevant failure to actual committed state without allowing stale completions to override newer intentions. Test both-fail, first-fail/second-success, first-success/second-fail and successful rapid sequences in the standard suite and actual React/WebView probe. Error state must correspond to the relevant save and clear on successful recovery. Preserve ordered writes and the original PM2 fixes.

## Next AI work in order

1. **Close PM3-01/02:** reproduce the current observations, implement the narrow provider/error presentation fixes and add real pass gates. Do not reopen resolved PM2 defects.
2. **R5 recovery/implementation critical path:** start from `LWB-R5-001/002` and existing verified inspectors. Trace the remaining descriptor/proof/ticket value producers and validation, child-launcher input decoding and argument/environment transport. If deeper disassembly/decompilation is needed, discover/install a suitable tool and document its invocation. Do not stop at “no Ghidra integration.” Once contracts are supported, implement owned start/status/stop, exact ABI choice, fresh bridge identity/heartbeat, startup/reconnect/repair and failure cleanup. Verify current artifacts before using recovered offsets/values.
3. **R6 parallel deliverable:** recover/implement remaining predicates and sort expressions, `map_data_options`, actual counts/summary and full filtered Excel export. Start from the implemented default query, not an empty store. Test real frontend query envelopes across all eight categories; an omitted value, false, zero and empty string must not be treated as equivalent without evidence. Maintain truthful unsupported errors.
4. **R6 integrity:** recover native per-kind keys/types/update/removal semantics; implement versioned schema/migration, consistent query snapshots and staging-to-committed publication. A process-local store lock does not make separate count/page SQL reads a cross-connection snapshot. Add scoped clear generation protection and reject late run/server/profile results. Do not invent successful scan publication from nonzero row counts.
5. **R7–R9:** after prerequisites, connect bounded then full scans, durable automatic server cycles and all conditional actions/jobs. Require authoritative current-client outcomes and record specific world/target availability limits. Preserve the 47 full acceptance cases.
6. **Regression coverage:** add the current standalone native host matrix, the new preference expectations and all three new recovery inspectors to appropriate automated checks. The current CI only compiles older inspectors and does not run `--host-probe`; a manual report is not recurring CI coverage. Build-specific/current-install checks must have explicit prerequisites and remain separate from game-independent CI.
7. **Checkpoint delivery:** document new findings immediately, update `BACKLOG.md`/feature ledger, run applicable checks and commit/push the coherent checkpoint. Verify the remote commit; do not leave status documents beginning with superseded blockers.

## Verification performed in review 3

Current results, source hashes, limitations and reproduction commands are in [the review 3 record](../evidence/lwbridge-implementation/2026-09-08-pm-review-3.json).

- Recovered frontend hash/generation check and Release build passed, zero warnings/errors.
- Standard deterministic backend suite passed, including the optional read-only installed-client diagnostic; real configuration comparison passed and no game/launcher was running in that snapshot.
- The independent PM2 reproducer now reports all four `passed:true` outcomes.
- Node missing-native and transport-boundary harnesses passed.
- The isolated native WebView probe passed its current gate: actual reload/session/ownership, cancellation, duplicate/error, picker/rollback/ordered save and close paths ran. It also reported the PM3-01 visible-error failure, which its current gate omits. Real user config bytes were unchanged; no live game commands ran.
- The three new static inspectors reran successfully against the verified reference/recovered frontend and installed xLua. This confirms reproducibility of the saved contracts and current-file selector result; it does not complete R5 launch semantics.
- The additional PM3-02 provider failure test failed as described above. Treat it as a remaining correctness gap even though the standard suite passes.
- Fresh fixture/browser comparison results are recorded in the evidence record. Populated production map states and successful game lifecycle remain unverified.

## Tools, authorization and genuine limits

This review used existing .NET, Node, Python, WebView2 and the existing PE/disassembly inspectors; it did not need a new analyzer installation. `Get-Command` resolved Python/Node/.NET. Java/Ghidra/headless/LLVM/7z names were not found on the inspected PATH, which does **not** establish that those tools are absent elsewhere. The next AI must discover/install what its specific investigation needs, rather than relying on this limited inventory.

The user's permission is not a missing prerequisite for project-related tool setup. Distinguish setup errors, unrecovered contracts, unavailable live targets and actual platform restrictions in every blocker report. Record exact errors and pursue permitted alternatives/independent work. Do not infer that broad user consent changes an external platform's restrictions or claim a denied action succeeded.

No game launch, injection, travel, scan, claim or message delivery was performed in this management review. Completed cleanup and the login-free recovered UI are preserved. Hand off [AGENTS.md](../AGENTS.md), [task.md](../task.md) and [BACKLOG.md](../BACKLOG.md); they retain one clear rule/requirement/checklist structure.
