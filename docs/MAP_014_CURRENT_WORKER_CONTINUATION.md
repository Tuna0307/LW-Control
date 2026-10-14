# MAP-014 current worker continuation — 2026-10-14

Status: **READY_FOR_LEAD_FUNCTIONAL_REVIEW** for available approved Map
functions; overall **PARTIAL** for unavailable Celebration Party/Doom positives,
separate Train lifetime and unqualified mobile/Resource optimizations. This
compact ledger preserves historical negative receipts. Branch
`codex/map-owner-fixes-014`, draft PR #10, inherited checkpoint `f068c6c0`.

## Current measured scanner routes

| Selection | Genuine baseline median | Genuine opt-in FOV20 median | Decision and evidence |
| --- | ---: | ---: | --- |
| City only, 2212 | **227.912 s** | **86.179 s** | **Production FOV20 selected, pending actual packaged proof.** Fresh Oct14 six-round exact-owned engine benchmark in `native-benchmark-city-20261014-085244`: every round 4,114 unique current owners, zero missing/extra identities in all three pairs, zero duplicate owner locations, 10,000 AOIs and identical camera checkpoints; one player moved (760,164) to (335,171) at a later source observation. Median 62.19% reduction. No fixed global server census claim. Earlier historical pairs 146.498 vs 68.588 had one duplicate pre-fix location in each candidate; retained as historical negative. |
| Resource | 149.747 s | 73.370 s | Baseline selected pending native population-difference attribution and final package/Stop gates. Historical pairs have 7,970/7,998/7,990 shared identities with unchanged locations, resource types, levels and capacities; there are 19/4/3 missing and 25/0/0 extra candidate identities, and 7/2/1 changing occupancy states. |
| City+Resource+Monster+Truck+Railway+Dispatch | 168.075 s | 90.754 s | **Reject faster mobile route.** All six scans covered 10,000 point AOIs; candidate Truck populations 39/39/36 vs baseline 103/95/89. Baseline→candidate missing UUIDs were 92/92/83, of which 90/88/82 had explicit final `arriveTs` later than candidate completion. |
| Truck, Railway, other individual kinds | Existing guarded serial LOD0 | No qualified equivalent | Keep existing method; the point manager's AOI reply is not a march-manager rectangle/queue/reply completion barrier. |
| Consecutive A→B→C final restore | Existing guarded restoration per probe | Private 49.203-second 10,000-AOI discovery | Diagnostic-only success; product engine, persistence, Stop/cancellation, mixed march correctness and packaged UI gates pending. Earlier stale native subscription negatives remain valid. |

Read-only measurements above originate in `artifacts/map014-lead-live/native-benchmark-{city-20261014-015840,resource-20261014-024146,mixed-20261014-021149}`.
The repaired `tools/map_014_lead_native/audit_benchmark.py` uses SQLite
`server_id`/`record_key`, independently checks source geometry, server, camera,
coverage, identity, stored counts and stable fields, and records historical
duplicate locations rather than hiding them. It deliberately does not infer
world census or live expiry from sequential snapshots. Additional exact March
UUID/expiry qualification is in `tools/map_014_march_research/README.md`.

## Current deliverable and pending gates

| Scope | Current evidence | Status |
| --- | --- | --- |
| Camera, warzones, remote saved Jump/Auto return/Clear, Resource filters, Truck power/rewards, images, Dispatch owner name, Truck manual Follow and natural-expiry Stop | Preserved genuine native/packaged witnesses in `docs/MAP_014_SOLO_REVIEW_CANDIDATE.md` | Passed previously; relevant changes require regression |
| City canonical native owner accounting | Lead source correction, current-main-base resolver and explicit superseded old-point tracking, native controlled adversarial inverses | Current guarded source passes controlled checks; whole-world recall unknown |
| City latest-location row merge | One `serverId:ownerUid` within exact profile/session/run; newer observation wins, delayed older ignored, conflicting same-time positions rejected. Actual `native-city-20261014-084003` current-client owned run completed 2,500 blocks and 100% native acquisition; `audit_owned_city.py` established 4,114 SQLite rows, 4,114 unique native owner identities and coordinates, server 2212. Three fresh baseline/FOV20 paired observations preserved stable identities; one genuinely timestamped player relocation. | Controlled C# and genuine six-round engine/SQLite accuracy/relocation pass; conflicting equal-time adversarial live witness remains unavailable |
| Scattered Supplies / Celebration Party / Other | Exact native type27 positive numeric supplies subtype and event27000/type23 conditional classifier; separate Other selection, results, Manual/Auto, SQLite migration/reopen/collision, counts/query/subtypes/Clear/Jump and nine locales; one Treasure census, no duplicate acquisition. Controlled source/SQLite/cancellation/Auto and frontend tests pass; worker-3 found no static blocker among eight earlier issues. Genuine packaged `native-ui-20261014-013823` **positively acquired three native type27/type1 Scattered Supplies on 2212**, completed all 2,500 blocks and mounted Other search, exact shutdown/restoration. | Other-only and genuine Scattered Supplies POSITIVE PASS; Celebration Party conditional native event27000/type23 has no spawned positive yet |
| Truck/Train motion and expiry | Truck native 27-requests/26-positions/19-tiles natural expiry and Auto Stop; march receipt `arriveTs` differs from leg `endTime` | Truck passed previously; native separate Train timer and fresh march completion gate pending |
| Bounds 0/999, region seams, cancellation, failed-scan rollback, remote server data | 10,000-AOI union and genuine remote 2212→2213→2212; controlled selected-kind Stop after staging and late reply rejection. Fresh native `native-stop-20261014-094721` stopped a City scan after initial acquisition with identical independent camera snapshots, terminal idle, exact task-owned exit and original script restoration. | Available cancellation/camera and bounds gates passed; unspawned edge-object positive not inferred |
| Actual Windows package, Home, localization, game recovery and PR | Genuine packaged `native-ui-20261014-013823`: Other-only 2,500 blocks, 3 Scattered Supplies; `native-ui-20261014-014338`: City-only FOV20 2,500 blocks, 4,114 Cities. Both actual WebView tab/search responses, zero failed blocks, exact app/game exit/original three script hashes. Home/Map/Auto C# Release, mounted EN/light desktop+JA/dark narrow, MAP011 saved-owner, production frontend and new Other checks passed. | Actual selected packaged functions PASSED; final RC/source-commit/remote SHA still pending |

Current process and checkout read-only reconciliation at takeover found no
`LastWar.exe`, `LastWarLauncher.exe` or `LWBridge.Desktop.exe` process. All
deliberate lead changes were preserved. The October 14 owned City run's raw
admission, owner, camera, engine log, SQLite, probe metrics, final, exact-stop
and restoration evidence is under
`artifacts/map014-lead-live/native-city-20261014-084003/` with audit output
`owned-city-audit.json`. It exited cleanly with zero account actions and no
remaining owned game process or active recovery journal.

New exact-owned six-round City pair benchmark under
`artifacts/map014-lead-live/native-benchmark-city-20261014-085244/` was
independently audited by `audit_benchmark.py` (`audit-result.json`). Baseline
times 227.606/227.912/229.487 seconds, FOV20 85.589/87.260/86.179; zero
identity substitutions, one timestamped relocation, 10,000 AOIs in every
round, zero scan failures and unchanged camera. Original official v24 script
hashes restored (`final.json`, `stop.json`), no game/app process or recovery
journal after exit. Scanner policy in `CurrentClientMapBlockSource` selects
FOV20 **only for City alone**, with an explicit diagnostic ForceLegacy switch;
mixed mobile and Resource retain original conservative methods.

New genuine packaged source-identified UI receipts are
`artifacts/map014-takeover/native-ui-20261014-013823/` for the Other-only
scan, including three native Scattered Supplies records at (804,738),
(776,732), (442,5) on 2212, pointType27/suppliesType1, and
`native-ui-20261014-014338/` for the production FOV20 City scan with 4,114
native Cities. Both have positive real mounted Windows tab/search and
source-identity receipts, complete 2,500-block publication and genuine
owned exit/original script restoration. Earlier native on-stop receipt
`artifacts/map014-lead-live/native-stop-20261014-094721` verifies the
FOV20 path restores the actual camera when cancelled after first acquisition.

Preserved negative: the initial genuine `treasure-other` combined native
diagnostic `artifacts/map014-lead-live/native-treasure-other-20261014-094852`
reached 57.4% source acquisition, then a five-second fresh world-state status
poll timed out (`CurrentClientMapBlockSource.StatusState.cs:66`) and the
diagnostic host exited without a final publication; the exact game and
original scripts were restored. This is a real status-observation failure,
not evidence of missing event records. `Map317CommandService.ReadScanStatusAsync`
now retains the current exact run's last accepted overlay on this narrowly
defined transient timeout while the underlying engine continues. A controlled
inverse passes. **Distinguishing real combined retest PASSED** at
`artifacts/map014-lead-live/native-treasure-other-20261014-095454`: 10,000
native AOIs, 2,500/2,500 completed, 21 ordinary Treasure and one native
Scattered Supplies subtype `supplies:1`, zero duplicate category record keys,
unchanged independent camera snapshots and original script restoration.
Read-only `audit_owned_other.py` report is `owned-other-audit.json`. A prior
packaged Other-only run saw three Supplies earlier; different time-separated
counts are not an independently proven scan omission.
