# Project manager checkpoint — review 2

Reviewed: 2026-09-08, base commit `0664e0a0b38bfc37d7ff195a45bf30b56ec93825` plus the current uncommitted foundation/map-index changes. This report supersedes the earlier audit narrative. The review evidence records the exact implementation file hashes; the forthcoming Git commit identifies the checkpoint without requiring a self-referential commit hash in this file.

## Decision and reading order

**Keep the new foundation and SQLite work. Neither Overview nor Map Data is complete.** Standard checks pass, but four independent edge-case reproductions fail. R2 and R3 therefore remain partial, rather than fully signed off.

1. Read [task.md](../task.md) for the full two-page requirements and 47 acceptance cases. This is the primary AI instruction file.
2. Follow [BACKLOG.md](../BACKLOG.md) for the ordered next work and checkboxes. It was previously named `TASKS.md`; the rename removes the confusing pair of near-identical task filenames. Do not recreate that old file.
3. Use [the feature ledger](lwbridge-feature-ledger.md) for per-feature proof and the existing subject documents for recovered technical contracts.

The next coding batch is **PM2-01 through PM2-04 below**, then remaining host/production-mode verification. Continue R5 bootstrap research and recoverable R6 offline map work in parallel. Do not redo UI reproduction or legacy cleanup.

## Progress credited in this review

| Area | Verified progress | Remaining limit |
|---|---|---|
| R1 profile/transport contract | Generated API restores active-profile injection and envelope filtering; typed native scope checks; Node runtime tests of wrong-session/profile and late responses | The JavaScript harness is a Node VM, not an actual WebView host. Host origin/reload and operation-level identity tests remain. |
| R2 config foundation | Durable replacement before memory commit; isolated test/capture storage; backups; schema/owner checks; normalized stored history; basic UI rollback | PM2-01–03 expose lost-update and recovery-policy gaps. Rapid overlapping UI saves also need a real interaction test. |
| R3 request foundation | Shared request executor/registry, cooperative cancellation, duplicate IDs, allowlisted subscription ownership and close cleanup | PM2-04 fails for a noncooperative late return. Current synchronous handlers still run on the UI thread; actual navigation/session invalidation is missing. |
| R4 checks/compatibility | Deterministic suite is now in CI; game installation is an optional diagnostic; missing-native mode rejects; AMD64 plus PE32+ checks added; probe bootstrap disables startup launch | Exact xLua ABI selection and controlled production-WebView tests remain. A class-level simulated reload is not a real WebView reload test. |
| R6 map foundation | Recovered SQLite tables/indexes, explicit-key upsert/read/count, profile-specific database, mark/unmark, transactionally scoped clear preserving marks; restart/large-ID tests | No real ingestion, query/options/export implementation, generation guard, completed-run publish or job scheduler. Per-kind authoritative key derivation remains unknown. |
| UI/cleanup | Original assets/generator retained; nine fixture captures pass; current tracked legacy cleanup retained | Fixture rendering is separate from production behavior and current-client outcomes. |

## Reproduced defects — fix these first

The standalone [review reproducer](../evidence/lwbridge-implementation/pm-review-2-repro/Program.cs) references the real production classes and uses unique scratch config directories. It does not launch a game or write real user settings. Run from the repository root:

```powershell
dotnet run --project evidence/lwbridge-implementation/pm-review-2-repro/Audit.csproj --configuration Release
```

Its JSON contains a `passed` value for each expected property. It intentionally reports known failures without failing the normal build/CI; **exit code zero is not a passing audit**. Current result: all four expected properties are false. Add corrected cases to the standard regression suite when fixing them; retain this historical evidence.

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

- **UI responsiveness:** The executor awaits the supplied operation; it does not itself move synchronous work off-thread. Config file-lock waits, installation reads and SQLite handlers still execute synchronously through the UI message handler. Introduce deliberate service scheduling/cancellation for potentially blocking work while keeping the folder picker/UI delivery on the UI thread. Test slow storage without freezing rendering.
- **Actual document lifetime:** `NavigationStarting` currently validates the URL only; the session ID/executor/subscriptions last for the window. Same-origin reload/navigation does not rotate session identity or cancel old document requests. Test and implement actual document generation/teardown. Creating a fresh executor in a console test does not prove this host path.
- **Production-mode matrix:** Verify origin rejection, missing bridge, error/busy states, save rollback, picker cancel/invalid selection, reload, closed-window late response and page navigation using a controlled native service in WebView2. Retain explicit startup suppression and storage isolation for probes. Do not interpret the earlier execution-policy rejection of a production probe as a product test result; it remains unverified.
- **Compatibility:** AMD64/PE32+ is implemented. Exact secure/plain xLua ABI/export fingerprint selection remains unresolved R5 work; do not mark ABI compatibility complete.
- **Diagnostics/events:** `append_log` remains a no-op, runtime heartbeat/pending semantics are missing, and events are mainly local config/mark refreshes. Connect durable correlated diagnostics and real worker state when those services exist.

## R5–R10 implementation queue

| Work | Next concrete deliverable | Acceptance limit |
|---|---|---|
| R5 Overview | Recover the exact `LaunchEnvelope` producer/representation/validation and ABI selection from existing reference evidence; implement one owned lifecycle with verified handshake/heartbeat, stop, startup, reconnect/repair and cleanup | Launch still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; close rejects `INSTANCE_NOT_OWNED`. Stored switches and reconcile status reads are not working lifecycle features. |
| R6 offline map | Finish recovered query/filter/sort/options/counts, persistent marks in results, full filtered Excel export; recover per-kind keys and fields; version/migrate store and implement staging/publication/generation rules | `map_data_options`, `map_search` and `map_city_export` validate then explicitly reject `MAP_INDEX_UNAVAILABLE`, even though the SQLite foundation exists. `map_summary` is still unavailable/zero. |
| R7 manual scan | Connect runtime capture and exact scheduler, retries/resume, typed ingestion and commit/drain gates; prove bounded then full scans | Normalization is implemented; `map_scan_start` rejects `BRIDGE_NOT_READY`, stop returns a placeholder, and no live scan proof exists. Clear must prevent late records resurrecting after it completes. |
| R8 auto scan | Durable single-owner scheduler, confirmed travel, intervals/Run Now/cancel/restart and return-to-origin | No live travel or automatic cycle exists. Do not tie service ownership to mounted pages. |
| R9 actions/jobs | Jump/follow, treasure claims, dispatch/truck/train workflows, job reconciliation and alliance-share payloads | SQLite job tables are not functioning job services. Use authoritative outcomes; message delivery requires explicit authorization. |
| R10 acceptance | Execute the 47-case matrix, normal-user walkthrough, visual/state regressions and package a runnable build | No full-case acceptance is newly signed off by this audit. Report partial/offline/live proof separately. |

**Map clear/marks credit:** The backend now uses the SQLite store for mark/unmark and per-server clear, and emits player-mark refresh. Clear deletes that server's scan runs/map records while preserving marks and other servers. This is real offline implementation; calling it entirely unimplemented is stale. It is also not complete M05 acceptance until generation/cancellation/failure/late-result behavior exists.

## Verification and delivery record

Fresh checks and findings are recorded in [review 2 evidence](../evidence/lwbridge-implementation/2026-09-08-pm-review-2.json), including exact file hashes and test limits. The older milestone, foundation and PM audit JSON files are historical records, not rewritten claims.

- Source/hash generation, Release build and existing deterministic backend checks pass; game/launcher were not running in the diagnostic snapshot.
- Node missing-native and transport-boundary harnesses pass. These are JavaScript runtime tests, not full native UI tests.
- Nine fixture desktop captures pass, with real configuration bytes unchanged.
- The independent edge-case audit above reports four failures; these are release blockers, not build failures.
- Fresh source-reference browser/pixel verification and final post-rename checks are recorded in review 2 evidence.
- No live launch, injection, travel, scan, claim or message delivery was performed. This commit is a development checkpoint, not a release declaring both pages complete.

## Instructions for the next AI

1. Read `task.md`, then `BACKLOG.md`, then this audit and relevant evidence. Recheck Git state and changed source; preserve the original recovered assets and completed cleanup.
2. Fix PM2-01–04 with meaningful regression tests. Preserve the already working foundation. Update only the affected completion claims when their exact exit tests pass.
3. Complete remaining R3/R4 host verification; progress R5 research and R6 query/export work independently where contracts permit. Do not stop all map work just because live bootstrap is blocked.
4. Update the backlog, feature ledger and subject recovery document after each batch. Record exact code revision/file hashes, command results, and remaining unknowns. A checked box needs implementation plus its stated evidence.
5. Keep `task.md` as requirements/instructions, `BACKLOG.md` as current checkboxes, and this report as the dated review. Do not recreate `TASKS.md` or add another competing task specification.
6. Preserve unrelated work and exclude local config, databases, captures, binaries, credentials and scratch directories from commits. Commit coherent progress with accurate limits; push without force only to the user-authorized branch/remote.
