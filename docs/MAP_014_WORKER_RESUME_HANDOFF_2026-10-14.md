# MAP-014 worker resume after owner interruption — 2026-10-14

The owner stopped lead execution to conserve tokens and requested a worker
handoff. Implementation and live tests are paused in this lead chat; this file
assigns the remaining work to the resumed worker. It supersedes older MAP-014
continuation priorities, without rewriting historical evidence or permissions.

## Exact handoff state

- Checkout: `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`.
- Existing branch: `codex/map-owner-fixes-014`; existing draft PR #10.
- Product source at interruption: `e01d01b5d493dd19a7b44033963436e26ebadd90`.
- Direct remote branch matched that SHA; working tree was clean.
- Canonical publish completed at `artifacts/map014-takeover/publish/` for that
  product source. This handoff is documentation only; no new release ZIP exists.
- Fresh process query at handoff found no LastWar, LastWarLauncher or
  LWBridge.Desktop process. Completed native attempts have exact Stop/restoration
  receipts. Recheck process/session and installed bytes before the next launch.
- Main is untouched. No merge, publication or review ZIP was produced.

Follow AGENTS.md's newest delegation and background authority. Use available
subagents for non-overlapping source/tests/review, with one primary integrator
and one live controller for game/app/installed scripts/restoration. Task-owned
background launches, passive scans, Follow and authorized server view switching
are allowed without another owner relay. No mouse/global keyboard, foreground
forcing, minimization/rearrangement or owner-window control. Preserve Chinese
game language. No live claims, plunder, sharing, marches, combat, paid spending,
relocation, updater actions or protected-service/authentication bypass. Respect
new tool denials without retrying/rerouting them. Earlier specific denials are
historical, not a permanent owner pause; this takeover encountered no new denial.

## Preserved corrections and actual evidence

1. `193f0e1a`: publication-phase ownership, immediate busy manual navigation,
   inclusive 0–999 Jump, passive Other candidate inspection and pre-acceptance
   Auto-return ownership. Actual engine/provider/SQLite and inverse cases pass.
2. Restoration admission now recognizes exact supported official v26 originals
   without reverting installed files to v24 or weakening production anchors.
3. `b3d46abef0021552c95e162b3f4851e6c2861437`: diagnostic Auto return heartbeat
   observation, typed Auto-disable payload, original session pin and fresh
   navigation request-ID fence. Genuine packaged remote proof PASSED at
   `artifacts/map014-takeover/native-ui-20261014-122631`: 7,998 Resource rows on
   2213, Auto return to 2212, retained remote browsing, native Jump back to
   2213 (990,911), real Auto Clear of 2213 preserving 7,975 home rows, Auto off,
   exact Stop/restoration. Preserve earlier failed selector/heartbeat/config
   receipts separately; cleanup ReturnHome cannot certify the Auto return.
4. Genuine packaged City-only source `193f0e1a` published 4,056 rows with 2,500
   completed blocks, 10,000 AOIs and zero failures at
   `artifacts/map014-takeover/native-ui-20261014-082632`. This is an observation,
   not a fixed population quota or proof of every city on the server.
5. A diagnostic mixed run at
   `artifacts/map014-lead-live/native-mixed-20261014-202959` FAILED: full discovery
   completed, later Resource hydration reported state unavailable, inappropriate
   retries/fallback prolonged the run, one block failed and nothing published.
   The original first hydration cause was overwritten and remains UNKNOWN.
6. `e6956440c98eb3f291cf16aa71090a996764033f`: terminal full-world Resource
   hydration validation retains immutable request-correlated first-reply/error
   evidence and avoids retrying consumed acquisition state. Actual engine/SQLite
   tests prove fatal cleanup/no publication/old data preservation, while ordinary
   IO/timeout retries and cancellation still work. Invalid diagnostic storage
   does not replace the original error. Do not broaden lease tolerance or relax
   hydration/accounting validation to make scans pass.
7. Fresh packaged source e695 mixed run at
   `artifacts/map014-takeover/native-ui-20261014-125647` completed acquisition and
   publication: City 4,132; Resource 8,010; Monster 14,028; Truck 193; Dispatch
   842; Railway 0. Native 10,000 AOIs, 2,500 completed blocks, zero failures.
   First Resource hydration had 7,694 native targets/details, zero send failures,
   no error and passed existing accounting. Zero Railway rows is not a positive
   Railway witness. This scan succeeded, but the overall UI proof FAILED below.
8. Independent production MapStore reopen/query/pagination/export audit on that
   genuine mixed DB PASSED:
   `artifacts/map014-lead-finish/current-mixed-reopen-audit/` and its adjacent log.
   Direct production SQL resolved 50/50 Dispatch owner names in about 546 ms.
   This does not establish the failed frontend request succeeded.

The full inert suite passed at
`artifacts/map014-lead-finish/inert-checks.log`; hydration changes passed the
affected suite at `artifacts/map014-lead-finish/hydration-inert-checks.log`.
Release builds had zero warnings/errors. Actual captured clone locale was
Chinese/light; an `--language en` argument is not proof of an English screenshot.

## First priority: actual Dispatch browsing failure

The packaged mixed proof failed with `MAP014 real UI tab did not settle:
dispatch`. `map014-ui-extras.json` shows active Secret Task tab with 842 records,
dispatch table class, not busy, no displayed error, but no observed successful
map_search response after Search. The last and manualSearch replies were null.
Resource combined filtering had already passed. Do not certify the UI from the
successful scan or blame missing owner names: the read-only production DB audit
resolves them. The cause is still unproven.

Current e01d adds opt-in rejected/cancelled/error map_search observation to
LWBridgeWindow, writing `native-ui-map.json.command-errors.jsonl`. It changes no
production query behavior. Some earlier profile-generation rejection/closed
session paths are not yet instrumented; an empty error log is not proof of no
rejection. Trace frontend issuance, request ID, profile/session generation,
backend response and mounted acceptance before changing code.

Current run_ui supports a read-only SQLite backup of the genuine mixed DB into
a new isolated browse proof. After verifying current source and preflight, use:

```powershell
python tools/map_014_lead_native/run_ui.py browse --saved-db artifacts/map014-takeover/native-ui-20261014-125647/profiles/map014-native-ui/map-data/map-data.db
```

This needs a fresh owned game/app session but no new full-world scan. Inspect
the new command-errors JSONL and actual extras receipt, reproduce the first
failure, fix the demonstrated cause and add a distinguishing regression. Then
finish real Resource/Truck filters, Dispatch names, icons/menu images and
Follow/Stop checks. Do not repeatedly full-scan to diagnose a saved-data table.
No live test of e01d's new query-error observer has yet occurred.

## Remaining work, in order

1. Close the real Dispatch browse issue above, including stale/late query,
   profile-switch, clear/reopen and wrong-server inverses. Preserve genuine DBs.
2. Review all owner issues against the current canonical implementation:
   camera geometry after completed/cancelled scans; stable Connected status and
   cross-server popover; row server/world navigation after Auto return; Resource
   level/full/partial/unknown/black-tile filters; Truck inclusive max escort
   power and multi-item ALL filtering, genuine icons and moving UUID Follow;
   Secret Task owner names; visible Other grouping; Auto Clear and Stop-return.
   Reuse valid evidence when source paths are unchanged, but execute new genuine
   proof where a product fix or failed current flow invalidates older proof.
3. Fresh current-source active Stop/camera restoration has not been run in this
   takeover. Build the matching diagnostic host before using run_owned.py stop,
   or exercise the actual packaged controls. Verify cancelled durable state,
   discarded staging, old published data, no next target, Auto disabled and exact
   restoration. Controlled positive-staged Stop already passes; only claim a
   genuine positive-stage Stop if actual positive staging was observed first.
4. Verify current Other inspection/grouping: historical genuine Supplies (three
   records) exists at `native-ui-20261014-013823`; Party event27000 is source-
   identified but has no spawned positive. Unsupported/malformed records must
   not be silently categorized as Other. No claim sends.
5. Qualify moving/expiring target behavior by native UUID, server/world, arrival
   or expiry timestamp and latest observation. Historical natural Truck Follow
   has 27 requests, 26 positions, 19 camera tiles and automatic expiry Stop.
   Do not assume every Truck always lives exactly three hours, or apply that
   duration to Train. Source Train arriveTs exists; final countdown/expiry proof
   remains missing. Source moving Doom Walker support exists but no genuine
   moving BossVO has been available. Pursue a positive if naturally present;
   otherwise retain an explicit availability gap, not a fabricated success.
6. Finish an honest per-selection scanner decision. City-only FOV20 is selected
   from prior repeated identity-qualified and packaged evidence. Resource,
   Monster, Truck, Railway, Dispatch, mixed and Other retain guarded shared
   serial LOD0 acquisition. Mixed selections share discovery. Actual camera
   concurrency is one; requested 8/20 is not measured parallel requests.
   Resource FOV20 has unexplained identity differences; mixed FOV20 misses
   unexpired Trucks; both remain disabled. Private consecutive A/B/C discovery
   (119 probes / 10,000 AOIs / 49.203 s) has no complete production native
   pipeline and cannot establish mobile-kind recall. Research a credible
   source-supported alternative only with fresh native request/reply/march
   queue/rectangle and accurate identity/field evidence. Repeated geometry-only
   success is insufficient. Retaining the best tested safe route is acceptable;
   no globally fastest, only-possible camera method or complete city census claim.
7. Audit remaining action capability honestly. Real Treasure aggregate claim
   status/claim and Ghost terminal execution remain unavailable in
   CurrentClientMap317ActionProvider. Read-only inspection, previews and
   preparation are not working real actions. Recover available source-backed
   request/result semantics and implement controlled genuine-capable adapters
   if supported; otherwise record the exact unavailable edge. Never blind-send
   to claim completion. Ghost/seasonal positives are owner-deferred; Dispatch/
   Truck plunder and alliance sharing need owner-only live verification. This
   handoff does not authorize those actions.
8. Complete one affected regression/build sweep after integrated fixes, then
   one final canonical Windows review ZIP, extracted byte/source identity,
   private-file exclusion, startup/Home regression and Map packaged proof.
   Use package_review_candidate.py only when the clean HEAD matches the
   published source identity. Documentation-only handoff HEAD differs from
   e01d's existing publish: rebuild for the final candidate, not for every note.

## Current original-file admission

The official current original triplet is v26, not the older v24 baseline:

- LWScripts.data SHA256:
  `49f6249d1530b5f16aad3750388d017ac3032f4b8ef584eec6cff9ea01d19b6d`
- LWScripts.txt SHA256:
  `52d5b30c486e9ff222e125eb05a13cc01a6407b0bdb40f15cbf12af0389ffae0`
- version.txt marker26 SHA256:
  `5f9c4ab08cac7457e9111a30e4664920607ea2c115a1433d7be98e97e64244ca`

Existing critical anchors remain unchanged. The runner retains exact historical
v24 and v26/old-marker admissions separately. Verify same-session backup/start/
Stop/installed originals; do not overwrite a historical negative receipt using
later changed bytes. If a legitimate newer official package appears, do a
read-only compatibility/source delta assessment rather than weakening admission.

## Working protocol and return boundary

Diagnose -> source-backed fix -> distinguishing test -> genuine affected proof
-> self-review -> coherent checkpoint -> next ready item. Ordinary failure is
not a reason to idle and ask the lead to choose the next step. Assign independent
source/test/review subtasks; primary worker integrates and reviews contributions.
Serialize live controllers, builds sharing outputs, game scripts and restoration.
Do not reset, discard, force-push, bulk-import research, regenerate huge evidence
or make a release ZIP for every small correction. Keep useful raw evidence local
under artifacts and a compact committed continuation/difference ledger.

Commit owned coherent changes, push normally, verify direct remote SHA and update
existing PR #10 to the actual delivered scope. No merge or publication. Final
report must distinguish product working, genuine/native proof, controlled-only,
unavailable population and unavailable provider. Include every remaining Map
feature, not just scanner speed. A clean checkout/ZIP or passing offline tests
alone cannot establish whole-Map completion.

Return READY_FOR_LEAD_FUNCTIONAL_REVIEW only for a precisely stated genuinely
verified available scope. Whole Map remains PARTIAL while an expected feature
cannot operate or requires an unperformed native/owner gate. Complete all ready
work first and report exact dependencies for the rest. Independent acceptance
and the owner's account-action verification remain separate.
