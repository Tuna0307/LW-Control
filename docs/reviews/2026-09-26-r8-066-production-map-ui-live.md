# R8-066 — production Map WebView live proof

**Date:** 2026-09-26
**Classification:** LIVE-WORKING / EQUIVALENT_REIMPLEMENTATION; original acquisition remains UNKNOWN.

## Goal

R8-065 proved the production Home lifecycle and Manual Map service directly. R8-066 raises the Map acceptance bar one layer higher: prove the normal Release desktop application, recovered Map Data WebView, production lifecycle service, production Manual scanner, native Search command and rendered Resource table work together against the installed current-v21 Last War client.

The historical `--normal-ui-live-resource-proof` is not suitable for this gate because it intentionally replaces the production scanner with the older bounded live-resource helper. R8-066 therefore adds a separate `--normal-ui-live-map-proof` mode that keeps the normal persistent profile database and `ManualMapScanCommandService` wiring.

## Failures found while building the stronger gate

The old helper proof first exposed a proof-only null handling bug: nullable numeric scan-status values were passed to `TryGetInt32/TryGetInt64` even when the JSON value was explicit `null`. The reader now checks `JsonValueKind.Number` before parsing.

The stronger production proof then hit normal startup reconciliation already in progress. It was corrected to observe the exact R8-042 native `profile_instance_status` projection and reuse app-owned startup when present, only issuing Start after a stable no-instance state. This avoids racing normal UI startup.

A later run completed scan and Search but failed render correlation. Failure-side DOM evidence proved the exact queried row was visibly rendered; the proof matcher was stale because it required five Resource cells while the recovered/current table now renders six columns with a resource-amount cell before status and Updated At. The correlation contract and passive owner-evidence matcher now accept both the legacy five-cell shape and the current six-cell shape while still matching coordinate, level, content and timestamp.

## Final live result
The final Release run started from no Last War/LWBridge process, opened the real Map Data WebView, reached `connectionState="connected"`, selected Resource through the rendered checkbox controls, clicked the real Start Reading button and let the production Fast scanner finish.

- Server: 2212.
- Mode: Fast, concurrency 20.
- Selected type: Resource.
- Scan: 2500 / 2500 blocks read.
- Failed blocks: 0.
- Unread blocks: 0.
- Terminal phase: `idle`.
- Resource Search total: 515.
- The first returned row at `699,901`, level 2, was correlated to the rendered six-cell table row.
- Rendered status: `Idle`.
- Desktop proof process exited 0.
- Last War and LWBridge processes were both absent after proof cleanup.

The local proof also captured a screenshot after successful render correlation. Shared evidence intentionally removes the local profile ID, instance ID, process ID and Windows user path.

## Regression validation

The Release checks project built with 0 warnings and 0 errors. The full deterministic checks executable returned `ok=true`, all deterministic groups true, `failures=[]`, and reported no game or launcher process running.

The Resource render contract now has deterministic coverage for both the current six-cell row and the legacy five-cell row. Passive owner-evidence correlation uses the same compatible rule.

## Boundary

R8-066 proves the user-facing production path **desktop app → Home lifecycle → recovered Map WebView → production Manual scanner → map_search → rendered Resource row** works live on current-v21.

It does **not** claim the current Map acquisition algorithm is the original LWBridge 0.3.1 algorithm. Original acquisition remains an evidence-recovery task.

Shared evidence: `evidence/lwbridge-implementation/2026-09-26-r8-066-production-map-ui-live.json`.
