# LWBridge documentation index

This directory contains current LWBridge reconstruction and official-client evidence. Start here instead of browsing files by date or guessing which notes are authoritative.

**Mandatory before working:** [../AGENTS.md](../AGENTS.md) defines the user's repository-wide reverse-engineer-first, no-invented-values, immediate-finding-documentation and checkpoint commit/push rules. Every AI and contributor must follow them. New findings require source identity/hash, an exact locator, reproduction steps, evidence status and limitations; update this index and the feature ledger when adding durable material.

## Reading order

Start with [`lwbridge-project-status.md`](lwbridge-project-status.md) for review 4: accepted preference fixes, new R5/R6 findings, fresh checks and the research/implementation split. [`deep-binary-handoff.md`](deep-binary-handoff.md) is the separate research supplement for the user's Daybreak task, including DB-01–06 and the operation-specific SB-01/02 restrictions. It does not replace `task.md` or authorize replaying denied actions. The [review 3/follow-ups](reviews/2026-09-08-review-3-and-followups.md) and [review 2/follow-ups](reviews/2026-09-08-review-2-and-followups.md) are historical only.

1. [`lwbridge-feature-ledger.md`](lwbridge-feature-ledger.md) — current implementation/proof matrix for Overview and Map Data.
2. [`official-runtime-architecture.md`](official-runtime-architecture.md) — current installed Last War runtime baseline and official launcher observations.
3. [`lwbridge-architecture.md`](lwbridge-architecture.md) — recovered LWBridge application/runtime architecture.
4. [`lwbridge-ui.md`](lwbridge-ui.md) — recovered frontend reproduction and visual verification.
5. [`lwbridge-injection.md`](lwbridge-injection.md) — recovered bootstrap/injection lifecycle, fail-closed states, descriptor checkpoint, and remaining gaps.
6. [`lwbridge-map-scan.md`](lwbridge-map-scan.md) — recovered Map Scan request/native-capture contract and remaining gaps.
7. [`lwbridge-artifact-evidence.json`](lwbridge-artifact-evidence.json) — machine-readable artifact evidence supporting recovered LWBridge structure.

## Planning

- [`../task.md`](../task.md) is the primary AI instruction file and full Overview + Map Data acceptance specification.
- [`../BACKLOG.md`](../BACKLOG.md) is the current progress checklist, renamed from `TASKS.md`. Do not recreate the old name or duplicate the requirements.

- [`deep-binary-handoff.md`](deep-binary-handoff.md) assigns unresolved research questions and identifies ordinary implementation that can continue independently.

## Visual and machine-readable evidence

- [`ui-reproduction/`](ui-reproduction/) contains the current deterministic UI regression evidence.
- [`task-reference/`](task-reference/) contains the user-supplied Overview and Map Data visual references.
- [`../evidence/lwbridge-0.3.1/`](../evidence/lwbridge-0.3.1/) contains immutable recovered frontend evidence.
- [`../evidence/lwbridge-implementation/`](../evidence/lwbridge-implementation/) contains rebuild milestone evidence.
- [Review 4 evidence](../evidence/lwbridge-implementation/2026-09-08-pm-review-4.json) records the audited implementation, fresh check results, retained safety-review restrictions and precise proof limits.
- [`../evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json`](../evidence/lwbridge-implementation/2026-09-08-pm2-foundation-fix.json) records the PM2 repair source hashes, exact locators, commands, results and remaining limits.
- [`../evidence/lwbridge-implementation/2026-09-08-r3-native-host.json`](../evidence/lwbridge-implementation/2026-09-08-r3-native-host.json) records the real isolated WebView2 reload/session and slow-storage responsiveness checkpoint.
- [`../evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json`](../evidence/lwbridge-implementation/2026-09-08-r4-native-host-interactions.json) records the recovered picker result contract and the completed controlled native-WebView duplicate/preference/picker/closed-window interaction matrix.
- [`../evidence/lwbridge-implementation/2026-09-08-pm3-preference-fix.json`](../evidence/lwbridge-implementation/2026-09-08-pm3-preference-fix.json) records PM3-01/02: visible rejected-save feedback, confirmed-value rollback, deterministic rapid-save outcome matrix, strengthened real WebView host gates and recovery behavior.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-launch-envelope-producer.json`](../evidence/lwbridge-implementation/2026-09-08-r5-launch-envelope-producer.json) records `LWB-R5-001`: exact outer-host `LaunchEnvelope` field/value locators, JSON serialization/handoff and the remaining proof/ticket/child-input blockers.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-xlua-abi-selector.json`](../evidence/lwbridge-implementation/2026-09-08-r5-xlua-abi-selector.json) records `LWB-R5-002`: exact `LWXE1\n` export-fingerprint construction, ordinal/name ordering, SHA-256 selector, bundled secure/plain values and current-client secure classification.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-launch-validation.json`](../evidence/lwbridge-implementation/2026-09-08-r5-launch-validation.json) records `LWB-R5-003`: child `LaunchEnvelope` parsing, recovered descriptor safety gates, two-segment launch-proof validation and `LWLT1`/`LWLT2` ticket grammar/timing, with producer/ownership/consumption gaps kept open.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-launch-material.json`](../evidence/lwbridge-implementation/2026-09-08-r5-launch-material.json) records `LWB-R5-004`: outer `LeaseActivationResponse.launchProof` propagation, exact ticket-source labels, missing-ticket transition and cached launcher-report reuse/fallback fields, with signing/ownership/child-input gaps kept open.
- [`../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption.json`](../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption.json) records `LWB-R5-005`: child post-start ticket polling, exact replacement/timeout/failure branches and opaque comparison inputs, with caller identities, units and result variants kept open. The linked focused disassembly excerpt is durable evidence for the checkpoint.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-query-frontend.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-query-frontend.json) records `LWB-R6-003`: exact frontend Map Data query-builder/sort/page/export-envelope locators plus the implemented default persisted-search slice and its fail-closed filter/sort limits.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-frontend-consumers.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-frontend-consumers.json) records `LWB-R6-004`: exact Map Data option/summary response consumers, per-tab conditional query emission and search/options/summary stale-generation guards; the eight real tab envelope families are pinned by offline checks while unrecovered aggregation/filter SQL remains fail-closed.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-query-backend.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-query-backend.json) records `LWB-R6-005`: verified-binary file offsets and exact backend predicates for city alliance/no-alliance, resource/monster name keys, and truck/railway `currentGoods` item membership. Time-dependent, quality, treasure, plunderability and alternate-sort semantics remain blocked.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-query-boolean-filters.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-query-boolean-filters.json) records `LWB-R6-006`: unique, byte-adjacent verified-binary field/predicate pairs for dispatch/ghost `specialOnly` and `reindeerOnly`. Its original truck/railway frontend-kind correlation is historical; `LWB-R6-010` supersedes that allowance and proves the visible reindeer selector is truck-only.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-keyword.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-keyword.json) records `LWB-R6-007`: exact keyword SQL, backslash/percent/underscore escaping order, literal-substring wrapping and four-parameter construction from bounded verified-binary data flow; the predicate is implemented and offline-tested.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-search-snapshot.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-search-snapshot.json) records `LWB-R6-008`: the explicit rebuild policy that count and page reads share one SQLite snapshot, plus the deterministic second-connection WAL-writer regression proving a stable `{rows,total}` generation without blocking the committed write.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.json) and its [focused static excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.txt) record `LWB-R6-009`: recovered option SQL/latest scan-summary ranges plus implemented treasure option-pair and dispatch selected-level predicates. The reward `arriveTs` cutoff and other time-dependent mappings remain blocked.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-selector-kind-gates.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-selector-kind-gates.json) and its [focused frontend excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-selector-kind-gates.txt) record `LWB-R6-010`: exact quality-capable kinds, ordinary selector values, numeric display mapping and the corrected truck-only `reindeerOnly` frontend gate. Ordinary quality SQL/range semantics remain blocked.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.json) and its [focused static excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.txt) record `LWB-R6-011`: exact schema-version metadata read/upsert strings, the `MAP_SCHEMA_TOO_NEW` branch vocabulary, dispatch-assist migration SQL and legacy-import completion marker. The supported schema number, migration threshold and timestamp unit remain blocked, so no migration behavior was guessed.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-special-ur-exclusion.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-special-ur-exclusion.json) and its [focused static excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-special-ur-exclusion.txt) record `LWB-R6-012`: the byte-adjacent `quality`/special-UR exclusion evidence. `LWB-R6-013` below refines its earlier broad interpretation to truck+ordinary-UR only.
- [`../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-filter.json`](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-filter.json), its [focused static excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-filter.txt), and [offline implementation validation](../evidence/lwbridge-implementation/2026-09-08-r6-map-quality-implementation.json) record `LWB-R6-013`: exact `n/r/sr/ssr -> quality = 1/2/3/4`, `ur -> quality >= 5`, plus the additional `isSpecialURQuality = 0` predicate only for truck+UR, now implemented and deterministic-test covered.
- [`../evidence/official-runtime/`](../evidence/official-runtime/) contains read-only current-client runtime snapshots.

## Evidence labels

- **RECOVERED** — statically recovered from the verified LWBridge artifact or embedded assets.
- **IMPLEMENTED/OFFLINE-TESTED** — implemented and verified without claiming a live game result.
- **LIVE-PROVEN** — observed against the current installed client with authoritative evidence.
- **UNKNOWN/BLOCKED** — still unresolved.

Temporary analysis under `.codex-live/lwbridge-*` is convenience material only. Durable conclusions belong in this directory or under `evidence/`.
