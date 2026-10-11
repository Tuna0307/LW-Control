# MAP-SCAN-METHODS-008 — accurate efficient scanning for every type and combination

Current continuation, 2026-10-12: delivery `473d6483` independently reviewed;
see `docs/reviews/2026-10-12-MAP-008-LEAD-REVIEW.md`. Preserve mounted MAP-007 fixes
and all-spatial deferred restoration. Fix accuracy-auditor LEADMAP008-01/02,
revalidate source/run-owned archive qualification without overwriting history,
then finish finite ready comparison gates below. No repeatable Fast advantage
or whole-Map acceptance has been established. Same branch/PR and live authority.

ASSIGNED by the lead after owner discussion, 2026-10-11. Repository:
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Use existing checkout/branch **codex/map-action-providers-007**, PR #9.
Submitted product checkpoint: fbbfa98305fc241df78dfddb7b092830920ce834.
Fetch/read the lead review commit first. No new branch/worktree, no research merge.
PR #9 must remain unmerged until corrections and lead review; accepted main and
v0.6.0 remain preserved. Update final PR title/body around its final combined scope.

## Owner goal and authority

Owner wants a verified usable scanner before personal testing. Latest owner
clarification, 2026-10-11: Truck and ten servers were examples, not scope limits.
Cover City/player, Resource, Monster, Truck, Railway, Secret Task/Dispatch, Ghost
and Treasure, each individually, mixed selections and all selected together,
including multi-server cycles. Individually fast methods may be inefficient or
incomplete when combined. Optimize the complete selected workload, not just Truck.
Earlier results also age while later servers are scanned. Establish real Normal/
Fast behavior and accurate fresh records. Camera movement is a question to solve,
not a proven requirement or assumed sole bottleneck. Passing a scan or producing
a plausible count alone does not close performance or accuracy acceptance.

Require the fastest accuracy-qualified route among credible supported alternatives
actually investigated and tested for each representative workload. Absolute global
optimality and perfect recall without an independent oracle cannot be established
by finite benchmarks. Never claim that no possible method could outperform it.
Explain tested conditions, rejected alternatives and unresolved proof limits.

Read AGENTS.md, the functional replacement direction, current Map status,
MAP_006_LEAD_ACCEPTANCE_2026-10-11.md, MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md,
MAP_007_ACTION_PROVIDER_HANDOFF.md and reviews/2026-10-11-MAP-007-LEAD-REVIEW.md.
Faithful workflows/results and reliability matter. Tested own acquisition design
is allowed and must be labelled. Original encrypted traversal is not an automatic
blocker or authority for an invented method. Work solo: no subagents/other AI chats.

Bounded passive scans, single-game Home lifecycle and server switching/Auto return
remain authorised after current compatibility, isolation, exact owner identity,
backup and restoration checks. Use available Windows-MCP/Remote Desktop Commander
when needed. No live claims, plunder, sharing, troop marches, spending, updater or
protected-service access. Do not arm action jobs. Ghost positive and full seasonal
Treasure stay deferred; do not wait for them. Respect explicit pauses/real denials.
Do not stop/change an unrelated game session or manipulate owner networking.

## A — finish the two reviewed MAP-007 corrections

Fix LEADMAP007-01 and -02 before larger scanner changes. New preview/preparation
operations must release their own busy state on retirement, reject obsolete tab/
server/profile/API responses and never clear a newer operation's busy/result.
Verify actual mounted controls with held success/rejection, leave/return, profile/
server replacement, and successor overlap. Preserve the immutable negative JSON
and old probe; write a corrected adapter/proof if component structure changes.
Preparation/preview is not an executed Ghost attack or Treasure reward.

## B — establish the actual acquisition baseline

Reproduce LEADSCAN008-01 through actual production engine/provider boundaries.
Normal requests 8, Fast 20, but current engine serially awaits captures and both
use the same full-world path. Existing names FastCity/scanMode fields are not proof
of speed. Trace every actual use of mode/concurrency down to game request/response.
Freeze the current source/method identity, useful negative records and existing
genuine receipts. Use compact metrics; do not log unbounded raw packages into Git.

Separate server selection, camera position, view/LOD request, object acquisition,
detail hydration, dedupe, staging and publication. Identify which steps actually
consume time, and which depend on mutable shared client state. Baseline telemetry
must include elapsed acquisition/total cycle/switch/return time, attributable Lua
commands, waits/retries, verified footprints, source identities and capture age.
Network packet totals must remain unmeasured unless properly attributable.

## C — investigate the two distinct camera/server questions

1. For each scan kind, can a supported current-client request retrieve its complete
   target-server population by server ID without changing active server or camera?
2. If server context must change, can complete acquisition avoid camera sweeping,
   use a complete official list, or use larger/fewer fresh region requests?

Inspect actual admitted-client producers, list scope/pagination/caps, serializers,
callbacks, current scan Lua and known region/list paths. Recover legitimate field
and response semantics before implementing a candidate. A list may cover alliance,
match/event or home-server data rather than all Trucks visible on the target map.
Do not infer a universal fetch-all from a fast empty/cached response. No invented
endpoints. Read original/research tools with git show only where useful; revalidate
old v22/v19 conclusions against the admitted build rather than claiming old proofs
establish current v24 capability.

Existing 8.065-second direct-AOI timeout and incomplete official-list trial reject
those particular attempts, not every possible direct request. Do not repeat the
same failed trial unchanged; test a materially different, source-supported hypothesis.
Consider per-kind and combined acquisition with no Truck-only priority. Investigate
camera restoration/no-visible-movement independently of speed. A supported complete
list may be useful for one kind while region acquisition remains necessary for
another. Evaluate a hybrid route rather than assuming one method fits every kind.

Trace which selected kinds share spatial requests, responses, hydration and
mutable client state. Compare separate sweeps with a single shared fresh sweep
that projects all selected kinds, plus any source-supported complete-list routes.
Avoid repeated acquisition of the same region merely because more boxes are ticked.
Do not reuse an old cache as fresh coverage or merge records from the wrong server.
Record why each type is assigned its route and how combined scheduling avoids
starvation, ownership conflicts, redundant requests and lost type populations.

## D — define accuracy gates BEFORE selecting the winner

A candidate qualifies only if it preserves required population scope, freshness,
fields and cleanup. For each viable route record:

- Exact game build/profile/session, queried target server and source scope; distinguish
  native response server from normalized record.serverId and object ownerServer.
- Supported type population and verified area union OR an independently established
  complete list scope, including paging/caps and known exclusions.
- Fresh response identity/timestamp/callback, not previously cached data relabelled.
- Type-specific stable identities and duplicate/missing/extra IDs; verify required
  native fields for each kind (including position, owner, level/quality, goods,
  protection/expiry or event state where applicable). Presence alone does not
  establish that a field's value or a decoded type is correct.
- Independent sampled native region/list/record witnesses where possible; union
  size and total row count alone do not prove all objects were returned correctly.
- Time-sensitive expiry/movement/spawn differences bracketed by observation time,
  never hide discrepancies by forcing counts to match or excluding inconvenient rows.
- Exact camera tile/LOD/FOV/aspect and any mutated manager index/flags restored;
  Stop/failure leaves no publication, subscriptions or requests from the retired run.

Keep observed-coverage claims separate from perfect world recall. If independent
ground truth cannot be obtained for some field/coverage, label its proof limit.
Do not silently replace a full scanner with a faster partial list. Preserve the
last completed data on failed/cancelled trials. Do not add a fake success/fallback.

Build independent representative native witnesses per available kind, across
sampled regions and density/LOD differences. Compare individual versus combined
selection per kind, with time-bracketed witnesses to account for moving/spawning
objects. Test omission of known witnesses, truncation, wrong-server data, stale
callbacks, duplicate identities and dropped kinds: each must be detected. A
10,000-AOI coverage counter does not alone establish correct object acquisition.
An implausibly small population requires investigation, not completion by count.
Use explicit correctness tolerances and explain dynamic differences; do not tune
tolerances after seeing failures. Do not assert exact recall from samples alone.

## E — implement meaningful Normal/Fast behavior safely

Correct the dead mode setting through the actual request path. Normal should be
the lower-load reliable strategy; Fast should provide an actual supported higher-
throughput strategy (batching, pipeline, request count or measured pacing) with
equivalent intended coverage. Do not artificially slow Normal solely to create a
marketing difference. Do not blindly issue 20 parallel camera mutations sharing
one callback/index/response flag. Prove request-scoped result ownership first.

Recovered 8/20 host limits are not a mandate to corrupt shared game state. If the
admitted game requires serialized view operations, choose and document a tested
functional mode distinction; keep concurrency telemetry truthful about actual
in-flight operations rather than merely echoing the requested maximum. No silent
identical modes. Retain visible familiar controls and saved mode semantics.

## F — finite genuine comparison and multi-server proof

Use the actual packaged application/native providers for successful candidates.
Controlled helpers alone do not establish a speedup or fresh coverage. For viable
methods/modes obtain repeated representative measurements, alternate order and
report median/range, not a favorable single run. A bounded sampled rejection is
enough for candidates that demonstrably fail an accuracy gate; do not run an entire
world repeatedly with a route already known incomplete.

Create a workload matrix covering:

- All eight individual type selections through production planning/ingestion and
  controlled adversities; genuine positive timing/accuracy for every currently
  available kind, not a Truck-only benchmark.
- City + Resource + Monster, Truck + Railway + Dispatch, a cross-group selection
  such as City + Truck, and all six currently available kinds together.
- All eight selected together through actual production controlled boundaries;
  positive live Ghost and full seasonal Treasure remain owner-deferred. Mark their
  live performance/accuracy unknown until available, rather than counting empty
  responses, fixture rows or historical observations as a fresh positive.
- Representative individual and mixed selections in bounded two/three-server
  Auto cycles using reachable admitted targets (e.g. previously verified
  2212/2213 if still valid). Do not visit 2000..2010 merely because the owner used
  those numbers as an example. Compare modes on more than Truck alone.

Exercise all 255 non-empty eight-kind selections through a compact production
planner/ingestion checker if that boundary is shared and supports meaningful
assertions. Do not substitute 255 repetitions of the same implementation-derived
expectation for adversarial or genuine mixed-workload proof. Full live sweeps of
all 255 combinations are unnecessary: choose representative combinations from
actual shared and distinct acquisition paths and explain that selection.

For each measured workload record total and per-kind acquisition/hydration/
publication time, switch/return overhead, median/range, oldest result age, verified
scope and sampled identity/field differences, attributable requests, retry/error
counts, CPU/memory pressure and camera disturbance. An unmeasured metric remains
unmeasured. A shortcut independent of active server must prove its actual target
data, not just accept a serverId argument. Compare mixed scans with individual
scans and explain request reuse, additional detail cost and type-specific limits.

Stress the combined pipeline: repeated bounded cycles, maximum selected kinds,
dense populations, slow/partial replies, safe queue backpressure, cancellation
during acquisition/hydration/publication, late replies and server/profile changes.
Check bounded memory/queue growth, UI responsiveness, accurate progress, per-kind
completion, cancellation latency and preservation of last published datasets.
Unsafe or unavailable cases use labelled controlled providers; real throughput
and freshness claims require the genuine provider. Keep live stress bounded and
stop on restoration/identity failures, not by weakening those gates.

Select the fastest qualifying method for the tested workload. If the current
route wins, show why other credible supported routes fail scope/freshness or time;
do not call a quick partial list the winner. Say best tested under specified
conditions, not globally fastest. Deploy a faster route only after successful
Stop/cancellation/timeout/failure/profile/session/native-state restoration tests.
When no viable supported optimization is found, return the real mode correction,
finite method evidence and exact remaining edge; do not report optimization solved.

## G — integration, release candidate and loop

Preserve accepted Home and six kinds, query/export/marks/persistence, stable Connected
polling and corrected Auto Stop-before-return. Exercise Stop during acquisition,
late result after cancellation, server change/loss, profile replacement, manual/Auto
overlap, incomplete coverage, duplicate/ambiguous replies and successor ownership.
Use controlled adversity for unsafe/rare branches and bounded real passive positives.

Applicable final gates: canonical frontend check/build/check:production-build;
Desktop default Release suite; isolated named-pipe recovery probe; fresh Desktop
Release build; new scanner/mode/method distinction and affected MAP-007 proofs;
mounted/extracted EN/light and JA/dark/narrow affected controls; git diff --check.
One final source-identified Windows ZIP after useful code settles, extract verify
source/EXE/UI integrity and no private game data/scripts/credentials. No ZIP per fix.
End with exact task session Stop/Close, original script hashes restored, no task
process/journal/requests/subscriptions, Auto disabled and zero newly armed jobs.

Diagnose -> supported fix -> distinguishing retest -> self-review -> coherent
checkpoint -> next ready item. Continue ordinary failures without relaying each
one. Keep one compact queue, preserve failed attempts and actual measurements.
Do not substitute more documentation for a remaining demonstrated defect.

Push normal commits and verify direct remote SHA. Keep one existing PR #9 and
update its title/description for the final combined source. Report corrected IDs,
per-type/combined method selection and rejected alternatives, real Normal/Fast
and multi-server timings, workload matrix and stress results,
accuracy proof limits, changed user behavior, cleanup, candidate ZIP and simple
owner test steps. Return READY_FOR_LEAD_FUNCTIONAL_REVIEW only for gates that
actually pass; otherwise PARTIAL with exact remaining capability. Lead independently
reviews/merges/publishes; owner personal testing follows this implementation proof.
