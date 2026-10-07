# RECOVERY-003 independent second look — 2026-10-07

Recommendation: **no demonstrated in-scope blocker to scoped offline acceptance of
R3-01 through R3-05**. This is an independent source/evidence audit, not project-lead
acceptance. Home/Map live parity, protected providers and whole-clone acceptance
remain outside this recommendation.

Reviewed HEAD: `88591df03cd92f1d28b710a0d107a096266fd360`, branch
`research/offline-controller`. The starting working tree was clean. Read-only
scope was assigned by the lead; the only new file is this review. No build, native
check, UI/browser/game/helper execution, Git index/history change or live action
was performed by this reviewer.

## Sources and boundaries

Read `AGENTS.md`, `docs/AI_WORK_PROTOCOL.md`, current `task.md`, current Home/Map
tab/handoff status, RECOVERY-003 work item and the preserved
`2026-10-07-LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-002-LEAD.md`.
Compared relevant production/test changes against reviewed RECOVERY-002 commit
`59d3cde5192f03d39ae01580e05d1a6249158b20` and read the RECOVERY-003 checkpoint
design reports, inverse script and corrected EN attempt5 / JA attempt1 JSON packets.

The reference identity is the work item's LWBridge 0.3.17 SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
This reviewer did not independently hash the executable. Source adaptation and
offline execution are distinct from `EXACT_BYTES`, `EXACT_CONTRACT` and
`LIVE_PROVEN` original/current-game findings.

## Five corrections

- **R3-01:** `OverviewRuntimeFileOwnership.cs:17-50` opens an existing destination
  read/write with no sharing, validates bytes through that identity, then writes,
  truncates and flushes it. Missing destinations use CreateNew. Lifecycle lease
  refresh and cancellation publication call that guard at
  `OverviewLifecycleService.cs:1483-1536`. Foreign or unverifiable bytes cannot
  admit those writes. Strict UTF-8/duplicate-key ownership parsing remains at
  `:1641-1662,1724-1751`. The helper's corresponding mutation uses the same
  Windows identity transaction and bounded contention retry
  (`tools/run_overview_bridge.py:169-280`). No foreign-file overwrite path was
  found in these assigned mutations.
- **R3-02:** `OverviewRuntimeFileOwnership.cs:53-76` requests DELETE on the same
  exclusive opened identity, validates that handle's bytes and submits
  FILE_DISPOSITION_INFO before close. Lifecycle cleanup no longer validates one
  path identity then calls pathname DeleteFile (`OverviewLifecycleService.cs:
  1621-1638`). The test's replacement attempt occurs under the real guard,
  fails admission, and publishes the foreign replacement only after guard
  release (`HomeRuntimeFileOwnershipChecks.cs:30-53`). This is materially stronger
  than a second unlocked metadata read. No remaining replacement interval was
  found in these paths.
- **R3-03:** `MapScanStateMachine.cs:355-411` keeps cancelable admission and local
  durable cancellation but passes `CancellationToken.None` to provider Stop after
  that commit. The concrete current-client provider entry checks the caller token
  before signaling capture, then waits for its actual active task
  (`CurrentClientMap317ScanProvider.cs:174-195`); the changed handoff closes the
  reviewed abandoned-capture interleaving. The new test uses actual native
  service/store/process-lease plus a controlled run provider
  (`Map317NativeBoundaryChecks.cs:474-588`), holds terminalization and proves
  cancellation signal, retained lease, queued Start, repeated Stop and distinct
  next run. It does not execute the real capture provider. Existing best-effort
  provider exception handling is preserved, so local idle alone remains no claim
  of successful game-side termination.
- **R3-04:** `App.jsx:211-212,821-872` separates confirmed global preference from
  native per-owner mirror. New owner generations inherit preserved global intent;
  earlier same-owner success updates confirmation without replacing a later
  optimistic UI value. Latest rejection rolls back to the confirmed global value.
  Current-owner fencing precedes dispatch, acknowledgement and rejection. The
  distinguishing divergent/inverse/earlier-success-later-rejection and A/B/A
  callbacks are present in the extracted actual-production inverse suite and
  mounted packet source. No native-profile takeover regression was found.
- **R3-05:** actual Add/remove/type handlers pass optional operation intent
  (`MapDataPage.jsx:367-369,841-855,1037`); actual App applies the same intent to
  native drafts and preview persistence (`App.jsx:484-495`).
  `mapAutoConfig.js:92-110` rebases append/remove/set-type over acknowledged arrays
  and protects the last type. Coordinator successful acknowledgements retire
  covered operations before considering whether the response is the latest
  (`autoScanNativeCoordinator.js:65-86`). This addresses the earlier cap/no-op
  replay counterexample as well as initial hydration replacement. Failed intent
  remains queued and retired owners stop dispatching. No new concrete replay or
  scalar-merge regression was found.

## Corrected proofs inspected

Both corrected packets bind to product version
`1.0.0+d90f764aa2a721e6bf26952a81fb1a6161bec0d4`, matching canonical UI source
fingerprint `6d83a69870ad62131d7b21895b485ad2acf126ccc074d0759ea249be1360a685`
and artifact fingerprint
`6633977812436f36c2f9a0d0e887e5224a5e664e6f27441324c5a7a0b7ae3909`.
The proof checkpoint precedes documentation HEAD; no artifact hash repinning is
proposed. They record zero external game actions, zero unexpected complete-session
issues and clean isolated shutdown. The reviewer read the explicit divergent/
inverse/concurrent global rollback fields, saved hydration base, merged semantic
arrays, pending-save result and hydration retry state.

Decoded and visually inspected `package-en-light-attempt5-home-rollback.png` and
`package-ja-dark-narrow-attempt1-auto-rebase.png`. The first shows the rejected
action with the global switch off; the second shows JA/dark narrow Auto config
with preserved317, appended13 and City/Treasure/Resource selected. Screenshots
support the displayed state, not native ownership, original protected pixels or
live acquisition by themselves.

The 14-scenario/62-assertion Node inverse suite extracts actual production callbacks
and the actual coordinator merge expression. Its saved-source/closure locators
and negative baseline are explicit. It does not mount React. The corrected
package packet supplies real mounted Add/Enter/toggle/removal/native-boundary
coverage separately. Cap/last-type and operation-pending-before-failure proof
remain extracted composition cases; the mounted retry adds after the initial
status failure. These limits are stated and do not produce a demonstrated
remaining product defect.

The lead separately reports rerunning 14/62 inverses, profile-owner/native Map
checks, production package validation and packet/hash/six-image validation.
Those are lead execution, not checks executed by this reviewer. Historical lead
negatives and EN attempts1-4 remain preserved in the inspected packet tree.

## Bounded Home Launch + read-only City/Resource pilot

**Ready offline:** the five assigned implementation blockers have source-supported
corrections and distinguishing offline/inert evidence. No further RECOVERY-003
implementation task is identified by this second look.

**UNKNOWN live:** the real game Lua engine/xLua binding must invoke the newly
reflected `Func<string,string>` metadata reader successfully
(`PipeClientAdapter.cs:16-47`; `current_overview_bridge.lua:260-267,389-412`).
`tests/home_runtime_lease_lua_checks.py:34-50` substitutes a Lua function for this
delegate. Its isolated Lua5.3/5.4/5.5 success cannot establish actual delegate
marshalling or game-host compatibility. The native predicate test separately
establishes Windows sharing classification; combining these two isolated proofs
still does not establish the live binding. First pilot acceptance must verify
same-session readiness/heartbeat, current reader availability and bounded
lease-publication contention without stale freshness or request consumption.
Failure stops the pilot before scanning.

The RECOVERY-003 assignment is explicitly offline and does not authorize that
pilot. A separate bounded live work item/owner authorization, current process and
session-ownership inventory, assistant-owned game session, exact selected game
root and restoration/cleanup plan are prerequisites. Foreign or malformed shared
metadata must remain fail-closed; do not remove owner files to make launch pass.
Current real provider activation, read-only City/Resource acquisition/publication,
progress/query correlation and exact owned Stop cleanup remain to be live-proved
against the reviewed canonical package. Historical v21/v22 Resource success in
the master documents cannot promote this changed0.3.17 composition to LIVE_PROVEN.

Treasure claim/status, Ghost preparation and positive Railway/Ghost/Treasure
population are separate dependencies; none needs to be exercised to evaluate
this narrowly scoped City/Resource pilot. The pilot would establish only its
specified current-client paths, not original traversal completeness or whole-clone
acceptance.
