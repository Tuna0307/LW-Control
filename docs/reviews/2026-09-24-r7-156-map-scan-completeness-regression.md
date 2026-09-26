# R7-156 — Map scan completeness regression and correctness rollback

> **R8 parity note:** this checkpoint is retained as current-client regression evidence and a temporary correctness rollback. It does not establish the original LWBridge 0.3.1 scan algorithm. Under R8, the reference implementation still must be recovered before this path can be called one-to-one parity.

**Finding:** `LWB-R7-156`
**Date:** 2026-09-24
**Scope:** owner-reported Map Data undercount, Clear error, and unrequested Secret Task Quick Find.

## Result

The fast R7-151 wide/native shortcut is **not a complete map scan on current Last War v21**. It can finish 2,500/2,500 logical blocks while returning only a small fraction of the live population. The direct Train-list-only shortcut has the same completeness problem for Truck on the sampled v21 state. The ordinary production scan is therefore restored to the movement/AOI path for correctness.

The same live server 2212 comparison reproduced the owner's failure mode:

| Kind | R7-151+ wide/native shortcut | Movement/AOI baseline | Outcome |
|---|---:|---:|---|
| Player City | 93 rows, alliance-filter 33, 9.73 s | **6,727 rows, alliance-filter 99, 130.47 s** | wide path missed ~98.6% of City rows |
| Resource | **0 usable; authority guard failed** | **624 rows (572 full), 119.83 s** | wide path did not materialize authoritative Resource details |
| Monster | 71 rows, 10.54 s | **3,370 rows, 132.32 s** | wide path missed ~97.9% of Monster rows |
| Truck | **0 rows; positive-population guard failed** | **200 rows, 131.91 s** | direct list was not equivalent to world Truck population |

Counts are live snapshots, not fixed expected values. The important result is the completeness gap on the same client generation/server: the shortcut reported completion while population was orders of magnitude below the movement/AOI result.

## Source identity

Current-client compatibility remains pinned by `lwbridge-current-client-critical-anchors-3`: game SHA-256 `905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d`, xLua SHA-256 `d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45`, Assembly-CSharp SHA-256 `bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`. The shortcut comparison used detached Git commit `2009903`; the movement baseline is `24ab001` (R7-150) plus the R7-156 fixes described below.

## Diagnosis

The 68-request R7-151 path proved `_curViewIndex` AOI coverage, but that is not the same thing as proving that every City/Resource/Monster record in those AOIs has been materialized into the managers copied by the probe. Current v21 can therefore expose a geometrically complete AOI union with a population snapshot that is grossly incomplete. The old movement path forces the client through the world regions and allows the point/march managers to populate before records are collected.

The R7-148/R7-150 `GetTrainList(true)` shortcut also cannot be used as a blanket ordinary Truck completeness source on the current sampled v21 state: it returned zero where the world movement/AOI path found 200 Trucks. It remains useful as a bounded source/coverage diagnostic, but not as the ordinary full-map Truck replacement.

## Implementation impact

Production now restores the R7-150 movement/AOI acquisition behavior for ordinary full-world scanning and removes the Truck/Railway-only zero-AOI fast-path admission from the normal scan. Exact 2,500-block / 10,000-AOI completion and transactional publication gates remain unchanged.

The owner did not request a one-target Secret Task helper. The R7-151 `Quick Find Secret Task` UI/API/backend surface is removed rather than left as an extra product feature.

`Clear Map Data` remains a local-data operation. Its status refresh previously called the live-server resolver after deletion; a transient bridge resolver exception could therefore make a successful Clear surface as a generic action failure. Live-server resolution is now advisory for status/Clear, preserves the last known server, and has a deterministic regression test for a resolver exception after the server identity is known.

## Validation

Release build passes with 0 warnings / 0 errors. The full deterministic check suite passes with `failures=[]`, including the new Clear resolver-gap regression. Production/test/tool grep confirms the Dispatch Quick Find command/backend/UI identifiers are absent.

Current-v21 live proofs after the correctness rollback all complete 2,500/2,500 with zero failed/unread blocks: City 6,727; Resource 624; Monster 3,370; Truck 200. These are the acceptance numbers for this snapshot, not promises about future population.

## Limits and next optimization rule

The correctness path is slower, roughly two minutes per populated full-server scan in these measurements. That is preferable to a 6–10 second scan that reports completion with only 1–2% of the real population. Future acceleration may be reintroduced only after a same-snapshot A/B proves population equivalence against the movement/AOI baseline, or after an independently authoritative full-map source is recovered and proven to return the complete population.

Machine-readable evidence: `evidence/lwbridge-implementation/2026-09-24-r7-156-map-scan-completeness-regression.json`.
