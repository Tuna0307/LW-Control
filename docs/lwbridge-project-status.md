# Project-manager checkpoint — review 7

Reviewed 2026-09-09 against `02735690ed8c61c249a997e274a536d4e5f69c53` on `research/offline-controller`, with a clean starting worktree. This audit covers all 15 commits after PM checkpoint `ff36e6f`. The [review 6 and follow-up narrative](reviews/2026-09-09-review-6-and-followups.md) is historical; use this page for current decisions.

## Decision and functional status

Accept PM6-01's cleanup repair, R6-024–037's bounded findings, the corrected offline alliance aggregation and checkpoint persistence. The new research substantially narrows uncertainty, but **does not enable production launch, scanning, public options, summary or export**. The summary handler now correctly rejects instead of returning fabricated zero-valued success. No new behavioral regression was found in the reviewed changes and checks; this is not an exhaustive audit of every old feature.

The planning estimate remains **25–30% combined engineering completion**, with Overview 20–25% and Map Data 25–35%. **0/47 full acceptance cases are formally signed off.** This is not a claim that every control is broken: local preferences, host interactions and supported offline queries pass. See [the estimate and rubric](lwbridge-completion-estimate.md).

## Accepted changes and exact limits

| Commit/finding | Accepted result | What remains |
|---|---|---|
| `d3d80ec` / PM6-001 | Bounded owned-process cleanup; primary and cleanup failures preserved; CI termination-failure regression | Test orchestration only. Fresh normal and failure-path checks pass; PM6-01 closed. |
| `159db1a`, `adde6eb` / R6-024/025 | Candidate map sources and summary envelope/server/count flow; backend removes synthetic summary success | Public summary still returns `MAP_INDEX_UNAVAILABLE`; no authoritative state provider. |
| `29701b8`, `0e7a582` / R6-026/027 | Ordered frontend multi-sort inventory and native direction branch, including distance special case | Full expressions, kind gates, null order and tie-break composition remain unresolved. Alternate sorts stay gated. |
| `2ba7d39` / R6-028 | Test-only block checkpoint upsert/reopen/clear cascade | No scheduler, acknowledgement, retry transitions or production resume/completion. |
| `b65c78d` / R6-029 | Negative vocabulary search in tracked readable current-runtime evidence | Not an exhaustive search of installed artifacts; R6-034 subsequently supplies an RDL decoder. Native keys remain open. |
| `d6fc196`, `c89bcd6` / R6-030/031 | Source/run selector, alliance/no-alliance assembly, top-level order and scan-progress row selection | Selector and persisted aggregates are test-only; no staged aggregate integration; exact progress serialization still missing. |
| `f3fe362`, `a80f8f2` / R6-032/033 | Persisted serializer boundary identified; summary state/count errors propagate | Callback fields/null/error semantics and a production shared-state provider remain missing. The callback operation denial is retained. |
| `e272c6a`, `4721f33` / R6-034/035 | Current build-1078 managed/xLua state methods and `GameEntry.Data -> CustomDataManager.Player -> DCPlayer.GetCurServerId()` chain | Static current-client facts do not establish LWBridge's encrypted handler implementation or live readiness. |
| `3dda56d`, `0273569` / R6-036/037 | LWBridge shared-producer request order/normalization and a specific invalid-server error branch | Bridge-script linkage, complete source/readiness mapping, propagated transport errors and exact map-state-unavailable trigger remain unknown. |

Detailed locators, source hashes and original reproduction commands remain in [Map Scan findings](lwbridge-map-scan.md) and the [evidence index](README.md). This PM audit reviewed committed source, recorded evidence and call sites; it did **not** independently rerun native disassembly, reference verifiers or the installed-client RDL decoder. Acceptance of a recorded static finding is separate from fresh binary reproduction and live proof. Historical descriptions of later successful reads do not certify that every past alternative respected the original restriction.

## Audit findings and cleanup

- **PM7-01 — stale current instructions, corrected here.** The task header, handoffs and dashboard still called source selection/no-alliance unresolved and asked for an undifferentiated ESC-003 packet. The selector was already recovered, and its attempt history is now detailed. Current documents distinguish resolved subquestions, missing integration and the serializer restriction.
- **PM7-02 — research code is not production integration.** Actual call sites confirm `SelectOptionSourceForTest`, persisted aggregation, block checkpoints and staged publication are internal test paths. Next task A below brings the known source/scope logic into one integration service; its completion must still disclose any public gate.
- **PM7-03 — repeated current-status narratives, corrected here.** Archived review 6 with follow-ups, shortened the regular handoff and ledger summary, and retained the full 47-case specification. Only `task.md` is the task specification; `BACKLOG.md` tracks progress. Do not restore `TASKS.md` or create case-only duplicates.
- Updated stale source comments and a test description that still claimed the recovered selector/no-alliance contract was unknown. No executable behavior was changed by this PM checkpoint.

## Next work and ownership

Post-review continuation `LWB-R6-038` completes **PM7-A**. `LWB-R6-039` begins **PM7-B** at the package-key/runtime-material boundary, and `LWB-R6-040` now closes the host parsed-envelope persistence/cleanup seam: response string plus LF, `NtWriteFile`, replace-existing finalization and invalid/expired deletion are recovered against the verified LWBridge host. The embedded proxy read/decrypt path, protected handlers and authoritative readiness/error mapping remain open, so no production summary provider is enabled. The regular AI still owns **PM7-B**, **PM7-C**, and **PM7-D**. Work on one coherent item at a time and name its acceptance criteria before editing.

PM7-A is complete as a single bounded integration checkpoint. Next address PM7-B's production-blocking source/handler/readiness question or PM7-C/R5 when that path is independently actionable. Launch/stop/reconnect remain a critical dependency under R5; preserve their evidence and ownership gates.

**Daybreak has no approved technical assignment.** ESC-001 is CLOSED. ESC-003 is now **NOT_ASSIGNED after PM review**, not merely waiting for the same paperwork: its attempt history is adequate for this decision, but no specific permitted specialist approach has been identified for the remaining serializer boundary, and no supported live observation target was running during checks. Reopen only with a concrete permissible approach or newly available evidence/target. This does not resolve the serializer or authorize repeating the denied callback operation. ESC-002 and ESC-004 still need method/result/alternative inventories and bounded specialist value. See [the decision register](daybreak-escalations.md) and [Daybreak instructions](deep-binary-handoff.md).

## Fresh validation and delivery

Release build passed with **0 warnings/errors**. The deterministic backend suite returned `ok=true` with all five groups true, no failures, real configuration preservation, and no game/launcher running. Recovered frontend integrity, all five preference scenarios, missing-native transport and transport-boundary checks passed. The isolated native host matrix passed with no user configuration writes or live game commands; the termination-failure regression passed in 158 ms. These observations validate the rebuild/test harness, not a working scan or launch.

Post-review `LWB-R6-039` validation reran the hash-locked bridge-runtime-material inspector, Python bytecode compilation, recovered frontend integrity, the Release desktop build (**0 warnings/errors**) and the deterministic backend suite (`ok=true`, all five groups true, `--verify-real-config-unchanged`). The installed diagnostic again reported no running game or launcher. R6-039 is therefore static recovered boundary evidence only; it adds no live acceptance result.

`LWB-R6-040` adds a second hash-locked read-only inspector for the direct runtime-material persistence path. The inspector and Python bytecode compilation pass against the same immutable LWBridge SHA-256 and generate `2026-09-09-r6-runtime-material-persistence.json`. R6-040 is static recovered host evidence only; no public provider is enabled and no live acceptance case is claimed.

The [review evidence](../evidence/lwbridge-implementation/2026-09-09-pm-review-7.json) records commands, inspected source hashes, checks and limits. Markdown/evidence validation and exact preservation of all 47 acceptance rows are part of this checkpoint. Commit/push to the existing branch and remote verification remain mandatory; report the delivered PM commit separately from the reviewed implementation hash above.
