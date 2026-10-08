# Home Native Semantics 008 — independent lead review, 2026-10-08

Reviewed worker checkpoint: `9cc5bcee7a7bff19c391995c87ba8a3359e6d3aa`.
Decision: **ACCEPT bounded native value/default recovery and its inert comparisons; complete Home parity remains PARTIAL and ready for further work.**

Fresh unchanged original-byte assertions verify the two nominal 100×100ms close loops, the request's autoLaunchAll boolean fallback, and the local 5s/100ms and 90s/250ms wait values. The output-only redirected preservation validator passes, including six metadata probes. Fresh Release Desktop.Checks build has zero warnings/errors; actual reconcile comparisons pass eight cases. Accepted Map engine/sink cancellation regression, current close/reconnect behavior checks, profile ownership, canonical frontend checks and package integrity all pass.

These are bounded recovered contracts/current implementation regressions, not protected original-runtime execution. The reconnect regression's 0.3.1-derived thresholds are not upgraded to 0.3.17 evidence because the check passed. No canonical production source changed in 008.

## Important acceptance limits

1. A matching nominal 10s budget does not establish full close semantics. Original cap-before-process-check ordering differs mechanically from the current event-based Process.WaitForExit(10000). The final timer can resume at the exhausted-cap error before another process-presence check. In an ideal zero-overhead model, a disappearance at 9950ms lies after the last scheduled process check and before the event-wait deadline. See the lead's `close-boundary-hypothesis.json`.

   **LEAD008-H1** is a source-backed hypothesis requiring matched close trigger, process identity/cardinality, caller mapping and controlled actual production comparison. It is not yet a confirmed product defect. The reconstructed model is not execution of either original Rust code or the actual production closer. Do not fix policy from the model alone or claim equal terminal outcomes merely from both saying 10 seconds.

2. The local 5s and 90s loops do not establish the entire Home launch deadline, retry policy, error propagation or health monitor. Clock provenance, response types and continuation branch outcomes remain available static work. The worker's refusal to substitute those local values for 120s/190s adapter deadlines is appropriate.

3. Eight reconcile cases establish the public boolean default decision through the actual production handler with inert dependencies. They do not establish all-profile startup order, global versus profile gates, process lifecycle, original once-only semantics or complete preference parity. Those require separate original authority.

4. Full health thresholds, ordinary/maintenance retry schedules, caps/resets, cancellation/error precedence and profile/process transitions remain READY_STATIC_NATIVE_RECOVERY_INCOMPLETE. These must remain assigned work, not become unavailable merely because 008 produced a few source facts.

## Preservation and execution boundary

Worker evidence is unchanged; new assertions/results are in lead-review-2026-10-08. The wrapper redirects only three output destinations and records original script hashes; it retains all original assertions and input locations. Reference EXE, original installed scripts and archived 004 real Resource dataset pass the worker preservation checks. No lead game launch, desktop capture/input/focus, original-service call or product change occurred.

## Continuation

One longer solo campaign: **LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009**, sequential milestones A–G, one final relay. It closes remaining available Home recovery and coupled Home/Map lifetime comparisons, fixes proven discrepancies, and maintains precise unresolved static/external/live boundaries. Preserve accepted Map/query/ownership/UI work and all negative evidence. No complete Home/Map A→A or LIVE_PROVEN promotion.
