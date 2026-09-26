# LWB-R8-002 — clean pending R7 Map work and remove known deviations

**Date:** 2026-09-24
**Scope:** reconcile the 30+ pending GitHub Desktop changes left from R7-156 and the abandoned finder redesign discussion.

## Result

The pending work was split into two classes.

Kept and committed:

- R7-156 correctness rollback from the incomplete wide-FOV/68-request production path to the complete movement/AOI baseline.
- removal of the rebuild-only Secret Task Quick Find UI/API/backend surface.
- removal of the direct Truck/Railway no-jump Auto shortcut from ordinary production routing.
- Clear Map Data live-server resolver-gap hardening and deterministic regression coverage.
- matching frontend generator, generated assets, tests and historical R7-156 regression evidence.

Discarded:

- exploratory Dispatch native-finder probe code.
- exploratory Ghost Ops native task-list probe code.
- their temporary command-line test entry points.
- temporary live-probe additions that existed only for the abandoned LW Atlas-inspired redesign.

## Parity interpretation

This is cleanup, not one-to-one completion.

The movement/AOI scanner remains a temporary correctness fallback. It is not claimed to be the original LWBridge 0.3.1 algorithm. R8 P0 remains recovery of the original protected bridge scripts and exact Map implementation.

## Validation

- recovered frontend generator check: PASS.
- Release test project build: PASS, 0 warnings / 0 errors.
- deterministic desktop suite: PASS, `ok=true`, `failures=[]`.
- Quick Find production/test/tool surface grep: absent.
- exploratory Dispatch/Ghost finder probe references: absent.
- browser scripts requiring Playwright were not rerun because the local Node environment does not currently have the `playwright` module installed; this cleanup did not claim those checks.

## Git hygiene

The pre-cleanup state was saved under ignored `.codex-live` patch files before reconciliation. No unrelated committed history was rewritten.
