# Map — single current completion queue

2026-10-11. Whole Map: **PARTIAL**. MAP-006: **PARTIAL DEVELOPMENT CHECKPOINT**.
Owner requested tidy/audit before discussion. See
[lead checkpoint](reviews/2026-10-11-MAP-006-PARTIAL-LEAD-CHECKPOINT.md).
Latest permission: server switching and multi-server passive scan/Auto assessment
allowed; no worker live claims/plunder/sharing. See
[owner clarification](OWNER_DIRECTION_MAP_SERVER_SWITCHING_2026-10-11.md).
Scanner origin/alternatives/efficiency assessment:
[current method](MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md).

Owner requested all remaining Map tasks together under the faithful functional
replacement policy. Current task:
`docs/work-items/LWB317-MAP-COMPLETE-DELIVERY-006.md`.
Branch: `codex/map-complete-delivery-006`, above MAP-005 source `023e34d0`.

| Area | Current disposition / next requirement |
| --- | --- |
| Single-game Home | Released and preserved; affected regressions only. Two-game F-07 remains separate. |
| Stored City/Resource browsing + City export | Previously accepted on main. |
| Manual City/Resource scans | MAP-005 worker complete / PR #7 pending lead review. Source/package/receipt preserved; not reaccepted here. |
| Six remaining manual types | Saved mixed-run receipts have positive Monster/Truck/Dispatch/Treasure populations. Railway/Ghost are zero/unproven; final package/source and remaining controls need review. |
| Auto Scan | Saved same-server Truck cycle published 434 rows after an earlier empty run. Fresh controlled terminal/ownership checks pass; final acceptance/evidence reconciliation outstanding. |
| Row controls/navigation/inspection | Inventory each actual control, finish missing implementations and authoritative result evidence. |
| Treasure status/claims | Existing high-level providers unavailable; implement supported controller/results. Ask owner before live claims. |
| Scheduled Dispatch/Ghost/Truck | Finish preparation/execution/correlation/jobs/history; ask owner before live plunder/sharing/jumps. |

The [finite operation/result-gate matrix](MAP_006_OPERATION_MATRIX.md)
inventories visible and conditional controls against current production entry
points. The first MAP-006 safety corrections require Auto's exact owned scan to
reach a positively completed terminal phase before counting a server as done,
and prevent any Ghost job entering the Dispatch plunder transport. These are
controlled-test results, **not** whole Auto/Ghost completion.

MAP-006 holds the finite queue/differences ledger rather than duplicating historical
research. All expected Map features remain in scope; no hidden exclusions or fake
success. Owner now permits server switching and multi-server scan/Auto assessment.
Worker live claims/plunder/sharing remain excluded; owner will test them personally.
All code and controlled tests remain assigned.
Preserve MAP-005 PR #7 for independent review and incremental main delivery.

## Checkout lifecycle

Use the one current checkout. Keep main, research, submitted MAP-005 and active
MAP-006 while their work is unmerged. Do not delete either feature branch prematurely.
Lead retires approved merged branches/worktrees after preserving necessary artifacts.

## Continuation

Worker progress and earlier live receipts have been preserved; fresh native,
frontend and existing production UI checks passed. Current cleanup independently
verified: no game/clone processes, restored script hashes and no isolated journal.
The prior "no live work yet" continuation was stale and is superseded by the
dated lead checkpoint above. No new lead live experiment was made.

Owner-reported status flicker has a reproducible UI cause: five-second polls mark
the status pair unready, so Connected changes to Checking even with unchanged
connected native fields. Not fixed yet; inspect actual App callback/effect semantics
and retain truthful disconnect/error handling. Owner discussion precedes next work.
Whole Map, final package, acceptance/merge/publication remain incomplete.
