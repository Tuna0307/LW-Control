# MAP-014 — owner-fix and scanner finish handoff

Owner direction, 2026-10-14: the external worker may resume after the lead's
solo takeover. Use the existing branch `codex/map-owner-fixes-014`, checkout
and draft PR #10. The lead stops editing when handing this back.

Latest owner update, 2026-10-14: use as many subagents as needed within the
environment's supported concurrency. This supersedes all older solo,
no-subagent and no-delegation instructions for this assignment. The primary
worker remains responsible for integration, independent checking of agent
contributions and the final handoff. Assign non-overlapping files/subsystems;
coordinate shared builds, Git operations and test resources. Only one designated
agent at a time may control live app/game sessions, mutate shared installed
scripts or perform restoration. Other agents may analyze source, implement
separate changes or run isolated checks in parallel. Do not add branches or
worktrees without an existing applicable authorization. Delegation does not
expand desktop, gameplay, account-action or tool-denial permissions.

The owner confirms cities can relocate at any time, Trucks have approximately
three-hour lifetimes, and map X/Y each span 0–999 inclusive. These are owner
observations, not substituted native-field evidence. Read native timestamps
and canonical identities. No fixed City population is known or required.
Add a visible **Other** group/filter for supported uncategorized events,
including Scattered Supplies and Celebration Party, rather than hiding them
inside unrelated categories. Preserve real identities, source subtype and
existing Treasure workflows; never manufacture rows or broad unknown types.

## Exact takeover state

Repository: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
Committed checkpoint: `f068c6c0f274dcdc27e7debff32a1950698471d4`.
The checkout additionally contains deliberate, **uncommitted lead changes**:
canonical City owner accounting, latest-location deduplication, diagnostic
negative retention, matching controlled tests, benchmark audit draft and docs.
Inspect `git status` and `git diff`; preserve these changes and finish them.
They are not unrelated worker noise and must not be reset or discarded.
The benchmark audit draft needs repair: read SQLite `server_id`/`record_key`
instead of assuming every JSON payload contains `serverId`/`recordKey`.
It reports comparisons, not automatic accuracy qualification.

No LastWar, LastWarLauncher or LWBridge.Desktop process was found at handoff.
Resource benchmark final receipt confirms exact Stop and original restoration.
Recheck processes, journals, installed hashes and repository state before work.

Read, in order:

- AGENTS.md, with this newest direction preceding historical assignments.
- `docs/MAP_014_CANONICAL_CITY_CORRECTION.md`.
- `docs/MAP_014_LEAD_LENS_QUALIFICATION.md`.
- `docs/MAP_014_SOLO_REVIEW_CANDIDATE.md` (its opening status predates new trials).
- `docs/MAP_014_COVERAGE_TRACKING_CONTINUATION.md`.
- Relevant MAP-014 lead reviews and the current PR #10 diff.

## Preserved genuine functional positives

Camera restoration on completed scans and active Stop; stable warzone popover;
saved remote-server browsing/coordinate Jump after Auto return; Resource
level/fullness/black-tile filters; inclusive maximum Truck escort power;
multi-reward ALL filtering; native item PNGs; Dispatch owner names; Auto Clear
of remote data while preserving home data; moving Truck Follow, manual Stop
and natural-expiry automatic Stop. Their locators are in the review candidate.
Do not rerun these blindly or retire their negative-to-positive histories.

## New live evidence and limits

- `artifacts/map-013-live/native-causal-20261014-004239`: FOV20 consecutive
  A/B/C native observations matched independently restored controls.
- `.../native-causal-20261014-004916`: 119 probes, exact 10,000-AOI union,
  49.203 seconds diagnostic discovery, final camera restoration. This excludes
  production enrichment/storage/UI and is not a shipping speed claim.
- `artifacts/map014-lead-live/native-benchmark-city-20261014-015840`:
  three alternating pairs, baseline median 146.498 s versus FOV20 68.588 s;
  all 4,111 owner identities and compared stable fields matched. Candidate
  had one duplicate historical location. The subsequent aggregation fix must
  receive final native/package confirmation before acceptance.
- `.../native-benchmark-mixed-20261014-021149`: after dedup, City 4,111 unique
  owners; baseline median 168.075 s versus candidate 90.754 s. **Mobile-kind
  candidate fails**: first pair Truck 103 versus 39; 92 baseline UUIDs missing,
  90 had final arrival timestamps after candidate completion. Do not treat
  this as ordinary expiry or enable the candidate for mixed mobile kinds.
- `.../native-benchmark-resource-20261014-024146`: six completed rounds,
  all 2,500 blocks, zero failed blocks. Baseline 151.326/149.747/149.601 s;
  candidate 73.370/73.054/73.501 s. Row counts respectively
  7,989/7,995/8,002/7,998/7,993/7,990. Median 149.747 versus 73.370 s.
  Exact camera/Stop/scripts passed. Field/identity accuracy is still to audit.

FOV20 remains opt-in; normal default unchanged. No globally fastest or complete
independent world census claim. The historical rejected raw-cache City
supplement must stay rejected: it resurrected an obsolete location. Actual
`GetBaseMainInfoByOwnerUid` now accounts for superseded points source-correctly.

## Finish obligations

1. Reconcile lead changes and repair the read-only benchmark auditor. Audit
   missing/extra/duplicate identities, world/server, bounds, fields and real
   expiry; distinguish temporal changes from unexplained omissions.
2. City movement: one current observed location per server/player; native
   canonical resolver, explicit observation time, deterministic latest merge.
   Relocation within/between regions, old/new locations and delayed old replies
   must not duplicate a player or overwrite a fresher observation. Preserve
   exact unexplained-source failure guards. No 7,000-row quota or fake recall.
3. Finish per-kind and mixed scanner method selection. Qualify stationary
   candidates; diagnose mobile freshness using actual native march lifecycle
   and timestamps before extracting Truck/Train data. Native point AOI coverage
   alone does not prove march coverage. Existing source identifies
   WorldMarchDataManager's separate queue, receive/processing timestamps,
   camera rectangles and WorldGetRectMarchInfos request. Use private passive
   instrumentation first; do not send guessed requests or add arbitrary sleeps.
4. Investigate retained-camera A→B→C with one final restoration. Private
   success is not production integration. Enable only after actual engine,
   mixed accuracy, Stop/failure/retirement and packaged-camera tests. If it
   cannot qualify, retain the best proven route with concrete evidence.
5. Other: scan selection/result grouping and subtype filtering for supported
   nonstandard map events, consistent Manual/Auto/query/persistence/Clear/Jump.
   Use a canonical acquisition/storage lane to avoid duplicate scans/rows or
   accidental Treasure action eligibility. Keep ordinary Treasure independent.
   Source: type-27 Supplies/type-1 config key `season_s2_ice_supplies_11`;
   Celebration Party detect_event 27000/type23/name key
   `activity_wajueji_27000_name`. Prior events 27015/25098 are not Party.
   Localize all nine app languages; game stays Chinese. Unknown event types
   need truthful native labels or explicit unavailable text, never fabricated.
6. Confirm moving Doom tracking when an actual source-backed object exists;
   confirm Supplies/Party positive acquisition if spawned. Seasonal/rare
   absence may remain explicitly pending without blocking available fixes.
7. Actual Truck/Train expiry: correlate displayed timer, server clock, native
   final-arrival/disappearance and UUID; distinguish movement-leg endTime.
   Retest tracking retirement with meaningful existing evidence. Never invent
   one Train lifetime or dismiss every missing object as expiry.
8. Verify bounds 0/999, corners, region seams, mixed dedup, remote provenance,
   two-server Auto return and cancellation after positive staging. Preserve
   published data on failed/cancelled runs and reject retired-owner results.
9. Choose the fastest **accuracy-qualified tested** route per selection. A
   selection with unqualified mobile kinds must retain a qualified route.
   Both Normal/Fast may use the same selector; do not force an artificial
   speed difference. Mixed kinds share discovery and targeted enrichment.
10. Finish real packaged-app checks and one final source-identified Windows
    RC, including appropriate Home regressions, native/storage/pipe checks,
    frontend localization/mounted checks, package/private-file integrity,
    source/ZIP hashes, exact original restoration and cleanup. No fixture-only
    positive can establish a real operation. No repeated ZIP per small change.

## Loop and execution authority

Diagnose → supported fix → distinguishing inverse → real applicable retest →
self-review → coherent commit → next ready obligation. Ordinary failures do
not require another handoff. Preserve each failure and distinguish a tool
limitation, an unavailable event and an actual product defect. Do not repeat
unchanged failures or request more permission already given.

Background own-app/game starts/restarts, passive scans, passive Follow and
server-view switching/return are allowed. No global mouse/keyboard, owner
window control, foreground forcing or minimize/rearrange. Keep game Chinese.
No claims/plunder/sharing/marches/attacks/spending/relocation/updater/protected
services or access-control bypass. Stop only exact task-owned sessions; bind
creation identity and restore only after confirmed installation exit. Explicit
owner stops and actual tool denials remain binding. Never reroute a denied call.
Subagents are authorized under the newest owner update above. No new
branch/worktree, merge or publication. Do not message unrelated AI chats.

Repair stale test pins only after auditing exact changed source and preserving
historical hashes. Never weaken hash, identity, reply-freshness or action gates.
The prior denied `npm.cmd run check` is not an executed pass or permission to
reroute that operation. Report the affected check and finish independent checks.

## Delivery / return boundary

Maintain one current compact continuation, owner-issue table and supported
behavior ledger; preserve historical receipts, do not import bulky research.
Commit/push coherent checkpoints normally and verify direct remote SHA.
Update existing draft PR #10 around the final scope. Check branch/worktree
cleanup eligibility but preserve unique research and active work.

Return READY_FOR_LEAD_FUNCTIONAL_REVIEW only when available approved functions
and selected scanner gates pass. State clearly any event-unavailable positive
or owner-only action still pending; never call whole Map complete while a
required operation is broken. If a real dependency remains after meaningful
attempts, finish other ready work and return an exact bounded PARTIAL report.
Final handoff: done/pending matrix, accuracy-qualified selection table,
individual/mixed/multi-server timings and populations with temporal explanation,
real versus controlled evidence, commit/PR/package identities and exact cleanup.
No personal owner testing is a substitute for worker verification. Lead reviews
once the final available scope is ready; owner-only actions remain separate.
