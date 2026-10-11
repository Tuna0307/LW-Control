# MAP-006 — finite operation and result-gate matrix

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

Authority codes: **P** = current-server passive capture and same-server Auto
are approved after isolation/identity/backups; **C** = controlled only without
further consent; **ASK** = explicit permission before any live navigation,
sharing, claim, or plunder. No paid/game-combat/unrelated actions are approved.

| Visible operation / purpose | Production entry → result boundary | Missing proof or defect / actual result gate | Authority, existing evidence, next action |
| --- | --- | --- | --- |
| Home for Map / select exact profile and connected server | `LWBridgeWindow` lifecycle → owned current-client session | Exact current build, owner and clean script restoration | **P**. Released single-game Home; recheck on task isolation before any new live scan. |
| Manual City / Player City records | `map_scan_start` → provider/engine → SQLite → City tab | Fresh full completion, Stop, persistence, queries and export | **P**. Submitted MAP-005/PR #7 with real server-2212 evidence; preserve/retest affected code only. |
| Manual Resource / Resource Points | Same scan path → Resource tab/status/name lookup | Actual record identity and resource-name fallback resolved where metadata exists | **P**. MAP-005 actual Resource records; some labels still `Unknown resource`. Check current-game metadata, not fabricated strings. |
| Manual Monster / world enemies | Type `monster` → full-world AOI/monster protected detail → `map_search` | Positive current natural population, correct kind/template names, persisted records, Stop | **P**. Current direct provider exists; no MAP-006 genuine witness yet. |
| Manual Truck / railway-truck entries | Type `truck` → direct `TryGetTrainList(true)` producer → `map_search` | Positive source, train/server identity, mode, current-state/name/goods | **P**. Direct source exists; no MAP-006 genuine witness yet. |
| Manual Train/Railway | Type `railway` → direct train-list producer → Train tab | Correct kind split, positive records, persistence and goods | **P**. Direct source exists; no MAP-006 genuine witness yet. |
| Manual Secret Task/Dispatch | Type `dispatch` → AOI producer → Dispatch tab | Fresh qualified rows, states, protected time and sort/filter validity | **P**. Current extractor source exists; positive live proof missing. |
| Manual Ghost Ops | Type `ghost` → AOI producer → Ghost tab | Fresh positive Ghost rows, correct expiry/owner identity; must not transport as Dispatch attack | **P** scan; **ASK** attack. Provider source exists; no positive MAP-006 witness. Dispatch transport now explicitly rejects Ghost. |
| Manual Treasure | Type `treasure` → AOI producer → Treasure tab | Natural positive rows and correct alliance/expiry/state/name/assets | **P** scan; **ASK** claim. Source/inspection exists; positive MAP-006 proof pending. |
| Mixed kinds, Normal/Fast and empty selection | `MapScanStateMachine` start → engine selected kinds, 8/20 admission | Duplicate Start, selected-only publication and retention of other kinds, source block coverage | **P**. MAP-005 City/Resource plus controlled engine, expand to all eight and mixed current extraction. |
| Scan busy/progress/Stop/error/failed block | `map_scan_status`/`map_scan_stop` → state/engine/sink/SQLite | Acquisition vs staged vs published; exact-owner cancel and late/teardown races | **P**. MAP-005 controlled and real City/Resource; other-kind genuine and profile switch pending. |
| Stored browse/tabs/search/filter/sorts/pages | `map_search`, `map_data_options`, `map_summary` → `MapStore` | Each kind's correct visible columns, all conditional filters, stale result/profile race and persisted rows | **P**. City/Resource accepted; `MapDataPage` uses request generation fences; others need fresh source evidence. |
| City Excel / result file | `map_city_export` → native Save As → XLSX writer | Exact filtered fresh City rows/headers/independent reopen; Resource has no export | **P**. MAP-005 verified; other export is not an advertised control. |
| Player star/mark and task selection | `map_player_mark_set` → SQLite; row selection state in UI | Correct current row identity, persistence, tab return, no wrong-profile mark | **P** local. MAP-005 real City mark verified; selection/actions for other kinds pending. |
| Clear data | `map_scan_clear` → store transaction scoped to task server | No active scan, no unrelated server/profile loss, Empty UI/SQLite | **P** task DB only. MAP-005 controlled genuine isolated clear verified. |
| Auto enable/servers/ordering/types/interval/speed/return | `local_map_auto_scan_config_set/status` → profile JSON + frontend hydration | Stable ordering and dedupe, type protection, concurrent updates, restart | **P** same server; **C** cross-server only. Existing implementation/tests partial; expand races. |
| Auto Run Now/countdown/cancel/manual overlap | `local_map_auto_scan_run_now/cancel` → scheduler → owned `map_scan_start` and status | A target counts only for same run/server **completed publication**; disable prevents next cycle | **P** bounded same server. MAP-006 controlled terminal result/identity fix and reopen tests pass; actual UI Auto cycle pending. |
| Auto target server/return navigation | Scheduler → `server_jump` provider → owned client/server check | Real target/return transition, no stale session, no unauthorized cross-server movement | **C** multi-server; **ASK** any live cross-server. Same-server `server_jump` is no-op when already there. |
| Coordinate and march-follow row actions | `map_coordinate_jump`, `map_march_follow` → owned client camera | Correct requested client view confirmed, rejection/timeout/session, not send-only | **ASK** live; current source supports commands but authoritative end-view proof and controlled adversity pending. |
| Server jump/history | `server_jump`, history get/set/import → action provider/SQLite | Valid range, observed reached server, dedup/history persist, no accidental cross-server action | **ASK** live; controlled implementation and current-provider validation to review. |
| Resource/Monster/Truck/Treasure name and assets | `game_asset_image`, `map_data_options`, row projections/assets | Source-key identity, labels, missing names/error and profile-bound image access | **P** passive reads; MAP-005 Resource fallback names remain, all-kind proof pending. |
| Treasure batch inspection/refresh | `map_treasure_state_refresh/_all` → batches of 100, identity, persisted claim states | Current viewer/alliance, server/profile change, partial loading/errors | **C** controlled; passive inspection can be considered under passive Map scope only after confirming no account action. Source-backed inspection exists, full state proof pending. |
| Treasure claim status and single/boxes/season | `map_treasure_claim_status`, `map_treasure_claim` → `CurrentClientMap317ActionProvider` | **Provider explicitly unavailable**; claim needs authoritative reward/claimed-state confirmation, caps and cancellation | **C** implementation; **ASK** live. UI claim buttons disabled, cannot claim complete. Find legitimate client producer. |
| Dispatch sharing | `map_dispatch_share_alliance` → current-client share response | True only on acknowledged share, partial row failures and ownership | **ASK** live. Source-backed acknowledgement adapter exists; no MAP-006 live permission/proof. |
| Scheduled Dispatch create/delay/Cancel/Clear/Retry | `map_dispatch_plunder_*` → validator/SQLite/worker → current-client pending result | Exact session/job terminal correlation, limits, restart and ambiguous outcome; queued != succeeded | **C** controlled; **ASK** execution. Current source/worker exists, more identity/race tests needed. |
| Scheduled Ghost create/Cancel/Retry | Ghost task selection → `PrepareGhostPlunderTasksAsync` → persisted Ghost job/worker | **Preparation unavailable**; separate Ghost game transport absent. Cannot reuse Dispatch; no fake terminal result | **C** code; **ASK** execution. MAP-006 guard blocks dispatch transport even for preexisting Ghost jobs; real provider still incomplete. |
| Scheduled Truck create/Plunder Again/Cancel/Clear/Retry | `map_truck_plunder_*` → SQLite/worker → train attack/result | Exact job/session, authoritative battle/reward, history/expiry/daily limit | **C** controlled; **ASK** execution. Current producer exists, actual terminal game proof pending. |
| Scheduled jobs tab/counters/history | `map_plunder_jobs_list` plus list/clear/retry, changed events | Correct attempts, countdowns, state transitions, restart, terminal event correlation | **C** all local tests; **ASK** account-changing arm. Current store/worker present, full adversarial coverage pending. |
| EN/light, JA/dark, narrow/error/accessibility | Production UI/IPC, localized messages, responsive harness | All new states and disabled/incomplete actions truthful, narrow overflow-free, no JS errors | **C**. MAP-005 EN/JA and release tests; re-run when changed MAP-006 code/UI settles. |

## MAP-006 corrections / differences so far

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

## Continuation

2026-10-11 lead tidy supersedes the earlier continuation below. Saved mixed live
capture is positive for City/Resource/Monster/Truck/Dispatch/Treasure; Railway and
Ghost remain zero/unproven. Saved later same-server Auto Truck completed with 434
published rows; earlier empty Auto capture is preserved. Fresh maintained native
and frontend checks pass. See reviews/2026-10-11-MAP-006-PARTIAL-LEAD-CHECKPOINT.md
for source/package limits, cleanup and the reproduced Connected/Checking UI bug.
The table above describes initial gaps, not final acceptance of each operation.
Owner discussion is next; resume remaining MAP-006 gates afterward.

Checkpoint at start `172b3718ffc475b6fa066d1d185131d2913494f0`;
working changes: MAP-006 Auto terminal ownership gate/tests and Ghost transport
fence/tests. Next: expand controlled multi-kind capture/publication and Auto
adversity, resolve current-client producer gaps, then bounded authorised live
scans/Auto using a **new** isolated MAP-006 task root. Preserve all MAP-005
ignored artifacts/receipt/ZIP and PR #7. No new live game activity yet.
