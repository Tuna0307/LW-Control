# PM-027 — Map filter lifecycle lead review

Submitted delivery: `c99c7613c6320fe5659e96e4a7235896e9ad9770`.
Project-lead decision: **CHANGES_REQUIRED**, one bounded source-parity defect.

## Finding and consequence

The new `loadOptions` mismatch branch discards the lists and sets the browse
server to the payload's server. That matches one original expression but misses
the original subsequent scan-server synchronization. With scan server 321 and
an options(321) reply naming 322, the original makes option requests
`[321,322,321]` and settles at 321. Submitted production requests `[321,322]`
and settles at 322. An unexpected/stale provider server ID can therefore leave
the local UI browsing a different server than the source would at that point.

Evidence is executed original/current component behavior, not an inferred
backend schema. `evidence/lwbridge-0.3.17/ui/LWB317-PM-027/independent-cases.mjs`
normally exits 1 on this delivery; the preserved result reports one failure and
five passes. This case was not covered by the delivered suite.

Authority: exact `MapDataPanel-B4GXEND2.js`, SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
Options effect is UTF-8 bytes 34871-35650. The synchronizing effect at
33502-33682 compares `C.serverId !== R`, resets search/cache/page state and
sets R to C.serverId; its dependencies include both `[R,C.serverId]`.
Canonical `MapDataPage.jsx` has no equivalent resynchronization for this
options-only redirect. Recover the combined lifecycle before correcting it;
do not simply ignore all mismatched payloads or force every server to a fixed ID.

## Validated remainder and reviewer assessment

The worker's earlier independent reviewer found duplicate option reloads after
a delayed Clear/server transition. The lead considers that finding valid:
the original Clear updates parent scan state, and the corrected source-like
sequence `[322,321]` is covered and passes. Preserve that correction.

The superseding reviewer recommended PASS. The lead agrees with the verified
ordinary option validation, alliance sentinel separation, selective Clear
resets, Treasure initialization/storage/query behavior and action fences, but
does not accept the whole campaign because the additional redirect case fails.
No production code was changed by this review; a small R1 assignment is issued.

Lead independently ran the worker source recovery, filter lifecycle checker and
evidence validator; request lifetime (38 scenarios, 0 current failures), navigation
(17 searches, 0 current failures), exact original interactions (38/38), strict
integration (14/14) and historical filter/table/Checking/row/state replays.
All pass. Five extra lead checks pass: pending options success/failure after
backend loss and unmount, and obsolete rejection after a current server success.

Canonical check/build/package pass with the submitted fingerprints
`9c710c0c28448648769682aaf12073d2d1d2523b2ee96e5e1031fbed931a5bfd` /
`5a925d4c24e7f02e8056002b3c4b99d5d2c6000740dafdb0f07994d6e95b7e8f`.
The target EXE hash matches AGENTS.md; protected-WIP and diff checks pass.
Historical tests/evidence were not rewritten. The worker's lifecycle result
checker was rerun and generated no tracked result change.

The worker browser JSON and 375px Japanese/dark screenshot were inspected.
There was no fresh lead browser session. The original Treasure picker remains
a details/menu surface while canonical uses a select; exact menu-surface parity
is a separate remaining UI gap, not covered by this defaults/lifecycle acceptance
scope. No full Map or global UI/pixel/native acceptance follows from these checks.

## Continuation

Only `LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1` is assigned next: correct the
options-only redirect/server synchronization and verify related Clear/server/
request ownership. The parent remains CHANGES_REQUIRED pending lead review.
Do not restart the four milestones or begin scan polling/native/gameplay work.
