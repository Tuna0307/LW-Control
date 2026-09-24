# LWBridge documentation index

**Current checkpoint:** `LWB-R8-001`, 2026-09-24.

The project direction changed on 2026-09-24 from “working reconstruction with selected owner customizations” to **strict one-to-one recovery of LWBridge 0.3.1**.

## Read in this order

1. [`../AGENTS.md`](../AGENTS.md) — mandatory repository rules.
2. [`strict-parity-recovery.md`](strict-parity-recovery.md) — current product directive.
3. [`lwbridge-parity-matrix.md`](lwbridge-parity-matrix.md) — current whole-program completion matrix.
4. [`implementation-handoff.md`](implementation-handoff.md) — current continuation state.
5. [`../BACKLOG.md`](../BACKLOG.md) — current parity work queue.
6. [`lwbridge-project-status.md`](lwbridge-project-status.md) — project-manager status.
7. [`deep-binary-handoff.md`](deep-binary-handoff.md) — protected package / binary recovery priority.
8. [`lwbridge-architecture.md`](lwbridge-architecture.md) — recovered original architecture.
9. [`lwbridge-ui.md`](lwbridge-ui.md) — recovered frontend provenance.
10. [`lwbridge-map-scan.md`](lwbridge-map-scan.md) — cumulative Map recovery evidence.
11. [`tabs/home.md`](tabs/home.md), [`tabs/map-data.md`](tabs/map-data.md), [`tabs/shared-release.md`](tabs/shared-release.md) — current parity interpretation of previously reconstructed surfaces.
12. [`external-audit-guide.md`](external-audit-guide.md) — reviewer guidance.
13. [`../evidence/lwbridge-implementation/README.md`](../evidence/lwbridge-implementation/README.md) — chronological evidence navigation.

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

The reference was re-hashed on 2026-09-24 before the R8 direction reset.

## Completion authority

The old 47-case Home/Map acceptance matrix remains valid evidence for the behaviors it tested. It no longer means the project is close to one-to-one completion.

Current completion authority is [`lwbridge-parity-matrix.md`](lwbridge-parity-matrix.md). The matrix distinguishes exact recovered bytes/contracts, equivalent reimplementations, deviations and unknown original behavior.

## Historical documents

`docs/reviews/`, R1-R7 evidence, the old acceptance matrix, and cumulative recovery ledgers are intentionally retained. Do not rewrite them to make the project appear more consistent. They explain how the reconstruction evolved and where drift entered.

A historical file may say a feature was intentionally removed or optimized. Those statements remain true of that checkpoint, but the R8 strict parity directive supersedes them as current product policy.

## P0 evidence focus

The protected `bridge-scripts.dat` package is now a first-class recovery target. Existing R6-039 through R6-046 findings already establish major loader/crypto boundaries. The next work is to recover the missing envelope/package linkage and original script contents through permitted analysis methods.

Historical SB-79 records an operation rejected by a previous environment. Preserve that record and do not reroute a prohibited operation. Do not mistake the historical denial for evidence that the underlying package is impossible to recover.
