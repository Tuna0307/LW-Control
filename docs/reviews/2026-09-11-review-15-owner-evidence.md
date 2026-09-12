# PM review 15 — owner evidence recorder

Date: 2026-09-11. Implementation under review: `154ce35`, following `504800c` and `2d3915d`, on `research/offline-controller`. Worktree clean at entry. This is a source/evidence audit, not a new game run or independently executed worker test suite.

## Decision

Accept the owner's completed no-saved-context observation and the source-backed explanation in LWB-PM13-009. Do not ask the owner to repeat it. The collector and simple entry point now exist; the owner no longer needs to collect terminal output. The latest no-context repair is implemented/offline-tested according to Web's evidence, not a new owner-observed run of the repaired build.

**Return the saved-row reopen collector to Web for two bounded repairs before accepting it for future use.** PM13-04 fresh acquisition remains BLOCKED/NOT_RUN under SB-97. No owner task, separate Sol task or Daybreak assignment is created by this review.

## Evidence reviewed

- Latest five source/guide hashes in `evidence/lwbridge-implementation/2026-09-11-pm13-owner-no-saved-context.json` match the checkout.
- All four original owner-attempt hashes in that finding match the local immutable files under `%LOCALAPPDATA%\LWBridgeRebuild\owner-evidence\20260911T085114Z-eb1cd35e-7f21ad24\`.
- The original postflight records exit code 0, matching runtime integrity, clean cleanup, zero resource rows, zero Search responses and no completed Search/render proof. The original INCOMPLETE output remains preserved; it was not rewritten as a successful live test.
- Source confirms normal `map_summary` errors are recorded before returning to the frontend. The no-saved-context completion branch checks that error plus empty pre/post published servers and absence of DB read errors. It does not invent a Search call when the frontend short-circuits.
- Web reports successful Release/deterministic/browser/collector checks in LWB-PM13-009. PM reviewed the reports and coverage; PM did not rerun those suites, launch an app, click anything or perform live acquisition.

## PM15-01 — P1: reopen can reuse session-one proof

Source: `tools/collect_owner_resource_evidence.py` at reviewed commit, SHA-256 `8e17f0213c3cb08fc74ddd02ae40e31cce43de127c8609269b33adecc4db8da8`.

Locators: `collect_ui_events` lines 160–204, `postflight` lines 305–323, shared UI directory at lines 348–350, and reopen decision at lines 387–396. The native recorder names files by PID and appends to them in `src/LWBridge.Desktop/OwnerEvidenceRecorder.cs` constructor.

Both app sessions write into the same attempt `ui` directory. Each postflight reads all `ui-session-*.jsonl` files. The collector selects the last Search from lexicographically sorted files, not from a proven current app session. Reopen compares that aggregate signature to the first session's signature.

Source-derived counterexample for Web to reproduce offline: session one contains a valid saved-row Search/render pair; session two only opens/closes and never searches. With unchanged profile/runtime and clean exit, the aggregate still contains session one's correlated request and signature, allowing the current code to report COMPLETE for reopen. A lower second-session PID/file ordering can also cause older evidence to be selected after a newer observation. This is a source-confirmed acceptance flaw; PM has not executed the counterexample.

Required repair: bind each launch and captured event set to a distinct current attempt/session identity and require that session's actual Search/result/render evidence. Preserve prior evidence for audit but never use it to satisfy the current session's gate. PID alone, filename sorting and aggregate latest-request selection are not sufficient session ownership. Same saved row is expected on reopen; reusing the previous session's proof is not.

Web must add isolated meaningful regressions: second session without Search cannot pass; second session with an uncorrelated/mismatching result cannot pass even if session one passed; file order/PID reuse cannot select old proof; a second distinct session's valid request/render for the same saved row can pass. No game or owner repetition is needed to test this logic.

## PM15-02 — P2: failed process observation can look clean

Same collector source. Locators: `process_snapshot` lines 78–92, `relevant_running` lines 95–102, `preflight` lines 275–303 and `postflight` lines 305–324.

`process_snapshot` returns an empty list on a nonzero command exit. JSON parse failure returns an error-shaped item which `relevant_running` ignores. The callers therefore cannot distinguish an actual empty process list from failed observation and may report no blockers/cleanupClean. This is source-confirmed missing-evidence handling, not evidence that the owner's successful historical observation failed.

Required repair: retain process observation success/error separately from rows. Unknown/failed observations must produce BLOCKED/INCOMPLETE instead of an empty/clean claim. Preserve the raw bounded diagnostic and a plain owner-facing status. Apply this evidence-health check to success decisions: recorded app failure, evidence parse errors or missing current-session logs must not silently allow COMPLETE. Do not overwrite or delete failed-attempt evidence to reset the state.

Web must test nonzero process-query exit, malformed response, and a successful empty result as distinct cases, plus app/evidence failure after an earlier valid session. Use isolated fixtures/mocks, not live fault injection or new owner tests.

## Exit criteria and next owner

Web fixes PM15-01/02 only, updates durable evidence and the exact package identity, and runs relevant isolated checks. Keep COMPLETE_NO_SAVED_CONTEXT distinct from saved-row reopen and preserve the owner's original files. Retain the guide's no-commands/no-technical-diagnosis requirement. Commit/push/verify and return to PM. Do not request another test for the current empty profile, relax SB-97, seed saved data or start unrelated research.

After these fixes, PM can accept the collector's offline correction. A real fresh scan still requires its own permitted execution and evidence; recorder repair does not solve that external restriction or complete the two pages.
