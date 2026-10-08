# Home Parity Closeout 009 — independent lead review

Reviewed checkpoint: `1d9b54c6433a24899b9e7fa3e390f8d16a8c4a55` on `research/offline-controller`; the direct remote matched before review. Review baseline: accepted lead checkpoint `b15c13e4f0c8191cd9e6f92814948b548801d124`.

**Decision: CHANGES_REQUIRED. Campaign 009 is PARTIAL, not complete. Home/Map original A→A parity remains PARTIAL.** Preserve the useful recovered contracts and implementation; do not revert unrelated accepted behavior. No complete Home/Map or LIVE_PROVEN acceptance is given.

## Reproduced production findings

### LEAD009-01 — restoration after an unverified exit (high priority)

In `tools/run_live_resource_probe.py`, `terminate_owned_game_process_for_stop` exits its poll when `image_path(pid)` returns `None`, then unconditionally reports `processExited:true`. `tools/run_overview_bridge.py::run_stop` subsequently restores the script backup and clears the recovery journal.

The new lead inverse runs the actual production `run_stop` with an isolated journal and a controlled process API. Termination is accepted, but the process remains present; image lookup becomes unavailable at 300 ms. Result: `ok:true`, `processExited:true`, **`gameRunning:true`**, one restoration call while the PID still exists, and the journal removed. See `stop-ownership-negative.json`, LEAD009-01.

The original reconstructed presence predicate also treats an unavailable path as absent. That fact does **not** establish permission to restore a current-client instrumented installation under a surviving process. Preserve the original close-policy fact, but keep the independently required current-client restoration/ownership gate. Do not change or repin the original oracle merely to hide this boundary. Require exact captured-process exit evidence or preserve the journal and truthful unresolved/failure state.

### LEAD009-02 — PID incarnation replacement between validation and termination (high priority)

The same Python Stop helper validates creation identity through `selected_game_processes`, then independently queries the PID path and opens a new `PROCESS_TERMINATE` handle. It does not verify creation identity on the handle being terminated. A same-path PID replacement after the inventory checks can therefore pass path validation and be terminated.

The lead inverse changes the controlled process incarnation at `open_terminate`, after both inventory checks. Actual production accepts and terminates that replacement, restores, clears the journal and returns success. See LEAD009-02. This is an adversarial process seam, not an actual Windows PID-reuse witness; the production lack of handle-bound incarnation checking is directly inspected. Verify and act on the same process object/handle, and preserve checks for absent, inaccessible, foreign, replaced and legitimately owned processes. Audit the analogous native recovery effect: it too validates a `Process` then separately calls `OpenProcess(pid)`; its current ownership tests do not prove this race closed.

## Assigned work still unfinished

### LEAD009-03 — launch/readiness static work remains available

Worker `semantic-obligations.json` explicitly says B is not compared to current production, while treating the lease boundary as OUT_OF_SCOPE and setting `next` to no available work. In the same entry it names local continuations `0x1D72C9–0x1D74E5` and `0x1DD3F7–0x1DD6B5` as undecoded. The original assignment also includes `0x2A2887`, `0x2C75CA` and `0x2C6A65`, their outcomes, cancellation and error precedence.

Protected service execution remains excluded; static reading of available code does not require service access. Recoverable branches behind an auth-related boundary cannot be marked complete or externally blocked solely because the original lease cannot be acquired. Decode their local outcomes and compare equivalent supported layers; only a precise unavailable input is an external dependency. This finding does not prescribe a guessed product change.

### LEAD009-04 — profile/preferences/Home–Map lifetime work remains incomplete

E records loop structure with predicates UNKNOWN and no current comparison. Closures `0x39E153`, `0x39E77D`, `0x1E70F7`, profile record producers/filter ordering, pending preference saves, A→B→A and Home Close during Map/recovery remain ready work. A single syntactic launch call inside a loop is not evidence of single-profile launch behavior. Recover predicates/order and then compare actual native handlers and mounted App; preserve R3 and accepted Map cancellation/ownership fixes.

### LEAD009-05 — coverage and closeout do not justify completion

The recovery comparator deliberately removes cleanup events, normalizes most error strings to `<error>`, separates status from other effect ordering, omits some status fields and collapses consecutive duplicate statuses. Its 230 successes establish only that normalized reconstructed model contract. The harness drives monitor/run serially; it does not establish production timer overlap, held termination/cleanup with user Stop/profile replacement, process-handle identity, restoration failures, complete profile lifetime or exact observable error/order semantics. Program.cs retired 351 lines of older assertions; mapping every still-applicable obligation to executed replacement proof is unfinished.

The campaign work item was still READY / PROJECT-LEAD ASSIGNED, continuation/progress explicitly say PARTIAL, and there is no final dated 009 worker review closing A–G. Existing masters also contradict each other about pending Windows checks and launch OUT_OF_SCOPE. This is an honest partial checkpoint in the repository, irrespective of an external “done” message. Finish current records without overwriting historical negative evidence. Available static work may be saved PARTIAL if interrupted, but cannot be described as completed.

## Independently executed verification

- Fresh Release Desktop.Checks build, including canonical frontend build/package embedding: **0 warnings, 0 errors**. UI fingerprints `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685` / `6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`.
- Fresh original-byte assertions and actual Python Stop comparator: **21 cases, 0 reconstructed-contract mismatches**.
- Fresh actual native recovery trace and new oracle generation: **230 scenarios, 0 normalized effect/status mismatches**. Fresh lead outputs, not just rereading worker results.
- Lead runner **14/14 commands pass**: actual trace, comparison, process ownership, close timing, reconnect policy, Home lifecycle, profile runtime ownership, canonical Map, Map native boundary, Python lifecycle, byte contracts, recorded contract assertions, canonical frontend check and production-package check. Exact commands, exit codes and logs in `executed-checks.json`.
- Fresh actual mounted App profile A→B→A/deferred fulfilled/rejected responses and teardown replay: **20 cases PASS** in EN/light and JA/dark with the inert native bridge and archived current-client Resource payload. This repeats accepted current behavior; it does not close the missing original E predicates or pending native recovery/Map overlaps.
- Independently reproduced **2/2 missing Stop ownership/restoration inverses**, despite those general checks passing.
- Read-only preservation: both reference EXEs match `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`; installed original script triplet and archived 004 source/sidecar/copy hashes match historical preservation inputs; archived snapshot has **8,008 Resource rows**.

All lead evidence is in `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009/lead-review-2026-10-08/`. Reconstructed oracle proofs are not execution of original protected native code. No new game launch, installation mutation, desktop input/capture/focus or protected service call occurred. No production source was changed by this review.

## Continuation

One solo **HOME-PARITY-CLOSEOUT-009-R1** continuation: first correct LEAD009-01/02, then finish ready launch/profile/lifetime branches and the replacement coverage/closeout requirements LEAD009-03/04/05. Use sequential durable checkpoints and one final owner relay. Do not start another unrelated campaign or live witness to avoid these findings. Original encrypted Map/provider inputs and current-game live witnesses remain distinct dependencies.
