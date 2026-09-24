# Home / Overview — strict parity status

**Current through:** `LWB-R8-008`, 2026-09-24.

The current Home reconstruction works at many previously tested lifecycle/status scopes, but that is no longer enough to call the tab complete. The acceptance target is exact LWBridge 0.3.1 behavior.

## Current parity interpretation

| Area | Current classification | Next parity requirement |
|---|---|---|
| Original Overview component/assets | EXACT_BYTES-derived | Keep original assets unchanged |
| Launch / Close lifecycle | EQUIVALENT_REIMPLEMENTATION | Audit exact original launcher/profile/ownership/error semantics |
| Launch at startup | EQUIVALENT_REIMPLEMENTATION | Recover exact original persistence/timing/default behavior |
| Automatic reconnect | EQUIVALENT_REIMPLEMENTATION | Tie thresholds/cancellation/errors to reference |
| Status / pending / refresh | PARTIAL EXACT_CONTRACT | Close exact bridge readiness/request-result grammar |
| Same/cross-server navigation | EQUIVALENT_REIMPLEMENTATION | Recover original routing and error semantics |
| Profile/account selection | PARTIAL | Restore original account/auth presentation and semantics |
| Login/activation/renewal/unbind | DEVIATION | These original surfaces were removed and are now parity gaps |
| Header/account geometry | DEVIATION | Remove rebuild-only geometry workaround when original header/account controls return |

## What remains valuable from R7

The lifecycle stress, rollback, reconnect, status and navigation evidence remains useful proof that the reconstruction can operate against the current Last War client.

It does not prove that the implementation is the same as LWBridge 0.3.1.

## Required direction

Do not redesign Home around our current service model. Recover the original Rust/Tauri command/service behavior, launcher descriptor, profile semantics, bridge readiness and account flows, then make the current-client compatibility layer reproduce those contracts.

Historical R7 PASS statuses are preserved in their evidence files but are not current parity completion claims.
