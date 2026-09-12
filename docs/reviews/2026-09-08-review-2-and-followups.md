# Historical review 2 and implementation follow-ups

**Superseded by [review 3](../lwbridge-project-status.md).** Retained as historical audit/recovery evidence only. Statements about open defects and next actions below refer to earlier snapshots and are not the current backlog.

# Project manager checkpoint — review 2

Reviewed: 2026-09-08, base commit `0664e0a0b38bfc37d7ff195a45bf30b56ec93825` plus the current uncommitted foundation/map-index changes. This report supersedes the earlier audit narrative. The review evidence records the exact implementation file hashes; the forthcoming Git commit identifies the checkpoint without requiring a self-referential commit hash in this file.

## Decision and reading order

**Keep the new foundation and SQLite work. Neither Overview nor Map Data is complete.** Standard checks pass, but four independent edge-case reproductions fail. R2 and R3 therefore remain partial, rather than fully signed off.

1. Read [task.md](../../task.md) for the full two-page requirements and 47 acceptance cases. This is the primary AI instruction file.
2. Follow [BACKLOG.md](../../BACKLOG.md) for the ordered next work and checkboxes. It was previously named `TASKS.md`; the rename removes the confusing pair of near-identical task filenames. Do not recreate that old file.
3. Use [the feature ledger](../lwbridge-feature-ledger.md) for per-feature proof and the existing subject documents for recovered technical contracts.

The PM2-01 through PM2-04 follow-up checkpoint is now **IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED**. The later R3/R4 native-host checkpoints also complete the controlled WebView2 lifetime and interaction matrix. Continue R5 bootstrap research and recoverable R6 offline map work in parallel. Do not redo UI reproduction or legacy cleanup.

## Post-review PM2 foundation fix — 2026-09-08

These fixes are rebuild safety/lifetime policy, not recovered claims about original LWBridge internals. The verified reference remains `lwbridge-0.3.1.exe` SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`; no new original command, value, offset or ABI contract is inferred here. Machine-readable evidence is [2026-09-08-pm2-foundation-fix.json](../../evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json).

| Finding | Source identity / locator | Reproduction and result | Validation / limits | Implementation impact |
|---|---|---|---|---|
| **PM2-FIX-01** — IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED | `LWBridgeBackend.cs` SHA-256 `fa0c0f9216eb96a7278b1050572fdfa5bde56bf1fdef8c5ee47feb5a827d3cd7`, `SetLocalConfig` around line 245 | Standard checks interleave another owner's reconnect/root/history commits with backend `local_config_set`; reload preserves those fields, the profile ID and the requested auto-launch change. Independent PM2-01 now reports `passed:true`. | Isolated filesystem only; a real overlapping WebView preference interaction is still R4. | Partial config writes now mutate the fresh baseline supplied under the storage lock instead of replacing it with a stale snapshot. |
| **PM2-FIX-02** — IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED | `LocalConfigStore.cs` SHA-256 `a42dfa8f27d7496fa21d33bb69d0f430d9bf5dfe2c73152ce3c9f72722b4f7c6`, `Load` line 153 and `GetFilePresence` line 233 | Standard checks separately cover corrupt-primary recovery, missing-primary + valid owned backup, missing-primary + incompatible backup and unreadable primary storage. Independent PM2-02 now reports `passed:true`. | No live user config is modified; OS-specific denied-access behavior beyond deterministic directory/read faults remains a production-host test. | A missing primary first validates/reuses an owned backup; unsupported/uncertain storage fails closed rather than silently generating a new profile identity. |
| **PM2-FIX-03** — IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED | `LocalConfigStore.cs` same hash/locator as PM2-FIX-02 | Owner mismatch and future schema are tested with and without valid backups; exact primary bytes remain unchanged and structured `CONFIG_OWNER_MISMATCH` / `CONFIG_SCHEMA_UNSUPPORTED` errors surface. Independent PM2-03 now reports `passed:true`. | Static isolated storage; no downgrade/migration is implemented for future schemas. | Explicit compatibility rejection is separated from corruption recovery; backups no longer overwrite incompatible primaries. |
| **PM2-FIX-04** — IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED | `NativeRequestRegistry.cs` SHA-256 `230e9667bad6512ac446aad1d0572e012d82518925812bd5073793ee1ebb3080`, `Close` line 65; `NativeRequestExecutor.cs` SHA-256 `e4182486c2855a09843accbf792ee68af6e0871ac0a164b86e081f390661d9b5`, `ExecuteAsync` line 21 | Standard checks cover cooperative cancel, noncooperative normal return/fault after close, explicit cancel with late return and 32 cancel/completion races. Independent PM2-04 returns `Cancelled` and `passed:true`. | Executor policy suppresses late closed/cancelled faults as `Cancelled`; durable logging remains unavailable because `append_log` is still a no-op. Cancellation cannot undo a service mutation already committed before its cancellation boundary. | Close cancels but request completion owns disposal; a stable token remains usable until late work drains and late success publication stays blocked. |

## R3 native-host lifetime follow-up — 2026-09-08

This checkpoint is **IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED**. It changes rebuild host scheduling/session ownership only; it does not assert a newly recovered original LWBridge timeout, thread, navigation or lifecycle contract. Durable evidence is [2026-09-08-r3-native-host.json](../../evidence/lwbridge-implementation/2026-09-08-r3-native-host.json).

| Finding | Source identity / locator | Reproduction and result | Validation / limits | Implementation impact |
|---|---|---|---|---|
| **R3-HOST-01** — nonblocking native command scheduling | `LWBridgeWindow.cs` SHA-256 `89a848d5dace7e004d99b4ab68915e4b0583b6c1656d7e263bda37d3106088ed`, `OnWebMessageReceived` around line 543 and `SelectGameRootAsync` around line 638 | Isolated real WebView2 probe holds the real `LocalConfigStore` `config.lock` for ~800 ms, invokes `local_config_set`, and measures a 50 ms browser timer. Observed timer ~55.8 ms while storage completion was ~867.5 ms. | Real WebView2 + real isolated config persistence; no user config or game command. The diagnostic timings are test policy, not recovered production thresholds. | Backend config/install/SQLite calls enter through `Task.Run`; folder picker remains on the UI thread, while root inspection/save run off-thread. |
| **R3-HOST-02** — per-document request/subscription generation | `LWBridgeWindow.cs` same hash, `OnNavigationStarting` line 191, `RotateDocumentSession` line 216, `DocumentSession` line 749 | `CoreWebView2.Reload()` rotates session ID/generation, records one old active request and seven subscriptions, cancels/drains the request, clears subscriptions and reaches generation 2. | Actual WebView2 host path, not the console executor surrogate. Same-origin reload is covered; remaining page/picker/busy interaction matrix stays R4. | Each document owns its own executor/subscription registry; same-origin navigation tears down old ownership before loading the next document. |
| **R3-HOST-03** — stale publication/session rejection | `LWBridgeWindow.cs` same hash, `OnWebMessageReceived` line 543 and `IsCurrentDocument` line 714 | After reload, an explicit invoke carrying the old session ID does not enter the diagnostic service (`slowSyncStarted` remains 0 before/after); old delayed work cannot publish into the new document. | Host probe also verifies structured `DIAGNOSTIC_EXPECTED` error delivery. Closed-window late response is still an open R4 case. | Request handlers capture their document owner and publish only while it remains the current live document. |
| **R3-HOST-04** — native origin/startup isolation | `LWBridgeWindow.cs` same hash, navigation handler line 191; `Program.cs` SHA-256 `5929104720fe52eccc70cf146eb52bfc922dfe98420ee3c319031198850b76a8`, `--host-probe` line 11 | Probe navigation to `https://example.invalid/...` is rejected without rotating the current local session; reloaded bootstrap reports auto-launch suppressed. | Probe uses a unique temp config root and in-memory map DB, deletes the temp config afterward and performs no live-game command. | Controlled native-host verification can exercise production transport without mutating normal user storage or startup lifecycle. |

## R4 production-WebView interaction follow-up — 2026-09-08

This checkpoint combines one **RECOVERED** frontend contract with **IMPLEMENTATION POLICY + IMPLEMENTED/OFFLINE-TESTED** rebuild behavior. The verified reference remains `lwbridge-0.3.1.exe` SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`; machine-readable evidence is [2026-09-08-r4-native-host-interactions.json](../../evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json).

| Finding | Source identity / locator | Reproduction and result | Validation / limits | Implementation impact |
|---|---|---|---|---|
| **R4-HOST-01** — duplicate active request ownership | `LWBridgeWindow.cs` SHA-256 `328ec16e5cc87e1f261892d4b16d3475d9144480c9d799341b455de7689cd754`, `RunHostProbeAsync` line 247 / `OnWebMessageReceived` line 812 | Real WebView2 posts the same active request ID twice. Exactly one delayed service operation starts; the duplicate receives `DUPLICATE_REQUEST_ID`, the owned request receives `COMMAND_CANCELLED`, and ownership drains. | Isolated diagnostic service; no production retry count/timing or live game outcome is inferred. | The duplicate-ID production host path is now tested through real WebView2, not only the executor surrogate. |
| **R4-PREF-02** — actual React rollback + overlapping saves | `WebUi/local-providers.js` SHA-256 `25f61d31c81ddc3a0a479a78cf7e7d633db0c4b3100c23a5587939cbe53fe0ff`, lines 7–41; `HostProbeCommandService.cs` SHA-256 `90a454dddfc273aa8dfb3bb4b1b9ecc07044bfe5659ff121fcb780b54e4e2418`, `BeforeProductionCommandAsync` line 86 | The rendered auto-launch switch changes `false -> true -> false` after a delayed rejected save while durable config remains false. Two rapid successful toggles are serialized (`configSaveMaxActive=1`) and final DOM/config both match the latest false intent. | Save sequencing is explicit rebuild policy, not recovered original concurrency behavior. The recovered profile-error pane is hidden in the login-free single-profile layout; that observed presentation limit remains recorded in machine evidence. | Preference writes are sequenced and a stale save failure cannot roll back a newer UI revision. |
| **R4-PICKER-03** — recovered picker result contract | Immutable recovered `index-sfL2sT3K.js` SHA-256 `4d18cbc036a417b1a13a2c9905f1abc5dda1427531ad1ab4f7b03f4b269704b3`, line 32 `async function en()`; `LWBridgeWindow.SelectGameRootAsync` line 916 | **RECOVERED:** original Overview reads both `canceled` and `valid` from `game_root_select`. Real WebView probe drives the rendered missing-root button: busy disables for cancel/invalid, cancel preserves missing-root state, invalid displays localized `INVALID_GAME_ROOT`. | Probe chooses cancel/invalid deterministically only under `--host-probe`; normal mode keeps the real `FolderBrowserDialog` and installation validation. | Native cancel returns `{canceled:true}` and invalid status is returned to the recovered UI instead of being converted into a host exception. |
| **R4-HOST-04** — closed-window late response suppression | `LWBridgeWindow.cs` same hash, late probe / `SendMessage` line 1000; `Program.cs` SHA-256 `b1194af8d14d66a7d1e6cc06432086f0d23855ee3aa5ca9cd3622a1bfe7b0be2`, lines 20–30 | Start one noncooperative late request, close the real form, then release completion. Service completes and ownership drains `1 -> 0`; native posted-message count stays `79 -> 79`. | Probe-only ApplicationContext keeps the UI thread alive long enough to persist post-close evidence. Normal application lifetime is unchanged. | No late success/error is published after the actual window closes. |

## Progress credited in this review

| Area | Verified progress | Remaining limit |
|---|---|---|
| R1 profile/transport contract | Generated API restores active-profile injection and envelope filtering; typed native scope checks; Node tests plus the isolated native WebView host verify origin/session/error/reload boundaries | Remaining native interaction cases are tracked under R4 rather than a missing real-host proof. |
| R2 config foundation | Durable replacement before memory commit; isolated test/capture storage; backups; schema/owner checks; normalized stored history; basic UI rollback; PM2-01–03 partial-update/recovery/compatibility regressions now pass | Rapid overlapping UI saves still need a real production-WebView interaction test. |
| R3 request foundation | Shared request executor/registry plus real per-document WebView ownership; PM2 cancellation races pass; slow config storage is off-thread and actual reload cancels/clears old ownership | Durable correlated diagnostics still wait for a real lifecycle/scan worker. |
| R4 checks/compatibility | Deterministic suite is in CI; missing-native mode rejects; AMD64/PE32+ checks pass; isolated native WebView now verifies origin/session/error/reload/storage responsiveness and startup suppression | Busy state, picker cancel/invalid selection, overlapping preference save/rollback and closed-window late response were completed by R4; `LWB-R5-002` now resolves the separate xLua ABI selector recovery. |
| R6 map foundation | Recovered SQLite tables/indexes, explicit-key upsert/read/count, profile-specific database, mark/unmark, transactionally scoped clear preserving marks; restart/large-ID tests | No real ingestion, query/options/export implementation, generation guard, completed-run publish or job scheduler. Per-kind authoritative key derivation remains unknown. |
| UI/cleanup | Original assets/generator retained; nine fixture captures pass; current tracked legacy cleanup retained | Fixture rendering is separate from production behavior and current-client outcomes. |

## Reproduced defects — fix these first

The standalone [review reproducer](../../evidence/lwbridge-implementation/pm-review-2-repro/Program.cs) references the real production classes and uses unique scratch config directories. It does not launch a game or write real user settings. Run from the repository root:

```powershell
dotnet run --project evidence/lwbridge-implementation/pm-review-2-repro/Audit.csproj --configuration Release
```

Its JSON contains a `passed` value for each expected property. At the review-2 snapshot all four properties were false; retain that historical review evidence. Against the current repaired source, the same reproducer reports all four `passed:true`, and the corrected cases plus additional owner/schema/storage/race branches are now in the standard deterministic suite.

### PM2-01 — Backend preference save loses another owner's committed value (P0 / R2)

**Source:** `LWBridgeBackend.SetLocalConfig` reads `config.Snapshot` into `next`, then calls `UpdateConfig(_ => next)`. Although `LocalConfigStore.Update` reloads under its writer lock, this callback discards that refreshed baseline.

**Reproduction:** Open two stores on one isolated root. Construct the backend with store A. Store B saves `AutoReconnect=true`. Use backend A to save only `autoLaunchGame=false`. Reopen the store: reconnect has reverted to false.

**Instruction:** Validate the requested fields first and apply only those fields to the baseline passed into the locked update callback. Do not replace the whole record with a stale snapshot. Test through `local_config_set`, including interleaved history/root/reconnect updates, not only direct `store.Update(c => c with {...})` calls.

**Exit:** Each partial save preserves all unrelated committed fields and the same profile identity across two owners and reload.

### PM2-02 — Missing primary ignores a valid backup and creates a new identity (P0 / R2)

**Source:** `LocalConfigStore.Load` immediately creates a default when `config.json` is absent, before considering `config.backup.json`.

**Reproduction:** Create config and perform a save so a backup exists; record the profile ID; remove only the primary inside the isolated test directory; reopen. A new profile ID is created. Because map DB paths use the profile ID, this can also strand access to the previous profile's map data.

**Instruction:** Define and implement a missing-primary recovery policy. Validate and recover a valid owned backup before creating a fresh profile. Distinguish absent storage from unreadable storage; preserve unsupported or uncertain data and report the problem explicitly.

**Exit:** Missing primary plus valid owned backup recovers the same profile; no usable primary/backup produces the explicitly intended new-install or recovery-error behavior. Include real storage fault scenarios, not just abandoned temp files.

### PM2-03 — Owner/schema rejection is treated as corruption and overwritten (P0 / R2)

**Source:** `LocalConfigStore.Load` catches every `LocalConfigStoreException` and restores a backup, including owner mismatch and unsupported schema.

**Reproduction:** Create an owned config with a valid backup. Replace the isolated primary with valid JSON whose owner is `OtherApplication`. Reopen: the backup is loaded and the foreign primary is replaced. The earlier foreign-owner test without a backup does not cover this branch.

**Instruction:** Separate corrupt/unreadable recovery candidates from explicit owner/schema incompatibility. Refuse automatic replacement or downgrade of an incompatible primary. Test owner mismatch and future schema with and without a valid backup, checking exact file preservation.

**Exit:** Unsupported files remain byte-for-byte unchanged and produce the intended structured error; valid corruption recovery still works.

### PM2-04 — Late noncooperative return after close throws instead of cancelling (P0 / R3)

**Source:** `NativeRequestRegistry.Close` cancels/disposes active cancellation sources. When an operation later returns normally, `NativeRequestExecutor.ExecuteAsync` reads `cancellation.Token`, which throws `ObjectDisposedException` after disposal.

**Reproduction:** Execute a request returning an unresolved task that ignores its token; close the executor; then resolve that task normally. Awaiting the request throws `ObjectDisposedException`, rather than returning `Cancelled`.

**Instruction:** Give cancellation-source disposal one clear owner and preserve a usable token/lifetime through operation completion. Ensure teardown blocks publication and disposal does not race a returning operation. Test noncooperative normal return and fault after cancel/close, plus cooperative cancellation and the cancel/completion race. Never claim cancellation can undo a mutation already committed by a service; real workers need cancellation checks and reconciliation at their commit boundaries.

**Exit:** No teardown-induced exception or late success escapes; ownership drains without leaks. A late service fault is handled/logged according to the closed-session policy.

## Remaining host verification after those fixes

- **UI responsiveness — IMPLEMENTED/OFFLINE-TESTED:** backend work is scheduled off the WinForms UI thread; the real isolated config-lock probe keeps browser timing responsive during an ~868 ms storage wait. Folder dialog presentation remains UI-owned.
- **Actual document lifetime — IMPLEMENTED/OFFLINE-TESTED:** same-origin reload/navigation rotates document session/generation and closes old requests/subscriptions; actual WebView2 reload proves cancellation/reset and stale-session rejection.
- **Production-mode matrix — IMPLEMENTED/OFFLINE-TESTED:** origin/error handling, duplicate active IDs, session rotation/stale rejection, reload cancellation/subscription reset, slow-storage responsiveness, actual React preference rollback/ordered overlapping saves, picker busy/cancel/invalid behavior, closed-window late-response suppression and startup suppression all pass in the isolated native host. Missing-native browser mode also passes. This does not establish any live game lifecycle/scan result.
- **Compatibility:** AMD64/PE32+ is implemented. `LWB-R5-002` now recovers the exact secure/plain xLua ABI/export fingerprint algorithm and confirms the current official xLua matches the secure bundle fingerprint. Lifecycle use of that selector remains blocked with the rest of R5 launch.
- **Diagnostics/events:** `append_log` remains a no-op, runtime heartbeat/pending semantics are missing, and events are mainly local config/mark refreshes. Connect durable correlated diagnostics and real worker state when those services exist.

## R5–R10 implementation queue

| Work | Next concrete deliverable | Acceptance limit |
|---|---|---|
| R5 Overview | `LWB-R5-001` recovers the outer-host `LaunchEnvelope` JSON producer/handoff. `LWB-R5-002` recovers the exact xLua ABI fingerprint selector and confirms current build `1.0.361 / 1078` maps to `secure`. Next recover proof/ticket generation + validation and child-launcher input decoding; then implement one owned lifecycle with verified handshake/heartbeat, stop, startup, reconnect/repair and cleanup | Launch still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; close rejects `INSTANCE_NOT_OWNED`. Static selector recovery does not make launch operational. |
| R6 offline map | `LWB-R6-003` now serves the recovered default persisted `map_search` slice with pagination, stable `updatedAt`/`record_key` ordering, `{rows,total}`, city mark projection and `markedOnly`. Next recover remaining filter/sort SQL, options/counts, full filtered Excel export, per-kind keys/fields, version/migration and staging/publication/generation rules | `map_data_options` and `map_city_export` still reject `MAP_INDEX_UNAVAILABLE`; advanced filters and non-`updatedAt` sorts reject IMPLEMENTATION POLICY `MAP_QUERY_UNRECOVERED`. `map_summary` is still unavailable/zero, and no live scan/query result is proven. |
| R7 manual scan | Connect runtime capture and exact scheduler, retries/resume, typed ingestion and commit/drain gates; prove bounded then full scans | Normalization is implemented; `map_scan_start` rejects `BRIDGE_NOT_READY`, stop returns a placeholder, and no live scan proof exists. Clear must prevent late records resurrecting after it completes. |
| R8 auto scan | Durable single-owner scheduler, confirmed travel, intervals/Run Now/cancel/restart and return-to-origin | No live travel or automatic cycle exists. Do not tie service ownership to mounted pages. |
| R9 actions/jobs | Jump/follow, treasure claims, dispatch/truck/train workflows, job reconciliation and alliance-share payloads | SQLite job tables are not functioning job services. Use authoritative outcomes; message delivery requires explicit authorization. |
| R10 acceptance | Execute the 47-case matrix, normal-user walkthrough, visual/state regressions and package a runnable build | No full-case acceptance is newly signed off by this audit. Report partial/offline/live proof separately. |

**Map clear/marks credit:** The backend now uses the SQLite store for mark/unmark and per-server clear, and emits player-mark refresh. Clear deletes that server's scan runs/map records while preserving marks and other servers. This is real offline implementation; calling it entirely unimplemented is stale. It is also not complete M05 acceptance until generation/cancellation/failure/late-result behavior exists.

## Verification and delivery record

Fresh checks and findings are recorded in [review 2 evidence](../../evidence/lwbridge-implementation/2026-09-08-pm-review-2.json), including exact file hashes and test limits. The older milestone, foundation and PM audit JSON files are historical records, not rewritten claims.

- Source/hash generation, Release build and existing deterministic backend checks pass; game/launcher were not running in the diagnostic snapshot.
- Node missing-native and transport-boundary harnesses pass. These are JavaScript runtime tests, not full native UI tests.
- Nine fixture desktop captures pass, with real configuration bytes unchanged.
- The independent edge-case reproducer reports four PM2 passes. The isolated native WebView host additionally passes the R3 reload/session/storage cases and the complete controlled R4 interaction matrix recorded above.
- Fresh source-reference browser/pixel verification and final post-rename checks are recorded in review 2 evidence.
- No live launch, injection, travel, scan, claim or message delivery was performed. This commit is a development checkpoint, not a release declaring both pages complete.

## Instructions for the next AI

1. Read `task.md`, then `BACKLOG.md`, then this audit and relevant evidence. Recheck Git state and changed source; preserve the original recovered assets and completed cleanup.
2. Preserve the completed PM2-01–04 regression coverage and the fail-closed compatibility behavior; do not reopen those gates without contradictory evidence.
3. Preserve the completed R3/R4 native-host lifetime/interaction proof and `LWB-R5-002` xLua ABI selector recovery. Progress R5 proof/ticket/child-input recovery and R6 query/export work independently where contracts permit.
4. Update the backlog, feature ledger and subject recovery document after each batch. Record exact code revision/file hashes, command results, and remaining unknowns. A checked box needs implementation plus its stated evidence.
5. Keep `task.md` as requirements/instructions, `BACKLOG.md` as current checkboxes, and this report as the dated review. Do not recreate `TASKS.md` or add another competing task specification.
6. Preserve unrelated work and exclude local config, databases, captures, binaries, credentials and scratch directories from commits. Commit coherent progress with accurate limits; push without force only to the user-authorized branch/remote.
