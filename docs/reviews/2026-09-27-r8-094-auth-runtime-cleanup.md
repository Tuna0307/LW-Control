# R8-094 — correct AuthState runtime cleanup ownership

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT / CORRECTION
**Scope:** host-side runtime cleanup of persisted authorization ticket/package-key envelope and reconciliation of the R8-093 no-session branch. No stored artifact contents, private keys, or live auth requests are read.

## Correction

R8-093 initially described helper `0x14023CBB8` as a grace-clock reset.

That interpretation is wrong.

Direct disassembly plus constructor field mapping proves `0x14023CBB8-0x14023CC15` is a two-path cleanup helper for:

- `authorization.ticket`;
- `package-key.envelope`.

The corrected no-usable-session sequence is:

`SessionV2 restore reports no usable session -> best-effort ticket/envelope cleanup -> normal signedOut publication`

There is no grace-clock reset at this call site.

## Exact path ownership

The auth-service constructor creates exact path names:

- `authorization.ticket`;
- `package-key.envelope`.

The constructor copies their PathBuf values into the service object at:

- ticket PathBuf start `+0x178`;
- package-envelope PathBuf start `+0x198`.

The cleanup helper reads:

- ticket members `+0x180/+0x188`;
- envelope members `+0x1A0/+0x1A8`.

Those are the data/length members inside the two constructor-owned PathBuf slots.

## Cleanup behavior

At `0x14023CBB8` the helper:

1. loads the ticket path from `+0x180/+0x188`;
2. calls shared wrapper `0x1405B6540`;
3. if that wrapper returns an error object, consumes/drops the error and continues;
4. loads the envelope path from `+0x1A0/+0x1A8`;
5. calls the same wrapper;
6. likewise consumes/drops any returned error and returns.

R6-040 already source-locked `0x1405B6540` as the host path that reaches the file-delete primitive / `DeleteFileW`.

Therefore this helper is best-effort cleanup: failure to remove the first artifact does not prevent an attempt to remove the second.

## Caller ownership

Recovered direct callers of `0x14023CBB8` include:

- heartbeat error handling;
- supervisor session-error handling;
- supervisor no-session handling;
- login/auth projection;
- another auth-state-related path;
- a service cleanup wrapper;
- auth artifact ingestion.

This is shared auth-runtime lifecycle cleanup, not a helper dedicated to one transition.

The specific R8-093 no-session branch is exact:

1. call `0x14023CBB8`;
2. call normal `signedOut` emitter `0x14023855D`.

## Relationship to artifact ingestion

R8-004 already established that auth-response ingestion validates/writes/replaces authorization-ticket and package-key-envelope runtime artifacts and removes stale/invalid material.

R8-094 reconciles the concrete cleanup helper with that older evidence:

- invalid envelope ingestion reaches the established delete wrapper directly;
- the same ingestion function can also call the shared two-artifact cleanup helper.

This checkpoint therefore corrects ownership/semantics; it does not introduce a new protected crypto claim.

## Impact

R8-093's SessionV2 findings remain valid:

- v2-first restore;
- legacy fallback;
- version 2 writer/reader gate;
- centralized supervisor ownership;
- secure-storage decode boundary;
- decoded metadata -> role projection;
- heartbeat interval/service configuration.

Only the description of `0x14023CBB8` is superseded.

No production behavior changes in this checkpoint.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_auth_runtime_cleanup.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-094-auth-runtime-cleanup.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`
