# Loop work queue

Only the project lead should normally change what is authorized here.

## Current authorization

| Campaign / work item | State | File | Notes |
|---|---|---|---|
| LWB317-UI-CAMPAIGN-8H | ACCEPTED | `docs/LOOP_CAMPAIGN_8H.md` | Static UI recovery accepted; direct original post-auth pixel validation remains blocked by the original auth boundary |
| LWB317-RE-MAP-001 | ACTIVE | `docs/GOAL_CAMPAIGN_PHASE2_MAP.md` | Map-only Phase 2 Goal; exact/static production plane implemented, bounded current-v21 live validation is the remaining campaign stage |
| Other LWB317-RE-* | BLOCKED | not assigned | Do not begin another subsystem until the Map Goal is closed |
| Auth/login/licensing reconstruction | OUT_OF_SCOPE | n/a | Boundary/dependency only; no recreation or bypass |

When the 8-hour campaign stops, the worker must change its campaign state from
`ACTIVE` to `AWAITING_REVIEW`, commit/push that checkpoint, and report
`WAITING_FOR_PROJECT_LEAD`.

## State meanings

- `ACTIVE` — project-lead-authored campaign may execute its internal ordered stages.
- `READY` — one bounded work item may execute now.
- `IN_PROGRESS` — informational state while a worker is active.
- `AWAITING_REVIEW` — worker finished; project lead must review.
- `BLOCKED` — worker must not start.
- `ACCEPTED` — project lead accepted the result.
- `OUT_OF_SCOPE` — intentionally not part of the reconstruction target.
