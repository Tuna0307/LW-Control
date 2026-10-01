# LWBridge 0.3.17 master backlog

This is the current queue. Historical 0.3.1 backlog content is archived under
`docs/archive/lwbridge-0.3.1-management/BACKLOG.md`.

## Current project-lead queue — 2026-10-01

1. `LWB317-UI-COMPLETE-001`: complete recoverable UI/UX across the shell and all
   eight pages, with Home/Map first, isolated preview QA and no game control.
   No fixed elapsed-time stop; checkpoint completed milestones.
2. Independently review the worker and outstanding Map closeout, retaining each
   unvalidated/blocked scope explicitly.
3. Assign a separate bounded Home native lifecycle contract/integration task.
4. Close original visual parity only with legitimate reference state evidence.

Owner clarification: no login UI; recover required auth-related local dependencies
within assigned feature scope. No product fallback. Retire the existing
selectable `--legacy-ui` path in a separate bounded host/package task, preserving
historical source/evidence and the canonical production path.

Use the current parity matrix and takeover audit for completed implementation
versus remaining validation; the phase checklists below are broad historical
milestones, not evidence that the existing Map implementation is absent.

## Scope exception: login/auth/licensing

The new clone does **not** recreate LWBridge's original login/account/licensing
system.

For login/auth/entitlement:

- document the visible access boundary only;
- do not rebuild login/account/licensing UI;
- do not reverse engineer credential, token, purchase or license-validation
  protocols;
- do not bypass authentication/entitlement;
- only trace minimal auth-produced state if a later in-scope feature proves it
  consumes that state.

## Phase 0 — project preparation

- [x] Delete abandoned `bot/rebuild-v1` branch locally and remotely.
- [x] Return active worktree to `research/offline-controller`.
- [x] Remove leftover ignored bot-rebuild projects and local repo Windows-MCP submodule.
- [x] Preserve old 0.3.1 management docs before resetting current authority.
- [x] Fingerprint LWBridge 0.3.17 reference.
- [x] Reset root README / AGENTS / task / backlog to 0.3.17.
- [x] Create current documentation index and worker-AI protocol.
- [x] Create fresh 0.3.17 parity matrix and feature ledger.
- [x] Create fresh UI-parity master plan.
- [x] Commit and push the preparation checkpoint.
- [x] Project lead assigns and accepts static frontend package inventory
      (`LWB317-UI-001A`).

## Phase 1 — UI parity

Work only from assigned bounded work items or a project-lead-authored Loop campaign.

- [x] Recover and hash the embedded 0.3.17 frontend package.
- [ ] Capture runtime visual shell/navigation baseline (`LWB317-UI-001B`) —
      `BLOCKED` at the out-of-scope auth boundary; boundary evidence captured.
- [x] Capture/reference every statically recoverable in-scope top-level 0.3.17 screen.
- [x] Inventory all visible in-scope navigation entries.
- [x] Inventory the statically recoverable nested tabs/cards/controls for the inventoried in-scope surfaces.
- [x] Record evidence-backed labels, defaults, disabled/loading/empty states and validation copy.
- [x] Record exact static themes, colors, typography, spacing and assets.
- [ ] Record post-auth runtime viewport/window behavior — static responsive CSS
      is captured, but the original post-auth window remains auth-blocked.
- [x] Record the login/locked boundary only; it is not reproduced.
- [x] Build the separate in-scope post-auth UI shell without fabricated backend results.
- [ ] Establish direct reference-vs-clone visual diff — clone before/after
      evidence exists, but the reference side remains auth-blocked.
- [ ] Close remaining runtime-observable parity gaps before function wiring —
      project-lead review/authenticated reference access is required first.

## Phase 2 — function recovery

Phase 2 is now open for **Map Data only**. Other subsystems remain blocked until
the Map Goal is reviewed.

- [ ] Complete `LWB317-RE-MAP-001` from `docs/GOAL_CAMPAIGN_PHASE2_MAP.md`.
- [ ] Recover/revalidate Map frontend/Tauri command surface.
- [ ] Recover/revalidate Map scan lifecycle/state machine.
- [ ] Recover/revalidate Map storage/query/export/marks/history/options.
- [ ] Implement/test the 0.3.17 Map local control/data plane.
- [ ] Produce current-client Map compatibility matrix.
- [ ] Live-prove Map connection/world-state/navigation/scan lifecycle against
      an assistant-owned current Last War session where live state permits.
- [ ] Live-prove at least one real completed Map acquisition path and query the
      resulting records.
- [ ] Project-lead review Map Goal before opening another subsystem.
- [ ] Build command/event inventory from later in-scope 0.3.17 surfaces.
- [ ] Trace later function families one at a time from UI to backend/runtime.
- [ ] Revalidate useful 0.3.1 findings against 0.3.17.
- [ ] Recover storage/config/database ownership where required by in-scope features.
- [ ] Recover game/provider/native contracts for in-scope features.
- [ ] Trace auth-produced state only when a target feature proves it needs that dependency.
- [ ] Do **not** reconstruct the original login/token/license/purchase system.
- [ ] Record every finding with 0.3.17 source identity and locator.

## Phase 3 — current-client compatibility

- [ ] Discover/fingerprint current Last War runtime.
- [ ] Map recovered in-scope 0.3.17 calls to current client contracts.
- [ ] Add only evidence-backed compatibility shims.
- [ ] Preserve observable in-scope 0.3.17 behavior.

## Phase 4 — live parity

- [ ] Define per-feature live acceptance checks.
- [ ] Prove assistant-owned lifecycle.
- [ ] Live-prove functions individually.
- [ ] Compare visible state/results to 0.3.17.
- [ ] Close remaining UNKNOWN/BLOCKED in-scope rows.

## Release rule

No feature is complete because a button exists, a command returns success, or an old 0.3.1 path worked.

Completion requires recovered 0.3.17 in-scope behavior plus appropriate validation.
