# Team workflow - Overview startup and automatic reconnect

Owner-visible O04/O05 acceptance passed on 2026-09-12 under `LWB-OVR-011`. [AGENTS.md](../AGENTS.md) is mandatory. The active work is now **Map Data → Player City (`city`)** only; preserve the accepted Overview lifecycle and do not begin the next Map Data type until Player City works end-to-end with durable evidence.

## Current target

Recover and implement original startup/reconnect behavior without guessing. Startup ON must reuse the proven owned Overview lifecycle once at normal app startup; startup OFF must not launch or take control. Reconnect ON must follow recovered eligibility/timing/update/maintenance semantics and must never undo an intentional Close; reconnect OFF must cancel/suppress future recovery. Resource/Monster work stays deferred.

## Worker prompt

```text
Work in C:\Users\chimw\OneDrive\Desktop\Github\LW-Control. Read AGENTS.md, task.md O04/O05, the accepted LWB-OVL-003 checkpoint and current source/evidence. Preserve the accepted manual Launch -> injected ready/message -> Close lifecycle. The owner explicitly selected the next feature: implement Open games at startup and Automatic Reconnection exactly as the original evidence supports. Reverse-engineer first. Recover original startup reconcile trigger/default/suppression and the reconnect recovery service eligibility, state transitions, waits/backoff/retry/update/maintenance/manual-close behavior before production implementation. Do not invent values; unknowns stay blocked. Reuse the proven OverviewLifecycleService only where contracts match. Add isolated regressions, automatic technical evidence, commit/push/verify coherent checkpoints, then prepare a beginner live test. Do not start Resource/Monster or another feature.
```

## Continuity

Continue direct dependencies across checkpoints without routine PM stops. The owner explicitly defined `ok continue` as permission to advance through the agreed queue. O04/O05 owner-visible verification is complete under `LWB-OVR-011`; proceed with Map Data in strict type order, beginning now with `city` → `resource` → `monster` → `truck` → `railway` → `dispatch` → `ghost` → `treasure`. Do not advance from one Map Data type until that type works end-to-end and has durable evidence. Stop only for necessary owner-visible verification, a real external blocker, or an evidence-backed specialist escalation. Do not rerun the accepted manual Overview test unless required by a changed lifecycle path. Existing operation-specific restrictions remain in force.
