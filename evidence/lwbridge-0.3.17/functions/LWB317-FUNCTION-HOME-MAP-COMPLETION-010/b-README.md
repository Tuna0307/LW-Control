# Checkpoint B — LEAD009R3-01 correction (2026-10-09)

Defect: `RetireIncompleteAdoption` called `StopLeaseTimer` (service-wide, unconditional)
before its ownership check, so a late-completing old adoption disabled the successor
session's lease timer (and bumped `leaseGeneration`, silencing any in-flight renewal).

Fix (src/LWBridge.Desktop): lease-timer ownership = (session, challenge);
`StopLeaseTimer(expected...)` disposes the timer / bumps the generation only for the
exact owner; `StartLeaseTimer(session, nonce)` refuses to start for a non-owner and a
late-started orphan retires itself; adoption attempts carry a registration serial
(`LWBridgeControlPipeRegistry.Register` -> serial; `Unregister(id, serial)`;
`CancelLaunchBinding(id, serial)`), so a late attempt can remove only its own
registration/route. Adoption retire no longer cancels a registration it never created.

Evidence:
- adoption-stop-start-fixed.json — lead's immutable inverse probe (unchanged) now 2/2
  match (`adoption-stop-start-negative.json` stays the pre-fix 1/2 witness).
- b-adoption-check.json — native check 26 assertions: 6 new held-adoption cases
  (released/faulted/cancelled x before/after new Start) assert successor timer present,
  lease RENEWAL continues, successor registration/state intact, no old-owner lease write,
  one launch/one stop; plus registration-scoped retirement.
- Mutation: forcing the pre-fix unconditional timer stop makes the new check fail at
  OverviewAdoptionChecks (late-retirement disabled successor timer); fix restored.
No real game launched or terminated (controlled helper/process seams only).
