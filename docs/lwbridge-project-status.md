# Project-manager checkpoint — review 5

Reviewed 2026-09-09 against `d4e97903122372378ce324edf8d1444d6ccdc0e6` on `research/offline-controller`. Worktree clean at start. This review combines both completed task streams, updates ownership/escalation instructions, and fixes stale evidence-tool metadata. Production code is unchanged by the PM checkpoint. [Review 4 and follow-ups](reviews/2026-09-09-review-4-and-followups.md) are historical.

## Decision

**Both AIs completed useful checkpoints; neither completed the full Overview/Map Data project or all DB research.** Their eight commits are already integrated in the same branch. Fresh build/backend/provider/transport checks pass. Preserve the repaired preferences, snapshot transaction, exact filters and corrected kind gates.

The user now requires regular-AI ownership first, including permitted binary analysis. Daybreak receives only a bounded escalation after the regular AI documents relevant exhausted methods and the PM reviews the reason. This is mandatory in AGENTS.md section 6. Deliver the [regular task](implementation-handoff.md) and [Daybreak task](deep-binary-handoff.md) separately; both inherit the one `task.md` specification. [ESC-001/002](daybreak-escalations.md) need information; no new specialist technical assignment is approved yet.

## Combined accepted progress

| Owner / commit | Finding and verified slice | Boundary / correction |
|---|---|---|
| Daybreak task Start deep binary handoff / `6e1d80f` | R6-006 special/reindeer SQL predicates and initial implementation | Railway kind interpretation was overbroad; use regular-AI R6-010 correction. Predicate recovery survives. |
| Same / `d5be1d8` | R6-007 literal keyword escaping and search over name/alliance/UUID/JSON | Fresh offline tests pass; no live ingestion/query proof. This completes two slices of DB-05, not all DB-01–06. |
| Regular task Read Map Scan Recovery Docs / `e69c8e4` | R6-008 deferred count/page snapshot | Meaningful two-connection WAL test passes. Closes the review-4 structural limitation as IMPLEMENTATION POLICY. |
| Same / `dc797df` | R6-009 option SQL evidence, paired treasure/supplies and dispatch-level filters | Isolated filter tests pass. Real treasure envelopes still contain unresolved viewer/visibility/lucky fields; complete options/summary/export remain unavailable. |
| Same / `cffe8ed` | R6-010 truck-only reindeer correction | Railway requests now reject; fresh regression passes. Historical R6-006 JSON retains its original superseded claim. |
| Same / `8ed4b4b` | R6-011 schema metadata/future-schema/import markers | Static partial recovery; supported version, migration order/threshold and timestamp producer remain open. No migration implemented. |
| Same / `cfe43ff` | R6-012 adjacency evidence beside ordinary quality | Superseded interpretation: exclusion is not universal ordinary quality behavior. Preserve its historical scope. |
| Same / `d4e9790` | R6-013 equality selectors 1/2/3/4, UR >=5, special-UR exclusion only for truck UR | Fresh offline suite passes; persisted result coverage is strongest for truck. Other kinds have envelope acceptance tests but need persisted exclusion-boundary regressions. |

## PM5-01 — stale boolean inspector metadata, corrected

`tools/inspect_lwbridge_map_query_boolean_filters.py` still emitted `reindeerOnly: [truck, railway]` after production correctly changed to truck-only. Its binary marker checks cannot establish frontend kind gates. This could mislead the next AI into restoring the old bug.

Review 5 changes emitted metadata to truck-only, links the saved R6-010 finding and explicitly says the tool does not independently verify frontend gates. The original R6-006 JSON remains historical; the current subject summary identifies the supersession. Inspector source syntax/static metadata checks pass; the verifier was not executed against the binary in this audit. This is evidence-tool maintenance, not new recovery or a production fix.

## Current functional boundary and next regular-AI work

- **Overview remains incomplete:** start still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; no owned stop, real startup/reconnect worker or authoritative fresh heartbeat exists. Stored preferences and process presence do not prove these features.
- **Map Data remains partial:** new filters and snapshot persistence work offline. Complete options/summary/export, native record keys/normalization, production capture/scheduler, generations/publication and automatic jobs still need implementation/evidence. Treasures, completion/plunderability and many real frontend query combinations remain gated on missing contracts.
- **Next regular task:** hidden/bounded host-probe helper; useful cross-kind quality and combined-query coverage; permitted options/summary/cutoff/migration/export research; independent storage/generation/diagnostic engineering. See the detailed priority/exit criteria in the regular handoff.
- **No automatic DB transfer:** launch, native map keys, scheduling and action research stay regular-AI-owned until an ESC packet establishes a specialist need. Missing integration is setup work; tool installation remains pre-authorized within the environment rules.

## New reported research limits and escalation quality

The regular task reports additional denied operations: an options verifier (SB-03), an existing user-profile database query (SB-04), and completion-context/range reads (SB-05). Sources and exact evidence limits are in the escalation register. No denied operation was rerun by this review.

The completion cutoff and schema prerequisites are candidates, but their packets do not yet establish that all relevant permitted alternatives were exhausted. Some source searches used invalid wildcard paths; a command failure or truncated result does not establish source absence. Preserve the failed attempt, repair the query where permitted, and explain remaining alternatives instead of transferring a whole feature as "blocked."

The task also reported a profile-only `map_summary` frontend input and further export/sort leads. Those do not establish native server selection, writer behavior or sort/null ordering. Continue from readable consumers and saved excerpts; document a complete locator/source/result before treating a chat lead as a confirmed new contract. No clock or server fallback is approved.

## Fresh verification and evidence limits

See [review 5 evidence](../evidence/lwbridge-implementation/2026-09-09-pm-review-5.json) for source hashes, task attribution and check results.

- Release build: passed, zero warnings/errors.
- Standard backend suite with real-config preservation flag: `ok=true`, profile/persistence/request/map suites true, no failures. Optional installed-client diagnostic valid; game/launcher absent in that snapshot.
- Recovered frontend integrity/generation check, five-scenario preference provider matrix, missing-native transport and transport-boundary checks: passed.
- All LWBridge inspector sources were syntax-parsed; PM5-01 emitted metadata was checked from AST/source only. No new binary extraction or verifier execution.
- No native-host/screenshot matrix rerun: those files/behaviors were unchanged by the audited source commits; review 4 retains the prior host/nine-fixture/35-browser/32-pixel results as historical proof. No live game command was performed.
- All 47 acceptance rows are preserved unchanged. No complete live feature or full acceptance case is newly signed off.

## Management cleanup and delivery

Archived the prior mixed narrative and broad research handoff, consolidated R6-006–013 with explicit supersessions, added two scoped task handoffs and a durable ESC template, and corrected the evidence tool. Keep every successful recovery in subject docs/evidence and commit/push each coherent checkpoint with remote verification. Do not merge unrelated unfinished changes or run competing builds into the same outputs.
