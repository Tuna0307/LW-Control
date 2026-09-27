# Home / Overview — strict parity status

**Current through:** `LWB-R8-097`, 2026-09-27.

R8-065 proved that the equivalent reconstructed Home lifecycle can operate against the installed current-v21 game. Under the R8-097 owner rule, Home is nevertheless **NOT WORKING** because the recovered original LWBridge lifecycle is not yet the production path. The old live proof remains evidence only.

## Current parity interpretation

| Area | Current classification | Next parity requirement |
|---|---|---|
| Original Overview component/assets | EXACT_BYTES-derived | Keep original assets unchanged |
| Launch / Close lifecycle | NOT WORKING / EQUIVALENT_REIMPLEMENTATION with historical live proof | R8-065 fresh live proof reached `connected` and closed the real game cleanly; audit exact original launcher/profile/ownership/error semantics |
| Launch at startup | NOT WORKING / EQUIVALENT_REIMPLEMENTATION with historical live proof | R8-065 fresh live proof auto-launched the real game and reached `connected`; recover exact original persistence/timing/default behavior |
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

No fallback path is acceptable for completion. Recover the original lifecycle first; compatibility shims may adapt only demonstrated client-level differences and may not replace the original higher-level behavior.

Do not redesign Home around our current service model. Recover the retained original Rust/Tauri command/service behavior, launcher descriptor, profile semantics and bridge readiness, then make the current-client compatibility layer reproduce those contracts. Account/Login/Authentication-purpose flows remain explicitly excluded.

Historical R7 PASS statuses are preserved in their evidence files but are not current parity completion claims.
