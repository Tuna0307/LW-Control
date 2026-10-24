# LwTool vs LW-Control full product method matrix

Date: 2026-10-23

Implementation branch: `codex/lwtool-full-improvement`

Released comparison baseline: `main@45e151c87350c8b8cf4d046a889ac6a735c12556`

Recovered LwTool backend SHA-256:
`f041b8b38cd71764c2a854a0b16d1038898395ca6903e8cd52a5d7cfdfb0168d`

Recovered LwTool UI SHA-256:
`b2b08cf2fb607612350a6e6333bedbc2554cb469e465c74c2b000c015a0cf317`

Authenticated LwTool native DLL SHA-256:
`aa5f5156e872d0301aece10d662153914946b935a00e5fcb54c472f194e03831`

This matrix covers active production paths only. Retained, historical,
feature-gated and removed code is not credited as normal behavior merely
because it still exists in a source file.

Decision vocabulary:

- **IMPLEMENTED** — a competitor-supported principle exposed a worthwhile,
  architecture-compatible improvement and it was implemented on this branch.
- **RETAIN** — current LW-Control behavior is at least as strong for the owner’s
  accepted product and should not be changed for parity.
- **DEFER** — potentially useful, but a concrete correctness/evidence blocker
  remains.
- **REJECT** — copying the competitor behavior would weaken accepted accuracy,
  ownership, product scope or safety.

## Home, profiles and lifecycle

| Item | LW-Control active method | LwTool active method | Decision |
| --- | --- | --- | --- |
| Start/attach | Per-profile `OverviewLifecycleService` creates a random session/challenge, prepares the authenticated pipe binding, augments the official Lua package and launches the official launcher. `src/LWBridge.Desktop/ProfileRuntimeOwner.cs:74-156`; `OverviewLifecycleService.cs:810-956`. | Home reaches hard-enabled Route-C: `recovered-ui.html:393-413,3764-3771`; `recovered-core.py:30197-30259,30422-30556,30564-30633`. Native `RcOrchestrateSingle` creates the official launcher suspended, APC-loads its bridge, hooks child creation and binds the real game child. | **RETAIN** both architecture choices. Do not import remote injection just to reduce steps. |
| Initial process identity | Selected installation, exact game PID/path/start time, profile/session/challenge and authenticated pipe client image. `tools/run_overview_bridge.py:948-977`; `LWBridgeControlPipeIsolatedHandshake.cs:83-150`. | Route-C BOOT nonce/key/instance + expected launcher/game paths + Job Object + thread/process/job verification and child state. Native evidence in `docs/research/lwtool/2026-10-23/evidence.json`. | **RETAIN** LW exact owner continuity. LwTool proves strong startup identity but does not justify weakening ours. |
| Native/game readiness | Helper exact ready record plus authenticated route and fresh matching heartbeat. `tools/run_overview_bridge.py:1250-1301`; `OverviewLifecycleService.cs:1752-1793,1835-1884`. | Native BOOT ACK is intermediate; final connected requires numeric server ID, valid authenticated session and UID verification. `recovered-core.py:30422-30556,31349-31615`. | **RETAIN** LW single exact-session readiness predicate. |
| Slow readiness | Bounded helper supervision; failed exact-session readiness stops/restores the exact owned game. `OverviewLifecycleService.cs:956-1018`. | `_late_connect_watch` retries a still-running game every 5 s for up to 600 s. `recovered-core.py:31349-31402`. | **DEFER** LwTool-style late-connect. Safe adoption needs a new exact-owner pending journal state, Stop semantics and stale-successor tests; no fresh evidence shows current 90 s ready failures justify that complexity. |
| Same-session transport reconnect | Game-side bridge reconnects the same authenticated profile/session/token once per second, bounded at 90 attempts. `tools/current_overview_bridge.lua:349-457`. | Shared-memory IPC health/reconnect plus five-second monitoring and late-connect logic. `recovered-core.py:24074-24387`. | **RETAIN**; do not duplicate an already-supported transport reconnect. |
| Whole-game recovery | Two-second recovery monitor; explicit hang/disconnect/login thresholds and bounded retry policy. `OverviewLifecycleRecovery.cs`; `OverviewRecoveryPolicy.cs:8-36`. | Five-second watcher/reconnect loops, false-positive process checks and IPC/memory confirmation. `recovered-core.py:24074-24387`. | **RETAIN** current exact-owner policy. Both are substantive; no source-only winner. |
| Stop | Exact profile/session/PID/path/start identity; prove exit, prove no selected-install survivor, restore exact backup. `tools/run_overview_bridge.py:1580-1693`. | Normal Home uses `taskkill /F /IM LastWar.exe /T`. `recovered-core.py:31617-31625`. | **RETAIN** LW; **REJECT** image-wide Stop. |
| Cancellation during Start/Stop | Cancellation is fenced around publication, lease, route and restoration; stale effects cannot retire a successor. `OverviewLifecycleService.cs:956-1052`; `tests/LWBridge.Desktop.Checks/HomeR2RecoveryChecks.cs:916-1009`. | LwTool has stop/reopen controls and multi-generation cleanup, but normal single helpers later fall back to image-name process probes. `recovered-core.py:10642-10699`. | **RETAIN** LW. |
| Simultaneous real owners | Intentionally blocked while the installed Lua package is shared per Windows user. `OverviewLifecycleService.cs:840-849`. | Feature-gated multi-window path has per-instance generations, suffixed IPC and environment identity. `recovered-core.py:1215-1264,2829-2874,4497-4537`. | **DEFER** multi-game support until independent installation/script isolation is qualified. |

## Shared transport and infrastructure

| Item | LW-Control | LwTool | Decision |
| --- | --- | --- | --- |
| Host command authority | Authenticated framed named pipe; OS client PID/image/build/token/profile/session checked. Calls use exact generated request IDs and bounded queues. `LWBridgeControlPipeIsolatedHandshake.cs:83-150`; `LWBridgeControlPipeRpcSessionTransport.cs`. | Per-instance `CMD/RESULT/OUT/ERR` shared mappings with a serialized client lock. `recovered-core.py:2134-2230`. Missing `callbackId` can fall back to the pending ID in the active result path. | **RETAIN** LW stronger correlation/authentication. |
| Adapter↔Lua boundary | Numbered JSON mailbox files between managed adapter and Lua; Lua drains on game update thread. `PipeClientAdapter.cs:136-233`; `current_overview_bridge.lua:3608-3793`. | In-process proxy/shared mappings avoid the extra disk mailbox. | **DEFER** direct queue/shared-memory replacement. Must first prove xLua thread affinity and keep one authenticated command authority, bounded correlation/overflow and generation retirement. |
| Adapter idle lease work | Before this programme the 10 ms adapter loop reparsed `lease.txt` every iteration. | LwTool uses mmap/native session state and avoids this specific file-parse loop. | **IMPLEMENTED** commits `73b510ca` + `5a1fdc5f`: 250 ms successful exact-session parse cache; five-second timestamp freshness and current lease-file identity are re-evaluated on every hit. Deletion/replacement, instance change and due contention remain fail-closed. Challenge/token authority remains in the authenticated transport/lifecycle layers, not `LeaseIsFresh` itself. |
| Adapter idle polling | 10 ms adapter loop checks outbound file + pipe. | Shared-memory result polling uses 3 ms fast, 5 ms then 20 ms normal. `recovered-core.py:2169-2215`. | **DEFER** further signaling until measurements isolate outbound/file-peek cost. Host pipe I/O itself is already blocking/event-driven. |
| Host/Lua periodic work | Overview 10 Hz, heartbeat >=0.75 s, host lease 1 s, recovery 2 s. `current_overview_bridge.lua:3839-3872,4102-4121`. | Multiple five-second health/reconnect loops plus 1 s UI status. | **RETAIN** current correctness cadences; optimize only proven hot work inside them. |
| UI connection watchdog | Native status + proxy + persisted config were read every 5 s, alongside event fast paths. | Aggregate `py_get_status` and self-scheduled ~1 s refresh. `recovered-core.py:29039-29070`; `recovered-ui.html:8311-8313`. | **IMPLEMENTED** commit `ccfd924f`: retain status/proxy watchdog, skip redundant persisted-config read on periodic ticks. |
| Hidden Map UI polling | Retained pages previously kept `map_summary` polling every 5 s even off Map. | LwTool throttles large partial table updates based on work/render cost. `recovered-core.py:25485-25506`. | **IMPLEMENTED** `ccfd924f`: hidden Map no longer polls aggregate summary; entry/profile/scan-complete refresh remains. |
| Backpressure | Pipe transport uses item + byte budgets and write/backpressure timeouts. | Shared mapping is one serialized slot per client plus fixed mapping capacity. | **RETAIN** explicit LW queue budgets; no evidence supports adopting one-slot serialization globally. |
| Settings persistence | Cross-process writer lock, backup recovery, validation, unique temp write-through and atomic replace. `LocalConfigStore.cs:120-360`; revision-CAS profile stores. | Migration exists, but broad read/decrypt failures can fall to defaults and writes include direct fallback/background retry. `recovered-core.py:14743-14953,15213-15260,15451-15473`. | **RETAIN** LW; reject silent-default/direct-overwrite behavior. |
| Schema evolution | SQLite stores are revisioned at row level but do not yet use `PRAGMA user_version`. | Settings include an internal `_sver` migration path. | **DEFER** transactional DB schema-version framework until the next schema change requires it. |
| Diagnostics | Sanitized local log append. `AppendLogCommandService.cs:72-117`. | Bounded remote queue/drop/retry telemetry. `recovered-core.py:8349-8598`. | **REJECT** remote telemetry copying. Local rotation/crash receipt is a future low-risk improvement, not required to close this programme. |

## Map Data

Active LW path: `MapDataPage` / `mapBackend` → `Map317CommandService` →
`LWBridge.Map-0.3.17` control plane/store → `CurrentClientMap317ScanProvider`
→ `MapScanEngine` → current-client native sources. The legacy Desktop
`MapDataStore` proof path is not normal UI production.

| Kind / behavior | LW-Control active method | LwTool active method | Decision |
| --- | --- | --- | --- |
| City | Full current source census; camera-free point proof and transactional publication. | Normal Map keeps only selected city rows (e.g. level/UID filtering); fast path is external server-cluster data. `recovered-core.py:20938-21167,24965-25208`. | **RETAIN** LW full census; reject filtered/external substitution. |
| Resource | Full census plus fail-closed dynamic hydration. | Normal Map intentionally keeps a filtered subset/top levels; fast path external. `recovered-core.py:21038-21167`. | **RETAIN** LW accuracy. |
| Monster | Native source with source/coverage proof. | Separate premium scan retains selected running boss/family subsets. `recovered-core.py:32035-32140`. | **RETAIN** LW general Monster source. |
| Zombie Boss | Separate accepted kind with verified source proof. | Competitor boss/family subset does not establish equivalent completeness. | **RETAIN** LW. |
| Truck | 625-rectangle current-client sweep with repair and zero unproven rectangles. `FastTruck` source; accepted MAP-019 evidence. | Fast path filters already-robbed/reward-interest rows before publication. `recovered-core.py:25689-25738`. | **RETAIN** LW census. |
| Railway | 625-rectangle proof matching Truck rigor. | No stronger recovered full-census proof. | **RETAIN** LW. |
| Dispatch | Native census/publication, complete-row query/filter support. | Fast task path keeps selected high-value Secret Task rows and caps reward hydration. `recovered-core.py:25586-25625,25784-25808`. | **RETAIN** LW completeness. |
| Ghost | Native kind/source proof; query paging/materialization after page boundary. | Remote ghost-recon hydration/stream, but no stronger local completeness proof. `recovered-core.py:25655-25687`. | **RETAIN** LW. |
| Treasure | Independent accepted source/claim state path. | Competitor “supplies” metadata is not the same contract. `recovered-core.py:25741-25781`. | **RETAIN** LW. |
| Other | Deliberately derived category from verified Treasure/event source, with shared census/cleanup owner. `MapOtherEventScanSource.cs`; `MapOtherEventSharedScanSource.cs`. | No equivalent stronger general source. | **RETAIN** LW. |
| Mixed/all | Shared exact run, 10k AOI/63 reply point proof; canonical 2500 block engine accounting; Truck/Railway independent exact proofs. | Fast server parts can stream partial external results. | **RETAIN** LW atomic/exact run. |
| Verified empty | Zero is accepted only with source-alive/coverage proof appropriate to kind. | External/filtered empty states have different semantics. | **RETAIN** fail-closed LW. |
| Camera/server effects | Local camera-free source; remote scans preserve server switch/restore when current parser requires it; cleanup restores original state. | Normal city/resource can avoid jump because its route/source differs. | **RETAIN** current source-specific behavior; do not infer jump removal from a different API. |
| Publication | Per-run staging; publish only after exact checkpoint completion; transaction replaces only selected kinds. `MapControlPlane.cs:126-148`; `MapStore.cs:387-453`. | Partial in-memory/server-part merge is more immediately visible but can be incomplete/filtered. | **RETAIN** atomic LW publication. |
| Stop | Cancel + join activation/task before lease release; local cancellation committed under publication gate. | Separate stop flags/tasks; semantics differ by scanner. | **RETAIN** LW exact cancellation. |
| Clear one/all | Atomic server/all snapshot clear, rejected while reading and fenced by current server/Auto lease. `MapStore.cs:501-539`. | Per-table clear is available. | **REJECT** per-table clear because it creates mixed-age Map state. |
| Saved servers | Derived from published `map_records`; verified-empty does not fabricate a server. | Competitor tracks configured/scanned server lists. | **RETAIN** published-data semantics. |
| Tracking | Fresh UUID follow with bounded passive 3 s same-march polling and server/scan/arrival/error retirement. `MapDataPage.jsx:972-1046`. | Moving boss view can request one march position. `recovered-core.py:38208-38220`. | **RETAIN** LW. |
| Filters/paging | SQL snapshot count + LIMIT/OFFSET, default 50/cap 200, kind-specific validated filters. `MapStore.Query.cs:34-143`; `mapBackend.js`. | Row pull caps <=200/<=64 KiB; large partial whole-table pushes are dynamically throttled. `recovered-core.py:31993-32033,25485-25506`. | **RETAIN** existing paging. **DEFER** adaptive refresh until 10k/50k p95 UI evidence exceeds threshold. |
| Names/icons | Build/root/language-fenced localization and validated native PNG assets; dynamic state is not cached as static metadata. | Caches config/localized names/reward metadata. | **RETAIN** LW freshness boundaries. |

Current MAP-019 accepted measurements remain decisive: camera-free point kinds are
roughly 16–24 s versus ~189–201 s camera sweeps; Truck ~52–79 s versus ~198 s;
Railway ~60–61 s versus ~205 s; all-nine ~114–120 s. Previously attempted
coarse/parallel variants lost recall, so no scanner change was promoted here.

## Automation and Squads / AFK

| Item | LW-Control | LwTool | Decision |
| --- | --- | --- | --- |
| Automation off state | R30 engine has true inactive zero-work gate and only pumps active configurations. `feature018_automation_engine.lua:17-29,57-109`. | Connect path can auto-start AFK/chat; host `afk_loop` reloads settings, probes ACK/config and performs unrelated maintenance. `recovered-core.py:31308-31318,15902-16235`. | **RETAIN** LW; reject broad auto-start/repair loop. |
| Active cadence | Automation native pump is one-second gated; individual engines own their exact state. | Multiple host loops/settings repair cadences. | **RETAIN** LW separation. |
| AFK profile/squad arbitration | `used[slot]` plus pending jobs reserves a slot across profiles; send edge rechecks owner/free/target. `feature018_afk_engine.lua:141-226`; `feature018_afk_native.lua:239-300`. | Central resource scan loop services multiple team requests and filters target candidates. `recovered-core.py:12292-12443,13205-13312`. | **IMPLEMENTED** `26eb5255`: share only the cleaned `available_monsters()` snapshot within one Pump; keep per-profile filters, `seen` state and send-edge native target/owner/squad revalidation live. |
| AFK target scan cost | Before programme, each eligible profile×squad `Targets` call rebuilt `GetAllMarches` and `AutomationMonsterInfo`. | Central broker scans/filter/sorts once for multiple team requests. | **IMPLEMENTED** one-Pump snapshot cache; 4×4 no-match inert fixture 16→1 enumerations. |
| Troop/squad availability | Farm/join/gather adapters recheck exact free formation, troops, stamina and target state at send boundary. | Recovered host/UI expose similar configuration concepts, but lower injected dispatch FSM is not sufficiently present in allowed backend/UI evidence. | **RETAIN** LW; do not infer unseen competitor superiority. |
| Duplicate/replay protection | Pending/uncertain tombstones; sent action is never retried merely because reply is missing. AFK/Automation exact receipts/post-state. | Various attacked/stage filters and callback/mmap correlation. | **RETAIN** LW stronger explicit no-replay contract. |
| Stop/configure | Stop prevents new sends; already-sent work remains observable; configuration changes cancel only unsent chooser state. | Broad host loop/config re-push can repair its own multi-writer state. | **RETAIN** LW confirmed profile boundary. |
| Concurrent engines / same squad | Individual native adapters recheck squad idle immediately before native mutation, but Automation service pending state is keyed by kind. A deterministic scheduler fixture can plan resourceGather + allianceGather in the same tick before game post-state. | Competitor per-team attack/join settings are mutually exclusive. | **DEFER** shared cross-engine reservation. It is a qualification gap, not a proven game double-send; needs actual-adapter delayed-visibility fixture before changing accepted concurrency. |
| Multi-window startup staggering | Current real multi-owner support is blocked. | LwTool staggers per-window feature starts. `recovered-core.py:6113-6150`. | **DEFER** until multiple independent runtime owners are supported. |

## City Layout

No active recovered LwTool city-layout editor/move implementation was found in
the authenticated backend/UI searches.

LW-Control retains:

- profile-generation fencing — `CityLayoutPage.jsx:87-92`;
- native snapshot/draft/apply/status — `CityLayoutPage.jsx:391-410`;
- local + server validation — `CityLayoutPage.jsx:470-490`;
- confirmed apply start and cancel/restore preflight — `CityLayoutPage.jsx:521-582`;
- bounded/centered viewport and lazy native images — `CityLayoutPage.jsx:105-125,635-745`.

Decision: **RETAIN**. There is no supported competitor technique to adopt here.

## Hotkeys

| Item | LW-Control | LwTool | Decision |
| --- | --- | --- | --- |
| Admission | Exact game PID/path/start/HWND + owned focus; fresh profile/session/UID/server + recent text-input-clear; recheck around dispatch. `Feature018HotkeyCapture.cs:238-279`; `LWBridgeWindow.Feature018Hotkeys.cs`; `Feature018HotkeyActions.cs:81-175`. | UI `requireGame()` primarily checks connected state; `py_get_hotkeys` exposes bindings and settings gates. `recovered-ui.html:1739-1754`; `recovered-core.py:29139-29166`. | **RETAIN** LW. |
| Key handling | Accepted owner bindings, mixed/modifier/held-on-focus protections and per-action provider gates. | F6/F7/F8 teleport, F11 fullscreen, G defence, Space shield hold, QWER/ASDF, Shift+1..4 speedups, Z/X/C/V equipment are competitor semantics. | **REJECT** semantic/binding parity. Several directly conflict with accepted owner behavior or intentionally removed features. |
| Completion feedback | Providers distinguish submitted/confirmed and require exact native post-state where applicable. | Equipment apply returns true after launching an rlua action; UI later refreshes. `recovered-core.py:36849-36868`. | **RETAIN** LW stronger completion model. |
| Capability metadata | Binding/provider truth is distributed across managed providers and UI. | `py_get_hotkeys` offers a consolidated read-only capability/settings view. | **DEFER** a read-only LW capability manifest; useful for drift prevention, but no current defect requires expanding scope in this performance programme. |

## Mini Games / retained action surfaces

| Feature | LW-Control | LwTool | Decision |
| --- | --- | --- | --- |
| Land | Exact pending cell and request-causality/account fence; unrelated unlock cannot complete the request; remount does not resend. | `py_open_monopoly_cell` sends `SaveMonopolyRecord` and reports success without a matched reply/post-state. `recovered-core.py:37144-37187`; `recovered-ui.html:6298-6316`. | **RETAIN** LW. Never live-test Unlock Land Cell per owner direction. |
| Frontline G | Accepted manual configurable reinforcement with exact post-state `+N` proof and stage-run identity. | Separate premium Frontline Auto +200 feature. `recovered-core.py:37283-37299`; `recovered-ui.html:5895-5912`. | **REJECT/DEFER** competitor premium automation; retain accepted manual G. |
| Sheep / Food House | Manager + solver observation; send-only is not success and terminal receipt survives manager loss. | No equivalent allowed recovered backend/UI path found. | **RETAIN** LW. |
| Treasure chest marker | Success only after visible native marker effect; disable/unmark and owner fences preserved. | No stronger equivalent path found. | **RETAIN** LW. |
| Removed Space / speedup semantics | Owner-removed/locked. | Competitor still exposes Space shield hold and march speedups. | **REJECT** reinstatement. |

## Settings, localization and operations

| Item | LW-Control | LwTool | Decision |
| --- | --- | --- | --- |
| Settings save | Owner-fenced UI apply/persist/rollback plus atomic native persistence. | Native save plus localStorage/UI settings; bounded retry/background pending blob on contention. | **RETAIN** LW. A bounded sharing retry can be considered only if real AV/sharing failures are measured. |
| Localization | Lazy locale modules, localStorage fallback, generation-fenced load/persist; full UI checks validate nine locales. `i18n.jsx`. | URL/localStorage/browser detection and native setting override. `recovered-ui.html:8227-8293`; `recovered-core.py:15177-15210`. | **RETAIN**. Native language persistence is not justified without evidence of WebView localStorage loss. |
| Startup/shutdown | Runtime owners drain before shared transport/WebView teardown; exact lifecycle owns game processes. `LWBridgeWindow.cs:6312-6417`. | Shutdown/atexit includes image-wide LastWar taskkill and plugin purge. `recovered-core.py:17158-17174,24408-24462`. | **REJECT** broad kill/purge; retain exact shutdown. |
| Update/feedback UI | Native update commands are allowlisted but current update status is effectively idle; feedback export is explicitly unimplemented. | Competitor has its own online update/support flows. | **DEFER**; document as a current LW limitation rather than fake parity. |
| Distribution | Inspectable multi-file publish with canonical UI build/check. `LWBridge.Desktop.csproj:47-72`. | Compiled/Nuitka-style application with local cache/purge behavior. `recovered-core.py:162-176,39924-40013`. | **RETAIN** LW multi-file package; reject one-file/cache purge without evidence. |
| Package integrity | Existing R30 tooling pins branch/clean pushed source, per-entry hashes, private-data rejection and ZIP verification. | Authenticated online payload/update mechanisms. | **IMPLEMENT** final programme package by reusing LW source-pinned integrity rules; no competitor payload in package. |

## Deferred / rejected implementation candidates and decisive blockers

1. **Direct adapter↔Lua in-memory queue — DEFER.** The performance case is
   plausible, but the game Lua must execute on its update thread. A safe design
   needs a bounded managed queue plus game-thread drain, exact request/session
   retirement and pressure tests. Direct off-thread Lua callbacks are rejected.
2. **600-second exact-owner late-connect — DEFER.** The competitor establishes
   UX value, not a safe drop-in design. LW would require a new pending recovery
   journal stage and exact Stop/restoration during the grace period. Implement
   only if real slow-ready failures justify that state expansion.
3. **Proxy resource hash metadata cache — REJECT for now.** Target hash must stay
   live; caching packaged resource hashes by size/timestamp alone can weaken
   tamper/repair accuracy. Revisit only if the cache is bound to a trusted
   source/package manifest.
4. **Map parallel/reply-time pipeline — DEFER.** Earlier polling parallelism lost
   rows. Only an immutable request-correlated reply-time capture with repeated
   exact coverage/field comparisons can reopen this candidate.
5. **Cross-engine squad reservation — DEFER.** Scheduler fixtures show a possible
   same-tick plan overlap, but native adapters independently recheck squad idle.
   Build a delayed-native-visibility actual-adapter fixture before changing the
   accepted engine concurrency model.
6. **Hotkey capability manifest — DEFER.** Good drift-prevention idea, but no
   correctness/performance defect in this assignment depends on it.

## Programme implementation outcome

The comparison selected three low-risk, measurable improvements while retaining
the stronger current correctness contracts:

- `73b510ca` + `5a1fdc5f` — bounded exact-session lease parse cache with immediate normal delete/rewrite invalidation;
- `ccfd924f` — remove redundant retained-UI config/hidden-Map polling;
- `26eb5255` — reuse the cleaned AFK monster snapshot inside one scheduler pump.

No accepted feature, account restriction, exact-owner Stop/restoration rule,
authentication rule, readiness predicate, Map completeness rule or action
completion gate was weakened to obtain those gains.
