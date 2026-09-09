# LWBridge implementation backlog

Last updated: 2026-09-09

Project-manager review 6 audited `3138fe9`. See [the current audit](docs/lwbridge-project-status.md), [outcome/ESC register](docs/daybreak-escalations.md), [regular handoff](docs/implementation-handoff.md), [Daybreak handoff](docs/deep-binary-handoff.md) and [rough completion estimate](docs/lwbridge-completion-estimate.md). Clock and treasure-key questions are resolved; public options/export/schema/launch/capture remain incomplete. Test-only helper checkpoints are not finished page functions.

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Project rules

**Mandatory on every task/checkpoint, not one-time checkboxes:** follow [AGENTS.md](AGENTS.md). Prioritize reverse-engineering verified LWBridge and current official Last War artifacts; never invent production facts or numbers. Immediately document every successful recovery with source/hash/locator/reproduction/limits. Update evidence and progress, run applicable checks, then commit and push the completed task/checkpoint to the verified GitHub branch and confirm the remote revision. Report actual blockers rather than silently skipping these requirements.

**Tool permission is already granted:** discover/install/configure useful reverse-engineering tools and runtimes as needed. Missing integration is a setup problem to solve, not a reason to guess or stop. Record tool versions/locations/invocations. Attribute real environment denials to the rejecting system and its stated reason, not to the user; continue permitted independent work.

- [x] LWBridge is the only feature authority for this repository.
- [x] Active login/account/license UI is excluded from the independent rebuild.
- [x] Original recovered frontend assets are persisted with an integrity manifest.
- [x] Fixture/capture mode is isolated from live game actions.
- Keep RECOVERED, IMPLEMENTED/OFFLINE-TESTED, LIVE-PROVEN, UNKNOWN/BLOCKED and explicitly labelled IMPLEMENTATION POLICY distinct in every feature record.
- Document each newly confirmed finding immediately; no recovery/task checkpoint is complete without its durable evidence and GitHub delivery or an explicitly recorded delivery blocker.

## Ownership and escalation — review 6 outcomes

**Regular AI first, including permitted binary analysis.** DB-01–06 are topic labels, not model assignments. Only request Daybreak when relevant permitted methods have been exhausted and recorded with outputs/alternatives/reasons under an ESC ID; the PM reviews the bounded scope. See AGENTS.md section 6. A denial or missing integration alone does not establish a specialist assignment.

- [x] Integrate Daybreak R6-006/007 and regular-AI R6-008–013 in the shared branch; no cherry-pick required.
- [x] PM5-01: correct old boolean inspector's railway/reindeer metadata and identify R6-010 as the saved frontend authority.
- [x] Regular AI: persisted quality regressions now cover railway/dispatch/ghost UR rows with `isSpecialURQuality=true`, a non-special truck quality above five, truck UR page/count consistency, and server isolation. Railway also combines recovered UR + retained-item predicates.
- [ ] Regular AI: complete ESC-002/003/004 method/alternative packets or close their questions with supported evidence. ESC-001 is CLOSED via R6-014; no specialist technical assignment approved.
- [ ] PM: review each completed packet, record the reason and approve only the specific permitted question; return insufficient requests with concrete missing evidence.
- [ ] Regular AI: integrate/validate each accepted specialist return; retain all live-proof gates.

All existing DEEP-BINARY/ARTIFACT-REVIEW labels below describe the evidence method needed; ordinary ownership remains with the regular AI until an ESC is approved. Continue independent work while any request is pending.

- [x] Review 6 accepts R6-014 clock/time search and R6-023 treasure option keys; close those specific research gaps.
- [x] Verify previously delayed delivery: GitHub contains `3138fe9`; SB-10 is historical.
- [x] **PM6-01 / regular AI:** `LWB-PM6-001` replaces unbounded host-probe cleanup waits with a bounded owned-process cleanup helper, preserves termination errors, and adds a deterministic termination-failure self-test to CI. Both the self-test and normal isolated host probe pass; this is test-orchestration hygiene only, not a live feature. See `evidence/lwbridge-implementation/2026-09-09-pm6-host-cleanup.json`.
- [ ] Next checkpoint: advance a public options/summary, owned-launch or native-ingestion blocker, or supply its complete ESC attempt/reason packet. Explain the integration value of any additional test-only work.

## Completed foundation

- [x] Recover and reproduce the original React/Vite feature UI in WebView2.
- [x] Preserve eight normal pages and the explicitly requested hidden Advanced view.
- [x] Preserve themes, icons, navigation, and nine languages.
- [x] Add real WebView2 JavaScript-to-C# RPC with profile injection/filtering, request/session IDs, origin checks, structured errors and cooperative timeout/cancel/event plumbing. R3/R4 real-host lifetime and interaction verification now pass; real worker integration remains below.
- [x] Add local profile/configuration persistence; PM2 failure handling and PM3 feedback/reconciliation pass isolated regressions. Real lifecycle integration remains open.
- [x] Detect the current installation and check required files/AMD64/PE32+. The exact xLua ABI selector is recovered; current-runtime lifecycle validation remains R5.
- [x] Keep unmanaged game processes distinct from a verified LWBridge-owned instance.
- [x] Recover Map Scan selected-type allowlist and `normal=8` / `fast=20` request concurrency.
- [x] Add a read-only official-runtime inspector and machine-readable current-install evidence.
- [x] Keep launch and production scanning fail-closed until bridge readiness is proven.
- [x] Complete tracked legacy source/test/tool/document cleanup and retain current LWBridge evidence/references. Do not restore old implementation.

## R1–R4 — Preserve the foundation; complete worker integration (P0)

### Review 3 preference follow-up — completed

- [x] **PM3-01 / R2/R4:** rejected saves are visible in login-free single-profile mode through the existing `profile-error` visual language, and native-host `ok` now requires visible non-empty error feedback.
- [x] **PM3-02 / R2/R4:** optimistic saves reconcile against the last confirmed persisted value. Both-fail, fail/success, success/fail, all-success and recovery cases pass in the deterministic provider matrix; both-fail/mixed/recovery cases also pass in the real isolated WebView host.
- [x] Add recurring CI coverage for the isolated host interaction matrix and new preference assertions. The three current-build recovery inspectors remain compile-checked only because their runtime validation depends on reference/current-install artifacts that are not part of game-independent CI.
- [x] **IMPLEMENTATION / test hygiene:** `tools/check_lwbridge_host_probe.ps1` now launches hidden, quotes its path-valued output argument, uses the review-4 55-second orchestration bound as explicit IMPLEMENTATION POLICY, fails on timeout/nonzero exit, and cleans up only the process it started. The isolated configuration and existing required gates are unchanged.

### Resolved PM2 defects — preserve their regression tests

- [x] **PM2-01 / R2:** `SetLocalConfig` applies validated partial fields to the locked fresh baseline; deterministic backend tests preserve another owner's reconnect/root/history fields and profile identity.
- [x] **PM2-02 / R2:** missing primary recovers a valid owned backup and stable profile identity; incompatible backup and unreadable-storage cases fail closed instead of creating a fresh identity.
- [x] **PM2-03 / R2:** owner/schema incompatibility is not treated as corruption; foreign-owner and future-schema primaries remain byte-for-byte unchanged with and without valid backups.
- [x] **PM2-04 / R3:** cancellation-source disposal is owned by request completion; noncooperative return/fault after close or cancel resolves as cancelled, cooperative cancellation still passes, and cancel/completion races drain ownership without teardown exceptions.

Run the [independent reproducer](evidence/lwbridge-implementation/pm-review-2-repro/Program.cs) together with the standard suite. The original review evidence records four historical failures; the current source now reports four `passed:true` outcomes and the corrected expectations are in the standard deterministic checks.

### Existing foundation and remaining integration

- [x] **R1 / profile contracts:** restore original implicit active-profile injection and event-envelope filtering in the generated API/native adapter; classify global commands; validate malformed payloads and profile scope consistently.
- [x] Test implicit/explicit/foreign profiles, malformed input, wrong-session responses and wrong-profile/late events through the frontend/native boundary.
- [x] **R2 implemented slice:** durable write-before-memory, isolated storage, backup/owner/schema handling and the specified PM2 recovery cases pass. PM3 rejected-save feedback and failed-overlap reconciliation now pass in provider and native-host tests.
- [x] Single-save rollback, persistent validated server history, visible save errors, rapid failed-save reconciliation and successful recovery work in isolated tests.
- [x] Inject temporary config roots for checks/capture and prove real user configuration is unchanged. Test denied writes, restart, corrupt JSON, interrupted replacement and competing writers.
- [x] **R3 implemented slice:** shared executor/registry and allowlisted subscription ownership pass cooperative delayed-service, duplicate-ID and window-close checks. The native-host follow-up also offloads backend work and gives each WebView document its own request/subscription generation.
- [x] Test browser timeout propagation plus host explicit cancel/close/reload/duplicate IDs/late completion with a controllable delayed backend service; prove cancellation prevents the service commit and success publication.
- [ ] Connect the request/event lifetime foundation to the first real lifecycle/scan service and add durable correlated diagnostics when that service exists; `append_log` remains a no-op today.
- [x] Move potentially blocking config/installation/SQLite work off the UI thread with deliberate ownership/cancellation. The isolated native WebView probe holds the real config lock for ~868 ms while a browser timer fires at ~56 ms, proving rendering remains responsive; folder selection remains UI-owned while root inspection/save run off-thread.
- [x] Implement/test real document reload/navigation invalidation: a real WebView2 reload cancels the prior request, clears prior subscriptions, rotates the document session/generation and rejects an invoke carrying the old session ID.
- [x] **R4 / verification:** split deterministic tests from installed-game diagnostics; run deterministic backend/transport tests in CI.
- [x] Requested live mode rejects missing native transport in the Node harness; read-only bootstrap suppresses auto-launch and uses isolated storage.
- [x] Verified R4 slice: actual native-host duplicate/session/reload/slow-storage/picker/close/startup handling, visible single failed-save rollback, ordered successful saves, both-fail/mixed-failure reconciliation and post-failure recovery. The host `ok` gate now includes these preference requirements.
- [x] Check AMD64 COFF machine plus PE32+ for game/xLua; include official-runtime inspector syntax checks. Exact xLua ABI selection is now recovered by `LWB-R5-002`; lifecycle integration remains R5.

## R5 — Overview lifecycle (P0 critical path; research alongside R1–R4)

- [x] Recover the host-side producer for `LaunchEnvelope` (`descriptorJson`, `launchProof`, `gameLaunchTicket`). `LWB-R5-001` locates the outer-host state machine, exact field xrefs/value sources, JSON serialization and handoff clone. This is static recovery only; proof/ticket semantics remain below.
- [ ] **DEEP-BINARY DB-01/02:** finish the unresolved launch input/ownership/validation semantics below; preserve already recovered representation/validation findings.
  - [x] `LWB-R5-003` recovers the child three-field `LaunchEnvelope` parser, descriptor expiry/shape gates, two-segment `LWPM1|launch|...` proof framing/signature/timing checks, and `LWLT1`/`LWLT2` ticket grammar with 1–300 second span, 32-hex field and 128-hex signature.
  - [x] `LWB-R5-004` recovers outer `LeaseActivationResponse.launchProof` propagation, the exact `primary_official` / `cached_reusable` / `independent_official` ticket-source labels, the `ticket_missing` transition, and cached launcher-report `pid`/ticket/expiry fields before fallback processing.
  - [x] `LWB-R5-005` recovers the child post-start polling branches: observed-length/sequence mismatch -> `LAUNCH_TICKET_OWNERSHIP_CHANGED`, stored-deadline crossing -> `LAUNCH_TICKET_CONSUMPTION_TIMEOUT`, and mapped internal result variants -> `LAUNCH_TICKET_CONSUMPTION_FAILED`. Opaque input identities, time units and exact result-variant meanings remain unresolved.
  - [ ] **DEEP-BINARY DB-01/02:** resolve remaining descriptor semantics, producer/consumer and external trust-service boundaries, `LWLT2` extra-field meaning, poll caller/input identities, units/result variants, unresolved outer-helper semantics and child argument/input construction. Respect SB-01/02; do not fabricate credentials, proofs or signatures to bypass an unresolved dependency.
- [x] Recover xLua secure/plain ABI fingerprint selection exactly. `LWB-R5-002` recovers the `LWXE1\n` prefix, ordinal/name record construction and ordering, SHA-256 digest, bundle values, and exact current-build `secure` match.
- [ ] Implement owned `profile_instance_start`, `profile_instance_status`, and `profile_instance_stop` lifecycle.
- [ ] Require matching instance identity, bridge handshake, and fresh heartbeat before reporting connected.
- [ ] Implement startup launch preference through the same lifecycle service without double-start races.
- [ ] Implement automatic reconnect/recovery with explicit eligibility, cancellation, and bounded retry behavior.
- [ ] Recover and implement repair/update/restart presentation and state transitions.
- [ ] **LIVE-VALIDATION:** validate repeated cold start, restart, disconnect, and stop cycles against the current client after supported lifecycle implementation.

## R6 — Offline map contracts, index and result services (P0 parallel work)

This work can advance while R5 semantic research remains incomplete or a particular analysis operation is restricted. Static/offline proof does not replace later current-client proof.

- [ ] Trace every Map Data payload/result/conditional control in the original frontend and backend evidence; record unknowns explicitly.
- [x] Recover the original SQLite map-index tables/indexes, stored record identity `(kind,server_id,record_key)`, scan staging identity `(run_id,kind,server_id,record_key)`, player-mark identity `(server_id,owner_uid)`, server-clear scope and completed-kind publish transaction.
- [ ] **DEEP-BINARY DB-03:** recover exact per-kind native `record_key` derivation plus remaining typed normalization/update/removal rules for city, resource, monster, truck, railway, dispatch, ghost and treasure records.
  - [x] `LWB-R6-029` exhausts the currently tracked official-runtime readable snapshot for direct `record_key`/scan-bridge and known row-vocabulary clues: no current-runtime text hits were present. The build-1078 `Assembly-CSharp.rdl` and active `LWScripts.data` are identified as the next concrete current-contract sources; derivation remains UNKNOWN/BLOCKED.
- [ ] **IMPLEMENTATION:** implement transactional profile/server/run storage, consistent count/page snapshots, schema versioning/migration and checkpoint/generation infrastructure. Actual job eligibility and production run publication must use recovered contracts.
- [x] Add the recovered SQLite schema/index foundation with deterministic restart/upsert/server-scope tests; require an explicit already-derived `record_key` rather than guessing ingestion identity.
- [x] `LWB-R6-019` adds test-only transactional staging/publication infrastructure for the recovered completed-kind delete/copy scope and proves selected-run/kind/server isolation. It does not enable production publication or invent the unresolved completion/native-key gate.
- [x] `LWB-R6-020` proves the test-only publication transaction rolls back completely when failure is injected after the recovered delete and before staged copy; previous published rows remain intact. Production failure classification/retry and completion eligibility stay unrecovered.
- [x] `LWB-R6-028` adds restart-safe test-only persistence over the recovered `scan_blocks(run_id,block_index)` identity, proves same-key checkpoint replacement, file-backed reopen durability and `scan_runs` clear-cascade behavior. Original scheduling/ack/retry/status transitions and production resume eligibility remain UNKNOWN/BLOCKED.
- [x] `LWB-R6-011` recovers the original `schema_version` metadata read/upsert, `MAP_SCHEMA_TOO_NEW` error vocabulary, dispatch-assist migration SQL and `legacy_import_completed` marker. Schema migration code remains gated because the supported version constant, migration threshold/order and metadata timestamp unit are still unrecovered.
- [x] Implement persistent player mark/unmark and server-scoped clear using recovered identities/SQL scope; emit the recovered player-mark refresh event.
- [x] Implement the `LWB-R6-003` default persisted `map_search` slice: recovered pagination, `updatedAt` ordering with `record_key ASC`, `{rows,total}`, city marks/`markedOnly`, and fail-closed `MAP_QUERY_UNRECOVERED` for unsupported filters/sorts.
- [ ] Extend the existing default search with recovered remaining predicates/sorts, options and counts/summary; test actual frontend payloads for all eight tabs plus stale-query rejection. Do not reimplement completed LIMIT/OFFSET/updatedAt/markedOnly support.
- [x] `LWB-R6-004` pins all eight serialized frontend tab query families, explicit-false/zero/empty/omitted distinctions, `map_data_options`/`map_summary` consumed response fields, and frontend generation guards for stale search/options/summary responses; deterministic query-contract checks pass.
  - [x] `LWB-R6-005` recovers and implements exact backend predicates for city alliance/no-alliance, resource/monster name keys, and truck/railway retained-item membership from the verified LWBridge binary; deterministic persisted-search checks pass. Unrecovered filters/sorts remain fail-closed.
  - [x] `LWB-R6-006` recovers and implements the exact dispatch/ghost `specialOnly` and `reindeerOnly` JSON predicates from unique byte-adjacent field/SQL pairs in the verified LWBridge binary. Its earlier railway frontend-kind allowance is superseded by `LWB-R6-010`; the recovered visible selector emits `reindeerOnly` only for truck.
  - [x] `LWB-R6-007` recovers and implements the keyword predicate over `name`/`alliance_name`/`uuid`/`data_json`, exact backslash-percent-underscore escape order, literal `%...%` wrapping and four parameter copies; literal wildcard and case-insensitive deterministic searches pass.
  - [x] `LWB-R6-008` applies the documented **IMPLEMENTATION POLICY** that one `map_search` `{rows,total}` response comes from one SQLite read snapshot; a deterministic second-connection WAL writer proves count/page consistency while the concurrent write still commits.
  - [x] `LWB-R6-009` recovers the Map Data option SQL family/latest scan summary and implements the exact treasure option pair plus dispatch selected-level predicates. Reward-option `arriveTs` cutoff time semantics, quality/completion/plunder/treasure visibility and remaining sort mappings stay fail-closed.
  - [x] `LWB-R6-010` audits the original quality selector, narrows `reindeerOnly` to the truck-only visible frontend form, and preserves ordinary quality as fail-closed because `UR` renders every numeric quality `>=5` while the original filter predicate/range is still unrecovered.
  - [x] `LWB-R6-012` records byte-adjacent ordinary-quality/special-UR evidence; `LWB-R6-013` refines its scope to truck+ordinary-UR, recovers the exact frontend string mapping `n/r/sr/ssr -> 1/2/3/4`, `ur -> quality >= 5`, and implements/tests those forms while retaining unknown/numeric public forms fail-closed.
  - [x] `LWB-R6-014` recovers the shared precise wall-clock producer as `kernel32!GetSystemTimePreciseAsFileTime` converted to Unix milliseconds, the default truck/railway active-arrival cutoff, frontend-emitted truck/railway/dispatch `plunderableOnly`, dispatch/ghost `completionStatus`, and the reward-option `arriveTs` cutoff source/unit. The recovered public search forms are implemented; options still require source/run-context and complete response assembly.
  - [x] `LWB-R6-015` adds an offline/test-only persisted `map_records` option-aggregation kernel for the recovered R6-009 SQL families plus R6-014 reward cutoff. Deterministic tests cover grouping/order/deduplication, server isolation and newest non-discarded scan progress. This is explicit IMPLEMENTATION POLICY; public `map_data_options` remains fail-closed until the original source/run selector and complete counts/no-alliance/response assembly are recovered.
  - [x] `LWB-R6-016` pins the frontend count/no-alliance/summary consumer contract: exact eight kind count keys, same-server summary seeding/replacement, options-to-summary count propagation, zero-count clear reset and the `noAllianceCount > 0` filter-retention rule. Backend count/no-alliance aggregation and authoritative summary server selection remain unrecovered.
  - [x] `LWB-R6-021` extends the persisted-only R6-015 test kernel with all eight recovered frontend count keys, zero-fill for absent kinds and a server-scoped no-alliance value using the recovered null/empty city predicate. This is IMPLEMENTATION POLICY only; public source/run selection and native count assembly remain unrecovered.
  - [x] `LWB-R6-022` recovers the native `treasure:` / `supplies:` option-key prefixes and documents that direct RIP xrefs plus absolute-pointer metadata-slot tracing do not reach the formatter. Its suffix gap is superseded by `LWB-R6-023`.
  - [x] `LWB-R6-023` traces the already identified native option-assembly function and signed-decimal formatter to recover exact keys `treasure:<decimal treasureType>` and `supplies:<decimal suppliesType>`. The persisted-only R6-015 helper now emits those keys and deterministic tests cover multi-digit formatting. Public `map_data_options` remains gated on source/run selection and complete native response assembly.
  - [x] `LWB-R6-024` recovers the exact native option-source/scope candidates from the verified `map_index.rs` string cluster: `map_records` + `server_id=?1`, or `scan_records` + `server_id=?1 AND run_id=?2`. Frontend options provide only `serverId`; the native kind-count query is also source/scope-parameterized. This completes a concrete ESC-003 method packet but does not recover the selector condition, chosen run identity, exact `noAllianceCount` producer or full response assembly; public `map_data_options` remains fail-closed.
  - [x] `LWB-R6-025` recovers the native `map_summary` success envelope and data flow: the shared scan-state producer runs first, `scanState.serverId` becomes both the count-helper server argument and response `serverId`, and the original response assembles exactly `serverId`/`counts`/`scanState`. The count helper selects published versus staging rows from an internal optional run scope whose producer remains UNKNOWN/BLOCKED. The rebuild now fails `map_summary` closed with `MAP_INDEX_UNAVAILABLE` instead of returning a synthetic production success object.
  - [x] `LWB-R6-026` pins the public per-kind sort columns, the frontend's ordered multi-sort toggle/default behavior, eleven scalar native sort-key parser comparisons, four direct native expression references, and the adjacent sort-expression/null-order string inventory. The item-count SQL text begins at raw `0x00C8A926` after its one-byte `0x56` descriptor at `0x00C8A925`. No production sort is newly enabled: the frontend preserves prior sorts when a new sort is selected, while the complete native ordered multi-sort/null-order assembly plus remainingLootCount/shield/distance/railway-quality data flow are still UNKNOWN/BLOCKED.
  - [x] `LWB-R6-027` recovers the native public sort-direction branch: frontend `asc` selects the 3-byte `ASC` literal, ordinary `desc` selects the 4-byte `DESC` literal, and the exact `distance` branch overrides a selected `desc` back to physical `ASC`. The reason/value transformation behind that distance special case and complete multi-sort/null-order assembly remain UNKNOWN/BLOCKED, so production behavior stays fail-closed.
  - [ ] **ARTIFACT-REVIEW DB-05 + IMPLEMENTATION:** recover the remaining ordered multi-sort/null-order/tie-break assembly, the unresolved alternate-sort mappings/gates, the public option source/run selector plus backend counts/no-alliance/full response assembly, and the remaining summary optional run-scope/unavailable-state integration. Preserve R6-015/016/025/026's persisted-only/frontend/native results; escalate only identified opaque native calculations and do not re-recover the implemented/proven subset.
- [ ] **IMPLEMENTATION + ARTIFACT-REVIEW:** build export infrastructure and recover exact scope/columns/format semantics; implement full filtered Excel export and verify it reopens with correct rows/types/large IDs.
  - [x] `LWB-R6-017` pins the city-only frontend export envelope: current city query builder at page 1/pageSize 200, exact 12 header labels/translation keys, localized sheet/yes/no labels, button eligibility/busy gates and consumed `{canceled,rowCount,path}` result fields. A deterministic backend regression confirms the recovered envelope still stops at `MAP_INDEX_UNAVAILABLE`; pageSize 200 is not treated as a proven export row limit.
  - [x] `LWB-R6-018` recovers a bounded native-format slice: sheet/default filename/dialog/error vocabulary, embedded A-L OOXML package, frozen header, column widths, autofilter/margins, styles and partial D-L field adjacency. It deliberately does not infer exact A-C mapping, protect/shield fallback or per-column types.
  - [ ] Finish the native writer/file-picker contract: exact A-C and J fallback mapping, per-column value typing/coercion, internal pagination/full-filter scope, filename timestamp/default directory, complete cancellation/error control flow and lossless large-ID reopen behavior before enabling export.
- [ ] Connect offline services to real native handlers and result tabs; test using explicitly labelled recovered/synthetic samples. Keep unknown semantic fields open.

## R7 — Production manual scan (P0; requires R5/R6)

- [ ] Connect production `map_scan_start` to the recovered bridge/native capture path.
- [ ] **DEEP-BINARY DB-04:** recover exact block scheduler/tick behavior, block ordering, retry rules, capture acknowledgements/drop handling and resume/completion state.
- [ ] Persist scan run/checkpoint state atomically and reject stale run/session/server results.
- [ ] Preserve dropped/pending/acknowledgement/failure semantics and never silently complete with unresolved work.
- [ ] Implement actual Start/Stop/resume/Normal/Fast/selected-type behavior and prove stop cancels/drains owned work. Returning an unavailable status is not cancellation proof.
- [ ] Prove a bounded scan before full coverage; validate UI/query/export against committed data and record updates/removals.
- [ ] Validate representative data for all eight record kinds and repeated full-scan completion.

## R8 — Automatic scanning and server travel (P0)

- [ ] **ARTIFACT-REVIEW DB-06 → IMPLEMENTATION → LIVE-VALIDATION:** recover/implement confirmed cross-server travel, valid options/current/home context and active-scan conflicts; preserve existing persisted history.
- [ ] Implement Auto Scan as a single-owner durable scheduler: configuration, Run Now, intervals/eligibility, target sequence, failures, cancel and return-to-origin.
- [ ] Prove navigation, refresh, reconnect and restart cannot create duplicate cycles or change server context before confirmed travel.

## R9 — Conditional actions and scheduled jobs (P0)

- [ ] **ARTIFACT-REVIEW DB-06 → IMPLEMENTATION → LIVE-VALIDATION:** implement coordinate jump and march follow with authoritative visible outcomes.
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

Review 6: Release build/backend suite, frontend integrity, five preference scenarios, both transport harnesses and the normal isolated hidden WebView host all pass. No binary verifier or denied operation rerun. Post-review `LWB-PM6-001` closes PM6-01 with bounded owned-process cleanup plus a deterministic termination-failure regression; live game features remain unproven. See [review 6 evidence](evidence/lwbridge-implementation/2026-09-09-pm-review-6.json) and [PM6 cleanup evidence](evidence/lwbridge-implementation/2026-09-09-pm6-host-cleanup.json). All 47 full acceptance cases remain unsigned despite passing local/offline subchecks.

```powershell
python tools/build_lwbridge_frontend.py --check
dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release
dotnet run --project tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release
./tools/capture_lwbridge_ui.ps1
python tools/inspect_official_runtime.py --output evidence/official-runtime/2026-09-08-official-runtime.json
```

See [docs/README.md](docs/README.md) for the durable evidence/documentation map and [task.md](task.md) for the full Overview + Map Data acceptance contract.
