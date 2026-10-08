# 009-R1 progress (solo worker, static/headless/inert)

Start: lead checkpoint cdde6736521cf741700f090c75f80982c521c4e8. Historical lead negatives
(`../lead-review-2026-10-08/stop-ownership-negative.json`) and worker evidence are untouched.

## Checkpoint A/B — LEAD009-01 / LEAD009-02 (done in this commit)

Production changes
- `tools/run_live_resource_probe.py`: `Win32ProcessApi` gains handle-bound primitives (`open_process`, `handle_image_path`,
  `handle_creation_utc`, `handle_wait_exit`); `_terminate_handle_bound` opens ONE handle (QUERY_LIMITED|TERMINATE|SYNCHRONIZE),
  verifies image path (original ASCII-CI predicate) AND creation incarnation (100 ns FILETIME) on that handle, terminates that
  handle, and `terminate_owned_game_process_for_stop` keeps the reconstructed original 100x100 ms path poll unchanged but then
  requires `exitProof` (captured-handle signalled, or PID absent / foreign image proven on the handle).
- `tools/run_overview_bridge.py::run_stop`: restoration is withheld (journal kept, stage `closing_owned_game_for_restore`,
  `stopOutcome` recorded, `StopRestorationWithheld` raised) unless the exit is proven AND no other process of the selected
  installation runs. Restoration failure keeps `restoring_after_owned_game_exit`, retry works.
- `src/LWBridge.Desktop/OwnedProcessTermination.cs` + `OverviewLifecycleRecovery.cs`: native `TerminateOwnedProcessAsync` and
  `TerminateUpdateProcessesAsync` no longer validate a `Process` and then reopen the PID; identity is verified on the terminated,
  awaited handle. Existing error codes preserved.

Original-contract vs adaptation (kept distinct)
- RECONSTRUCTED original (0x41d84b/0x41e3cf/0xe5725): unreadable image counts as "gone"; unchanged and still compared by
  `home009_close_compare.py` (21 scenarios, 0 mismatches; production restore gating verified against an independent process table).
- ADAPTATION (not original): handle-bound identity, exit proof, installation-survivor gate.

Evidence
- `stop-ownership-before-after.json`: 19 cases replayed against the archived pre-R1 production (PID-addressed adapter) and the
  working tree. Behaviourally distinguishing (pre-R1 FAIL, R1 PASS): lead009_01 poll-denied survivor, denied-at-check-100 beyond the
  budget, surviving second installation process, same-path replacement before open, unreadable creation on handle.
  Other "distinguishing" rows differ only because pre-R1 had no `exitProof` field.
  `replacement-after-validation-before-terminate` passes before and after (a handle opened by PID binds at open on real Windows);
  it guards the new handle binding, it is not a before/after discriminator.
- Native: `--owned-process-handle-binding-check` (11 cases). Pre-R1 native logic is evidenced by inspection only
  (cdde6736 `OverviewLifecycleRecovery.cs` ~1077-1097: `ValidateExactProcessIdentity` then `OpenProcess(0x1, pid)`).
- Lead inverse script `check-stop-ownership.py` targets the pre-R1 `open_terminate` API shape and is retained as history only.

Pre-existing unrelated failure noted: `tools/test_overview_start_failure_matrix.py` raises AttributeError
(`close_owned_launcher_process`) at the lead checkpoint too (stale test; see checkpoint E).
