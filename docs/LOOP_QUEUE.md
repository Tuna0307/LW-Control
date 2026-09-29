# Loop work queue

Only the project lead should normally change which work item is `READY`.

The loop worker may change its completed item from `READY` to
`AWAITING_REVIEW` after validation, commit and push.

| Work item | State | Work-item file | Notes |
|---|---|---|---|
| LWB317-UI-001B | READY | `docs/work-items/LWB317-UI-001B-shell-navigation-visual-baseline.md` | Runtime visual shell/navigation baseline only |
| LWB317-UI-001C | BLOCKED | not created yet | Project lead decides after reviewing UI-001B |
| LWB317-UI-002 | BLOCKED | not created yet | First page-level inventory; scope depends on UI-001B evidence |

## State meanings

- `READY` — worker may execute now.
- `IN_PROGRESS` — optional informational state while the worker is active.
- `AWAITING_REVIEW` — worker finished; project lead must review.
- `BLOCKED` — worker must not start.
- `ACCEPTED` — project lead accepted the result.

There should normally be only one `READY` item.

