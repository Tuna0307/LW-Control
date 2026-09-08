# LWBridge implementation backlog

Last updated: 2026-09-08

Project-manager review 2 audited `0664e0a` plus the foundation/SQLite work. The follow-up PM2 checkpoint fixes all four independently reproduced edge cases, R3 proves real document lifetime/UI responsiveness, and the R4 isolated native-host checkpoint completes the controlled production-WebView interaction matrix. **The remaining foundation limit is integration with real lifecycle/scan workers and their durable diagnostics; Overview lifecycle and full Map Data functionality remain incomplete.** Read [the audit](docs/lwbridge-project-status.md), [PM2 fix evidence](evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json), and [R4 native-host evidence](evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json). Keep [task.md](task.md) as the detailed instructions/acceptance contract. This checklist was renamed from `TASKS.md` to avoid confusing the two files; do not recreate the old name.

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Project rules

**Mandatory on every task/checkpoint, not one-time checkboxes:** follow [AGENTS.md](AGENTS.md). Prioritize reverse-engineering verified LWBridge and current official Last War artifacts; never invent production facts or numbers. Immediately document every successful recovery with source/hash/locator/reproduction/limits. Update evidence and progress, run applicable checks, then commit and push the completed task/checkpoint to the verified GitHub branch and confirm the remote revision. Report actual blockers rather than silently skipping these requirements.

- [x] LWBridge is the only feature authority for this repository.
- [x] Active login/account/license UI is excluded from the independent rebuild.
- [x] Original recovered frontend assets are persisted with an integrity manifest.
- [x] Fixture/capture mode is isolated from live game actions.
- Keep RECOVERED, IMPLEMENTED/OFFLINE-TESTED, LIVE-PROVEN, UNKNOWN/BLOCKED and explicitly labelled IMPLEMENTATION POLICY distinct in every feature record.
- Document each newly confirmed finding immediately; no recovery/task checkpoint is complete without its durable evidence and GitHub delivery or an explicitly recorded delivery blocker.

## Completed foundation

- [x] Recover and reproduce the original React/Vite feature UI in WebView2.
- [x] Preserve eight normal pages and the explicitly requested hidden Advanced view.
- [x] Preserve themes, icons, navigation, and nine languages.
- [x] Add real WebView2 JavaScript-to-C# RPC with profile injection/filtering, request/session IDs, origin checks, structured errors and cooperative timeout/cancel/event plumbing. R3/R4 real-host lifetime and interaction verification now pass; real worker integration remains below.
- [x] Add local profile/configuration persistence. Failure handling and test isolation remain R2.
- [x] Detect the current installation, check required files and game/xLua PE32+ format. Exact machine/ABI compatibility remains R4/R5.
- [x] Keep unmanaged game processes distinct from a verified LWBridge-owned instance.
- [x] Recover Map Scan selected-type allowlist and `normal=8` / `fast=20` request concurrency.
- [x] Add a read-only official-runtime inspector and machine-readable current-install evidence.
- [x] Keep launch and production scanning fail-closed until bridge readiness is proven.
- [x] Complete tracked legacy source/test/tool/document cleanup and retain current LWBridge evidence/references. Do not restore old implementation.

## R1–R4 — Finish the foundation first (P0)

### Next batch — independently reproduced defects

- [x] **PM2-01 / R2:** `SetLocalConfig` applies validated partial fields to the locked fresh baseline; deterministic backend tests preserve another owner's reconnect/root/history fields and profile identity.
- [x] **PM2-02 / R2:** missing primary recovers a valid owned backup and stable profile identity; incompatible backup and unreadable-storage cases fail closed instead of creating a fresh identity.
- [x] **PM2-03 / R2:** owner/schema incompatibility is not treated as corruption; foreign-owner and future-schema primaries remain byte-for-byte unchanged with and without valid backups.
- [x] **PM2-04 / R3:** cancellation-source disposal is owned by request completion; noncooperative return/fault after close or cancel resolves as cancelled, cooperative cancellation still passes, and cancel/completion races drain ownership without teardown exceptions.

Run the [independent reproducer](evidence/lwbridge-implementation/pm-review-2-repro/Program.cs) together with the standard suite. The original review evidence records four historical failures; the current source now reports four `passed:true` outcomes and the corrected expectations are in the standard deterministic checks.

### Existing foundation and remaining integration

- [x] **R1 / profile contracts:** restore original implicit active-profile injection and event-envelope filtering in the generated API/native adapter; classify global commands; validate malformed payloads and profile scope consistently.
- [x] Test implicit/explicit/foreign profiles, malformed input, wrong-session responses and wrong-profile/late events through the frontend/native boundary.
- [x] **R2 implemented slice:** commit memory after durable replacement; add schema/owner checks, isolated storage and corrupt-primary backup recovery. Full recovery/ownership acceptance remains PM2-01–03.
- [x] Roll back rejected preference saves in the UI and make errors visible; persist/validate server-jump history instead of echoing it.
- [x] Inject temporary config roots for checks/capture and prove real user configuration is unchanged. Test denied writes, restart, corrupt JSON, interrupted replacement and competing writers.
- [x] **R3 implemented slice:** shared executor/registry and allowlisted subscription ownership pass cooperative delayed-service, duplicate-ID and window-close checks. The native-host follow-up also offloads backend work and gives each WebView document its own request/subscription generation.
- [x] Test browser timeout propagation plus host explicit cancel/close/reload/duplicate IDs/late completion with a controllable delayed backend service; prove cancellation prevents the service commit and success publication.
- [ ] Connect the request/event lifetime foundation to the first real lifecycle/scan service and add durable correlated diagnostics when that service exists; `append_log` remains a no-op today.
- [x] Move potentially blocking config/installation/SQLite work off the UI thread with deliberate ownership/cancellation. The isolated native WebView probe holds the real config lock for ~868 ms while a browser timer fires at ~56 ms, proving rendering remains responsive; folder selection remains UI-owned while root inspection/save run off-thread.
- [x] Implement/test real document reload/navigation invalidation: a real WebView2 reload cancels the prior request, clears prior subscriptions, rotates the document session/generation and rejects an invoke carrying the old session ID.
- [x] **R4 / verification:** split deterministic tests from installed-game diagnostics; run deterministic backend/transport tests in CI.
- [x] Requested live mode rejects missing native transport in the Node harness; read-only bootstrap suppresses auto-launch and uses isolated storage.
- [x] Complete the production-mode WebView matrix. The isolated native-host probe passes origin rejection, session rotation/stale-session rejection, structured errors, duplicate active request rejection, real reload cancellation/subscription reset, slow-storage responsiveness, actual React preference rollback with ordered overlapping saves, picker busy/cancel/invalid behavior, closed-window late-response suppression and startup auto-launch suppression. This is controlled host proof only; real lifecycle/scan workers remain R5/R7.
- [x] Check AMD64 COFF machine plus PE32+ for game/xLua; include official-runtime inspector syntax checks. Exact xLua ABI selection is now recovered by `LWB-R5-002`; lifecycle integration remains R5.

## R5 — Overview lifecycle (P0 critical path; research alongside R1–R4)

- [x] Recover the host-side producer for `LaunchEnvelope` (`descriptorJson`, `launchProof`, `gameLaunchTicket`). `LWB-R5-001` locates the outer-host state machine, exact field xrefs/value sources, JSON serialization and handoff clone. This is static recovery only; proof/ticket semantics remain below.
- [ ] Recover exact descriptor/proof/ticket representation and validation rules.
- [x] Recover xLua secure/plain ABI fingerprint selection exactly. `LWB-R5-002` recovers the `LWXE1\n` prefix, ordinal/name record construction and ordering, SHA-256 digest, bundle values, and exact current-build `secure` match.
- [ ] Implement owned `profile_instance_start`, `profile_instance_status`, and `profile_instance_stop` lifecycle.
- [ ] Require matching instance identity, bridge handshake, and fresh heartbeat before reporting connected.
- [ ] Implement startup launch preference through the same lifecycle service without double-start races.
- [ ] Implement automatic reconnect/recovery with explicit eligibility, cancellation, and bounded retry behavior.
- [ ] Recover and implement repair/update/restart presentation and state transitions.
- [ ] Validate repeated cold start, restart, disconnect, and stop cycles against the current client.

## R6 — Offline map contracts, index and result services (P0 parallel work)

This work can advance while R5 research is blocked. Static/offline proof does not replace later current-client proof.

- [ ] Trace every Map Data payload/result/conditional control in the original frontend and backend evidence; record unknowns explicitly.
- [x] Recover the original SQLite map-index tables/indexes, stored record identity `(kind,server_id,record_key)`, scan staging identity `(run_id,kind,server_id,record_key)`, player-mark identity `(server_id,owner_uid)`, server-clear scope and completed-kind publish transaction.
- [ ] Recover exact per-kind native `record_key` derivation plus remaining typed normalization/update/removal rules for city, resource, monster, truck, railway, dispatch, ghost and treasure records.
- [ ] Implement transactional profile/server/run storage, generations/checkpoints and durable jobs with restart recovery.
- [x] Add the recovered SQLite schema/index foundation with deterministic restart/upsert/server-scope tests; require an explicit already-derived `record_key` rather than guessing ingestion identity.
- [x] Implement persistent player mark/unmark and server-scoped clear using recovered identities/SQL scope; emit the recovered player-mark refresh event.
- [x] Implement the `LWB-R6-003` default persisted `map_search` slice: recovered pagination, `updatedAt` ordering with `record_key ASC`, `{rows,total}`, city marks/`markedOnly`, and fail-closed `MAP_QUERY_UNRECOVERED` for unsupported filters/sorts.
- [ ] Implement options/counts, search, remaining filters, stable sorting and pagination with late-result rejection.
- [ ] Implement full filtered Excel export and verify it reopens with correct rows/types/large IDs.
- [ ] Connect offline services to real native handlers and result tabs; test using explicitly labelled recovered/synthetic samples. Keep unknown semantic fields open.

## R7 — Production manual scan (P0; requires R5/R6)

- [ ] Connect production `map_scan_start` to the recovered bridge/native capture path.
- [ ] Recover exact block scheduler/tick behavior, block ordering, retry rules, and resume state.
- [ ] Persist scan run/checkpoint state atomically and reject stale run/session/server results.
- [ ] Preserve dropped/pending/acknowledgement/failure semantics and never silently complete with unresolved work.
- [ ] Implement actual Start/Stop/resume/Normal/Fast/selected-type behavior and prove stop cancels/drains owned work. Returning an unavailable status is not cancellation proof.
- [ ] Prove a bounded scan before full coverage; validate UI/query/export against committed data and record updates/removals.
- [ ] Validate representative data for all eight record kinds and repeated full-scan completion.

## R8 — Automatic scanning and server travel (P0)

- [ ] Recover/implement confirmed cross-server travel, valid options/current/home context, persistent history and active-scan conflicts.
- [ ] Implement Auto Scan as a single-owner durable scheduler: configuration, Run Now, intervals/eligibility, target sequence, failures, cancel and return-to-origin.
- [ ] Prove navigation, refresh, reconnect and restart cannot create duplicate cycles or change server context before confirmed travel.

## R9 — Conditional actions and scheduled jobs (P0)

- [ ] Implement coordinate jump and march follow with authoritative visible outcomes.
- [ ] Implement treasure refresh/status/claims; distinguish queued work from confirmed claim results.
- [ ] Implement dispatch/truck plunder schedules, cancellation, recovered train actions and Scheduled Plunder list; persist and reconcile jobs across expiry/restart/reconnect.
- [ ] Implement and test alliance-sharing payloads offline; live message delivery requires explicit messaging authorization.
- [ ] Prove each action's eligibility, duplicate suppression, failure behavior and authoritative outcome using suitable authorized targets.

## R10 — Acceptance and handoff (P0 release gate)

- [ ] Run and record all 47 full acceptance cases from `task.md` as pass/fail/not-run/blocked with exact build and evidence paths. Foundation subchecks do not close an entire case.
- [ ] Recheck production-mode behavior and the fixture visual matrix separately; preserve original labels/layout/assets and login-free startup.
- [ ] Deliver a fresh runnable build and exact commands; update the S/O/M ledger and R backlog with remaining unknowns. Do not claim either page complete with unresolved required cases.

## P1 — Remaining feature families

- [ ] Build the exact original command/event/config catalog from the verified application.
- [ ] Recover one feature family at a time using the same evidence labels and live-proof gates.
- [ ] Keep unsupported commands fail-closed until their contracts are recovered and implemented.

## Verification

Review 2: source/hash, Release build, deterministic backend checks, Node transport checks and nine fixture captures passed. The PM2 follow-up now also passes the independent PM2-01–04 reproducer plus expanded deterministic persistence/request-lifetime regressions. Installed-game inspection remains an optional diagnostic. The runtime inspector collects read-only evidence; it does not verify launch/scan functionality. See [review 2 evidence](evidence/lwbridge-implementation/2026-09-08-pm-review-2.json) and [PM2 fix evidence](evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json).

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
./tools/capture_lwbridge_ui.ps1
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
```

See [docs/README.md](docs/README.md) for the durable evidence/documentation map and [task.md](task.md) for the full Overview + Map Data acceptance contract.
