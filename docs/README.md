# Documentation index — LWBridge 0.3.17

## Current authority

Read in this order:

1. [../AGENTS.md](../AGENTS.md)
2. [../task.md](../task.md)
3. [strict-parity-recovery.md](strict-parity-recovery.md)
4. [lwbridge-project-status.md](lwbridge-project-status.md)
5. [lwbridge-parity-matrix.md](lwbridge-parity-matrix.md)
6. [lwbridge-feature-ledger.md](lwbridge-feature-ledger.md)
7. [lwbridge-ui.md](lwbridge-ui.md)
8. [PROJECT_LEAD.md](PROJECT_LEAD.md)
9. [AI_WORK_PROTOCOL.md](AI_WORK_PROTOCOL.md)
10. [LOOP_WORKER_PROTOCOL.md](LOOP_WORKER_PROTOCOL.md) — required for Loop mode
11. [LOOP_QUEUE.md](LOOP_QUEUE.md) — current Loop authorization
12. [LOOP_CAMPAIGN_8H.md](LOOP_CAMPAIGN_8H.md) — accepted static UI campaign
13. [GOAL_CAMPAIGN_PHASE2_MAP.md](GOAL_CAMPAIGN_PHASE2_MAP.md) — active Map-only Phase 2 Goal
14. [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)
15. [implementation-handoff.md](implementation-handoff.md)
16. [../BACKLOG.md](../BACKLOG.md)
17. [LEGACY_0.3.1_INDEX.md](LEGACY_0.3.1_INDEX.md)

## Reference authority

Target:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Current phase

**Map function recovery awaiting review; next task completes all recoverable UI/UX.**

The exact static frontend package baseline and Phase 1 UI campaign are accepted.
The canonical UI is already the normal Desktop frontend. The ordered Map
campaign has returned `AWAITING_REVIEW` with durable implementation/live evidence
and explicit remaining gaps; do not replay its old queue automatically.
The project-lead takeover now assigns UI-only shell/eight-page state and
interaction coverage under `work-items/LWB317-UI-COMPLETE-001.md`, with Home/Map
first and milestone checkpoints rather than a fixed clock deadline. This does not open another backend
family. Direct original post-auth pixel comparison remains blocked by legitimate
reference access and is not fabricated. Start with
`reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md` for the takeover audit.

All non-Map gameplay/backend function families remain blocked until the Map Goal
is closed. Auth/login/licensing reconstruction remains out of scope.

## Explicit scope exception

The original LWBridge login/account/licensing/entitlement system is **not being recreated**.

Necessary auth-related local dependency research is permitted under the owner's
2026-10-01 clarification in `AGENTS.md` section 6. Login/account/licensing UI is
excluded. Use supplied artifacts and authorized access; do not circumvent
original access controls. Earlier blanket research exclusions are superseded.

## Historical 0.3.1 material

The repository deliberately retains older research:

- `docs/reviews/`
- `docs/tabs/`
- `docs/ui-reproduction/`
- `docs/lwbridge-map-scan.md`
- `docs/lwbridge-overview-recovery.md`
- `docs/lwbridge-injection.md`
- `evidence/lwbridge-0.3.1/`
- `evidence/lwbridge-implementation/`
- older Last War/current-client evidence

These are **historical research inputs**, not current 0.3.17 authority.

Former high-level management docs were snapshotted under:

`docs/archive/lwbridge-0.3.1-management/`

Unless a document is listed in **Current authority** above, treat older top-level research documents as historical/contextual until the project lead promotes or revalidates them for 0.3.17.

Do not delete chronological research merely because the target version changed.

## New finding IDs

Use version-specific IDs so 0.3.17 work cannot be confused with R8/R9 0.3.1 history:

- `LWB317-PM-###` — project-management/baseline
- `LWB317-UI-###` — UI evidence/parity
- `LWB317-RE-###` — function/binary/runtime recovery
- `LWB317-LIVE-###` — live validation
- `LWB317-COMPAT-###` — current-client compatibility mapping
