# LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004

Status: **ASSIGNED FOR OWNER RELAY**
Date: 2026-10-07
Branch: `research/offline-controller`.
Reviewed product baseline: `fb70f281bada971e55b9d0643d6b22cd1f8e7b4a`.
Start from the latest descendant containing this assignment; preserve unrelated work.

## Goal

Determine which of the three reported blockers are genuine missing observable
semantics, downstream capability restrictions, or proof awaiting a future live
witness. Audit the lead's BT-01/02/03 questions against actual original/current
sources. This is a read-only product audit with derived evidence/tests allowed;
do not enable providers or change production behavior.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, current Home/Map masters, the
SEMANTICS-003 work item, worker review and complete contract matrix, and
`docs/reviews/2026-10-07-LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003-LEAD-TRIAGE.md`.

## Checkpoint A — precise observable contract and ownership

Trace exact 0.3.17 frontend and host consumers of status/claim results: required
fields, optional/missing fields, initial state, poll start/stop, batch transitions,
error behavior, navigation/profile changes and state produced by prior calls.
Pin asset/EXE hashes and byte/function locators. Do not require an identically
named game method or exact private implementation to prove an observable contract.
Do not infer a missing state transition from the clone itself.

For BT-01, test the hypothesis that status reads an owned batch/cache populated by
earlier operations. Determine which parts are source-established and which still
lack ordering, lifecycle or result semantics. A no-argument status call does not
by itself rule that hypothesis in or out. Produce actual-source distinguishing
callback/render cases where the sources permit; preserve negative evidence.

## Checkpoint B — separate preparation, capability and execution

For BT-02, trace original/current Ghost input validation, preparation call, returned
row transformations, persistence and later execution. Classify the strict internal
normalizer as exact contract, justified adaptation or incomplete hypothesis per
field. State whether preparation itself requires runtime data/hydration or terminal
steal confirmation. Identify any explicit evidence that mandates disabling both
preparation and execution when only execution is unavailable.

For BT-03, inspect current raw response schema, SFS response dispatch and any
request identifiers, pending queues, serialization/ordering and manager correlation
paths. Distinguish "handler does not read UUID" from "schema contains no UUID".
An unanswered possibility is UNKNOWN, not proof of safe correlation. Keep the
Ghost guard and all public unavailable states unchanged.

Inspect actual function bodies and existing semantic tools first. A source token
search is a locator, not the conclusion. If a source is genuinely absent, identify
the first missing edge and what artifact/witness could settle it. Do not access
protected services or bypass original authentication/entitlements.

## Checkpoint C — reproducible disposition and delivery

Produce a machine-readable per-stage matrix with separate columns:
original observable contract, current operation, bridge-owned adaptation,
downstream capability gate, terminal correlation, offline proof and live witness.
For every BLOCKED/UNKNOWN row give an exact missing edge and minimum evidence.
Identify ready offline work that can progress independently; do not implement it
in this audit. Correct overbroad explanations in new dated review/continuation
records without rewriting historical worker evidence or upgrading acceptance.

Run the new distinguishing audits, affected decoded-body/production Lua tests and
the current semantic validator. Production builds are unnecessary without product
changes. Review the diff; prove production source/accepted historical evidence and
owner state unchanged. Commit/push coherent checkpoints, verify direct remote SHA
and return AWAITING_REVIEW with BT-01/02/03 valid/invalid/partial dispositions,
source evidence, executed cases and concrete next implementation candidates.

Work alone, sequentially, without per-checkpoint owner relay, subagents, GPT Work,
Codex delegation or other AI chats. Live/shared-desktop work remains ON_HOLD_BY_OWNER:
no game/original launch or attach, installed-file changes, desktop capture/input/
focus, owner runtime/config changes, real network/game actions or updater. Use
static artifacts and isolated/inert roots only. No fallback, fabricated success,
new private schema or LIVE_PROVEN promotion. Lead acceptance remains separate.
