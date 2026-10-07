# EXPIRY-CORRELATION-005 — independent lead review

Date: 2026-10-07
Reviewed delivery: `8f6730ef5e46fc602c6331d21e25d206b2e95ca4`.
Disposition: **ACCEPTED for the assigned expiry correction and bounded offline/static recovery.**

## Decision and fresh evidence

Inspected the actual A production/test diff, B inspector, raw managed instruction
records and parser tests, C validator, worker review and queued next assignment.
The production change is limited to conditional expiry admission and the public
preparation-unavailable explanation. No provider capability was enabled.

Fresh invocation of the packaged production host/helper, through the worker's
reflection script with its output redirected into this lead-owned directory,
agrees for all six assigned cases: positive-after, zero, negative and missing
expiry accepted; positive-equal and positive-before rejected. All four accepted
rows preserve raw JSON. Evidence: `lead-review-2026-10-07/expiry-results.json`.
An independent assertion over the fresh outcomes returned
`LEAD_EXPIRY_BOUNDARY_OK cases=6 preserved=4`.

Fresh execution also passed:

- `tests/map_expiry_correlation_parser_checks.py`;
- `tools/lwbridge317/validate_map_expiry_correlation.py`;
- decoded current-game body oracles: 4/4;
- blocker-boundary body checks: 4/4;
- complete production Lua safety checks: 3/3;
- `git diff --check 9da977d^..8f6730e` and direct remote verification.

Historical failing expiry evidence retains SHA-256
`77FB89FC7BF4325E5CE656DFC6451E5209A0666367322FF7ABE880424978A7D8`.
The worker's Release/frontend/package build records were inspected, not rerun by
this read-only lead review. Fresh reflection exercised the existing Release
assemblies; no live game, desktop control or provider transport was invoked.

## Recovery limits and retained discrepancy

B provides useful hash-gated managed static dataflow evidence: shared send/receive
key operand, request association, conditional accounting, reset and Lua conversion.
It is accepted as partial recovery, not Ghost execution or a globally durable job
identity. The inspector intentionally leaves modified operands unresolved.

The parser test's manually specified token fixture checks its local allow-list and
mutation rejection. It is not an independently generated decoder fixture proving
every named operand or dictionary operation. Generated wrappers and actual method
dataflow provide separate bounded support; unresolved calls must remain visible.
The unique `fuid` literal is supporting evidence, not a decoded ldstr mapping.
No general token inverse or direct Ghost response-field emission is accepted.

Fresh replay retains two string-reader discrepancies: host accepts/defaults the
numeric/malformed strings while the helper throws InvalidOperationException.
005 explicitly excluded parsing broadening; accepting its six-case correction
does not accept these discrepancies as equivalent behaviour. Recover actual
protected preparation input/output semantics in 006 before assigning a reader
correction. Public preparation remains unavailable, so this does not enable a
working feature with differing semantics.

LR-GHOST-EXPIRY-005 is closed for its assigned numeric/missing cases. This does
not accept the entire historical SEMANTICS-003 or DEEP-RECOVERY-002 campaigns.
Home/Map stays PARTIAL; Treasure/Ghost providers remain unavailable, no LIVE_PROVEN
status changes, and live/shared-desktop work remains ON_HOLD_BY_OWNER.

## Next authorized owner-relayed work

Assign `LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006`, starting from the latest
descendant containing this lead review. Prioritize exact-original payload location,
loader/crypto inputs and controller semantics. Do not repeat current-game package
decoding as if it were original bridge-controller recovery. No product enablement
or protected-service access is authorized by that assignment.
