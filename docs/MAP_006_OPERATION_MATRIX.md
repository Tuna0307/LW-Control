# MAP-006 — finite operation and result-gate matrix

Latest independent review: MAP-005 is accepted/released; MAP-006 PR #8 is
CHANGES_REQUIRED for LEADMAP006-01 cross-server Auto Stop/return ordering.
Continue the R1 Stop/efficiency work item. Historical row snapshots below are
superseded by the current status/review; deferred features are still pending.

Latest owner clarification supersedes initial authority columns below: server
switching/multi-server passive scan and Auto are allowed. Worker live plunder,
claims and sharing remain excluded; owner will perform them personally. See
OWNER_DIRECTION_MAP_SERVER_SWITCHING_2026-10-11.md. Acquisition origin/efficiency
must follow MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md; no fastest/original-algorithm
claim without evidence.

2026-10-11. Current solo worker operation inventory. This is **not** a list of
released working features. The reference is LWBridge 0.3.17 and the checked-out
source at `172b3718`, plus MAP-006 corrections. Do not infer real game success
from a rendered button, synthetic fixture, queued job, or request acknowledgement.

Production routing: `MapDataPage.jsx` / `mapBackend.js` / `App` frontend IPC →
`LWBridgeWindow` and `Map317CommandService` or `MapAutoScanCommandService` →
`MapControlPlane`, `MapActionControlPlane`, `MapPlunderWorker` → current-client
scan/action providers and owned `CurrentClientMapBlockSource` bridge → official
client (when authorised) → `MapStore`/SQLite → `map_search`, `map_data_options`,
summary, XLSX, state notifications, or the appropriate authoritative game result.
`LWBridgeBackend` is a separate disconnected/local fallback; it is not live proof.

Authority codes: **P** = passive capture/Auto (including bounded cross-server
target and return navigation) approved after isolation, identity and backups;
**C** = controlled only without external side effects; **OWNER** = worker must
not execute live claims, sharing or plunder; the owner tests them. No paid,
combat or unrelated actions are approved.

| Visible operation / purpose | Production entry → result boundary | Missing proof or defect / actual result gate | Authority, existing evidence, next action |
| --- | --- | --- | --- |
| Home for Map / select exact profile and connected server | `LWBridgeWindow` lifecycle → owned current-client session | Exact current build, owner and clean script restoration | **P**. Released single-game Home; recheck on task isolation before any new live scan. |
| Manual City / Player City records | `map_scan_start` → provider/engine → SQLite → City tab | Fresh full completion, Stop, persistence, queries and export | **P**. Submitted MAP-005/PR #7 with real server-2212 evidence; preserve/retest affected code only. |
| Manual Resource / Resource Points | Same scan path → Resource tab/status/name lookup | Actual record identity and resource-name fallback resolved where metadata exists | **P**. MAP-005 actual Resource records; some labels still `Unknown resource`. Check current-game metadata, not fabricated strings. |
| Manual Monster / world enemies | Type `monster` → full-world AOI/monster protected detail → `map_search` | Positive current natural population, correct kind/template names, persisted records, Stop | **P**. Saved mixed 2,500-block run published 8,799 Monster rows; standalone/controls and catalog labels still need validation. |
| Manual Truck / railway-truck entries | Type `truck` → LOD0 AOI producer (official `TryGetTrainList(true)` is separately available) → `map_search` | Positive source, train/server identity, mode, current-state/name/goods | **P**. Saved mixed 458 and later Auto 434 Truck rows; no proof that the direct Train-list shortcut covers all Trucks. |
| Manual Train/Railway | Type `railway` → AOI union plus refreshed official Train-list producer → Train tab | Correct kind split, positive records, persistence and goods | **P**. Genuine Railway/Dispatch `[2212,2213]` Auto published two unique Railway rows on 2213; Railway/Ghost `[2213,2197]` published two on 2197. Dedicated manual Railway tab/goods behavior remains unaccepted. |
| Manual Secret Task/Dispatch | Type `dispatch` → AOI producer → Dispatch tab | Fresh qualified rows, states, protected time and sort/filter validity | **P**. Saved mixed run published 2,309 Dispatch rows; protected-state/filter semantics need full verification. |
| Manual Ghost Ops | Type `ghost` → AOI producer → Ghost tab | Fresh positive Ghost rows, correct expiry/owner identity; must not transport as Dispatch attack | **P** scan; **OWNER** live plunder. Genuine Railway/Ghost scan selected Ghost on 2213 and 2197, full 2,500 blocks each but **zero Ghost rows**. No positive extractor or plunder execution witness. Dispatch transport rejects Ghost. |
| Manual Treasure | Type `treasure` → AOI producer → Treasure tab | Natural positive rows and correct alliance/expiry/state/name/assets | **P** scan; **OWNER** live claim. Saved mixed run published 43 Treasure rows; authoritative claim is still unavailable. |
| Mixed kinds, Normal/Fast and empty selection | `MapScanStateMachine` start → engine selected kinds, 8/20 admission | Duplicate Start, selected-only publication and retention of other kinds, source block coverage | **P**. Saved all-eight run published six positive kinds; two selective live Auto cycles add Railway positive proof, Ghost remains zero. Controlled all-eight storage/reopen passes. |
| Scan busy/progress/Stop/error/failed block | `map_scan_status`/`map_scan_stop` → state/engine/sink/SQLite | Acquisition vs staged vs published; exact-owner cancel and late/teardown races | **P**. MAP-005 controlled and real City/Resource; other-kind genuine and profile switch pending. |
| Stored browse/tabs/search/filter/sorts/pages | `map_search`, `map_data_options`, `map_summary` → `MapStore` | Each kind's correct visible columns, all conditional filters, stale result/profile race and persisted rows | **P**. City/Resource accepted; `MapDataPage` uses request generation fences; others need fresh source evidence. |
| City Excel / result file | `map_city_export` → native Save As → XLSX writer | Exact filtered fresh City rows/headers/independent reopen; Resource has no export | **P**. MAP-005 verified; other export is not an advertised control. |
| Player star/mark and task selection | `map_player_mark_set` → SQLite; row selection state in UI | Correct current row identity, persistence, tab return, no wrong-profile mark | **P** local. MAP-005 real City mark verified; selection/actions for other kinds pending. |
| Clear data | `map_scan_clear` → store transaction scoped to task server | No active scan, no unrelated server/profile loss, Empty UI/SQLite | **P** task DB only. MAP-005 controlled genuine isolated clear verified. |
| Auto enable/servers/ordering/types/interval/speed/return | `local_map_auto_scan_config_set/status` → profile JSON + frontend hydration | Stable ordering and dedupe, type protection, concurrent updates, restart | **P** bounded multi-server passive. Genuine Truck, Railway/Dispatch and Railway/Ghost cycles completed and Auto disabled. Controlled in-flight type/server edit preserves admitted cycle and later uses new configuration. Restart/profile adverse proof remains open. |
| Auto Run Now/countdown/cancel/manual overlap | `local_map_auto_scan_run_now/cancel` → scheduler → owned `map_scan_start` and status | A target counts only for same run/server **completed publication**; disable prevents next cycle | **P** bounded passive. Genuine multi-server cycles complete. Visible Auto Stop uses native Cancel; controlled exact-run Stop/no next target and UI save→cancel/stale event tests pass. Live cancellation, Run Now/manual overlap adversity remain open. |
| Auto target server/return navigation | Scheduler → `server_jump` provider → owned client/server check | Real target/return transition, no stale session; explicit native return failures surfaced as durable `MAP_AUTO_SCAN_RETURN_FAILED`, preserving successful target publication | **P** bounded passive multi-server. Genuine 2212→2213 acquisition and proven native 2213→2212 return; controlled injected failed return remains persisted as failure. Adverse live navigation not yet proved. |
| Coordinate and march-follow row actions | `map_coordinate_jump`, `map_march_follow` → owned client camera | Correct requested client view confirmed, rejection/timeout/session, not send-only | **P** passive navigation only (no march initiation). Current source supports commands; authoritative end-view proof and controlled adversity pending. |
| Server jump/history | `server_jump`, history get/set/import → action provider/SQLite | Valid range, observed reached server, dedup/history persist, no accidental cross-server action | **P** bounded passive navigation. Controlled provider validation exists; genuine cross-server target/return and history proof pending. |
| Resource/Monster/Truck/Treasure name and assets | `game_asset_image`, `map_data_options`, row projections/assets | Source-key identity, labels, missing names/error and profile-bound image access | **P** passive reads; MAP-005 Resource fallback names remain, all-kind proof pending. |
| Treasure batch inspection/refresh | `map_treasure_state_refresh/_all` → batches of 100, identity, persisted claim states | Current viewer/alliance, server/profile change, partial loading/errors | **C** controlled; passive inspection can be considered under passive Map scope only after confirming no account action. Source-backed inspection exists, full state proof pending. |
| Treasure claim status and single/boxes/season | `map_treasure_claim_status`, `map_treasure_claim` → `CurrentClientMap317ActionProvider` | **Provider explicitly unavailable**; claim needs authoritative reward/claimed-state confirmation, caps and cancellation | **C** implementation; **OWNER** live. UI claim buttons disabled, cannot claim complete. Find legitimate client producer. |
| Dispatch sharing | `map_dispatch_share_alliance` → current-client share response | True only on acknowledged share, partial row failures and ownership | **OWNER** live. Source-backed acknowledgement adapter exists; owner will test actual share/proof. |
| Scheduled Dispatch create/delay/Cancel/Clear/Retry | `map_dispatch_plunder_*` → validator/SQLite/worker → current-client pending result | Exact session/job terminal correlation, limits, restart and ambiguous outcome; queued != succeeded | **C** controlled; **OWNER** execution. Current source/worker exists, more identity/race tests needed. |
| Scheduled Ghost create/Cancel/Retry | Ghost task selection → `PrepareGhostPlunderTasksAsync` → persisted Ghost job/worker | **Preparation unavailable**; separate Ghost game transport absent. Cannot reuse Dispatch; no fake terminal result | **C** code; **OWNER** execution. MAP-006 guard blocks dispatch transport even for preexisting Ghost jobs; real provider still incomplete. |
| Scheduled Truck create/Plunder Again/Cancel/Clear/Retry | `map_truck_plunder_*` → SQLite/worker → train attack/result | Exact job/session, authoritative battle/reward, history/expiry/daily limit | **C** controlled; **OWNER** execution. Current producer exists, actual terminal game proof pending. |
| Scheduled jobs tab/counters/history | `map_plunder_jobs_list` plus list/clear/retry, changed events | Correct attempts, countdowns, state transitions, restart, terminal event correlation | **C** all local tests; **OWNER** account-changing arm. Current store/worker present, full adversarial coverage pending. |
| EN/light, JA/dark, narrow/error/accessibility | Production UI/IPC, localized messages, responsive harness | All new states and disabled/incomplete actions truthful, narrow overflow-free, no JS errors | **C**. MAP-005 EN/JA and release tests; re-run when changed MAP-006 code/UI settles. |

### Available-scope supplementary proof (later than the row snapshots)

- Genuine six-kind manual scan on official server 2212, run
  `4fe2ae0635e64db2b1f506bd9cfdb761`: **2,500/2,500** blocks, 0 failed,
  positive 7,060 City / 8,003 Resource / 8,850 Monster / 459 Truck / 2
  Railway / 1,710 Dispatch, all unique stored keys. Packaged UI rendered
  each kind with corresponding native stored counts. Private receipt stays
  ignored in `trial-available_20261011/scan-six_available_full.json`.
- Read-only native/SQLite audit passed all-kind stored query/options and
  page1/page2 identities, current Name-key and Dispatch level filters and
  12-column City XLSX. Corrected its negative false alarm caused by a Truck
  naturally expiring between separate query timestamps; source query uses
  real-time expiry and the auditor now brackets its observation times.
- Actual City star and marked-only yielded one persisted marked row;
  connected passive City coordinate navigation verified requested tile
  **(987,903)**. Truck and Railway display unexpired row subset/quality/goods;
  Dispatch status/level/quality rendered. Current game text metadata may
  still yield Unknown resource/monster labels; no fabricated names used.
  Reuse MAP-005 accepted scoped Clear and City export evidence.
- Genuine Auto Stop on Truck run `31e407e06cb640179d0a80cdc87175d1`
  cancelled first target 2212 with no 2213 target or data loss but exposed
  redundant native return-to-origin error. Fix: only return to origin after
  **attempting a non-origin target**. After rebuilt host restart, genuine
  second Stop `aec96863d8284e309527d17a6c164f8b` persisted
  `outcome=canceled`, `lastError=null`, no second target/staging/jobs and
  preserved exactly all six prior record counts. Controlled same-origin Stop
  regression and the distinct genuine cross-server returns pass.
- Live Connected sampled over multiple five-second polls throughout scan,
  browse, Auto and host restart, with no observed transient Checking. Genuine
  Home Close produced truthful stopped/offline. Held intermediate poll and
  profile A→B, restart and stale response races passed existing production
  callback/mounted WebView controlled checks. Exact cleanup/installed script
  restoration/disabled Auto proved in ignored `finish` and `reentry`.
- **Deferred/pending:** Ghost positive until natural 2026-10-15; owner-deferred
  seasonal/full Treasure and unavailable claim/Ghost action providers; live
  Dispatch/Truck plunder and alliance sharing are strictly owner-only.
  See `docs/MAP_006_OWNER_ONLY_VERIFICATION_CHECKLIST.md`. Whole Map is PARTIAL.

## MAP-006 corrections / differences so far

- **OWN_DESIGN / Auto Stop UI:** Auto tab now provides a Stop button when
  the scheduler reports `running`, invoking `local_map_auto_scan_cancel` via
  the ordered current-profile coordinator. Disabling future Auto retains its
  separate native semantics. Controlled exact-owner Cancel verifies one Stop,
  no published target, `canceled` outcome and no next server. JS tests save→
  cancel ordering and stale event fencing; production preview release smoke
  verifies the visible button. Live cancellation remains to be owner-observed.
- **OWN_DESIGN / Auto configuration admission:** Controlled production-boundary
  test holds the first target while selected kinds and server list are edited;
  the admitted cycle completes with its original targets, and a later Run Now
  uses the saved changes. Live Railway/Dispatch→Railway/Ghost correction was
  performed between cycles, not during an active acquisition.
- **OWN_DESIGN / correctness gate:** the Auto Scan scheduler now requires a
  matching run ID, server ID, explicit `completed` phase, and absent error before
  adding a target to `CompletedServerIds`. Previously a cancelled run without
  error could be reported as completed. Controlled production scheduler tests
  cover positive publication, stopped, failed, stale-run, wrong-server, terminal
  error, disabled-next-cycle and reopened state. Does not prove a genuine Auto run.
- **Safety fence / incomplete provider:** current Dispatch transport rejects
  Ghost jobs before reading a game session or writing a request. Legacy Ghost
  entries must not execute by a different game action. This is a protective fix,
  **not** completion of Ghost preparation or execution. Dedicated Ghost client
  support and exact terminal correlation remain required.
- **OWN_DESIGN / Auto return honesty:** if `ReturnToOriginalServer` was explicitly
  requested and the owned native return fails, the cycle persists
  `MAP_AUTO_SCAN_RETURN_FAILED` and has `Outcome=failed` instead of silently
  claiming an entirely completed cycle. Previously published scan target IDs
  are retained. Native controlled test verifies exact original server, one
  return attempt, no Stop of the published scan and error persistence after
  reopening the scheduler. No cross-server game navigation performed here.
- **Home connection-poll correction:** current-owner five-second `readStatusSnapshot`
  no longer clears an acknowledged pair or a previous native error at read
  *start*. Native failures still invalidate; actual offline updates, newer
  `bridge://status` events and profile A→B ownership retire old responses.
  Pushed as `c6955ebd`; frontend callback integration checks pass.

## Continuation

2026-10-11 lead tidy supersedes the earlier continuation below. Saved mixed live
capture is positive for City/Resource/Monster/Truck/Dispatch/Treasure; Railway and
Ghost remain zero/unproven. Saved later same-server Auto Truck completed with 434
published rows; earlier empty Auto capture is preserved. Fresh maintained native
and frontend checks pass. See reviews/2026-10-11-MAP-006-PARTIAL-LEAD-CHECKPOINT.md
for source/package limits, cleanup and the reproduced Connected/Checking UI bug.
The table above describes initial gaps, not final acceptance of each operation.
Worker has resumed MAP-006 gates after owner clarification. Scan method
comparisons and saved live timing are in the separate assessment. The successful
baseline stays intact; no direct acquisition speedup or positive Railway/Ghost
witness has been established. The separate genuine multi-server Auto Truck
trial completed through the normal production UI, demonstrated two positive
server-specific scans and native return to owner server, then stopped and
restored the original Lua triplet (ignored
`artifacts/map-006/trial-methods_20261011/` receipts). Never equate this
Truck-only proof with finished Auto behavior for all eight kinds or full Map.
Separate genuine Railway/Dispatch and Railway/Ghost Auto receipts in ignored
`artifacts/map-006/trial-missing_20261011/` show two unique Railway records on
servers 2213 and 2197, zero Ghost, plus Dispatch 256/241 on 2212/2213.
Every target finished 2,500/2,500 blocks, with proven return to original 2212.
The `finish` and `reentry` gates establish full process, Lua script and Auto
restoration. Positive Ghost and complete manual/Auto semantics remain open.

Checkpoint at start `172b3718ffc475b6fa066d1d185131d2913494f0`;
working changes: MAP-006 Auto terminal ownership gate/tests and Ghost transport
fence/tests. Next: expand controlled multi-kind capture/publication and Auto
adversity, resolve current-client producer gaps, then bounded authorised live
scans/Auto using a **new** isolated MAP-006 task root. Preserve all MAP-005
ignored artifacts/receipt/ZIP and PR #7. No new live game activity yet.
