# Daybreak / specialist handoff — current

**Current through:** `LWB-R7-145`, 2026-09-22.

No specialist is currently assigned. Ordinary Home/Map implementation is not waiting on Daybreak.

## Only current protected-contract blocker

Acceptance E02 remains `blocked_implementation`: original Treasure `claimTreasures` protected scope filtering/order, lucky-slot scheduling, scout-slot selection/reservation, and exact terminal batch-state enumeration remain unrecovered.

The historical SB-79 operation targeting the protected loader/consumer was rejected by the environment and **must not be replayed, rerouted, repackaged, delegated, or inferred around**.

`docs/daybreak-escalations.md` retains a proposed metadata-only Rust/serde reconstruction method. It is recorded as **retained, not approved** and is not an active assignment.

## If specialist work is considered later

Follow `AGENTS.md` section 6. A specialist request must identify one exact missing contract, source/build/hash, permitted methods already tried, evidence paths, limits, and return criteria. A DB label or historical difficulty is not authorization.

## Current sources

- `docs/daybreak-escalations.md` — restriction/escalation register.
- `docs/lwbridge-map-scan.md` — cumulative Treasure/Map recovery ledger.
- `evidence/lwbridge-implementation/2026-09-20-r7-treasure-claim-offline-contract.json`
- `evidence/lwbridge-implementation/2026-09-20-r7-treasure-claim-frontend-status.json`
- `docs/external-audit-guide.md`

Historical specialist catalogs remain under `docs/reviews/` and Git history.
