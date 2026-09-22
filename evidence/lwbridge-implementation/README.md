# LWBridge implementation evidence index

This directory intentionally retains chronological machine-readable evidence. Do **not** infer current project status from an arbitrary older file.

## Current entry points

- `2026-09-22-r7-current-evidence-index.json` — curated current evidence grouped by Home, Map Data, and Shared/Release, with SHA-256 identities.
- `2026-09-22-r7-acceptance-matrix-r7145.json` — current 47-case acceptance matrix after the R7-145 documentation/evidence self-audit.
- `2026-09-22-r7-doc-evidence-self-audit.json` — R7-145 structural, performance, feature, and validation audit.
- `2026-09-22-r7-supplies-population-recheck.json` — newest population-gate evidence.
- `2026-09-22-r7-normal-user-restart-walkthrough-autolaunch.json` — current built-Release zero-argument navigation/restart proof with owner auto-launch safely suppressed/restored by the external verifier.
- `2026-09-22-r7-native-transition-matrix.json` — current point/march transition evidence.
- `2026-09-22-r7-map-correctness-multiserver-speed.json` — current Map correctness/performance evidence.

For human-readable grouping, start with:

- `../../docs/tabs/home.md`
- `../../docs/tabs/map-data.md`
- `../../docs/tabs/shared-release.md`
- `../../docs/external-audit-guide.md`

## Retention policy

Historical evidence is not deleted just because a later checkpoint supersedes its status statement. Older files remain useful for source identity, reproduction, regression history, and proof composition.

When an older file says a feature is pending but the current matrix says it passed, treat the older statement as historical context and follow the later evidence referenced by the matrix.
