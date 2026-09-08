> HISTORICAL: superseded by [review 5](../lwbridge-project-status.md) and AGENTS.md section 6. DB labels are research subjects, not automatic Daybreak assignments.

# Project-manager checkpoint — review 4

Reviewed: 2026-09-08, implementation `5174cf9298fd9895cfd6e70d2af3216077d3928d`, branch `research/offline-controller`. The worktree was clean before this audit. This checkpoint changes planning/evidence documentation, not production behavior. The [review 3 narrative and follow-ups](2026-09-08-review-3-and-followups.md) are preserved as historical evidence.

## Decision

**Accept the preference fixes and the new supported Map Data filter slice. Keep launch, production scan and full-page acceptance open.** Fresh provider and actual isolated WebView tests close PM3-01/02. Do not ask the next AI to fix those same defects again.

Give the research AI [the deeper binary handoff](../deep-binary-handoff.md), together with `AGENTS.md`, `task.md` and the referenced evidence. It identifies four clear deeper-analysis packages and two artifact-first packages. The ordinary implementation AI can continue storage/query/host work in parallel. `task.md` is still the sole full requirements file; `BACKLOG.md` is the checklist, and the new document is a research supplement. Do not recreate `TASKS.md`.

The reported safety-review denials are recorded as **SB-01/02**, with source and evidence limits. They came from the reviewing environment, not permission withheld by the user. No denied operation was retried here; switching models does not authorize replaying it through another executor.

Post-review continuation `LWB-R6-006/007` recovered and offline-tested the parameter-free `specialOnly`/`reindeerOnly` predicates and exact literal-substring keyword construction. `LWB-R6-008` then closed the review's count/page consistency gap with an explicitly labelled rebuild read-snapshot policy and a deterministic concurrent-WAL-writer regression. `LWB-R6-009` recovers the option SQL family/latest per-server scan summary and implements treasure option-pair plus dispatch selected-level filtering. `LWB-R6-010` audits the visible quality selector and corrects the earlier railway `reindeerOnly` overreach: only truck can emit that selector value. `LWB-R6-011` recovers schema-version/future-schema/legacy-import metadata strings but keeps migration code gated on the unrecovered supported version and timestamp semantics. `LWB-R6-012` recorded byte-adjacent quality/special-UR evidence; `LWB-R6-013` follows the control flow, narrows that guard to truck+UR and recovers exact `n/r/sr/ssr/ur` SQL mapping. Public options and other time/eligibility semantics remain blocked, so these checkpoints do not change the review's launch, ingestion, export or live-proof boundaries.

## New work reviewed

| Commit / finding | Accepted result | Limit / next requirement |
|---|---|---|
| `98065f2` — PM3-01/02 | Visible single-profile save errors; confirmed persisted-value reconciliation; both-fail, mixed-failure and recovery cases; stronger actual native-host gates | Fresh isolated Node/WebView checks pass. These are preference/host tests, not launch/reconnect proof. Windows CI now includes the provider and host matrices; this audit did not run GitHub Actions remotely. |
| `21333c3` — `LWB-R5-003` | Documented child envelope parser, descriptor gates, proof validation branches and ticket grammar | Review accepted the recorded static scope. It did not rerun binary extraction or establish a working producer/lifecycle. |
| `beb4677` — `LWB-R5-004` | Documented outer launch-material propagation, ticket-source taxonomy and cached report fields | The record says its new fixed-address verifier was syntax-checked but execution was denied. Do not describe the verifier as successfully run. SB-01 applies to that operation. |
| `b3e6c33` — `LWB-R5-005` | Retained bounded disassembly documents ownership-change, timeout and failure branches in the consumption poll | Broader analysis was denied (SB-02). Caller/input identities, units and variant meanings remain unknown. Saved-text validation is not fresh binary verification. |
| `198812c` — `LWB-R6-004` | Eight frontend query families, options/summary consumers and frontend stale-generation guards are documented/tested | Consumer field names do not establish aggregation, server selection, export or unsupported backend predicates. |
| `5174cf9` — `LWB-R6-005` | Persisted search implements recovered city alliance/no-alliance, resource/monster name-key and truck/railway current-goods membership predicates | Standard deterministic tests pass. Count/page use matching predicates. Remaining filters/sorts, options, summary, export and authoritative ingestion remain unfinished. |

No new production defect was reproduced by the checks in this review. That is a bounded result, not proof that all untested or unimplemented workflows are correct.

## Current operational boundary

- **Overview:** local profile/configuration, installation checks and substantial native-host interaction work exist. Start still rejects `OVERVIEW_LAUNCH_BOOTSTRAP_UNRECOVERED`; stop has no owned instance. Reconcile remains a status read. Startup/reconnect preferences are stored but do not constitute working lifecycle services. Process existence is not bridge readiness.
- **Map Data:** recovered schema, explicit-key persistence, marks, scoped clear and a supported query subset now includes treasure option-pair, dispatch selected-level filters, corrected truck-only `reindeerOnly` and `LWB-R6-013` ordinary `n/r/sr/ssr/ur` filtering with the extra truck+UR special-quality exclusion. R6-013 is implemented/offline-tested; numeric/unknown public quality forms remain fail-closed. Option aggregation SQL and newest non-discarded per-server scan selection are statically recovered, but public options remain unavailable because the reward `arriveTs` cutoff clock/source/unit is unresolved; summary server selection also remains open. No real capture/scheduler/ingestion service runs. Unknown native keys/types must not be replaced by frontend fallback identities or synthetic records.
- **Other shared/action features:** meaningful pending count, live runtime localization/diagnostics, confirmed travel, follow/jump, treasure/plunder/train actions and durable automatic jobs remain partial or unimplemented. No complete case in the 47-case acceptance matrix was signed off here.

## Research routing

Detailed questions, known evidence and research deliverables are in [the supplement](../deep-binary-handoff.md).

| Package | Classification | Why / implementation dependency |
|---|---|---|
| DB-01 — launch input/ownership semantics | **DEEP-BINARY** | Remaining producer/consumer relationships, descriptor/helper meanings, child input/quoting and external service boundaries constrain R5/O02–O06. Preserve SB-01 on the recorded verifier. |
| DB-02 — consumption poll | **DEEP-BINARY** | Opaque caller/input/producer identities, units and result variants cannot be established from branch names or constant magnitudes. Preserve SB-02 and the saved-evidence limit. |
| DB-03 — native map identities/normalization | **DEEP-BINARY**, readable artifacts first where available | Native key generation, per-kind types and update/removal transformations constrain trustworthy R6 ingestion. SQL primary keys and UI row fields alone are insufficient. |
| DB-04 — scan scheduling/capture/completion | **DEEP-BINARY** | Exact coverage/order/timing, queues/acknowledgements/drop, cancel/resume/drain and publication semantics constrain R7, later R8. |
| DB-05 — advanced predicates/derived sorts | **ARTIFACT-REVIEW**, deep analysis conditional | Existing SQL/resources/readable code may answer remaining filter/clock/eligibility questions. Escalate only the opaque native calculations. Do not repeat the recovered/implemented `R6-005/006/007/009/010/012/013` slices. |
| DB-06 — status/travel/actions/jobs | **ARTIFACT-REVIEW**, deep analysis conditional | Start with frontend and current official-client request/handler/event evidence; identify native gaps per operation. Authoritative live outcomes are a separate validation phase. |

Restrictions apply to operations, not entire topic names. A missing integration is a setup issue; an unknown contract is a research issue; an unavailable world state is a validation issue. Record the exact reason instead of calling all three “safety-blocked.” The handoff does not assign replaying denied disassembly, proof fabrication or live game actions.

## Ordinary implementation work that can proceed

1. **R6 query integrity:** preserve known predicates and add meaningful combination/profile/server/count/page coverage when changing the implementation. `LWB-R6-008` wraps count and page in one deferred SQLite read transaction and proves snapshot consistency with a second WAL connection that commits between the two reads. `LWB-R6-009` adds deterministic ordinary-treasure/supplies separation, malformed treasure-shape rejection and dispatch exact-level coverage. `LWB-R6-010` adds a railway rejection regression for the corrected truck-only `reindeerOnly` frontend form. Continue combination/profile/server coverage as query semantics expand; do not reinterpret the snapshot policy as recovered original transaction behavior.
2. **R6 persistence:** `LWB-R6-011` now pins the metadata read/upsert, `MAP_SCHEMA_TOO_NEW`, dispatch-assist migration SQL and legacy-import completion marker. Recover the supported version constant, migration threshold/order and metadata timestamp clock/unit before implementing versioned migration. Transactional failure recovery and run/server/profile generation boundaries can continue from the known schema/publication contract; keep real ingestion gated on DB-03/04.
3. **R6 options/summary/export:** `LWB-R6-009` recovers alliance/name/dispatch-level/treasure/reward option SQL and newest non-discarded per-server scan selection. Recover the reward `arriveTs` cutoff clock/source/unit and authoritative summary profile/server selection before wiring production responses. File dialog/cancel/error and lossless workbook infrastructure can proceed; full filtered export remains a feature gate.
4. **R3/R4 worker integration:** prepare typed ownership/cancellation/event/journal boundaries and durable diagnostics; connect real lifecycle/scan workers only after their contracts are supported. Preserve native document lifetime and the repaired preferences.
5. **Test/tool hygiene:** retain provider/WebView tests in CI. The existing host-probe helper uses an unbounded visible `Start-Process -Wait`; improve it to hidden execution with a bounded wait and cleanup of its own child process. This audit invoked the underlying probe hidden with an isolated output and a 55-second bound. That bound is test orchestration policy, not a recovered game timeout. Do not add denied binary verifiers to CI as a workaround.
6. **Delivery:** document new findings immediately, update the ledger/backlog, run appropriate checks and commit/push each coherent checkpoint. The research AI returns exact newly unblocked contracts; the coding AI does not wait on unrelated gaps.

## Fresh verification in this review

Source hashes, commands, machine-readable outcomes and limits are in [review 4 evidence](../../evidence/lwbridge-implementation/2026-09-08-pm-review-4.json).

- Recovered frontend generation/integrity check passed. Release build passed with zero warnings/errors.
- Standard backend suite passed, including map persistence/query contracts and optional installed-client diagnostic. Real configuration remained unchanged. No game/launcher was running in that diagnostic snapshot.
- The preference provider's five outcome scenarios and original PM3 failed-overlap reproducer passed.
- Actual isolated WebView host passed `ok`, visible failed-save feedback, both-fail/mixed reconciliation, successful recovery and late-close suppression. Real configuration hashes matched before/after.
- Nine desktop fixture captures and 35 browser checks passed. Of 32 source-reference screenshot pairs, 30 were identical; maximum MAE was approximately `0.00006310`, and no pixels crossed the comparison's high-delta threshold. This protects synthetic recovered-frontend regression behavior; it is not a new authenticated reference-app comparison or live feature proof.
- All LWBridge Python inspectors passed syntax parsing only. This audit did not execute them against the reference, repeat denied operations or generate new disassembly.

The independent PM2 reproducer and older transport harness results remain historical audit evidence; the standard suite covering the PM2 fixes was rerun now. No launch, injection, scan, travel, claim, plunder or alliance message was performed by this audit.

## Documentation cleanup

- Replaced stale “fix PM3 first” instructions with the verified current state and DB routing.
- Corrected BACKLOG text encoding and stale foundation/ABI statements.
- Preserved the prior audit and its relative evidence links in `docs/reviews/`.
- Kept one full task specification and all 47 acceptance cases; added the research supplement to the evidence index and ledger.
- Preserved recovered assets, production code and historical findings. No legacy cleanup was repeated.
