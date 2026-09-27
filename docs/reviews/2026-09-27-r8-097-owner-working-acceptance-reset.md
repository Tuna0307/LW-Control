# R8-097 — owner binary WORKING acceptance reset

**Date:** 2026-09-27
**Scope:** project-wide current-status/documentation policy; no production behavior change.

## Decision

Owner-facing `WORKING` is now binary. A retained feature is WORKING only when the recovered original LWBridge 0.3.1 logic is the production path and that path succeeds against the live current Last War client. Equivalent reimplementations, reconstructed scanners/lifecycle managers, fallbacks, test harnesses and output-equivalent alternatives do not qualify, even when they have live-success evidence.

Home and Map are therefore currently **NOT WORKING** for owner acceptance. R8-065/R8-066 remain valid evidence that the equivalent reconstruction can operate against current-v21; they are not erased or relabelled as failed tests. They simply no longer satisfy the owner acceptance label.

## No-fallback rule

The current movement/AOI Map scanner and equivalent Home lifecycle may remain only as historical evidence, comparison/oracle tooling, or isolated research/test harnesses. They must not silently activate as production fallback paths and must not mask failure of the recovered original path.

## Priority

1. Recover and live-prove the original LWBridge Map acquisition engine, especially `XluaBridgeMapScanTick` traversal/order/coordinates, work/request pacing, retry/backoff, queue/drop/removal handling and exact completion.
2. Recover and live-prove remaining original Home launcher/profile/proxy/lifecycle behavior.
3. Resume unrelated auth/entitlement/multi-lease and secondary-tab work after Home/Map, except where a specific dependency is proven necessary for those priority surfaces.

Server season progression must not be used as a blanket explanation for incompatibility. Compatibility shims require concrete evidence of an installed-client difference and may only adapt the low-level boundary while retaining recovered original behavior above it.

## Historical evidence

Do not rewrite historical reviews/evidence. Old `LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION` statements remain accurate descriptions of the tests/terminology at their checkpoint; this R8-097 policy supersedes them only for current owner-facing acceptance.
