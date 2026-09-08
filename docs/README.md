# LWBridge documentation index

This directory contains current LWBridge reconstruction and official-client evidence. Start here instead of browsing files by date or guessing which notes are authoritative.

## Reading order

1. [`lwbridge-feature-ledger.md`](lwbridge-feature-ledger.md) — current implementation/proof matrix for Overview and Map Data.
2. [`official-runtime-architecture.md`](official-runtime-architecture.md) — current installed Last War runtime baseline and official launcher observations.
3. [`lwbridge-architecture.md`](lwbridge-architecture.md) — recovered LWBridge application/runtime architecture.
4. [`lwbridge-ui.md`](lwbridge-ui.md) — recovered frontend reproduction and visual verification.
5. [`lwbridge-injection.md`](lwbridge-injection.md) — recovered bootstrap/injection lifecycle, fail-closed states, descriptor checkpoint, and remaining gaps.
6. [`lwbridge-map-scan.md`](lwbridge-map-scan.md) — recovered Map Scan request/native-capture contract and remaining gaps.
7. [`lwbridge-artifact-evidence.json`](lwbridge-artifact-evidence.json) — machine-readable artifact evidence supporting recovered LWBridge structure.

## Planning

- [`../task.md`](../task.md) is the detailed implementation and acceptance specification for Overview + Map Data.
- [`../TASKS.md`](../TASKS.md) is the concise current backlog.

## Visual and machine-readable evidence

- [`ui-reproduction/`](ui-reproduction/) contains the current deterministic UI regression evidence.
- [`task-reference/`](task-reference/) contains the user-supplied Overview and Map Data visual references.
- [`../evidence/lwbridge-0.3.1/`](../evidence/lwbridge-0.3.1/) contains immutable recovered frontend evidence.
- [`../evidence/lwbridge-implementation/`](../evidence/lwbridge-implementation/) contains rebuild milestone evidence.
- [`../evidence/official-runtime/`](../evidence/official-runtime/) contains read-only current-client runtime snapshots.

## Evidence labels

- **RECOVERED** — statically recovered from the verified LWBridge artifact or embedded assets.
- **IMPLEMENTED/OFFLINE-TESTED** — implemented and verified without claiming a live game result.
- **LIVE-PROVEN** — observed against the current installed client with authoritative evidence.
- **UNKNOWN/BLOCKED** — still unresolved.

Temporary analysis under `.codex-live/lwbridge-*` is convenience material only. Durable conclusions belong in this directory or under `evidence/`.
