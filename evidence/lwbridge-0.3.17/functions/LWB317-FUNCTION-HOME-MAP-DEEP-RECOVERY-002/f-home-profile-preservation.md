# Milestone F — Home/profile lifetime preservation

F revalidated the accepted RECOVERY-003 lifetime/ownership behavior against the
current Release binary rather than reopening it speculatively.

## Executed non-live checks

- `--profile-runtime-owner-check`: PASS;
- `--home-campaign-lifecycle-check`: PASS;
- `--map-auto-scan-campaign-check`: PASS;
- `--map317-restart-check`: PASS.

The Home lifecycle proof covers pending/repeated operations, Close during Start,
stale identity, native-start rejection followed by a fresh retry identity, profile
scope mismatch, recovery, shutdown, persisted desired-running state and fresh-host
startup reconciliation.

## Preserved contracts

No new counterexample was found against:

- selected game-root/profile lifetime;
- global Auto Launch intent/confirmation semantics;
- profile-scoped Auto Reconnect;
- Auto Scan semantic rebase/config ownership;
- exact shared runtime ownership and replacement fencing;
- readiness admission;
- committed Stop behavior;
- restoration/cleanup protections;
- restart reconciliation.

## Result

F requires no product change. RECOVERY-003 R3-01 through R3-05 remain the accepted
offline/inert behavior and this campaign adds no fallback or alternate ownership
path.
