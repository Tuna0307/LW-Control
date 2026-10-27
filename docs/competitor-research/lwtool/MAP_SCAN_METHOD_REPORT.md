> **Dated snapshot (2026-10-24).** Describes LW-Control as it was then. What is current, and where the archived files it cites live: [competitor-research README](../README.md).

# LwTool active scan method report

> Local Map closeout: [Map final qualification](../../design-notes/2026-10-25-map-final-qualification.md). Required final packaged gates now pass; earlier pending-package statements below are historical. Remote-service recovery limits are unchanged.

Date: 2026-10-24
Status: canonical recovered-method report for the Map comparison assignment.
Scope: sanitized client-side recovery from the shipped LwTool artifacts already retained under `artifacts/lwtool-connection-20261023/`, combined with existing accepted MAP-019 facts for comparison boundaries. This report contains source/function anchors and behavior summaries only; it does not reproduce proprietary recovered implementation.

**Owner local-only continuation, 2026-10-24:** the active *normal/local* LwTool scan, acquisition pacing and detail-hydration path is now independently traced, and its methods are compared to our qualified 63-reply point/625-rectangle mobile scanner in LOCAL_SCANNER_CONTINUATION.md (archived). The owner now prioritizes scanning through their own game and has ended the remote-service experiment. This original quick/remote analysis is retained as historical method evidence, not a recommendation to integrate its service. No current-source native acquisition change was made without an accuracy-qualified candidate.

No live LwTool or game control was used for this recovery. No production source was changed. Existing MAP-019 live measurements cited below are historical accepted evidence, not new results from this worktree or from this recovery pass.

## Executive answer

The active LwTool **quick Map** path does not perform the expensive normal Map scan through the user's local game. When Home `VIP Check` is enabled, the Map button submits a job to LwTool's remote scanner service, receives completed server data back, merges city/resource rows into the same in-memory result tables used by normal scanning, and progressively renders those rows. The shipped UI describes that remote source as a scanner-server cluster using separate accounts and multiple IPs in parallel (`recovered-ui.html:1472-1475`). The client does not expose the scanner farm's exact map-walking algorithm, its worker count, or its server-side cache/freshness policy.

The advertisement can therefore be supported only in a narrower form than its marketing wording suggests. The recovered client proves that quick Map asks the remote service for one selected server without a client-side ROI/row bound, and it proves that Map rows may include base/player identity, alliance, player/base level, total power, troop power and shield-related fields. It does **not** prove that every request freshly traverses every tile, and it does not prove a separate alliance-level field. The active UI shows **Alliance** and **Lv** as separate columns. The phrase `联盟等级` is therefore consistent with alliance plus player/base level, not a proven alliance-level metric.

The displayed quick-scan duration is also not an end-to-end equivalence timer for every advertised scanner feature. For Map it covers the background remote job and local merge up to LwTool's quick-completion event, but starts after initial UI/RPC setup and ends before final UI paint. For Secret Task and Ghost, visible completion can occur before later reward/name hydration has finished. Alliance Secret is not part of the remote `py_scan_nhanh` route at all; it uses a separate game-backed request/detail path with its own fixed waits.

## Advertised claim versus recovered behavior

Owner-supplied advertisement:

> 全图扫描 基地玩家 联盟等级 战力 部队战力
> 隐秘任务 联盟隐秘 幽灵行动 卡车 （扫描只需要几秒的时间）

The recovered client supports the following answers:

| Advertised item | Recovered active behavior | What is actually proved |
| --- | --- | --- |
| `全图扫描` / full-map scan | Quick Map sends the selected server to the remote `/api/scan` job with no client-side Map ROI/row range. Empty Map server input resolves to the user's current server. | The **client contract** asks for the whole selected server and can report `chua_quet` incomplete servers. Exact remote AOI/grid traversal, per-request freshness and server-side coverage are **unknown**. |
| Base/player rows | Quick Map receives city rows from the remote service and merges them directly into the Map table. | Base/player identity and displayed city metadata are present when supplied by the remote payload. |
| Alliance | The active Map table has an Alliance column and consumes alliance name/abbreviation fields. | Alliance display is supported. |
| `等级` / level | The active Map table has a separate Lv column sourced from city `level/lv`. | Player/base level is supported. No separate alliance-level field was found. |
| `战力` / power | Quick Map does not locally call the normal player-info hydration route before display. | Displayed total power must already be in the remote payload when present. |
| `部队战力` / troop power | Same quick Map behavior; the renderer consumes `armyPower`. | Displayed troop/army power must already be in the remote payload when present. |
| Secret Task | Supported by the remote quick endpoint. | Identities/table rows can arrive from the remote job; rewards/details may be filled later by the local game path and are capped/prioritized. |
| Alliance Secret | Separate active scanner tab and separate game-backed method. It is **not** accepted by `py_scan_nhanh`. | The generic remote few-second path does not establish Alliance Secret completion. |
| Ghost Ops | Supported by the remote quick endpoint. | Base rows can arrive remotely; owner-name/reward/detail hydration may continue afterward. |
| Truck | Supported by the remote quick endpoint. | Remote results are streamed/merged, but the recovered client does not establish an equivalent unfiltered full-census contract to LW-Control's current Truck proof. |
| “only a few seconds” | LwTool displays the `giay` value emitted when its background quick job completes. | This is a client job timer, not proof of cold, fresh, field-complete, all-feature end-to-end equivalence. |

## Active quick Map call path

The active button-to-result route is:

1. Scanner page and Map schema: `recovered-ui.html:509-542`. `btnScanMap` starts the Map flow. The web UI still requires a connected game through `requireGame()` (`recovered-ui.html:522,1750-1754`).
2. Quick-mode selection: `toggleScan()` chooses `scanNhanh('map')` when `nhanhBat('map')` is true (`recovered-ui.html:3791-3797`). Home `VIP Check` controls this route through `_nhanhOn()` (`recovered-ui.html:1489-1513`); the core default is disabled (`recovered-core.py:14853-14854`).
3. UI submission: `scanNhanh()` clears the old table, calls `eel.py_scan_nhanh(...)`, then returns while background events drive progress (`recovered-ui.html:1611-1640`).
4. Core entry: `py_scan_nhanh()` accepts only `map`, `task`, `supplies`, `ghost` and `truck` (`recovered-core.py:36514-36524`). Its Map branch clears `city`, `resource` and incomplete-server state before starting the job (`36596-36609`).
5. Server selection: `_ds_server_quet()` is the common resolver (`recovered-core.py:25946-26005`). For Map, empty input means the current/own server only (`25974-25987`); a numeric input selects that server.
6. Remote acquisition: `_scan_nhanh_lay()` and `_scan_nhanh_lay_once()` submit one client job and read its result (`recovered-core.py:24953-25208`). The request is a POST to `/api/scan` carrying scan type, resolved servers and a per-server streaming preference (`25046-25064`). No Map ROI, AOI row range or tile range is sent by this client path.
7. Streaming: when the service enables per-server streaming, the client reads `/api/scan/{job}/phan` on a reused connection (`25093-25176`). Each completed part is handed immediately to the merge callback. Final metadata includes incomplete/not-scanned and empty-server information.
8. Merge: each streamed part flows through `_dat_ket_qua(..., noi=True)` from `py_scan_nhanh()` (`recovered-core.py:36612-36626`). The Map branch merges/deduplicates city/resource rows into `_wres()` and publishes `scan_partial` (`25520-25584`).
9. UI transport: `_push_event()` uses one serialized/coalescing UI worker, and `_ui_emit()` invokes `window._onEvent` through the webview bridge (`recovered-core.py:21735-21845`). The UI's quick event handler updates progress/completion state (`recovered-ui.html:3580-3607`).

This means the user's local game connection remains a UI prerequisite, but quick Map's principal city/resource acquisition is remote HTTP rather than the normal local game sweep.

## Field provenance

### Quick Map

The Map merge path at `recovered-core.py:25554-25584` does not call the normal local player-info hydration route. It normalizes point identity, aliases level/country fields, and can supplement `ownerSrv` from the client's existing `_owner_cache`.

The renderer consumes city fields including `level/lv`, `power`, `armyPower`, alliance name/abbreviation, country, shield/protection end, point identity and map server (`recovered-ui.html:6405-6515`). Because quick Map skips local `get.user.info.multi`, those displayed name/alliance/level/power/troop-power/country/shield values must come in the remote city payload when they are available. `ownerSrv` is the notable client-cache supplement and can remain unknown when the cache has no owner record.

The active Map UI column set is visible at `recovered-ui.html:540-542`: Srv, Name, Alliance, flag, Lv, Power, Troops and Shield. There is no independent alliance-level column in this table.

### Normal Map contrast

The normal Map path is useful because it proves which work quick Map avoids. `_run_full_scan()` first acquires city/resource point identities locally and then hydrates city owners through the game network (`recovered-core.py:20938-21410`). Its player-info phase batches 100 UIDs per request and schedules up to 25 such batches per injected group with short request spacing (`21225-21266`). The corresponding replies populate name, level, alliance, total power, troop/army power, country and authoritative owner server (`21282-21308`).

Quick Map does not execute that client-side hydration phase. Its speed therefore comes at least partly from receiving already-enriched city rows from LwTool's remote service rather than discovering identities and then hydrating every owner through the user's own game connection.

### Secret Task and Ghost completion is different

For remote quick Secret Task/Ghost, map/identity data alone does not contain every later UI field. `_scan_nhanh_vat_pham()` explicitly requests point details through the game for rewards (`recovered-core.py:25784-25859`). It sends detail requests in batches of 50, prioritizes the useful rows, and caps that work at 1200 points. `_scan_nhanh_bu_vat_pham()` explicitly documents that the table is already displayed before those rewards are filled (`25862-25895`). `_scan_nhanh_ten_chu()` can also resolve missing owner names/alliance abbreviations asynchronously (`25898-25930`).

Consequently, a quick-scan `xong` event for these kinds is not equivalent to “every displayed/enrichable field has completed hydration.”

## Timing and completion boundary

The quick timer starts inside the background `_do()` created by `py_scan_nhanh()` (`recovered-core.py:36578-36580`). By that point the button click, Eel/webview call, argument parsing and server-list resolution have already occurred.

The timer ends when core emits the `scan_nhanh` `xong` event with a one-decimal elapsed-seconds value calculated from the background worker's start time (`recovered-core.py:36673-36678`). The UI prints that value in the quick completion status (`recovered-ui.html:3605-3607`).

For Map, this duration includes remote job submission/waiting/stream processing and local merge up to the completion event. It excludes at least:

- UI/button handling and the portion of RPC setup before the background worker starts;
- any final webview rendering after the completion event is queued;
- connection/login/preload time before the user can press Scan Map;
- any freshness work that might occur inside the remote scanner farm before/independently of this client job, because that server-side lifecycle is not visible in the client;
- for other quick kinds, post-completion reward/name/detail hydration described above.

A fair “few seconds” comparison must therefore measure common external boundaries such as button press to first row, all identities acquired, all required fields hydrated, durable publication complete and results fully browsable. LwTool's displayed `giay` cannot by itself be compared directly with LW-Control's full pipeline time.

## Client-side cache and cold/warm behavior

The quick client has an explicit 30-second submit/result dedupe cache: `_SCAN_SUBMIT_TTL`, `_scan_submit_cache` and `_scan_nhanh_lay()` are at `recovered-core.py:24953-25043`.

For the same scan type/server/options key:

- an in-flight duplicate shares the same entry and does not submit a second POST;
- already-received streamed parts can be replayed to the duplicate caller;
- a completed result can be returned from the same entry while its TTL remains valid;
- the success expiry is anchored to the original submit start, so a job taking longer than about 30 seconds has little or no post-completion replay window.

The UI and core both clear previous relevant Map rows before a new quick request (`recovered-ui.html:1616-1620`; `recovered-core.py:36596-36609`), so the old visible table is not simply left in place and called a new result. However, an immediate identical rerun can still be client-warm because the job/result entry survives briefly in RAM.

`_owner_cache` is another in-memory warm state, capped at 5000 entries (`recovered-core.py:1009-1018`). It can improve owner-related supplementation on later operations. It is not the source of quick Map power/troop-power fields.

Any **server-side** scanner cache, account readiness, pre-swept data, refresh cadence or TTL remains unknown. That unknown is essential when defining a cold comparison.

## Result storage and publication semantics

LwTool scanner results are primarily held in process RAM per window. `_w_scan_results` and `_wres()` are defined at `recovered-core.py:17590-17594,18410-18429`. `py_get_scan_data()` reads that live store (`31975-31985`), while `_scan_data_snapshot()` creates transient bounded pagination snapshots (`26490-26548`). The overlay SCANDATA mapping (`18688-18743`) is an IPC/render transport snapshot, not durable scan-result storage.

No durable scan-result database/file persistence path was found in the recovered quick Map pipeline. The client can progressively expose streamed parts before the remote job finishes. When the newer row-pull mode is enabled, `_scan_job` / `_scan_bao` publish incremental `scan_changed` notifications and distinguish `done` from later in-place refreshes (`recovered-core.py:21859-22046`).

This is materially different from LW-Control's Map publication contract documented by MAP-019: LW-Control stages data per exact scan run and replaces selected published kinds only after the run reaches its exact completion/checkpoint gate. A fair comparison should therefore distinguish **first visible rows** from **atomic committed result** rather than treating both products' completion labels as the same event.

## Full-map coverage: what is known and unknown

The strongest defensible statement from the recovered LwTool client is:

> Quick Map requests one selected server as a whole-server logical job, with no client-side ROI/row bound, and the client has an explicit incomplete-server result (`chua_quet`).

The following are **not** established by the shipped client artifacts:

- the remote backend's AOI/block geometry;
- exact tile/row count or scan footprint;
- whether every quick request initiates a fresh full-world traversal;
- whether some or all rows come from a service-side retained cache;
- scanner-account count per job or effective parallelism;
- request pacing/rate limits inside the scanner farm;
- freshness/expiry of player power, troop power, shield state or moving objects in the remote payload;
- whether a successful service job can omit a populated region without marking it incomplete.

The UI does surface `chua_quet` as a Map warning (`recovered-ui.html:6457-6477,7311-7317,7344-7346`), which is evidence that the client distinguishes some incomplete-server outcomes from a truly empty result.

The **normal** local Map fallback has explicit client coverage geometry: `_run_full_scan()` defaults to the full 100-row world and calls the block-route helper with 100 points per row, ten-row blocks and the city/resource point-type set (`recovered-core.py:20938-21029`; `_run_task_blockroute` begins at `20606`). That normal geometry must not be projected onto the remote scanner service without server-side evidence.

## Adjacent advertised scanner kinds

### Secret Task

`task` is an accepted `py_scan_nhanh` kind (`recovered-core.py:36514-36524`). Remote identities can populate the table quickly, but the quick result is filtered/prioritized by the client's task rules (`recovered-core.py:25586-25625`) and later reward detail work can continue after visible completion. This is not evidence for a complete unfiltered census equivalent to LW-Control's Dispatch/Secret Task contract.

### Ghost Ops

`ghost` is accepted by `py_scan_nhanh`. Rows can stream from the remote service, while missing names/reward details can be resolved later through local caches/game detail queries. Completion semantics therefore need field-by-field checking in any matched benchmark.

### Truck

`truck` is accepted by the remote quick endpoint. The recovered client streams/merges those results, but its filtering/selection behavior (`recovered-core.py:25689-25738`) differs from LW-Control's MAP-019 Truck definition. Existing cross-product documentation therefore treats LwTool's fast Truck output as a selected/filtered result set rather than a proven unfiltered full census.

### Alliance Secret

Alliance Secret is the clearest advertised exception. `py_scan_nhanh()` does not accept an Alliance Secret kind. The active web path `scanAllianceTask()` sends the alliance-task request, waits, requests per-point details twice, waits again, and then reads the manager/config/detail state (`recovered-ui.html:4943-4974`). The core equivalent `py_scan_alliance_task()` follows the same game-backed method and includes fixed waits of about 1.7 s, 1.6 s and 2.4 s before final bridge/read overhead (`recovered-core.py:36262-36368`).

Therefore the advertisement's combined wording must not be interpreted as proof that Alliance Secret is served by the same remote scanner farm or completes within the same quick-job timer.

## Active, fallback and dormant paths

| Path | Classification | Evidence / role |
| --- | --- | --- |
| UI `toggleScan` -> `scanNhanh('map')` -> `py_scan_nhanh` -> remote `/api/scan` | **Active quick Map** | Selected when `VIP Check` quick mode is active. Anchors above. |
| `_vip_scan_thuong()` -> `py_start_scan()` -> `_run_full_scan()` | **Active normal fallback** | Used when quick/VIP path is server-disabled; `recovered-core.py:12537-12558,31627-31702,20938-21410`. |
| `_run_full_scan()` unified block-route point acquisition | **Active normal primary** | Preferred normal city/resource path. |
| `Api.CityResourceScan*` inside `_run_full_scan()` | **Active conditional fallback** | Used if the preferred unified route returns no usable points or misses expected resource type 7; `recovered-core.py:21038-21093`. |
| `py_scan_alliance_task()` | **Active separate feature path** | Alliance Secret; game-backed and outside `py_scan_nhanh`. |
| `_mapscan*` / `_userscan*` globals referenced by cleanup | **Legacy/dormant for quick acquisition** | Cleanup references remain (`recovered-core.py:21189-21199,31712-31713`), but no evidence shows the active quick Map button using them as its principal source. |
| Auto-attack remote monster caches | **Active elsewhere, not Scanner Map** | Separate consumer of the quick-scan service with independent 60/120-second cache behavior (`recovered-core.py:12864-13031`); it must not be used to infer Scanner Map caching. |

## Targeted native check

The recovered Python/UI source already established the active quick route. A targeted follow-up search of the supplied `lwtool.dll` and the three available native decompile JSONs found no direct strings/anchors for `/api/scan`, `scan_nhanh`, `CityResourceScan`, `world.get.block`, `get.user.info.multi` or `Lw_Api`.

That negative targeted search means there is no stronger supplied native anchor for the remote scanner backend. It does **not** prove that equivalent logic cannot be obfuscated or implemented outside those artifacts. Whole-program decompilation was not repeated because the assignment requires targeted native work only when a concrete client-source gap remains, and the remaining gap is server-side scanner-farm behavior that is not present in the client package.

## Fair-comparison implications for LW-Control

The existing accepted MAP-019 evidence supplies a strong LW-Control baseline, but it does not by itself prove that LW-Control is faster or slower than LwTool's recovered remote service. The products currently perform different work at different completion boundaries.

### Existing supported LW-Control facts

`docs/MAP_019_SCAN_METHOD_REVIEW.md` records same-session current-versus-old-camera comparisons for the historical nine Map kinds. The accepted historical ranges include:

- camera-free point kinds at roughly **16–24 s** versus approximately **189–201 s** for the old camera scan;
- Truck roughly **52–79 s** versus approximately **198 s** camera;
- Railway roughly **60–61 s** versus approximately **205 s** camera;
- a historical all-nine combined scan around **114–120 s** on the populated remote test server;
- field comparisons, not only counts, for the compared kinds;
- rejected alternatives where smaller request batches were slower, the coarse Zombie Boss route was much faster but lost 60–75% of bosses, and tested overlapping requests lost substantial data;
- source review showing ordinary foreign-server replies are dropped by the game's current parser, so the retained LW-Control remote route preserves the qualified server-context switch/return behavior;
- verified-empty logic fixed for Zombie Boss only when complete source coverage and a live source are proved, rather than accepting an unproven zero.

These are retained historical MAP-019 results. This report does not re-label them as current-worktree measurements and does not claim a new LwTool-vs-LW-Control live speed result.

### What must be matched before comparing “few seconds”

A valid cross-product trial needs the same server/time window and the same definition of work. At minimum it should record:

1. selected kind(s), server and object-family definition;
2. cold/warm state, including LwTool's 30-second client dedupe window and any disclosed/observable remote-service cache state;
3. button press to first visible row;
4. acquisition completion for all typed identities;
5. required-field completion, especially power/troop power, owner/alliance and task/ghost rewards;
6. incomplete/verified-empty status and independent coverage checks;
7. durable publication/commit where the product has one;
8. fully browsable UI completion;
9. server-context switch/return time when required;
10. several repetitions to separate genuine speed from moving-object drift and warm-cache reuse.

The correct comparison target for LwTool quick Map is therefore not simply LW-Control's scanner stopwatch. LwTool's remote service pre-enriches at least some player fields outside the user's local acquisition route, exposes streamed rows progressively, and has no recovered durable publication boundary. LW-Control's MAP-019 contract includes exact coverage/freshness checks and transactional selected-kind publication. Those differences must either be normalized or reported separately.

### What the recovered method does suggest for LW-Control

The source-backed lesson is architectural, not a mandate to copy the competitor route:

- removing per-player local hydration from the critical path can materially reduce client-side work when an authoritative enriched source already exists;
- progressive transport/rendering can improve time-to-first-useful-row without claiming full completion early;
- batching/serialization overhead matters after acquisition, but it should be optimized without weakening exact-run completeness and publication gates;
- an external scanner farm can trade local latency for infrastructure outside the user's game process, so its headline time is not directly comparable to a self-contained local scan without equal freshness/coverage proof.

Existing MAP-019 evidence also constrains unsafe optimizations: previously tested coarse acquisition and naive parallel overlap improved speed only by losing recall or replies. The recovered LwTool source does not overturn those results because its remote farm is a different acquisition system.

### Current MAP-019 qualification boundary

`docs/research/lwtool/2026-10-24/map/QUALIFICATION_REMAINING_GAPS.md` records that Desktop checks, canonical UI checks and 49 Lua suites have passed, and that MAP-019 now covers the historical nine kinds plus genuine `other`, a true all-ten current-source publication, Truck experiments, typed/freshness audits and exact Stop/script restoration.

The final same-session all-ten ABBA at `artifacts/map014-lead-live/native-benchmark-review-20261024-032125/` completed direct **90.713 / 91.327 s** versus camera **142.766 / 142.512 s**, with every round 2500/2500 and zero failures. Its sanitized audit is `artifacts/map019/20261024-allten-final-abba-audit.json`. The all-ten/auto-all-ten qualification harness is also implemented and fail-closed tested.

The source-pinned `9ddd0ed8` Windows package now exists and independently passes ZIP/extraction/source-manifest checks. Packaged Manual and Auto all-ten, current-staging Stop, persistence/Clear sanity, bounded Home reconnect/Stop smoke and final independent production-code review remain open. See WORKER_STOP_CHECKPOINT.md (archived).

An inert transactional staging benchmark already shows the current candidate can stage a deterministic 24,362-record workload substantially faster than the old per-record staging path while preserving canonical staged/published hashes and row counts (`artifacts/map019/staging-benchmark/README.md`: old median 2483.671 ms, batch median 672.559 ms). Because that benchmark does not start LWBridge, the game or any native scan owner, it is evidence for the local persistence bottleneck only; it is **not** a new live scan-time result.

## Exact limitations

This recovery can establish the client-visible contract and orchestration, but not the scanner farm's internals. The following claims remain unsupported and must not be promoted to fact:

- that LwTool freshly scans every map tile on every quick Map request;
- that a successful quick job has the same typed-identity completeness as LW-Control's qualified source proof;
- that remote power/troop/shield values have a particular freshness bound;
- that LwTool's server-side cache is empty in a “cold” client run;
- the number of scanner accounts/workers or actual parallelism per server/job;
- an exact remote AOI/block/request count;
- that Alliance Secret is included in the remote quick path;
- that `联盟等级` means an alliance-level statistic;
- that the displayed quick `giay` equals button-to-fully-hydrated-and-browsable elapsed time;
- that the recovered LwTool route is faster than LW-Control under equivalent cold/fresh/field-complete work;
- that a final packaged LW-Control build has the same timing as the current-source live benchmark before the remaining final-package gates are run.

## Plain-language method

With `VIP Check` enabled, pressing **Scan Map** still requires LwTool to see a connected local game, but the quick Map rows are principally obtained from LwTool's own remote scanner service. The client asks that service for one selected game server, receives a job, streams completed data back, merges city/resource objects into per-window RAM and feeds the ordinary scanner UI. The remote city rows already carry the player fields that normal Map would otherwise hydrate through batches of local player-info requests. This is the strongest source-backed explanation for why LwTool can show Map results much sooner than its own normal scan.

That mechanism also explains why the marketing timer needs careful interpretation. The displayed duration begins only inside the background quick worker, immediate duplicate jobs can reuse a short in-memory result window, and other scanner kinds can continue detail hydration after the table is already visible. The client proves a whole-selected-server request and explicit incomplete-server handling, but it does not reveal whether the remote farm performed a fresh full-world sweep for that request. Fair comparison with LW-Control therefore has to match identity coverage, freshness, fields and completion boundaries, not just compare the two products' status-label seconds.
