# Team workflow - Overview zero-open PM audit handoff

The owner-visible O04/O05 acceptance passed under `LWB-OVR-011`. The later strict Overview audit closes O01 under `LWB-OVR-013` and live-proves O06 repair/update/relaunch under `LWB-OVR-014`. [AGENTS.md](../AGENTS.md) remains mandatory.

## Current target

**STOP for PM audit. Do not start Map Data / Player City.** The O01-O06 Overview functional matrix is the current zero-open candidate. Protected-original bootstrap parity, extended event/update live validation and the full 47-case release matrix remain separately tracked and are not silently promoted by this checkpoint.

## PM audit prompt

```text
Audit the latest research/offline-controller checkpoint for Overview zero-open functional closure. Read AGENTS.md, task.md, BACKLOG.md, docs/lwbridge-overview-recovery.md, docs/lwbridge-feature-ledger.md and the LWB-OVR-012/013/014 evidence. Verify O01 Game Root edge-case coverage, O06 correlated repairRequired detection, profile_instances_update_and_restart response/safety semantics, startup-reconcile suppression of the false unmanaged-game error, the live interrupted-session repair -> exact restore -> fresh ready relaunch -> normal Close evidence, full deterministic/build/frontend/lifecycle checks, and clean Git/CI delivery. Keep protected-original parity and broader 47-case gates distinct from O01-O06 functional closure. Return defects or approve the checkpoint. Do not begin Player City.
```

## Continuity

The owner's latest instruction supersedes the older automatic Map Data transition: once Overview reaches zero open functional items, hand it to PM and wait. Player City remains the first queued Map Data type only after that audit and an explicit owner resume. Resource acquisition restrictions, including SB-97, remain unchanged.
