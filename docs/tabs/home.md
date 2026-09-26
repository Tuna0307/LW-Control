# Home / Overview — strict parity status

**Current through:** `LWB-R8-065`, 2026-09-26.

The retained Home lifecycle is freshly **LIVE-WORKING** against the installed current-v21 game as of R8-065: manual launch reached `connected` and closed cleanly, then startup auto-launch reached `connected` and closed cleanly in the same dedicated proof. This is current-client functionality evidence, not one-to-one parity completion. The acceptance target remains exact retained LWBridge 0.3.1 behavior.

## Current parity interpretation

| Area | Current classification | Next parity requirement |
|---|---|---|
| Original Overview component/assets | EXACT_BYTES-derived | Keep original assets unchanged |
| Launch / Close lifecycle | LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION | R8-065 fresh live proof reached `connected` and closed the real game cleanly; audit exact original launcher/profile/ownership/error semantics |
| Launch at startup | LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION | R8-065 fresh live proof auto-launched the real game and reached `connected`; recover exact original persistence/timing/default behavior |
| Automatic reconnect | EQUIVALENT_REIMPLEMENTATION | Tie thresholds/cancellation/errors to reference |
| Status / pending / refresh | PARTIAL EXACT_CONTRACT | Close exact bridge readiness/request-result grammar |
| Same/cross-server navigation | EQUIVALENT_REIMPLEMENTATION | Recover original routing and error semantics |
| Retained profile selection | PARTIAL EXACT_CONTRACT + reimplementation | Continue one-to-one recovery of retained profile registry/selection semantics without reconstructing excluded authorization/account state |
| Login/activation/renewal/unbind/account-purpose surfaces | EXCLUDED — explicit owner directive | Do not research or restore these surfaces |
| Header / retained non-account geometry | PARTIAL | Restore only retained non-account header behavior; do not reintroduce excluded account controls |

## What remains valuable from R7

The lifecycle stress, rollback, reconnect, status and navigation evidence remains useful proof that the reconstruction can operate against the current Last War client.

It does not prove that the implementation is the same as LWBridge 0.3.1.

## Required direction

Do not redesign Home around our current service model. Recover the retained original Rust/Tauri command/service behavior, launcher descriptor, profile semantics and bridge readiness, then make the current-client compatibility layer reproduce those contracts. Account/Login/Authentication-purpose flows remain explicitly excluded.

Historical R7 PASS statuses are preserved in their evidence files but are not current parity completion claims.
