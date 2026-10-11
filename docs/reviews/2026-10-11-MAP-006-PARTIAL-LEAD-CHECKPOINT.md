# MAP-006 interrupted worker — lead tidy checkpoint

2026-10-11. **PARTIAL / preserved development checkpoint, not release acceptance.**
Owner requested an audit/tidy of half-finished worker changes before discussion.
Branch: `codex/map-complete-delivery-006`; prior committed HEAD `172b3718`.
No new game launch, desktop input/capture, Map acquisition, claim, plunder, sharing
or jump was performed by the lead. Main and submitted MAP-005 / PR #7 stay intact.

## Preserved implementation

The worker left uncommitted Auto terminal-publication gates, a Ghost-to-Dispatch
transport rejection, Dispatch/Truck attempt+launch-session result correlation,
maintained native checks/audit helpers and a MAP-006 isolated pilot helper.
The current changes compile and pass the maintained delivery suite. They are
saved as a provisional coherent checkpoint, not proof that all Map actions work.
No source behavior was changed by the lead during this tidy pass.

Tracked/untracked worker files were copied with SHA-256 manifest to ignored local
`artifacts/map-006/lead-checkpoint-20261011-131835/` (17 distinct content paths).
The binary Git patch and starting status are retained there. No private game data
or ignored runtime artifacts are staged. One checkout; no branch/worktree deleted.

## Existing live receipts inspected

The earlier handoff's "no MAP-006 live game work yet" statement was stale.
Ignored `artifacts/map-006/genuine-pilot/` contains these saved outcomes:

| Saved outcome | Recorded evidence | Limit |
| --- | --- | --- |
| Mixed all-eight run `9da7ae565efa46e09b0dc08677bbaa5a` | Completed 2,500/2,500 blocks, zero failures; City 7,059, Resource 8,011, Monster 8,799, Truck 458, Dispatch 2,309, Treasure 43 | Railway and Ghost zero; no positive acquisition proof for those two. Counts are observations. |
| Native/reopened SQLite audit of mixed run | Matching stored counts; pagination digests; City XLSX 7,059 rows/12 columns; zero staging/actionable jobs | Query-visible Truck/Treasure totals differ from stored counts; time/status filtering requires continued assessment, not automatic equality. |
| Earlier Auto Truck `9698f43ace1e49288554f6d1c95d38d2` | Completed very rapidly but published no Truck records | Preserved negative integration history; do not count as a positive Auto witness. |
| Later Auto Truck `5e2d231caf174d979c1106962bac258a` | Completed 2,500/2,500, Truck 434; native audit and saved Auto cycle completed the same server | Saved receipt, not a new lead live experiment or final source-identified package certification. |
| Final worker cleanup | Auto disabled/not running, jobs zero, processes empty, original hashes restored, no journal | Independently checked current process/file cleanup below. |

The source was uncommitted during the worker campaign. Final source/EXE/pilot
provenance and complete packaged EN/JA captures remain to be reconciled before
acceptance. Neither saved receipts nor passing general suites finish whole Map.

## Fresh checks executed by lead

- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: exit 0.
  Accepted Home, stored Map, MAP-005 scan/Stop/publication and MAP-006 Auto terminal,
  all-eight controlled publication, Ghost transport rejection, Dispatch/Truck
  correlation and navigation/share controlled boundaries all passed.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: exit 0; all five groups,
  all nine 1,383-key catalogs passed.
- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build`: exit 0,
  package markers `042462aa...64fdab3` / `7aa4cb2f...e08c509`.
  This checks the existing production UI, not a new final native release ZIP.
- `git diff --check`: passed.
- Current LastWar/LastWarLauncher/LWBridge.Desktop process count: 0.
- Installed `LWScripts.data`, `LWScripts.txt`, `version.txt` SHA-256 all match
  the saved worker originals; isolated recovery.json count: 0.

## Owner-reported Connected/Checking flicker

**Confirmed UI cause; not fixed in this tidy checkpoint.**

`src/LWBridge.UI-0.3.17/src/App.jsx`:
- `readStatusSnapshot` sets `statusPairReady=false` before awaiting reads (line 779).
- The routine polling effect invokes this callback every 5,000 ms (line 932).
- `connectionState` consumes that readiness flag (line 995).

`src/LWBridge.UI-0.3.17/src/mapBackend.js:185` returns `checking` when
`freshPair` is false, before inspecting the actual native connected fields.

Direct execution of the production formatter with unchanged
`{xluaOnline:true}` and `{gameRunning:true}`, toggling only readiness
true/false/true/false/true, produces:
`connected -> checking -> connected -> checking -> connected`.
Thus periodic polling can manufacture the observed flicker with no disconnect.
This does not prove the owner's former session had no transport interruptions;
there is no current live session to inspect. The bug also makes App's `online`
predicate false transiently, so merely hiding text is not a sufficient fix.

Next correction: separate refresh-in-flight from validity of the last acknowledged
owner's status pair. Keep initial/profile-change checking, explicit failure/offline
transitions and ownership fencing truthful. Test held poll replies, failed reads,
status events retiring polls, profile A/B changes and real disconnect. Do not
mask failures, pin Connected or weaken native readiness. No code fix yet.

## Remaining work and continuation

1. Owner discussion first; no automatic fresh live campaign started here.
2. Reproduce/fix the status-poll flicker through the actual App callback/effects.
3. Reconcile source/packaged evidence for existing passive mixed scan and Auto work.
4. Railway/Ghost positive producers, Resource/other names/assets and remaining
   all-kind browse/adversity proof need completion/assessment.
5. Treasure claim/status and Ghost preparation remain explicitly unavailable.
   Implement supported controllers; never treat a protective fence as completion.
6. Continue scheduling/correlation/controller work and final Map integration.
   Claims/plunder/sharing/jumps remain controlled-only until specific owner approval.
7. Final release package and incremental PR/lead review remain undone. MAP-005
   review remains separate; do not block its possible release on whole Map.

Resume the MAP-006 finite operation matrix/work item from this checkpoint after
the discussion. Ordinary implementation failures stay inside the self-correction
loop. No whole-Map acceptance, merge, publication or branch retirement occurred.
