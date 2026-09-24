# LWBridge implementation evidence index

This directory intentionally retains chronological machine-readable evidence. Do **not** infer current product direction from an arbitrary older checkpoint.

## Current authority — R8 strict parity

- `2026-09-24-r8-006-envelope-key-output-flow.json` ? proves caller-side output ownership from the opaque envelope consumer into the exact 32-byte package AES key, without inspecting the restricted consumer body.
- `2026-09-24-r8-005-device-login-material.json` ? exact original P-256 device public-point encoding and 32-byte authorization.challenge/launchNonce contract; returned server envelope fields still pending.
- `2026-09-24-r8-004-auth-key-envelope-transport.json` ? recovers host-side `LWKE1` key-envelope/auth-ticket framing, canonical payload encoding, expiry/magic fields and login device-public-key binding; agreement payload semantics/key still pending.
- `2026-09-24-r8-003-lwbp2-package-layout.json` — recovers the exact encrypted LWBP2 package field layout, AAD, package SHA/build checks and AES-GCM argument ownership without touching SB-79.
- `2026-09-24-r8-002-clean-working-tree.json` — reconciles the pending R7-156 correctness rollback, removes known rebuild-only Map deviations, and discards abandoned finder probes without claiming original parity.
- `2026-09-24-r8-001-strict-parity-direction-reset.json` — owner direction reset to whole-program one-to-one LWBridge 0.3.1 parity.
- `../../docs/strict-parity-recovery.md` — current product directive.
- `../../docs/lwbridge-parity-matrix.md` — current completion matrix.
- `../../docs/implementation-handoff.md` — current continuation state.
- `../../BACKLOG.md` — current work queue.

The former 47-case Home/Map matrix remains historical evidence for the reconstructed behavior it tested. It is not R8 whole-program completion authority.

## Important historical evidence retained

- `2026-09-24-r7-155-railway-v21-negative-population.json`
- `2026-09-24-r7-154-railway-v21-status-hygiene.json`
- `2026-09-24-r7-153-supplies-population-recheck.json`
- `2026-09-24-r7-152-ghost-population-recheck.json`
- `2026-09-24-r7-151-v21-dispatch-fast-scan.json`
- `2026-09-23-r7-acceptance-matrix-r7149.json`
- `2026-09-23-r7-direct-train-list-speed.json`
- `2026-09-23-r7-train-list-no-jump-auto.json`
- `2026-09-22-r7-map-owner-workflow-corrections.json`
- `2026-09-22-r7-map-correctness-multiserver-speed.json`
- R6-039 through R6-046 protected package/crypto recovery evidence

These files remain valid for their recorded observations, source identities and experiments. They do not automatically establish that the tested implementation matches the original LWBridge algorithm.

## Reference authority

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Reverified on 2026-09-24 before R8-001.

## Retention policy

Historical evidence is not deleted or rewritten merely because product direction changed. Preserve old performance experiments, regressions, owner customizations and feature retirements as provenance.

When an old evidence file describes a feature as intentionally removed or a custom strategy as production behavior, treat that as a fact about that checkpoint only. Current product authority comes from the R8 strict parity documents.
