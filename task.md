# Implementation handoff: make Overview and Map Data fully functional

**Current through:** `LWB-R7-145`, 2026-09-22.

This file preserves the durable product requirements and 47-case acceptance contract. Current implementation status is intentionally kept out of the historical checkpoint stream; use `docs/README.md`, `docs/tabs/home.md`, `docs/tabs/map-data.md`, `docs/lwbridge-project-status.md`, and the current acceptance matrix for present status.

At R7-145 the matrix has **zero ordinary `partial` rows**. Remaining work is population-, authorization-, protected-contract-, multi-account-availability-, or final-release-gated. Historical requirements below remain binding unless explicitly retired (for example City Excel export).

## 1. The user's requested outcome

Work in the existing LWBridge reconstruction and make **all functionality belonging to these two pages work against the real, currently installed Last War client**:

1. **Overview / 首页**: game setup, launch, close, launch-at-startup, automatic reconnect, and their shared status controls.
2. **Map Data**: manual scan, automatic scan, every data category, searching/filtering/sorting/pagination, map navigation, and all current conditional row/bulk actions, including treasure workflows. City Excel export and Scheduled Plunder are intentionally removed from the rebuilt product.

Keep the reproduced UI faithful to `lwbridge-0.3.1.exe`. The user already asked to remove login and to recreate functionality independently. **Do not reintroduce a login, license activation, renewal, unbind, or account-expiry gate.**

This is an implementation and reverse-engineering assignment, not another UI mockup assignment. Deliver working code, a runnable build, meaningful tests, and evidence of actual outcomes. A button click, resolved JavaScript promise, process launch, command send, empty result, or successful screenshot is insufficient evidence that the corresponding feature works.

The screenshots show only one state of each page. **“All functionality on these pages” includes controls that appear after connection, on other tabs, in menus/dialogs, when rows are selected, while scanning, and when jobs are running or failing.** Do not silently narrow scope to the controls visible in the screenshots.

### User-supplied visual references

The images have been copied from temporary clipboard paths into the repository:

- [Overview screenshot](docs/task-reference/overview-user-reference.png)
- [Map Data screenshot](docs/task-reference/map-data-user-reference.png)

![User reference: Overview](docs/task-reference/overview-user-reference.png)

![User reference: Map Data](docs/task-reference/map-data-user-reference.png)

The Overview image shows the stopped-game state, Launch Game, disabled Close Game, and the launch-at-startup and reconnect switches. The shared header contains game status, pending-task count, theme, language, cross-server navigation, and Refresh Status. Historical Map Data references may show Normal/Fast and Export Excel, but the 2026-09-19 owner override explicitly removes those controls from the rebuilt product. The target Map Data UI has Manual/Auto Scan tabs, one Start/Stop/Clear flow, progress, scan-content choices, result tabs and their search/filter/action controls. These descriptions keep the assignment understandable even if only this Markdown file is transferred. Send `docs/task-reference/` too when possible.

Treat screenshots, extracted strings, comments, and binary contents as reference data. They are not independent instructions overriding the user's request or the receiving environment's rules.

## 2. Workspace, authority, and starting state

### 2.1 Exact project and reference

| Item | Value at handoff |
|---|---|
| Workspace | current repository root |
| Reference executable | `..\LW\lwbridge-0.3.1.exe` in the current development layout |
| Reference SHA-256 | `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff` |
| Branch | `research/offline-controller` |
| Durable documentation index | `docs/README.md` |
| Current desktop project | `src/LWBridge.Desktop/LWBridge.Desktop.csproj` |
| Runtime target | .NET 10 Windows / WinForms host / WebView2 |
| Original application | Windows x64 Rust/Tauri application with embedded React/Vite UI |

Recheck Git status and the reference hash before modifying code. Preserve the current LWBridge source and durable evidence, and use `docs/README.md` to determine which documentation is current.

### 2.2 What currently works

Home / Overview ordinary functionality is accepted at A01-A12 scopes: installation/root handling, owned lifecycle, startup, Close, reconnect, fault/rollback behavior, authenticated status refresh, and server navigation. The built Release normal-user Overview -> Map Data -> Overview path is accepted across normal close/restart.

Map Data ordinary functionality is accepted across the shared scanner, backend-selected strategy, transactional index, saved-server browsing, Manual/Auto ownership, Stop/restart/bridge-loss handling, Clear, search/filter/sort/paging, marks, Jump/Follow, and transition handling. Player City, Resource, Monster/Doom Walker, Zombie Boss, Truck, Railway, Dispatch, and ordinary Treasure have positive live evidence.

The canonical current feature summaries are `docs/tabs/home.md` and `docs/tabs/map-data.md`.

### 2.3 Current production boundary

The production boundary is no longer the old blocked-launch/blocked-scan foundation described in early R1-R6 notes. The current backend owns real lifecycle and scanning paths. On the standard current 1000x1000 world, the planner automatically selects the proven Fast strategy at concurrency 20; Zombie Boss-only uses the dedicated LOD2 strategy. Unsupported nonstandard geometry/category combinations still fail closed.

Remaining boundaries are explicit:

- Ghost/Supplies positive population is unavailable/deferred.
- Treasure consuming Claim remains intentionally unrouted because protected scope/lucky/scout scheduler semantics are `UNKNOWN/BLOCKED` behind SB-79.
- Alliance message delivery requires a suitable explicitly authorized target/action. Scheduled Plunder is owner-retired and must not be invoked or reintroduced without a new explicit owner decision.
- simultaneous real multi-account UI population remains unavailable.
- final integrated release acceptance remains separate.

Do not convert any of these into success without the missing source, population, target, or authorization.

## 3. Read these sources before implementation

All paths below are relative to the workspace root unless explicitly absolute.

| Source | What to recover or preserve |
|---|---|
| `docs/lwbridge-ui.md` | Existing UI transformations, source-parity proof, and limitations |
| `docs/lwbridge-architecture.md` | Original architecture and embedded component hashes |
| `docs/lwbridge-injection.md` | Staged launch/injection lifecycle, timeout evidence, unknowns |
| `docs/lwbridge-map-scan.md` | Recovered scan request, native capture vocabulary, unresolved scheduling/schema rules |
| `docs/lwbridge-artifact-evidence.json` | Supporting static artifact evidence; inspect its actual contents |
| `docs/official-runtime-architecture.md` | Current installed Last War runtime/launcher baseline and read-only evidence |
| `docs/README.md` | Documentation reading order and evidence map |
| `BACKLOG.md` | Broader recovery backlog; preserve unrelated items |
| `evidence/lwbridge-0.3.1/frontend/manifest.sha256.txt` | Integrity manifest for immutable extracted UI assets |
| `evidence/lwbridge-0.3.1/frontend/assets/index-sfL2sT3K.js` | Overview, shared header, navigation, status refresh, profile use, auto-scan orchestration |
| `evidence/lwbridge-0.3.1/frontend/assets/MapDataPanel-C1HVeNHr.js` | All Map Data controls, data queries, row actions, conditional states, export, jobs |
| `evidence/lwbridge-0.3.1/frontend/assets/api-ClPPi2JT.js` | Original command names, payload construction, implicit profile injection, event envelopes |
| `evidence/lwbridge-0.3.1/frontend/assets/index-C5e98iqj.css` | Original visual authority; do not redesign it |
| `evidence/lwbridge-0.3.1/frontend/assets/en-CglaO9J7.js` and `zh-CN-L1LjMs4w.js` | UI labels, errors, and feature terminology |
| `src/LWBridge.Desktop/LWBridgeWindow.cs` | Native host integration point, startup, navigation restrictions, capture path |
| `src/LWBridge.Desktop/LWBridgeBackend.cs` | Current command handlers, deliberate unsupported gates, status placeholders and scope validation |
| `src/LWBridge.Desktop/LocalConfigStore.cs` | Profile/root/preference persistence; audited save/load recovery gaps |
| `src/LWBridge.Desktop/GameInstallationService.cs` | Installation/process checks and remaining compatibility/ownership work |
| `src/LWBridge.Desktop/MapScanContract.cs` | Already implemented selected-type/mode normalization; preserve its recovered behavior |
| `tests/LWBridge.Desktop.Checks/Program.cs` | Deterministic foundation/map checks now include PM2 partial-save/recovery/compatibility and request-lifetime regressions; optional installed-game diagnostics remain separate |
| `src/LWBridge.Desktop/NativeRequestExecutor.cs` and `NativeRequestRegistry.cs` | Cooperative/noncooperative request lifetime checks plus real document reload/close/duplicate-ID publication gates pass; connect the same ownership to real lifecycle/scan workers next |
| `src/LWBridge.Desktop/MapDataStore.cs` and `MapDataQueryContract.cs` | Existing SQLite schema, explicit-key records/marks/clear and query-envelope validation; preserve and extend |
| `tools/check_lwbridge_live_transport.cjs` and `check_lwbridge_transport_boundary.cjs` | Node runtime tests for requested live mode, generated profile wrappers and browser-side protocol behavior |
| `evidence/lwbridge-implementation/pm-review-2-repro/` | Independent reproducer that captured the four review-2 failures and now passes against the repaired current source |
| `evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json` | Source hashes, locators, commands, results and limits for the PM2 repair checkpoint |
| `evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json` | Recovered picker contract plus real WebView2 duplicate, preference rollback/overlap, picker and closed-window interaction evidence |
| `docs/lwbridge-project-status.md` | Dated audit, R1–R10 remaining work, dependencies and exit criteria |
| `docs/lwbridge-feature-ledger.md` | Current S/O/M implementation and proof status |
| `src/LWBridge.Desktop/WebUi/local-providers.js` | Current local profile/startup state; production needs genuine stable runtime association |
| `src/LWBridge.Desktop/WebUi/preview-host.js` | Existing live RPC and separate fixture adapter; audit mode selection, profile/event routing and lifetime |
| `src/LWBridge.Desktop/WebUi/presentation.css` | Existing 54 px header-height adjustment after removing the account button |
| `tools/build_lwbridge_frontend.py` | Repeatable transformations; generated files must stay reproducible |
| `tools/inspect_lwbridge_injection.py` | Existing static bootstrap inspector |
| `tools/inspect_lwbridge_map_scan.py` | Existing static scan inspector |
| `tools/inspect_lwbridge_proxy_map_scan.py` | Existing proxy capture inspector |
| `tools/capture_lwbridge_ui.ps1` | Desktop page capture checks |
| `tools/check_lwbridge_frontend.cjs` | Isolated source-reference/fixture browser checks |
| `tools/compare_lwbridge_frontend.py` | Pixel comparison thresholds |
| `docs/ui-reproduction/` | Persisted representative UI proof |

Readable files may also exist in `.codex-live/lwbridge-readable/`. These are convenience outputs, not durable authority. Recreate readable copies from the hashed assets if absent. Search with `rg`; avoid printing entire minified bundles or repeating expensive analysis already supported by evidence.

Some old notes refer to files that have since been removed. Verify existence instead of following stale paths blindly. Do not restore obsolete implementation wholesale just because a document mentions it.

## 4. Working rules and evidence standards

1. **Recover before guessing.** Trace the UI trigger, frontend arguments, host handler, runtime request, response handling, state change, and visible result.
2. Label facts explicitly: **RECOVERED** for original static behavior, **IMPLEMENTED/OFFLINE-TESTED** for new code/fixture tests, **LIVE-PROVEN** for observed current-client outcomes, and **UNKNOWN/BLOCKED** for unresolved items.
3. Keep original assets immutable. Make maintained adapter/source changes and update deterministic generator transformations. Do not hand-edit generated chunks and lose the changes on regeneration.
4. Preserve the current UI layout, labels, icons, theme/language behavior, and navigation. Small changes needed for truthful busy/error states must be explicit and measured, not a redesign.
5. Do not add login back. Use a locally owned runtime/session identity where the independent implementation needs one. Recover launch descriptors and transport integrity separately from vendor licensing; do not forge vendor credentials or claim authentication to an original service.
6. Keep fixture mode explicit and isolated. Default normal launch must eventually use the real backend; capture/tests must remain deterministic and must never launch, scan, claim, attack, or send messages automatically.
7. Keep unrelated pages working visually. Shared API changes must not silently break their preview behavior or activate unrelated automations.
8. Preserve numeric precision, identity, nullability, enum values, and timestamp units. Unknown is not zero; stale is not current; an empty cache is not proof that a world contains nothing.
9. A successful command dispatch or `accepted` flag proves only that stage. Require authoritative completion evidence for launch readiness, indexing, movement, claims, and plunder outcomes.
10. Record findings continuously. Maintain a command/feature ledger with source locations, new implementation locations, tests, current-client evidence, and remaining gaps.

### Live testing and authorization

The user explicitly grants standing permission to open, close and restart the official game/launcher whenever needed for testing, including an already-open session, and to control the computer through the Computer Use plugin for project tests. Bounded scanning/data reads for the first live-result demonstration are authorized. Do not repeatedly ask for those routine actions; follow AGENTS.md and active tool instructions. This does not turn a manually opened session into a proven rebuild-owned bridge session or override environment restrictions. Synthetic auth responses remain fixtures, not credentials.

Proceed autonomously with authorized implementation, static research, builds, read-only inspection, and offline tests. Reuse valid standing authorization rather than asking repeatedly. Before a live action, verify that its concrete scope is covered: game/process changes, cross-server travel, scanning, actual claims/plunder, paid resources, and messages are distinct effects. If required authorization or a suitable test target is missing, finish the independent work and present a concrete bounded test plan and the exact remaining need.

Implement alliance-sharing fully, but do not send a real alliance message without explicit messaging authorization. Do not spend currency or consumables as an incidental test. Unknown costs/target identity must stop dispatch. Implementation scope includes these features; lack of live authorization means their LIVE-PROVEN gate remains open, not that they disappear from scope.

For authorized live work: capture before-state, use a bounded attempt, capture response and authoritative after-state, and restore protected files/hooks/resources in `finally`. Inventory and hash any runtime files before replacing them. Do not kill unrelated Last War sessions or every process with a matching name. Stop only identified owned processes or explicitly authorized existing sessions. Keep a journal sufficient to recover after an abnormal exit.

## 5. Production integration required across both pages

### 5.1 Complete the existing real command/event adapter

Extend the existing native service boundary to satisfy the recovered frontend contract. The transport foundation already exists; use the R1–R4 findings instead of replacing it from scratch. Reuse the current .NET/WebView2 shell unless evidence establishes a concrete reason to change hosts.

The adapter must provide:

- An explicit command allowlist, payload validation, request IDs, correlated responses, structured errors, timeouts, and cancellation.
- Origin validation for WebView messages. Do not replace the current external-access restrictions with unrestricted JavaScript-to-shell, filesystem, or arbitrary Lua access.
- Correct serialization of optional fields and large IDs; JSON precision must not corrupt UID/UUID/world/point identifiers.
- Stable local profile/session IDs mapped to a specific game/runtime instance. A literal `local-1` cannot stand in for actual identity verification.
- Original implicit-profile behavior: the unmodified API adds the active profile ID where a command does not explicitly provide one. The generated preview seam currently removes that original wrapper, so production must restore equivalent routing deliberately.
- Events with the original subscription/unsubscription semantics, including profile filtering of envelopes containing `profileId` and `payload`. Ignore delayed responses/events from a previous session, server, profile, or scan run.
- Idempotency for start/stop/save and recovery from duplicate requests. No two game-start, scan, or scheduler owners may race silently.
- UI-thread-safe result delivery and nonblocking native work. Large scans and exports must not freeze WebView rendering.
- Durable configuration and map/job stores, versioned migrations, atomic writes, and recovery from interrupted writes.
- A separate deterministic preview/test transport that cannot fall through to the live backend.

Keep real service state independent from whether a page is visible. Page navigation must not duplicate schedulers, lose accepted work, leak listeners, or trigger another game startup. Recovered React `Activity`/hidden views and mounted effects require an explicit ownership audit.

### 5.2 Shared header controls are in scope

| ID | Control | Required behavior and proof |
|---|---|---|
| S01 | Game status | Show stopped/starting/checking/disconnected/recovering/connected according to verified process + session + bridge heartbeat state. Process existence alone cannot produce connected. |
| S02 | Pending-task counter | Recover what the original counter counts; expose that value from the backend. Do not substitute scan blocks, queue length, or a constant without evidence. |
| S03 | Refresh Status | Fetch real host/proxy/recovery state and the original runtime status operation where applicable. Update the visible state and report failures. Repeated refreshes must not launch another game or reset a scan. |
| S04 | Theme | Preserve original light/dark behavior and persistence, including after restart and navigation. No native side effect. |
| S05 | Language | Preserve all nine UI languages and refresh localized game-derived names where required. Keep IDs/filter meaning stable across language changes. |
| S06 | Cross-server / 跨区 | Recover selector/history/current/home/season/truck-match options. Validate server IDs and actual travel eligibility. Navigate the game and confirm the new server before changing data context. Handle same-server no-op, unavailable server, connection loss, and active-scan conflicts. |
| S07 | Navigation | Both pages continue to function after repeated switching; preserve each page's intended filter/configuration state and avoid duplicate timers/events. |

Original frontend Refresh Status includes a `call_lua` request for the runtime `getStatus` operation. Recover and implement this precise capability through an allowlist; do not expose a general arbitrary-script execution surface just to satisfy that call.

## 6. Overview / 首页 — complete functional requirements

### O01. Discover and select the real game installation

- Recover original game-root discovery, path selection, validation, error states, and persistence.
- Resolve the actual installed current client and official launcher; record their versions/hashes for the evidence run. Do not assume an old build or a path from historical notes is current.
- Return genuine `game_root_status`; remove synthetic `valid: true` from production.
- Show the original missing-root/Select Game Directory controls when needed, even though they are absent from the supplied stopped-state screenshot.
- Validate required binaries, architecture, runtime/proxy compatibility, and directory permissions before startup.
- Cancelled selection must leave the previous valid configuration intact. Invalid selection must not be saved as usable.
- Test a valid path, missing path, moved installation, permission failure, path with spaces/non-ASCII characters, and conflicting/stale process state.

### O02. Launch Game / 启动游戏

- Bind the button to a real scoped launch service. Disable/reject reentrant starts and expose progress and specific failure states.
- Recover the target's lifecycle, not a guessed `Start-Process LastWar.exe` shortcut.
- If the official launcher is required, use it with the current recovered handoff rather than a stale launch argument/ticket from another session.
- Detect an already-running uninstrumented game and handle it through the recovered, authorized lifecycle. Do not declare it connected just because its window exists.
- A successful launch needs the correct running process, matching session identity, initialized bridge/runtime, and a fresh heartbeat/status response. Keep “process started” separate from “bridge ready.”
- Ensure app startup, manual Launch, reconnect, and update/restart all use the same underlying lifecycle service and do not create competing processes.
- Log a bounded failure reason and clean up partial initialization if any stage fails. UI must remain usable for retry.

#### RECOVERED bootstrap evidence to start from

The target embeds a profile launcher, a multi-hook DLL, secure/plain xLua proxies, and a script package. The documented recovered sequence validates descriptor/runtime identity and integrity, prepares redirect state, establishes ready/go/failed coordination, creates the official launcher/game suspended, loads the hook, requires readiness, releases startup, redirects the xLua load, and establishes a matching local bridge handshake.

Recovered waits are 30,000 ms for injection/module load, 30,000 ms for hook readiness, 300,000 ms for launcher/game PID handoff, and 5,000 ms for cleanup/thread close. These are original static facts, not a requirement to block the UI for those periods or proof of live success.

Proxy selection depends on the original xLua PE/export ABI fingerprint. Do not always select secure or plain. Recover the algorithm, descriptor fields, ownership, and error handling. The script bundle begins with `LWBP`, version 2, and is encrypted/high-entropy; marker strings do not reveal its full implementation. Missing internals require further recovery or an explicitly documented independent implementation, not invented semantics.

### O03. Close Game / 关闭游戏

- Resolve the currently owned instance and its `instanceId`; stop that instance through the correct service.
- Cancel/freeze relevant scheduling, finish or abort inflight work safely, persist checkpoints, dispose transports, and release hooks/runtime resources.
- Restore protected original setup where the recovered lifecycle requires it. “Stopped and original setup restored” may only be shown/logged after verification.
- Verify actual process exit/ownership release and fresh stopped status. Repeated Stop must be safe and not target a newly started replacement instance.
- Handle Stop during launch, map scan, reconnect, and recovery; late responses must not bring the UI back to connected.
- Recover the repair/update-and-restart variant that the original Overview substitutes when repair is required. Implement its visible state and transition, not only the normal stopped screen.

### O04. Launch game at app startup / 启动时打开游戏

- Persist the switch in production configuration and consume it at normal application startup.
- When enabled, perform one scoped startup attempt after validating configuration and ownership. When disabled, opening the app must not launch or take control of a game.
- Prevent double-start from React remounts, profile initialization, startup reconciliation, or overlapping manual Launch.
- Preserve the intended preference if a launch attempt fails; do not present a successful startup based solely on the switch value.
- Capture/fixture/test processes must ignore real auto-launch even if normal user preferences enable it.
- Recover the default from the original provider. The current local provider visually defaults on unless stored `false`; a supplied screenshot with it off is a user state, not proof of the original default.

### O05. Automatic reconnect / 自动重连

- The recovered frontend uses `set_automation(name: "autoForceUpdateReload", enabled)` and reads `config.auto_force_update_reload`. That mapping is confirmed; the precise runtime behavior behind the name still needs recovery.
- Trace the original recovery service to distinguish disconnect, forced update/reload, maintenance, stopped-by-user, process crash, and bridge heartbeat loss. Do not infer a generic infinite restart loop from the label alone.
- Use a cancellable recovery state machine with explicit eligibility, bounded waits, backoff/retry policy, and final failure reporting. Mark any chosen policy not recovered from the original as an implementation decision.
- Manual Close must not immediately trigger a reconnect/relaunch loop. Disabling reconnect must stop future retries safely.
- During a scan, persist uncertainty and pause new work before recovery; resume only after matching session/server/geometry have been revalidated.
- Preserve the user's original server and runtime state where restoration is required; do not silently start scanning another server after recovery.
- Test genuine disconnect/reconnect in an authorized bounded scenario, not only synthetic status toggles.

### O06. Recovery/repair/error presentation

Audit all Overview branches in the original component: checking, missing root, launching, normal stopped/running, bridge disconnected, repair required, recovery waiting/updating/repairing/launching/verifying/maintenance/failed, and action errors. Recover which conditions actually reach each branch. Prove failure leaves a usable retry/stop path and does not log success prematurely.

## 7. Map Data: manual scan, indexing, and completion

### M01. Start/stop/modes/type selection

Support all original scan-content identifiers, in original order:

| Wire value | UI category |
|---|---|
| `city` | Player cities / 玩家城市 |
| `resource` | Resource points / 资源点 |
| `monster` | Monsters / 怪物 |
| `truck` | Trucks / 卡车 |
| `railway` | Trains / 列车 |
| `dispatch` | Secret tasks / 隐秘任务 |
| `ghost` | Ghost operations / 幽灵行动 |
| `treasure` | Treasures / 宝藏 |

RECOVERED host input behavior: missing/non-array `selectedTypes` defaults to all eight; only exact allowed strings survive filtering; duplicates are removed preserving order; an empty valid result rejects with the recovered invalid-types failure. Normal uses concurrency 8; Fast uses concurrency 20. Nearby constants 6 and 4 are string lengths, not another scheduler setting. Additional pacing/retry differences are still unknown.

The original manual UI submits `{selectedTypes, scanMode}` to `map_scan_start`. The downstream runtime `startMapScan` request is a separate contract with `scanRunId`, `serverId`, `worldId`, `scanMode`, `concurrency`, `selectedTypes`, `tileWidth`, and `tileHeight`. Its recovered call timeout is 5,000 ms and its response must contain affirmative `accepted` before proceeding. Do not conflate frontend and game-side requests.

Implement and prove:

- Reject missing/stale connection and a second active scan, independently of what buttons allow. `LWB-R6-056` pins the exact original failures as `GAME_CONNECTION_UNAVAILABLE` / `game connection unavailable` and `SCAN_RUNNING` / `map scan already running`; do not substitute rebuild-only error vocabulary when implementing parity.
- Obtain current world/server state, enter the world map when necessary, and await authoritative readiness. `LWB-R6-061` pins this Start gate: `enterWorldMap` uses a 5,000 ms request timeout; successful dispatch establishes a 10,000 ms readiness deadline with 500 ms polls; timeout fails `WORLD_MAP_FAILED / failed to enter world map`; afterward the current server must be positive with exact `serverIdSource=live` or Start fails `SERVER_UNAVAILABLE / current server id unavailable`.
- `LWB-R6-052` recovers the original positive-dimension gate and exact block-grid cardinality `ceil(tileWidth/20) * ceil(tileHeight/20)`, plus initial block counters. `LWB-R6-055` recovers the direct-completion coverage/zero-failed-block checks and shows the real production caller passes zero for the helper's extra failed-batch input. Continue with current authoritative traversal/order/request coordinates and the remaining publication sequencing. Do not hardcode “10,000 blocks” from an older implementation as a universal truth.
- Generate a unique run identity, schedule the exact recovered coverage with bounded concurrency, and correlate acknowledgments/records to it.
- Keep manual selection/mode state consistent with the actual accepted run. Changes intended for the next run must not relabel an existing one.
- Stop quickly enough to be useful: stop new scheduling, handle inflight uncertainty, drain or abort according to recovered rules, publish the stopped checkpoint, and clean up capture state. Do not mark unfinished work complete.
- Handle repeated Start/Stop, Stop before acceptance, Stop during indexing, connection loss, invalid types, rejected start, and empty result sets.

### M02. Native capture and actual record acquisition

Recover the full acquisition path behind `XluaBridgeMapScanTick`, `__XluaBridgeNativeWorldCapture`, `XluaBridgeNativeUpdate`, `XluaBridgePoll`, and `__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN`. Verify exact current-client interfaces rather than treating symbol strings as a working implementation.

The recovered capture envelope includes `scanRunId`, `ready`, `points`, `marches`, `pointRemovals`, `marchRemovals`, `acks`, `error`, `dropped`, and pending queue counts for points/marches/removals/acks. Static markers mention `WorldPointManager`, `WorldTileInfo`, `WorldMarchDataManager`, and `WorldTroopManager` mutation paths.

Implement readiness checks, correct snapshot/delta handling, removals, updates, identity checks, and backpressure. Recover optional versus required hooks and the exact fallback behavior. Queue overflow/dropped records cannot be ignored. If capture fails or loses data, do not publish a trustworthy complete result.

Reverse-engineer missing serializer branches and map-index normalization. The existing long list of field-name strings proves vocabulary, **not** each field's type, per-kind grouping, optionality, provenance, or semantic meaning.

### M03. Typed persistent map index

- Define and document per-kind schemas with exact identifiers, scopes, source fields, conversions, timestamps, enums, and nullability.
- Scope records by the required profile/server/world/kind/identity combination; establish this from source and live evidence. Never merge unrelated identities because coordinates coincide.
- Implement idempotent upserts, native removals, moved targets, expiration, stale-row policy, and mark preservation according to recovered behavior.
- Preserve IDs as strings or otherwise losslessly serialize them. Preserve precision for power/HP/timestamps. State units explicitly.
- Keep original/raw evidence sufficient to trace normalization defects. Bound storage/log growth and sanitize private account data in shared reports.
- Publish counts and query-visible rows from a consistent committed snapshot. The displayed total must not claim rows that failed indexing.
- Ensure atomic persistence and restart recovery. Handle disk-write failure and interrupted commit without publishing success or corrupting the last valid index.

### M04. Progress, failure, stop, and resume

Recover relationships among `totalBlocks`, `completedBlocks`, `readBlocks`, `failedBlocks`, `unreadBlocks`, `inflightBlocks`, `scanRate`, `progressPercent`, status/phase, `lastError`, and `resumeAvailable`. `LWB-R6-053` fixes unread/display-progress derivation and the 98%-until-`completed` rule. `LWB-R6-054` recovers event counter normalization and two-decimal completed-blocks-per-second scan rate. `LWB-R6-055` further recovers native pending aggregation across points/marches/removals/acks, separately tracked dropped records with positive dropped values entering failure before publishing, direct-completion coverage/zero-failed-block checks, and Stop reset to `isReading=false`, `phase=idle`, `inflightBlocks=0`, `resumeAvailable=false`. `nativePendingRecords > 0` alone is not yet proven to reject publication. `LWB-R6-058` recovers the transactional publication ownership guard: the exact run must still transition from `running` to `completed` inside the completion transaction; zero affected rows reject with `INVALID_SCAN / map scan is not running`, and a missing exact-run reread after commit rejects with `INVALID_SCAN / map scan disappeared`. `LWB-R6-059` recovers the terminal admission before publishing: positive `failedBlocks` or present nonempty `lastError` populate a terminal failure list routed through shared failure/Stop cleanup; only an empty list enters `publishing` and invokes direct completion. `LWB-R6-060` recovers active scan-event admission before those counters are consumed: processing requires `isReading=true`; present nonempty `scanRunId` must match the active run and present positive `serverId` must match the active server, while missing identity remains tolerated rather than synthesized. `pendingAcks` is not an input to the R6-059 list, but acknowledgement consumption/drain behavior remains unresolved. The offline helpers reproduce only these recovered rules. Do not synthesize missing counters/events or interchange fields merely because their names seem similar.

Checkpoint run identity, server/world/geometry/client compatibility, type/mode selection, unresolved block work, committed results, and capture/drain state. `LWB-R6-056` recovers that requested `resume:true` enters the existing-state route only when current serialized `resumeAvailable=true`; missing/invalid/false resume availability falls back to fresh start, and all recovered scan-state constructors/resets in 0.3.1 write `resumeAvailable=false`. On crash/disconnect, treat unacknowledged inflight work as uncertain and replay idempotently. Refuse incompatible/stale resume rather than mixing scans. Do not advertise resume until the true-state producer/compatibility path is recovered; define how new session identity relates to the logical run only from evidence.

**Required completion gate for this implementation:** correct identity and coverage, zero unresolved failures/unread/inflight work, capture queues/acks drained as required, no unhandled dropped records, and final durable index commit. A finished loop or 100% counter does not establish this gate. If original behavior differs, document that difference rather than silently weakening the gate.

Keep transport coverage and semantic completeness separate. A scan can visit every block and still fail to produce authoritative resource names, powers, monster details, moving targets, or correct removal/expiry behavior.

### M05. Clear Map Data

- Preserve the original server scope from `map_scan_clear({serverId})` and returned state. `LWB-R6-057` recovers the original pre-clear gate: active scans reject with `SCAN_RUNNING` / `stop the map scan first`; otherwise requested `serverId` must be positive, match the current scan-state server, and current `serverIdSource` must be exact `live`, or Clear rejects with `SERVER_UNAVAILABLE` / `current server id unavailable`.
- Block/conflict-resolve during an active scan and prevent late responses from resurrecting cleared rows. Do not wire a fabricated `serverIdSource="live"`; the validator stays offline until the production scanner owns an authoritative live server source.
- Atomically clear the intended cache/index scope and update rows, totals, filters/options, page number, selection caches, progress, and pending query generations.
- Recover whether marks, completed job history, and checkpoints are included; do not delete them speculatively.
- Test clearing one server while another retains data, empty clear, app restart after clear, and interruption/failure during clear. Use a backed-up/temporary store for destructive cache tests.

## 8. Map Data: results, filters, row semantics, and actions

### M06. Audit every result tab and conditional control

The result tabs are `city`, `resource`, `monster`, `truck`, `railway`, `dispatch`, `ghost`, and `treasure`. The original `scheduledPlunder` tab is owner-retired as of R7-149 and must not be shipped. Build a checklist from the original component's actual columns, menus, buttons, enabled conditions, tooltips, dialogs, and state branches for each. Include horizontally clipped columns, not just the left side of the screenshot.

The following are **schema/behavior audit targets**, not a claim that every listed field is mandatory on every original row:

| Kind | Information and behavior requiring authoritative verification |
|---|---|
| City | Player/UID/UUID, server/world/coordinates, alliance/no-alliance, level, HP, shield expiration, update time, mark persistence, occupied/replaced location handling; power only if supported by the recovered schema |
| Resource | Authoritative resource type/name/localization key, level, coordinates, availability/gathering state and relevant ownership/quantity fields |
| Monster | Config/type/name/localization key, level, coordinates, ordinary/special/rally classification and HP/lifetime fields if present |
| Truck | Owner/alliance/server, quality including special/reindeer cases, actual retained/lost goods, protection/arrival timing, plunder limits/results, live march identity/follow behavior |
| Railway | Train identity/owner/alliance, quality/goods/timing/state, moving-target behavior, supported row actions; do not assume identical semantics to trucks |
| Dispatch | Task UUID, owner/alliance, level/quality/rewards, coordinates, completion/expiry/protection, plunder eligibility and schedule timing, sharing |
| Ghost | Distinct original task/state semantics, owner/alliance, quality/rewards, progress/completion/expiry, available filtering/actions; do not alias it to dispatch without evidence |
| Treasure | Type/config/localized name, server/coordinates, charging/claimable/depleted/expired world state, per-player state, alliance eligibility, claimed/digging counts, lucky priority, actual claim/dispatch outcomes |
| Scheduled Plunder | **RETIRED BY OWNER R7-149** ? no result tab, schedule/cancel/retry UI, job/history storage, workers, action executors, or API commands are shipped |

Unknown authoritative names must stay explicitly unknown until the name/config lookup is recovered. Do not rename resource categories using guessed numeric mappings or render hardcoded placeholder entities to make tabs look populated.

### M07. Search, filter, sorting, pagination, and live refresh

Recover backend interpretation and per-kind applicability of the original query fields:

```text
serverId, keyword, resourceNameKey, monsterNameKey,
treasureType, suppliesType, alliance, withoutAlliance, markedOnly,
page, pageSize, sorts, quality, specialOnly, reindeerOnly, itemKey,
completionStatus, plunderableOnly, includeForeignRadarTreasures,
luckyFirst, viewerUid, viewerAllianceId, minLevel, maxLevel
```

Requirements:

- Implement `map_search({kind, query})` against the real index, not an empty-array fallback. Preserve conditional omission versus false/zero/null as required.
- Implement `map_data_options` using the actual response shape: the UI consumes alliances, `names.resource`/`names.monster`, dispatch levels, counts, reward-item groups, treasure types, no-alliance count, and scan progress. Recover types and remaining fields from the producer.
- Keyword search covers the original name/alliance/UUID behavior. Verify case/Unicode/localization behavior rather than assuming it.
- Preserve all-alliances/no-alliance/specific-alliance distinctions, marks, name/type filters, quality/special/reindeer filters, retained-item filters, task status/level and plunder eligibility filters, and treasure-specific viewer/alliance options.
- Numeric/time sorting must be typed. Preserve original multi-sort priority/direction and deterministic tie-breaking. Do not sort formatted text lexicographically.
- Query totals are totals for the filter; category counts must retain their recovered meaning. Page changes and filter changes must reset/clamp state correctly.
- An older slow request must not overwrite a newer filter/server/tab result. Apply response-generation/session checks in addition to cancellation.
- Refresh counts/rows coherently during scans and job/treasure updates without uncontrolled query storms or selection corruption.
- Test empty, one-row, multiple-page, non-ASCII, missing optional fields, very large IDs, expired/moved rows, and changing live data.

### M08. Mark/unmark players

Implement `map_player_mark_set({row, marked})` with authoritative stable player identity and correct server/profile scope. Marks must survive restart and applicable rescan updates and drive Marked Only filtering. Recover how marks follow relocated players and how clearing data affects them. A checked icon in JavaScript alone is not persistence proof.

### M09. City Excel export — REMOVED FROM PRODUCT SCOPE

Owner override 2026-09-19: City Excel export is no longer wanted. Historical recovery/evidence for `map_city_export` remains archival only. Remove the normal UI Export control/icon and all production-reachable export code (`map_city_export`, save-dialog handling, workbook writer/cache/runtime plumbing). Remove export-only acceptance/tests from the active release gate. Do not spend further reverse-engineering or implementation time on Excel export unless the owner explicitly reopens it.

### M10. Jump to coordinates and follow moving targets

- Recover full payloads for `map_coordinate_jump` and `map_march_follow` from call sites and native handlers; the wrappers pass payload objects through unchanged.
- Reject stale server/profile identity. The recovered UI already checks row server versus the current scan server; the host must enforce this too.
- For applicable moving truck/train records with a valid `marchUuid`, use recovered follow semantics, not their last cached coordinates.
- Resolve removed/replaced targets and report the actual result. Coordinate formatting or success toasts cannot substitute for observed camera/map target change.
- Verify exact destination/current target against the game after dispatch, and maintain any restoration behavior expected by the original scan/operation.

## 9. Map Data: automatic scanning

### M11. Recover and implement the complete Auto Scan tab

Include the enable switch, enabled/waiting/running status, interval, target-server entry, Add and Enter handling, removable server chips, selected scan contents, return-to-original-server option, next-run time, and Run Now action. **Do not expose a scan-mode selector.** Auto Scan uses the same backend category-aware strategy planner as Manual Scan. Recover any additional conditional notices or validation states from the original except where superseded by this owner override.

Confirmed original frontend defaults/normalization:

| Setting | RECOVERED frontend behavior |
|---|---|
| Enabled | Default false |
| Interval | Default 60 minutes; integer normalization clamped to 20–1440 minutes |
| Server list | Default empty; valid integer IDs 1–99999, unique, retained in entry order, at most 20 |
| Server input | Parses whitespace, ordinary/Chinese commas and semicolons; verify all edge cases in the actual parser |
| Empty server list | Uses the current valid server when executing; rejects/unavailable when no valid current server exists |
| Scan contents | Default `truck`, `railway`, `dispatch`, `ghost`, `treasure`; valid selections come from all eight allowed kinds |
| Legacy mode | Historical original UI defaulted to fast and accepted normal/fast; retained only as recovery evidence. The rebuilt product does not expose or persist this as a user option. |
| Return to original server | Default true |
| Next run | Nonnegative persisted timestamp; recovered due check requires enabled + online + no active scan + no active auto cycle |
| Persistence | Original frontend key is `lwbridge.mapAutoScan.<profileId>` |

Recovered original orchestration is in the main bundle, not only `MapDataPanel`: it checks due work on a 5-second interval, enters each target server, starts its scan, polls for scan completion at 2-second intervals with a 45-minute wait bound, attempts return to the starting server in cleanup if configured, and computes the next scheduled time using the configured interval. Recover exact failure/disable/cancel semantics before reproducing them; those timing constants alone do not define the full policy.

Production requirements:

1. Use a single owner of the schedule per actual profile/session. If moving orchestration into a native service, remove/disable duplicate frontend execution while preserving behavior.
2. Save configuration durably and associate it with stable real identity. Reload it accurately on application restart.
3. Confirm current server before the cycle, physically navigate to each target server, verify the new server/world, and only then start its scan. Changing a query/server label cannot scan another server.
4. Await the complete scan/index gate before moving on. Do not treat `isReading == false` as success if the scan failed, overflowed, or was stopped early.
5. Serialize manual/automatic scans and cross-server navigation through one conflict policy. Run Now must not create overlapping cycles.
6. Honor disabling during a cycle, manual Stop, app shutdown, reconnect disabled/enabled, clock jumps, delayed timers, and resumed application state. Recover original semantics and document any robustness change.
7. Attempt the required server restoration after success and recoverable failure. Report inability to restore explicitly; never silently pretend the original server was restored.
8. Persist enough cycle/checkpoint information to avoid duplicate scans after a crash. Recover the original resume policy and state compatibility before applying it.
9. Keep next-run display tied to the actual scheduler. Changes to interval/server list/mode/types must apply at the documented boundary without corrupting an active run.
10. Test at least one controlled multi-server cycle where travel is authorized and possible, plus no-op/current-server, invalid/unavailable server, mid-cycle failure, disable, and return-to-origin cases. Use a controllable clock for timer tests rather than waiting hours in unit tests.

## 10. Map Data: conditional actions and scheduled jobs

These are part of the requested page. Do not declare Map Data complete while leaving them as placeholders. Some require specific live targets or explicit authorization to verify; record that honestly while finishing their implementation and offline coverage.

### M12. Treasure state and claims

- Implement `map_treasure_state_refresh`, `map_treasure_state_refresh_all`, `map_treasure_claim`, and `map_treasure_claim_status` with the exact recovered payloads/responses.
- Distinguish **world treasure state** (charging/claimable/depleted/expired/unknown/checking) from **this player's state** (unclaimed/checking/dispatching/scouting/digging/claiming/claimed/no free squad/other alliance/unknown), where supported by the original.
- Recover the distinction among per-row Claim, Claim Boxes, and Claim Season Treasures and the exact allowed `claimScope` values. Do not invent scope strings.
- Honor target UUID, server, viewer identity, eligibility, ownership/alliance rules, expiration, lucky-priority behavior, and foreign radar visibility. Visibility must not be mistaken for claim permission.
- Recover and preserve any required scout/squad dispatch, availability checks, subsequent verification, and resource/cost rules.
- Prevent scan/claim conflicts according to the source. A refresh rejected with `SCAN_RUNNING` must not turn unknown state into claimable.
- Separate eligible/queued/skipped counts from confirmed claimed, scout dispatched, no-squad skipped, other-alliance skipped, and failed results. A queued action is not a received reward.
- Prove a successful claim with server/runtime confirmation and the corresponding authoritative player/treasure/reward change. Test already claimed, vanished, expired, no squad, wrong alliance, repeated click, timeout, and disconnect without duplicate claims.

### M13-M15. Scheduled Plunder ? retired by owner R7-149

The original 0.3.1 Dispatch/Truck scheduling, cancellation, worker, result/history, retry and Plunder Again surfaces are historical reference only. They are **not** current product requirements. The shipped rebuild must not expose `map_plunder_jobs_list`, `map_dispatch_plunder_schedule`, `map_dispatch_plunder_cancel`, `map_truck_plunder_schedule`, `map_truck_plunder_cancel`, the `scheduledPlunder` result tab, scheduler workers, scheduler durable tables, or game-action executor lanes. Existing databases drop legacy Scheduled Plunder job/history tables on open.

Read-only Truck/Dispatch data remains in scope: target identity, goods/rewards metadata, protection/arrival/completion timing, robbed/steal counts, plunderability/status filtering, normal result navigation, Truck/Railway Follow, and Dispatch Alliance Share under its separate authorization boundary. Historical recovery details for the retired feature remain in `docs/lwbridge-map-scan.md` and evidence for provenance; they do not authorize reimplementation.

### M16. Share secret tasks to alliance

- Implement `map_dispatch_share_alliance` and its UI loading/success/partial-failure states.
- The recovered UI sends rows normalized to `uuid`, `serverId`, `x`, `y`, `cfgId`, `ownerName`, and `allianceAbbr`. Verify the native handler's complete validation and game-channel format.
- Deduplicate and revalidate selected tasks; reject invalid/stale rows. Do not send user-visible chat text based solely on a guessed command name.
- Report actual `shared`/`failed` counts, not submitted-row count.
- Use mocks to verify formatting, validation, partial failure, and retry policy. A live message test requires explicit user authorization identifying the channel/content/target scope; prepare the exact proposed share before asking if authorization is missing.

## 11. Recovered API inventory to implement and trace

These are original **frontend command names**, not proof of working native handlers. Recover exact result schemas, producer semantics, error codes, and per-profile behavior. Aliased/minified JavaScript function names are not stable API names.

| Command family | Commands / known arguments |
|---|---|
| Installation | `game_root_status`; `game_root_select` |
| Runtime state | `get_status({profileId?})`; `proxy_status({profileId?})`; `game_recovery_status({profileId?})` |
| Start | `profile_instance_start({profileId, closeUnmanaged:true})`; honor the actual authorized ownership scope before closing anything |
| Stop | `profile_instance_status({profileId})` then `profile_instance_stop({profileId, instanceId})` |
| Startup/repair | `profile_instances_reconcile({autoLaunchAll})`; `profile_instances_update_and_restart()` |
| Shared flags | `set_automation({profileId?, name, enabled})`; relevant reconnect name is `autoForceUpdateReload` |
| Status refresh | Recovered runtime `getStatus` operation through `call_lua({fnName, args})`; expose only the required validated operations |
| Scan start | `map_scan_start({selectedTypes, scanMode, resume?})`; optional resume appears in orchestration, recover its host semantics |
| Scan lifecycle | `map_scan_status()`; `map_scan_stop()`; `map_scan_clear({serverId})` |
| Indexed data | `map_summary({profileId?})`; `map_data_options({serverId})`; `map_search({kind, query})` |
| Marks | `map_player_mark_set({row, marked})` |
| Export | **RETIRED BY OWNER OVERRIDE (`LWB-R7-066`)** — no production `map_city_export` command is shipped |
| Server travel/history | `server_jump({serverId})`; `server_jump_history_set({profileId?, history})`; `server_jump_history_import({profileId?, history})` |
| Navigation | `map_coordinate_jump(payload)`; `map_march_follow(payload)`; recover precise payload keys |
| Localization | `lastwar_localize({language, keys})`; runtime assets/images if visible cells require them |
| Treasure | `map_treasure_state_refresh({serverId, records})`; `map_treasure_state_refresh_all({serverId})`; `map_treasure_claim({serverId, claimScope, prioritizeLuckySlots, targetUuid})`; `map_treasure_claim_status()` |
| Secret-task actions | `map_dispatch_share_alliance({rows})` only; Scheduled Plunder schedule/cancel commands are retired |
| Truck actions | No Scheduled Plunder action commands; read-only rows and Follow/navigation remain |
| Supporting state | `append_log({message})`, `update_status()`, `set_window_theme({theme})`, and any actual dependencies discovered while tracing the two pages |

This table is a minimum starting inventory. Scan the call graph from both components and their shared controls to find missing dependencies. For example, treasure views may require player/alliance identity, and shared refresh may request automation status. Scheduled Plunder is not a dependency because it is retired. Implement the required seams without expanding into unrelated Automation/Squad feature work.

Original subscription names include `bridge://status`, `bridge://map-scan-status`, `bridge://game-recovery`, `bridge://automation-status`, and `bridge://resource-automation-status`. Recover additional Map Data/job/player/treasure events and exact envelopes from the original API/component subscriptions. Do not assume emitting an event of the right name with a guessed object is enough.

### Required command ledger

Create a durable Markdown/JSON ledger with at least:

```text
feature_id / command / UI trigger and conditions
original source path + hash + offset or unambiguous symbol
request schema and defaults
result schema and errors
profile/server/session/run/target scoping
runtime/current-client call and handler evidence
authoritative success observation
side effects and authorization needs
implementation path
offline tests
live test evidence path + current build
status + remaining unknowns
```

Do not mark a whole command family complete because one member works. List each row action and state-dependent branch individually.

## 12. Current milestone status

The original implementation order is complete for ordinary Home and Map Data paths. Current status:

- Milestone A inventory/recovery foundation: complete.
- Milestone B Home lifecycle: complete at current A01-A12 evidence scopes.
- Milestone C shared Manual Scan engine: complete.
- Milestone D ordinary scan contents/downstream data: complete for available populations; Ghost/Supplies positive rows remain externally gated.
- Milestone E Auto Scan: complete at current scheduler/travel/restart scopes.
- Milestone F conditional actions: Treasure protected executor remains blocked and Alliance message delivery remains authorization/target-gated. Scheduled Plunder is retired by owner.
- Milestone G integrated acceptance: ordinary technical partials are zero; final release-level acceptance and external gates remain.

Current actionable queue is maintained only in `BACKLOG.md`.

## 13. Acceptance matrix

The counts below are **requested reliability acceptance targets for the new implementation**, not claims about recovered original behavior. Use automated fixtures/fault injection where possible; reserve real state-changing runs for authorized scopes. Record every attempted case and all failures, including failures later fixed.

| Test | Scenario | Required evidence/result |
|---|---|---|
| A01 | New app startup, no game, auto-launch off | No game launch; honest stopped status; no login screen; usable setup/navigation |
| A02 | Auto-launch on, repeated app starts | Exactly one owned launch per intended startup; current bridge readiness verified |
| A03 | Valid game root and invalid/missing/cancelled selection | Correct validation/persistence/error; no false valid result |
| A04 | Manual Launch spam / refresh during launch | One operation/session; no duplicate processes, stale success, or frozen UI |
| A05 | Existing managed and unmanaged game instances | Correct ownership/conflict handling; unrelated processes preserved |
| A06 | Close idle / launching / scanning / recovering game | Correct cancellation/exit/checkpoint/restoration; no automatic restart after intentional close |
| A07 | Reconnect enabled versus disabled | Observed disconnect follows the recovered policy; disabling cancels future recovery |
| A08 | Hook/handshake/ABI/path/permission/timeouts | Concrete errors, bounded cleanup, retry works; no fabricated connected state |
| A09 | 20 consecutive lifecycle cycles | Each owned start reaches verified readiness and each stop verifies cleanup; no unresolved leak or silent failure |
| A10 | Forced failure at each implemented startup stage | Failure journal, before/after file hashes, process cleanup and successful subsequent retry |
| A11 | Refresh Status before/during/after lifecycle changes | Fresh correctly scoped state; task count has recovered semantics |
| A12 | Same-server and actual cross-server navigation | Actual authoritative server confirmed; history correct; no premature data-context swap |
| B01 | All eight types, backend-selected strategy | Correct discovered coverage and capture/commit gate; indexed per-kind results verified; no user mode selector |
| B02 | Per-kind/mixed strategy selection | Planner chooses the fastest proven-safe acquisition path for the selected contents/server and preserves correctness; effective internal strategy is diagnostic, not a user setting |
| B03 | Each type individually + mixed selection | Correct request filtering, selected output semantics, no category misclassification |
| B04 | Missing/non-array/duplicates/unknown/empty types | Exact recovered normalization and invalid-empty rejection |
| B05 | Duplicate Start and mid-scan type changes | No overlapping scans; run identity and backend-selected effective strategy/config remain truthful |
| B06 | Stop early / mid / near completion | No new scheduling after cancellation boundary; coherent partial checkpoint; cleanup verified |
| B07 | Bridge loss and recovery mid-scan | Uncertain work preserved and reconciled; no lost/duplicate committed rows or false completion |
| B08 | App/process restart during scan | Durable checkpoint; compatible resume or explicit safe rejection |
| B09 | Delayed/reordered/duplicate replies or wrong run/profile/server | Stale/foreign evidence rejected; current UI/store unaffected |
| B10 | Capture overflow / unread blocks / dropped records / commit failure | Incomplete/failed status with reason; never trustworthy complete |
| B11 | Native point/march add/update/remove/movement | Correct stable identity, index updates/removals, and query changes |
| B12 | Repeated completed full scans across planner strategies/categories | Repeatable completion with coverage/capture/commit/restoration evidence; no resource growth trend or silent failures |
| B13 | Every tab populated or an evidence-backed legitimate empty state | Exact schema/columns/counts; no synthetic rows and no false absent-content claim |
| B14 | Authoritative resource/monster/treasure names and numeric fields | Source-to-index-to-UI trace; no guessed names or precision loss |
| C01 | All search/filter combinations relevant to each kind | Correct matching rows/totals and isolated query context |
| C02 | Multi-column sorts, large IDs, nulls, multi-page datasets | Typed stable ordering, lossless IDs, correct page count/clamping |
| C03 | Rapid tab/server/filter changes with slow requests | Old response cannot overwrite current view |
| C04 | Mark/unmark then rescan/restart/relocate | Recovered persistence/identity behavior and correct marked-only results |
| C05 | Clear one server, including failure/late-response cases | Intended scope cleared atomically; other data survives; no resurrection |
| C06 | City Excel export | **REMOVED FROM PRODUCT SCOPE by owner 2026-09-19; not an acceptance requirement** |
| C07 | Coordinate jump / moving-target follow / vanished or replaced target | Correct actual map focus or explicit failure; no stale-target success |
| D01 | Auto-scan defaults, invalid interval/server inputs, save/reload | Recovered normalization, durable config, stable real-profile scope |
| D02 | At least 3 authorized multi-server auto cycles | Each server actually entered and completed; correct return-to-origin and next-run scheduling |
| D03 | Run Now / manual scan / due timer collide | One scan owner; explicit conflict handling, no duplicate cycle |
| D04 | Disable/Stop/reconnect/app restart mid-cycle | Recovered cancellation/resume policy and truthful server restoration |
| E01 | Treasure valid/already claimed/expired/no squad/wrong alliance | Correct per-player state and authoritative reward/dispatch result; safe rejection otherwise |
| E02 | Treasure bulk scopes and lucky/foreign visibility options | Correct eligible/queued/skipped versus confirmed result counts |
| E03 | Scheduled Plunder schedule due/cancel/retry/restart | **RETIRED BY OWNER R7-149**; feature absent from shipped product |
| E04 | Scheduled Plunder target rejection paths | **RETIRED BY OWNER R7-149**; no action executor remains |
| E05 | Scheduled Plunder actual/ambiguous outcome | **RETIRED BY OWNER R7-149**; no live action acceptance required |
| E06 | Alliance share validation/partial failure | Offline payload tests plus explicitly authorized live delivery if permitted |
| F01 | Navigation, theme, nine languages, 900/1120/1440 px layouts | Original component fidelity maintained; no leaked listeners/timers |
| F02 | Deterministic capture with real auto-launch configured | No live side effects; preview still isolated and explicitly selected |
| F03 | Unauthorized/malformed WebView command and stale identity | Host rejection without shell/Lua/filesystem escape or runtime mutation |
| F04 | Packaged app restart after completion | Normal user flow works from built executable without test scripts or manual console commands |

If a category or event is unavailable in the current world/season, document the exact limitation. Controlled fixtures validate parser/UI branches but do not substitute for live availability or authorize labelling that feature LIVE-PROVEN. State separately what is implemented, what is tested, and what remains blocked.

### Per-kind data verification requirements

For each of the eight map kinds, obtain multiple representative raw and indexed samples where the live world provides them. Prefer at least three distinct records and one update/removal/expiry transition where possible; log an explicit absence limitation when not possible. Match visible game/runtime evidence to identifiers, coordinates, classifications, and important fields. Do not assert that arbitrary row counts are invariant between scans of a changing world.

Use an independent source of truth for live assertions, such as a verified current runtime manager, response handler, or in-game observation. Comparing two fields produced by the same faulty normalizer does not independently validate correctness.

### End-to-end user walkthrough

Demonstrate a normal session from the built application: open without login; validate installation; launch; see genuine connected state; refresh; choose scan contents; press the single Start Scan control and let the backend choose the effective strategy; scan to a valid completion; inspect populated categories and filters; mark a city; jump/follow an eligible target; configure/run a bounded auto-scan cycle; stop and close; reopen and verify persisted settings/data. Excel export is not part of the product. Demonstrate conditional actions separately with their actual preconditions/authorization. No hidden manual console step should be required for the normal two-page workflow.

## 14. Evidence, diagnostics, and handoff durability

Create durable project documentation for the final behavior and store machine-readable acceptance artifacts in a clearly named evidence directory. Keep large transient captures under `.codex-live` if appropriate, but persist the essential summary and reproducible scripts in the project.

Suggested per-run record (adapt field names to the real implementation):

```json
{
  "caseId": "B01",
  "implementationRevision": "commit plus working-tree identifier",
  "referenceSha256": "verified original executable hash",
  "gameBuild": "observed installed build/hash",
  "startedAtUtc": "actual timestamp",
  "profileId": "sanitized stable scope",
  "sessionId": "runtime session identity",
  "scanRunId": "scan identity if applicable",
  "serverId": "observed server",
  "worldId": "observed world",
  "configuration": {},
  "beforeEvidence": [],
  "requestsAndResponses": [],
  "coverageAndCapture": {},
  "indexCommit": {},
  "afterEvidence": [],
  "restoration": {},
  "result": "pass | fail | blocked",
  "limitations": []
}
```

Record absolute times/units and durations, actual request/response identities, pending/failure counters, owned PIDs/resources, file hashes before/after, final server state, and any manual intervention. Redact credentials, tokens, and unnecessary player/account identifiers in shareable reports. Never write secrets into this handoff or source control.

Useful diagnostics should answer: which stage failed; which profile/session/server/run/target it belonged to; whether an action was only queued, accepted, applied, or independently confirmed; what cleanup ran; whether retry is safe; and what exact evidence supports that answer. Keep these implementation details in diagnostics, not scattered through the normal feature layout.

At interruption/context exhaustion, save:

1. Current branch/HEAD and meaningful uncommitted changes.
2. Current runtime/process/game/server state and any modifications still requiring restoration.
3. Completed milestones and evidence paths.
4. Exact blockers and the last failure, without hiding failed attempts.
5. The next concrete command/source trace/test, and any still-valid authorization scope.

The next AI should resume from that checkpoint rather than redo expensive recovery or assume that a previously started scan finished.

## 15. Build and verification commands already available

Run from the repository root. Verify tool availability instead of installing unrelated dependencies or assuming a previous AI's temporary environment exists. In the current development layout, the verified reference executable is `..\LW\lwbridge-0.3.1.exe`.

```powershell
# Establish current workspace state; do not reset it.
git status --short
git branch --show-current
git rev-parse HEAD
Get-FileHash '..\LW\lwbridge-0.3.1.exe' -Algorithm SHA256

# Verify original assets and that generated outputs match maintained sources.
python tools/build_lwbridge_frontend.py --check

# After an intentional maintained UI/adapter-generation edit:
python tools/build_lwbridge_frontend.py

# Build.
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj --configuration Release

# Existing deterministic desktop captures. Keep these non-live after integration.
./tools/capture_lwbridge_ui.ps1

# Browser source-parity checks: require Node playwright package + installed Edge.
node tools/check_lwbridge_frontend.cjs

# Pixel metrics: requires Pillow.
python tools/compare_lwbridge_frontend.py

# Final whitespace check; also inspect new/untracked maintained files.
git diff --check
```

If Playwright is supplied outside local `node_modules`, configure `NODE_PATH` to the verified package directory. Existing CI installs `playwright@1.62.1` and `Pillow==12.3.0`; inspect `.github/workflows/csharp.yml` for the current actual configuration. The current framework target directory is `net10.0-windows10.0.17763.0`.

Current static inspection entry points:

```powershell
python tools/inspect_lwbridge_injection.py '..\LW\lwbridge-0.3.1.exe'
python tools/inspect_lwbridge_map_scan.py '..\LW\lwbridge-0.3.1.exe' --embedded-assets
python tools/inspect_lwbridge_map_scan.py '..\LW\lwbridge-0.3.1.exe'
```

Inspect `--help` and source for additional inspectors before using assumed arguments. Add focused backend/unit/integration/fault tests for the new behavior; existing visual and static checks do not test real launch, scan, persistence, or game actions.

When production becomes the default, adapt capture/CI tools to request isolated fixture mode explicitly. Preserve their existing guarantees; do not let a CI screenshot invoke a real game, reconnect loop, scheduled scan, claim, plunder, or alliance share.

## 16. Definition of done and current completion boundary

The ordinary Home / Overview and Map Data implementation is complete at the scopes represented by the current 47-case matrix. A future release may be called fully integrated only after remaining population/authorization/protected-contract/multi-account gates are either proven, explicitly retired, or explicitly accepted as release limitations by the owner.

Current completion must never be summarized as ?every possible live action has been tested.? The precise current statement is: **zero ordinary technical partials; remaining gates are explicitly categorized external or protected-contract items.**

The final release package must continue to preserve fixture isolation, UI fidelity, evidence labels, cleanup/restoration, and unrelated worktree changes.

## 17. First action for the receiving AI

Read `AGENTS.md`, `docs/README.md`, `docs/implementation-handoff.md`, `docs/tabs/home.md`, `docs/tabs/map-data.md`, `BACKLOG.md`, and the current acceptance matrix. Then inspect HEAD/worktree before modifying anything.

Do not restart old category-by-category implementation work merely because a historical document calls it pending. Continue only a current `BACKLOG.md` item or a newly reproduced regression. Preserve SB-79 and other recorded restriction boundaries. Commit/push/verify every coherent checkpoint under `AGENTS.md`.
