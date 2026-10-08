# Home Parity 009-R1 — independent lead decision

Reviewed worker checkpoint: `17095dc1e250f5ad907110db80d695dcc2c82f5b` (direct remote matched). Baseline: `cdde6736521cf741700f090c75f80982c521c4e8`.

**Decision: ACCEPT bounded restoration, handle-binding and issuing-session corrections; CHANGES_REQUIRED for R1/009 campaign closure. Full original Home/Map A→A remains PARTIAL.** Do not revert the accepted fixes. “All A–F done” is not supported by the remaining available static branches and known observable differences.

## Accepted bounded corrections

- **LEAD009-01:** actual Python Stop now separates original image-based close policy from captured-process exit proof. It withholds installation restoration, records the reason and preserves a retryable journal when the exit or absence of selected-installation survivors is unproven.
- **LEAD009-02:** Python and native termination now verify image/creation on the same process handle used for termination/wait. Fresh inert inverse suites pass. This establishes controlled process-boundary correctness, not a real Windows PID-reuse witness. Original path-only close and current ownership adaptation remain separate authorities.
- **LEAD009-05 issuing-session correction:** a delayed old recovery termination can no longer unconditionally clean a newer Stop→Start session. Closed owners do not initiate the tested late cleanup effects. Eight held-effect groups and ten boundary groups pass independently. This bounded acceptance does not cover every unknown original continuation or arbitrary production scheduler interleaving.
- **closeUnmanaged:** the source-backed payload/default/close/wait branch and nine current inert groups pass. No live unmanaged game was closed by the review; error wording, full launcher rollback, multi-profile managed-set equivalence and exact original execution are not upgraded.

## Remaining findings

### LEAD009R1-01 — remove the demonstrated extra production Auto Launch gate

Original static predicate recovery (`home009_profile_contract.py`, `0x39E153/0x2EA391`, with the earlier lock-poison misinterpretation corrected) admits an enabled primary profile with no status reason when `autoLaunchAll` is true. It does not additionally read native persisted AutoLaunchGame.

Current `OverviewLifecycleService.ReconcileStartupAsync` still uses `autoLaunchAll && config.Snapshot.AutoLaunchGame`. The independent lead probe executes this actual handler with an isolated config and inert helper across four boolean combinations. With request=true and native value=false, **expected helper admission=1, actual=0**; the other three combinations match. See `reconcile-gate-negative.json`. This is current production versus recovered admission logic, not original runtime execution.

**Lead decision: remove this extra production AND-gate.** Preserve original UI local preference → explicit payload semantics. A test fixture's safety seed does not justify changing the shipped product's decision. Keep non-live tests/pilot hosts safe with explicit inert providers or host-only admission controls, outside production preference behavior. Audit every caller/fixture that relied on this gate before changing it; do not enable an actual game launch in a test. No owner permission question is needed for this authorized A→A correction.

The independently recovered `restartRequired` path and single-profile supported admission still require completion. Do not label original all-enabled-profile selection equivalent to selected-profile-only launch; genuinely missing multi-lease inputs remain separate dependencies.

### LEAD009R1-02 — readiness window remains an observable difference

Original recovery identifies a wall-clock `now + 90,000 ms` window **after the launcher result** at `0x1DD114/0x1DD119`, registry refresh, 250 ms polling and a final registry lookup. Current Python starts a single deadline at `launch_started + timeout_seconds` before launcher/game acquisition and uses monotonic time; production defaults the timeout to 120 seconds. These are different anchors, budgets, clocks and final-lookup behavior, not interchangeable constants.

The independent bounded timer probe runs actual `run_overview_bridge.await_ready` with a virtual clock and valid ready payload against the recovered deadline arithmetic:

| Controlled supported ready event | Recovered deadline model | Current actual wait |
|---|---|---|
| Game report at 5s; event at 100s | Deadline 95s: timeout | Deadline 120s: success |
| Game report at 70s; event at 150s | Deadline 160s: success | Deadline 120s: timeout |

See `readiness-window-negative.json`. This is a bounded timer comparison assuming an equivalent valid connection event, not protected original execution or live protocol-event equivalence. Decode remaining report/launcher stages and map the current adapter's actual handoff; separate launcher acquisition, bridge-connect deadline and helper supervision. **Do not defer the whole known rule until a live timing measurement.** Controlled production seams can test the recovered timer contract; live event mapping remains an additional proof obligation. Do not merely change a global 120 to 90 or weaken current session/authentication/lease gates.

### LEAD009R1-03 — available static work remains unfinished

Worker continuation lists the finalizer `0x1DDDE3`, launcher stage `0x1DC2BC–0x1DCDA0`, reconcile recovery sub-path `0x203B30–0x2043C8`, `restartRequired` reason producer `0x2A0CB7`, post-connect report `0x1E71D6/0x23C1E4/0x23FAC5`, and original error-entry content as undecoded. These are assigned recovery branches, not completed work or automatically external dependencies. The statement “remaining ready work is not unfinished code” does not close them.

Complete their local input/state/action/result flow and actual implementation comparisons. Only truly absent protected inputs or owner-restricted live events belong under external/live dependencies. Preserve immutable evidence and clearly report any still undecoded static action as INCOMPLETE.

## Independent verification and preservation

The lead reran **all 28 integrated commands** with outputs redirected to the new `r1/lead-review-2026-10-08/` directory. All exit 0: fresh Release check build (0 warnings/errors), scratch Release publish, actual 230-scenario recovery replay/comparison, current process/Stop/reconnect/Home/profile/Map checks, new 19 Python Stop cases, 12 handle-binding cases, nine unmanaged-close groups, eight held-async and ten boundary groups, source-byte/launch/profile contract checks, current-client contract, canonical frontend/package, and 20 mounted App profile/deferred-response cases. Normalized oracle success retains its prior limitations; no live or full original-runtime proof is inferred.

The two independent lead comparisons above add **one production admission mismatch and two bounded timing mismatches** despite general suites passing. Their initial harness setup errors (reference path normalization and function name) were corrected before results were recorded; they were harness errors, not product findings.

The prior lead negative packet has no worker diff from `cdde6736` to `17095dc1`; historical worker records remain preserved. The checked-in original reference still has SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`. Scratch verification publish was removed; lead reconcile configs/runtime were temporary and removed. No product source was changed by the lead. No new game launch, owner-game Stop, desktop input/capture/focus or protected service request was made. An existing owner game is not a cleanup target.

## Continuation

Continue **HOME-PARITY-CLOSEOUT-009-R2**, solo, headless/static/inert: preserve accepted R1 safety fixes, remove the extra product gate with inert fixture protection, finish launcher/report/finalizer/profile local semantics and correct the readiness policy at equivalent current-client boundaries. Finish all ready branches before one relay; a preserved PARTIAL checkpoint is appropriate if interrupted, a completed campaign claim is not.
