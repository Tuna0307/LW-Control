# LWBridge documentation index

**Current checkpoint:** `LWB-R7-149`, 2026-09-23.
This index is the canonical navigation page. Historical research/checkpoint files remain in place, but their old “current/open/pending” wording is not current status.

## Start here

1. [`AGENTS.md`](../AGENTS.md) — mandatory evidence, safety, testing, and delivery rules.
2. [`task.md`](../task.md) — durable product requirements and 47-case acceptance contract.
3. [`implementation-handoff.md`](implementation-handoff.md) — concise current continuation state.
4. [`tabs/home.md`](tabs/home.md) — Home / Overview status by feature.
5. [`tabs/map-data.md`](tabs/map-data.md) — Map Data status, scan categories, performance, remaining live gates.
6. [`tabs/shared-release.md`](tabs/shared-release.md) — shared runtime and Release status.
7. [`lwbridge-project-status.md`](lwbridge-project-status.md) — current project-manager audit summary.
8. [`external-audit-guide.md`](external-audit-guide.md) — handoff guide for another AI/reviewer.
9. [`../evidence/lwbridge-implementation/README.md`](../evidence/lwbridge-implementation/README.md) — current evidence navigation.
10. [`reviews/2026-09-22-r7-145-doc-evidence-self-audit.md`](reviews/2026-09-22-r7-145-doc-evidence-self-audit.md) — current cleanup/self-audit checkpoint and verifier finding.

## Current acceptance source

The current 47-case baseline is:

`evidence/lwbridge-implementation/2026-09-23-r7-acceptance-matrix-r7149.json`

R7-149 keeps the ordinary `partial` count at **0** and owner-retires E03/E04/E05 in addition to the already-retired City Excel export. The remaining non-pass cases are explicitly population-, authorization-, or blocked-implementation categories.

## Current unresolved gates

- Ghost positive-row proof: owner-deferred until 2026-09-24.
- Supplies positive-row proof: current 2026-09-22 full-world rechecks on 2212 and 2213 found zero Supplies.
- Treasure protected claim scheduler: `UNKNOWN/BLOCKED` under the preserved SB-79 boundary.
- Simultaneous real multi-account UI population: target/account availability gap.
- Final integrated release acceptance: separate release-level gate.

## Detailed technical ledgers

These remain cumulative evidence/recovery documents rather than current-status pages:

- [`lwbridge-map-scan.md`](lwbridge-map-scan.md) — Map contracts, source recovery, implementation-policy notes, historical scan findings.
- [`lwbridge-overview-recovery.md`](lwbridge-overview-recovery.md) — Home lifecycle/bridge recovery ledger.
- [`lwbridge-injection.md`](lwbridge-injection.md) — injection/bootstrap/proxy recovery history.
- [`official-runtime-architecture.md`](official-runtime-architecture.md) — official Last War runtime architecture/evidence.
- [`daybreak-escalations.md`](daybreak-escalations.md) — specialist/restriction register; denials must not be rerouted.
- [`reviews/`](reviews/) — dated checkpoint findings, including R7-130 through the current R7-149 Scheduled Plunder retirement review.

## Historical/superseded delivery documents

Some filenames are retained because other evidence links to them. They are no longer current planning authority:

- `map-data-delivery.md`
- `overview-live-delivery.md`
- `first-live-result.md`
- `lwbridge-completion-estimate.md`
- older sections of `live-test-handoff.md` replaced by the current owner-test list; Scheduled Plunder sections are historical only

Use the tab pages and current matrix for present status.

## Evidence labels

`RECOVERED` = original/static source. `IMPLEMENTED/OFFLINE-TESTED` = rebuild/test evidence. `LIVE-PROVEN` = current-client observation. `UNKNOWN/BLOCKED` = unresolved and intentionally not invented. `IMPLEMENTATION POLICY` = explicit rebuild design choice, not claimed original parity.
