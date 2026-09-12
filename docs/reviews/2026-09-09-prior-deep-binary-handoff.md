> HISTORICAL: superseded by [review 5](../lwbridge-project-status.md) and AGENTS.md section 6. DB labels are research subjects, not automatic Daybreak assignments.

# Deeper binary research handoff — for the user's Daybreak task

Prepared by project-manager review 4 on 2026-09-08 against implementation `5174cf9`. This is a research supplement to [task.md](../../task.md), not a replacement specification. Read [AGENTS.md](../../AGENTS.md), [BACKLOG.md](../../BACKLOG.md) and [the current audit](../lwbridge-project-status.md) first. The user will choose the destination model; no task was created or dispatched by this review.

## Scope and restriction boundary

Identify the unresolved contracts that genuinely require deeper analysis, starting with already committed evidence. A different model name does not establish that a previously denied action is now permitted. **This handoff is not an instruction to rerun the denied fixed-address verifier, reproduce rejected ad-hoc disassembly through another tool, or evade a safety review.** The receiving environment must assess its own permitted scope. If an action is disallowed there, preserve the restriction and continue permitted evidence review or independent work.

The user has authorized ordinary project-related tool discovery and installation. That addresses missing tools/integrations; it does not remove platform restrictions. Do not confuse a missing Ghidra integration, an unknown contract, an unavailable live target and an actual denied operation.

No live game launch, injection, ticket generation, claim, travel or message delivery is assigned by this research-only supplement. Do not forge vendor credentials/proofs, obtain private signing keys, disable verification or claim a service-issued identity. Describe external trust/service dependencies accurately and distinguish them from components the independent rebuild can legitimately own.

## Classification legend

| Tag | Meaning |
|---|---|
| **DEEP-BINARY** | Remaining behavior/meaning is inside native control flow, callers or data transformations; current excerpts/strings are insufficient. |
| **ARTIFACT-REVIEW** | Search existing extracted JS/Lua/managed code, SQL/resources, schemas or saved evidence first. Escalate to deep analysis only if those sources do not answer the question. |
| **IMPLEMENTATION** | Recovered contracts or explicit rebuild policy already support coding/testing. This work can continue without deeper disassembly. |
| **LIVE-VALIDATION** | Requires an authoritative current-client outcome after prerequisites exist. Static analysis cannot close this gate. |
| **RECORDED-RESTRICTION** | A specific prior operation was reported denied. This tag describes that operation, not every task concerning the same executable. |

## Recorded restrictions — read before selecting research actions

| ID | Recorded operation and source | Stated reason / evidence limit | Receiving AI instruction |
|---|---|---|---|
| SB-01 | Running the new fixed-address launch-material verifier against the LWBridge reference; see [LWB-R5-004 evidence](../../evidence/lwbridge-implementation/2026-09-08-r5-launch-material.json), `reproduction.currentCheckpointExecution`. The affected script is `tools/inspect_lwbridge_launch_material.py`. | Saved evidence says syntax passed but execution was denied by the environment safety review. The user reports the OpenAI review could not determine the action's safety status. Exact low-level rejection telemetry is not attached to this audit. | Do not present this verifier as successfully executed. Start with documented observations and their provenance. Do not replay or route the denied invocation through another executor/model to bypass the restriction. |
| SB-02 | Broader binary/deeper helper-disassembly requests during ticket-consumption research; see [LWB-R5-005 evidence](../../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption.json), `reproduction.currentCheckpointExecution`. | Saved evidence says the environment could not determine safety status. The recorded finding uses previously saved, bounded disassembly; it is not a fresh full-binary verification. | Review the [committed text excerpt](../../evidence/lwbridge-implementation/2026-09-08-r5-ticket-consumption-disassembly.txt) within permitted scope. Do not claim unresolved caller/input/variant meanings have been recovered, or repackage the denied broader operation as a workaround. |

These are historical/user-reported restrictions, not denials newly received by the project-manager audit. No denied operation was retried in review 4. The stored excerpt checker verifies saved text/hash, not the correctness of a new binary extraction. Tool installation alone does not resolve either restriction.

## Research queue that needs deeper analysis

### DB-01 — Remaining launch envelope semantics and child input/ownership contract

**Tags:** DEEP-BINARY; R5/O02–O06; associated restriction SB-01 for the recorded verifier only.

**Already recovered — do not repeat:** `LWB-R5-001` outer envelope construction/handoff; `002` exact xLua ABI selector; `003` child parser and validation branches; `004` outer response propagation, ticket-source taxonomy and cached report fields. Start with [lwbridge-injection.md](../lwbridge-injection.md) and those dated evidence records.

**Unanswered:** remaining descriptor field meanings; exact outer-to-child argument quoting/input channel; ownership/lifetime of launch material; semantic role of the unresolved outer helper and fallback branches; which prerequisites depend on a remote/vendor service versus locally observable state. Identifying a field, format or signature check does not establish the full producer contract or permission to manufacture its output.

**Why deeper:** existing field xrefs/string markers describe only portions of the flow; relationships among producer, caller, serializer, child decoder and state transitions remain incomplete.

**Research deliverable:** a source-attributed interface and state diagram, field/type/unit/lifetime table, producer/consumer boundary and failure behavior, with every remaining uncertainty identified. Document any service-only prerequisite as an external dependency. Do not substitute guessed data or signing material. Keep production launch gated until a supported independent lifecycle design and its validation exist.

### DB-02 — Ticket-consumption polling inputs, time units and result variants

**Tags:** DEEP-BINARY; R5/O02/O03/O06; associated restriction SB-02.

**Already recovered:** `LWB-R5-005` names the observed ownership-changed, timeout and consumption-failed branches from the retained bounded excerpt. Source identity/locators are in its evidence file.

**Unanswered:** semantic identity of opaque helper inputs, expected-sequence producer and lifetime, the caller-to-poll relationship, exact meaning of internal result variants and the units/source of deadline/timing values. The extra ticket field's meaning also remains unknown.

**Why deeper:** branch labels and numeric literals cannot establish caller semantics or units. Do not turn a raw constant into seconds/milliseconds based on magnitude alone.

**Research deliverable:** caller/input/producer mapping and documented state transitions, including uncertainty/failure behavior. Use already saved evidence where permitted; unresolved edges stay unknown if restrictions prevent further analysis. No ticket fabrication or verification bypass is part of this deliverable.

### DB-03 — Native map identities and normalization/update/removal rules

**Tags:** DEEP-BINARY, with ARTIFACT-REVIEW first where readable code exists; R6/M02/M03/M06.

**Already recovered:** persistent/staging SQL keys and frontend row vocabulary; explicit-key store implementation; original table/index layout. A frontend rendering fallback is not a native persistent identity.

**Unanswered:** how each of city, resource, monster, truck, railway, dispatch, ghost and treasure derives its stable key; native field grouping/types/units; point/march update, movement, expiry and removal rules; authoritative rich-field sources in the current official client.

**Why deeper:** database schema and UI consumers omit the producer transformations. Native capture and current-client serialization/handler behavior may need analysis after searching existing readable code and artifacts.

**Research deliverable:** one documented schema/identity/update map per kind with representative sourced samples and source-to-normalizer-to-store relationships. Separate missing content from legitimate empty states. Do not promote synthetic records into evidence of a real world/category.

### DB-04 — Scan scheduler, capture transport and completion/resume semantics

**Tags:** DEEP-BINARY; R7/M01/M02/M04, later R8/M11.

**Already recovered:** scan API/type allowlist and Normal/Fast concurrency; saved native capture vocabulary and staged index concepts.

**Unanswered:** exact coverage geometry/order, scheduling/tick behavior, retries/unread/inflight ownership, capture queue/acknowledgement/drop behavior, cancellation boundaries, resume compatibility and final drain/publication conditions. Transport success is not semantic scan completeness.

**Research deliverable:** scheduling and capture state machines, exact contract values with provenance, run/session/server identities and a completion truth table. Distinguish recovered behavior from any proposed rebuild policy. Live scan correctness remains a separate validation gate.

### DB-05 — Advanced filter/time/eligibility and derived-sort semantics

**Tags:** ARTIFACT-REVIEW → DEEP-BINARY if data/code evidence remains insufficient; R6/M06/M07/M09.

**Already recovered/implemented:** `LWB-R6-003/004/005/006/007/008/009/010/012/013`: pagination, updated-time/stable-key ordering, marks, serialized tab queries, literal-substring keyword construction, city alliance/no-alliance, resource/monster name-key equality, truck/railway current-goods item membership, dispatch/ghost special-only, truck-only reindeer, treasure option-pair and dispatch selected-level filtering. `LWB-R6-013` recovers ordinary quality exactly as `n/r/sr/ssr = 1/2/3/4`, `ur >=5`, with the special-quality exclusion only for truck+UR. Do not send those slices back for recovery.

**Unanswered:** completion-status time source and units; per-kind plunderability; treasure visibility/lucky behavior; remaining derived sort expressions. Several SQL fragments are known, but their predicates/parameters/clock sources are not fully joined up.

**Research deliverable:** exact per-kind predicate/sort/parameter/default tables, supported explicit-false/zero/empty/omitted semantics, time/identity dependencies and deterministic boundary examples. Search existing SQL/resources and readable producer code first; classify only unresolved native computation as deep-binary work.

### DB-06 — Authoritative status, event and game-action contracts

**Tags:** ARTIFACT-REVIEW → DEEP-BINARY where native producers are opaque; R3/R5/R8/R9, S01–S03/S06, M10–M16.

**Unanswered:** runtime pending-count/heartbeat/recovery semantics, confirmed server travel and restoration, coordinate/march follow identity, treasure/plunder/train/job eligibility and outcome reconciliation. Frontend command names alone do not implement these operations.

**Research deliverable:** per-operation request/response/event/state contracts using readable current-client sources first; elevate native gaps individually. Separate a queued request from an authoritative game result. Do not attempt live actions as part of this research supplement; identify the future validation needed.

## Work the regular implementation AI can continue now

| Work package | Classification | Concrete next result |
|---|---|---|
| Preserve PM2/PM3 fixes and expand regression coverage | IMPLEMENTATION | Keep confirmed persisted-state reconciliation, visible errors, mixed failures and successful recovery passing in Node and WebView. No deeper analysis is needed to retain these fixes. |
| Continue from recovered query/filter slices | IMPLEMENTATION | Test combinations, typed validation, server/profile isolation, correct counts/paging and source-to-UI wiring for already supported predicates. Unknown query semantics remain gated. |
| Options/summary field contracts and aggregation research | ARTIFACT-REVIEW plus IMPLEMENTATION | Frontend consumers are pinned by `LWB-R6-004`. Recover native aggregation/server-selection semantics from existing SQL/code before filling fields; implement only supported fields. Escalate a specific opaque native calculation, not the entire panel. |
| Persistent store engineering | IMPLEMENTATION POLICY | Consistent count/page snapshots, versioning/migration, transaction/failure recovery, checkpoint and stale-generation boundaries. Recovered publication semantics still constrain the eventual scan implementation. |
| Export infrastructure | IMPLEMENTATION with ARTIFACT-REVIEW for original formatting/scope | File dialog/cancel/error handling and lossless workbook round-trip tests can be prepared from the known frontend envelope. Do not invent original column/scope/format behavior or mark export complete before full filtered-snapshot proof. |
| Host lifecycle/worker interfaces and diagnostics | IMPLEMENTATION | Typed ownership/cancellation/events/journaling interfaces and isolated error-path tests. Actual game launch/readiness must wait for supported runtime contracts. |
| Tests, documentation and CI hygiene | IMPLEMENTATION | Integrate permitted source/evidence checks, label artifact prerequisites, preserve hashes, keep historical findings distinct, fix stale instructions and encoding. Do not add the blocked verifier to CI as a way to run it elsewhere. |
| Successful launch/full scan/actions | LIVE-VALIDATION after implementation | Obtain authoritative current-client evidence under the active task's permitted scope. A static research report cannot sign off these outcomes. |

## Instructions to the receiving research AI

1. Read the rules, current audit, SB-01/02 and the relevant saved findings before selecting any operation. Check the current commit/source identity and report what evidence is actually available.
2. Start by reviewing committed documents/excerpts and identify which DB item each unresolved question belongs to. Do not redo recovered work or execute denied operations simply because the destination model differs.
3. Apply your environment's safety/tool restrictions. Within permitted scope, choose the smallest evidence-producing analysis that answers an identified gap; missing integration may justify tool setup, but installation never overrides a denial.
4. For each completed finding, record the mandatory ID/date/source hash/locator/reproduction/result/limits/implementation impact. State whether it is newly observed, saved-evidence interpretation, unverified hypothesis or externally dependent.
5. Return a per-DB result table: resolved contracts, artifact references, still-unknown questions, exact restriction if applicable and which implementation tasks are now unblocked. Never mark the full launch/scan/action complete from partial static proof.
6. Update existing subject docs and link them from the feature ledger/backlog. Commit/push the coherent permitted checkpoint and verify the remote revision. Preserve the user's login-free UI and prior evidence.

The ordinary coding AI should keep working on the implementation table above while this research is pending. A research gap or a restriction on one operation must not turn the whole repository into a waiting task.
