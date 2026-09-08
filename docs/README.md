# LWBridge documentation index

This directory contains current LWBridge reconstruction and official-client evidence. Start here instead of browsing files by date or guessing which notes are authoritative.

**Mandatory before working:** [../AGENTS.md](../AGENTS.md) defines the user's repository-wide reverse-engineer-first, no-invented-values, immediate-finding-documentation and checkpoint commit/push rules. Every AI and contributor must follow them. New findings require source identity/hash, an exact locator, reproduction steps, evidence status and limitations; update this index and the feature ledger when adding durable material.

## Reading order

Start with [`lwbridge-project-status.md`](lwbridge-project-status.md) for project-manager review 2, the completed PM2 follow-up repair, and ordered remaining work. The original four PM2 failures remain documented as historical review evidence; current source passes the independent reproducer and expanded deterministic regressions. The subject documents below retain technical evidence.

1. [`lwbridge-feature-ledger.md`](lwbridge-feature-ledger.md) — current implementation/proof matrix for Overview and Map Data.
2. [`official-runtime-architecture.md`](official-runtime-architecture.md) — current installed Last War runtime baseline and official launcher observations.
3. [`lwbridge-architecture.md`](lwbridge-architecture.md) — recovered LWBridge application/runtime architecture.
4. [`lwbridge-ui.md`](lwbridge-ui.md) — recovered frontend reproduction and visual verification.
5. [`lwbridge-injection.md`](lwbridge-injection.md) — recovered bootstrap/injection lifecycle, fail-closed states, descriptor checkpoint, and remaining gaps.
6. [`lwbridge-map-scan.md`](lwbridge-map-scan.md) — recovered Map Scan request/native-capture contract and remaining gaps.
7. [`lwbridge-artifact-evidence.json`](lwbridge-artifact-evidence.json) — machine-readable artifact evidence supporting recovered LWBridge structure.

## Planning

- [`../task.md`](../task.md) is the primary AI instruction file and full Overview + Map Data acceptance specification.
- [`../BACKLOG.md`](../BACKLOG.md) is the current progress checklist, renamed from `TASKS.md`. Do not recreate the old name or duplicate the requirements.

## Visual and machine-readable evidence

- [`ui-reproduction/`](ui-reproduction/) contains the current deterministic UI regression evidence.
- [`task-reference/`](task-reference/) contains the user-supplied Overview and Map Data visual references.
- [`../evidence/lwbridge-0.3.1/`](../evidence/lwbridge-0.3.1/) contains immutable recovered frontend evidence.
- [`../evidence/lwbridge-implementation/`](../evidence/lwbridge-implementation/) contains rebuild milestone evidence.
- [`../evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json`](../evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json) records the PM2 repair source hashes, exact locators, commands, results and remaining limits.
- [`../evidence/lwbridge-implementation/2026-09-08-r3-native-host.json`](../evidence/lwbridge-implementation/2026-09-08-r3-native-host.json) records the real isolated WebView2 reload/session and slow-storage responsiveness checkpoint.
- [`../evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json`](../evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json) records the recovered picker result contract and the completed controlled native-WebView duplicate/preference/picker/closed-window interaction matrix.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-launch-envelope-producer.json`](../evidence/lwbridge-implementation/2026-09-08-r5-launch-envelope-producer.json) records `LWB-R5-001`: exact outer-host `LaunchEnvelope` field/value locators, JSON serialization/handoff and the remaining proof/ticket/xLua ABI blockers.
- [`../evidence/official-runtime/`](../evidence/official-runtime/) contains read-only current-client runtime snapshots.

## Evidence labels

- **RECOVERED** — statically recovered from the verified LWBridge artifact or embedded assets.
- **IMPLEMENTED/OFFLINE-TESTED** — implemented and verified without claiming a live game result.
- **LIVE-PROVEN** — observed against the current installed client with authoritative evidence.
- **UNKNOWN/BLOCKED** — still unresolved.

Temporary analysis under `.codex-live/lwbridge-*` is convenience material only. Durable conclusions belong in this directory or under `evidence/`.
