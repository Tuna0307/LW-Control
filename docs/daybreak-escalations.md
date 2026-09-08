# Daybreak escalation register

Updated 2026-09-09, PM review 5, implementation `d4e9790`. This is the decision/attempt log shared by [the regular task](implementation-handoff.md), [the specialist task](deep-binary-handoff.md) and the project manager. Mandatory rules are in [AGENTS.md](../AGENTS.md) section 6.

## State and ownership rules

`NEEDS_INFORMATION` → `READY_FOR_PM_REVIEW` → `APPROVED` → `ASSIGNED` → `RETURNED_FOR_INTEGRATION` → `CLOSED`. `NOT_ASSIGNED` records work retained by the regular AI or a request declined with a reason. PM approval is a workflow decision, not permission to evade platform restrictions.

The regular AI fills the packet and continues independent work. The PM checks whether relevant permitted methods have actually been exhausted and whether specialist analysis offers a specific benefit. A DB label alone assigns nothing. Current specialist technical assignments: **none**.

## ESC-001 — completion/reward cutoff source and units

- **Status/owner:** NEEDS_INFORMATION; regular AI prepares, PM reviews. DB-05/R6. Not assigned to Daybreak.
- **Question:** what produces the bound cutoff for completion-status comparisons and reward-option `arriveTs` filtering, with what units and server/profile context? Do not assume both use one clock.
- **Sources:** LWBridge 0.3.1 SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`; [R6-009 excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.txt) and [metadata](../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.json). Current official-client source applicability must be recorded separately.
- **Known:** SQL fragments/consumer vocabulary are saved; complete cutoff producer/source/unit is unresolved. No production time filter or option response is accepted from those strings alone.
- **Observed attempts:** the regular task searched docs/evidence/tools/source for completion/clock/arrival terms, reviewed the saved R6-009 excerpt and frontend consumers, and reported denied broad context and bounded range reads (SB-05). The attempted summary search also included invalid wildcard paths; its exit 1 is not proof of exhaustive searching. Review 5 inspected actual source paths and still found an unavailable rebuild summary, not an original native producer.
- **Why consider specialist:** tracing a producer through opaque native data flow may need additional expertise **if** permitted readable-artifact/current-client alternatives are exhausted. The current record does not establish that exhaustion or that a different model may execute the denied reads.
- **Missing before PM approval:** exact permitted attempt inventory with tool versions, query scopes, durable outputs, hypotheses eliminated, current-client/readable-artifact alternatives considered, reasons they cannot answer the question, and a concrete permitted specialist scope. Add exact denial telemetry if accessible; currently the completion-read denial is a task-reported restriction, not raw review telemetry.
- **Return criteria if approved:** source-attributed producer/consumer/type/unit/context mapping, contradictions and validation examples; or precise unresolved dependency. No guessed timestamp or live operation. Implementation remains with the regular AI.

## ESC-002 — schema version and migration prerequisites

- **Status/owner:** NEEDS_INFORMATION; regular AI prepares, PM reviews. R6 storage. Not assigned to Daybreak.
- **Question:** supported schema-version constant, migration thresholds/order and metadata timestamp producer/unit. A version found in one database would not alone prove all supported transitions.
- **Sources/known:** same verified reference identity as ESC-001; [R6-011 metadata](../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.json) and [excerpt](../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.txt) establish schema metadata/future-schema/legacy-import markers. Production migration is not implemented.
- **Observed attempts:** repository/schema/store/test searches, bounded printable-string observations and saved-evidence review are recorded in the regular task. A read-only query of the existing user-profile database was denied (SB-04); do not retry it via Daybreak. The current evidence does not list every relevant permitted alternative or establish a specialist-only capability gap.
- **Missing before PM approval:** method/tool/locator/output inventory, readable artifacts/import/migration producer search results, why remaining permitted approaches cannot resolve the constant/branch, and the narrow permitted question for the specialist. Retain unknown version/timing values.
- **Why consider specialist:** a genuinely unresolved native version/branch transformation may benefit from specialist analysis after that packet is complete; model choice alone is not justification.
- **Return criteria if approved:** exact constants/transition gates/timestamp source with provenance and limits, plus deterministic migration test requirements. The regular AI implements/tests the supported contract.

## Retained regular-AI work — NOT_ASSIGNED

Options/count/no-alliance assembly, profile-to-summary-server selection, alternate sorts, export semantics, host-test hygiene, supported-query regressions, storage/generation engineering, and DB-01–04/06 remain regular-AI work. Open an individual ESC record only when the mandatory attempts establish the need. The new ordinary-quality gap was resolved by the regular AI; it is not an escalation candidate.

## Recorded operation restrictions

| ID | Source and operation | Evidence / effect |
|---|---|---|
| SB-01 | [R5-004](../evidence/lwbridge-implementation/2026-09-08-r5-launch-material.json), new fixed-address launch-material verifier execution | Historical safety-review denial. Existing findings have limited provenance; no replay assigned. |
| SB-02 | [R5-005](../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption.json), broader helper/binary disassembly | Historical report: reviewer could not determine safety status. Saved bounded excerpt is not fresh extraction. |
| SB-03 | [R6-009](../evidence/lwbridge-implementation/2026-09-08-r6-map-options-advanced-filters.json), convenience executable verifier | Recorded denied after earlier successful bounded observations; exact rejected invocation is not reproduced in the metadata. Preserve that limit. |
| SB-04 | [R6-011](../evidence/lwbridge-implementation/2026-09-08-r6-map-schema-metadata.json), read-only existing user-profile database query | Recorded automatic-review denial; no database contents accepted as evidence. |
| SB-05 | Task **Read Map Scan Recovery Docs**, turn `01a081a6-77cb-7893-974c-af50f0104e7b`, completion-status context/range reads | Task reports reviewer could not determine safety status. Review 5 preserves the report in its evidence; it did not retry either operation or acquire raw rejection telemetry. |

No new denial occurred in review 5. Restrictions describe operations, not every analysis of the executable. Missing tools, unresolved contracts, external services and unavailable live targets must be labelled separately. Do not reroute denied operations to Daybreak or CI.

## Required request template — copy for each new ESC entry

1. **ESC ID/date/status/owner/base commit:** name the exact DB/R/S/O/M item and requesting task.
2. **Precise question and impact:** the missing fact, dependent behavior, what can continue independently.
3. **Source identity:** reference/current-client distinction, artifact paths/build/SHA-256, known locators and existing finding IDs.
4. **Known facts versus hypotheses:** preserve unknown values and contradictory findings.
5. **Attempt log:** method; tool/version/setup; command or bounded steps/locator; result/error; durable output; interpretation/why insufficient. Do not list a method as tried without evidence.
6. **Alternatives exhausted:** readable JS/Lua/managed/resources/SQL/logs, source/caller/data-flow approaches, suitable available tools/custom scripts; explain the relevant methods you considered and why each cannot answer. Do not require irrelevant techniques or prohibited attempts.
7. **Blocker class:** research/capability, setup, missing target, external dependency or environment restriction. Include exact denied operation/tool/reason if applicable; preserve restrictions.
8. **Why Daybreak:** specific specialist value, the smallest permitted scope, expected input/output, and why the regular AI cannot progress further with its identified permitted approaches.
9. **Acceptance and return:** provenance/type/unit/state requirements, what code is unblocked, validation still needed, file ownership and integration owner.
10. **PM decision and return log:** rationale, approved bounded scope or missing evidence, assigned task if any, findings/commit/push verification, integration tests and closure reason.

The PM-seeded entries above are deliberately incomplete requests, not a claim the regular AI has exhausted all methods. Complete them honestly before requesting assignment.
