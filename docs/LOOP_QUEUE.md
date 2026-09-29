# Loop work queue

Only the project lead should normally change what is authorized here.

## Active authorization

| Campaign / work item | State | File | Notes |
|---|---|---|---|
| LWB317-UI-CAMPAIGN-8H | ACTIVE | `docs/LOOP_CAMPAIGN_8H.md` | Pre-authorized UI-only campaign; stop before function reverse engineering |
| LWB317-RE-* | BLOCKED | not assigned | No gameplay/backend function recovery in this campaign |
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
