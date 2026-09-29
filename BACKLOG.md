# LWBridge 0.3.17 master backlog

This is the current queue. Historical 0.3.1 backlog content is archived under
`docs/archive/lwbridge-0.3.1-management/BACKLOG.md`.

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
- [ ] Capture runtime visual shell/navigation baseline (`LWB317-UI-001B`).
- [ ] Capture/reference every accessible in-scope top-level 0.3.17 screen.
- [ ] Inventory all visible in-scope navigation entries.
- [ ] Inventory every nested tab/card/dialog/popover that belongs to an in-scope feature.
- [ ] Record labels, defaults, disabled states and validation copy.
- [ ] Record themes, colors, typography, spacing and assets.
- [ ] Record viewport/window behavior.
- [ ] Record the login/locked boundary only if encountered; do not reproduce it.
- [ ] Build exact in-scope post-auth UI shell with no fabricated backend data.
- [ ] Establish repeatable visual-diff workflow.
- [ ] Close all visually observable in-scope parity gaps before function wiring.

## Phase 2 — function recovery

Do not start until the UI baseline is stable and the project lead opens the phase.

- [ ] Build command/event inventory from in-scope 0.3.17 surfaces.
- [ ] Trace functions one at a time from UI to backend/runtime.
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
