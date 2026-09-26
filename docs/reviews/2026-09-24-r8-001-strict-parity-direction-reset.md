# LWB-R8-001 — strict one-to-one parity direction reset

**Date:** 2026-09-24
**Scope:** project direction, documentation authority, parity classification
**Reference:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Owner decision

The project is no longer allowed to redesign LWBridge or treat a working reconstruction as completion. The end product must reproduce the original LWBridge 0.3.1 program one-for-one and must still work against the current Last War client.

Previous owner-specific removals, convenience features and performance substitutions are not current product authority.

## Repository changes

R8 introduces two canonical documents:

- `docs/strict-parity-recovery.md`
- `docs/lwbridge-parity-matrix.md`

Current planning/status/handoff/audit documents were rewritten around the parity goal. Historical R1-R7 reviews/evidence were preserved and given superseding notices where appropriate rather than rewritten.

## Important reclassifications

- The recovered frontend is the strongest exact-byte recovery, but the shipped rebuild still has boundary/product deviations.
- Home/Overview is a working equivalent reconstruction, not yet exact backend parity.
- Map Data acquisition is largely our reconstruction; original per-kind acquisition remains to be recovered.
- R7-151 Secret Task Quick Find is a rebuild-only deviation.
- R7-151 wide-FOV/68-request scanning is an optimization experiment, not original parity authority.
- City Excel export and Scheduled Plunder were previously removed but are now parity gaps because the reference contains those product surfaces.
- Auth/account flows were previously removed but are now parity gaps.
- The old 47-case Home/Map matrix is historical reconstruction evidence, not current completion authority.
- Whole-program surfaces outside Home/Map are now explicitly in scope.

## Protected package priority

`bridge-scripts.dat` plaintext recovery is P0.

Prior R6 evidence already establishes substantial package-key/envelope/device-key/KDF/AES-GCM architecture. Historical SB-79 records one operation rejected by a previous environment; it remains a restriction record, not evidence that the package is impossible to recover. The underlying question remains active through genuinely permitted methods.

## Validation

The reference executable was re-hashed directly on the development machine before this reset:

- size: 15,515,648 bytes
- SHA-256: `2A2DE09B35BB6A03F26B5E05F949F3AEA6215F294127E605D7D78481F855CDFF`

This checkpoint changes documentation/project authority only. Existing uncommitted R7 code/test work is intentionally preserved and not folded into this documentation commit.
