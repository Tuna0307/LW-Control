# LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005

Status: **ASSIGNED FOR OWNER RELAY**
Date: 2026-10-07
Branch: `research/offline-controller`.
Product/evidence baseline: `d900d8a548c072485cabc9b55d3dc5b07c4e6085`.
Start from the latest descendant containing this assignment. Record exact HEAD,
status and input identities; never reset/discard unrelated work.

## Goal and scope

Correct the independently reproduced Ghost expiry mismatch and investigate the
new managed correlation source identified in audit 004. Complete three sequential
medium checkpoints, continuing automatically. Work alone, without subagents, GPT
Work/Codex delegation or other AI chats. Final lead acceptance remains separate.

Read AGENTS.md, task.md, docs/AI_WORK_PROTOCOL.md, this item, current Home/Map
masters and the following evidence/reviews:

- `docs/reviews/2026-10-07-LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004-LEAD.md`;
- 004 worker review, A/B/C evidence and `blocker-boundary-matrix.json`;
- SEMANTICS-003 worker review, semantic tools/oracles, and current native providers;
- exact 0.3.17 action-host evidence and installed v22 package/managed metadata.

Original EXE SHA-256:
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Current v22 package SHA-256:
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.
Current Assembly-CSharp.rdl SHA-256:
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
Revalidate artifacts and retain older-version findings as hypotheses.

## A — LR-GHOST-EXPIRY-005 production correction

Inspect `CurrentClientMap317ActionProvider.PrepareGhostPlunderRows`, exact-original
conditional expiry semantics and the current TaskInfo zero default. Correct the
helper to accept zero/non-positive/missing expiry under the existing numeric-read
contract; only a positive expiry at/before plunder is rejected. Preserve the actual
input field/value and all other source-backed identity, timing, alias and row-count
guards. Do not change the global store, time units, random delay or live fences.
For malformed/numeric-string fields, follow the established reader contract and
document its behavior; do not silently broaden parsing to make tests pass.

Use actual production helper and host-boundary cases for positive-after, zero,
negative, missing, positive-equal and positive-before expiry. Include frontend
random delay and retained identity/counter/timing failures. Show the pre-fix helper
fails the new distinguishing cases and the corrected helper passes. Preserve the
lead's historical `expiry-results.json`; place fresh corrected evidence separately.

Keep `PrepareGhostPlunderTasksAsync` unavailable. Correct its misleading explanation
so missing preparation transformations and downstream execution correlation are
described separately, using established error codes/schema. This is explanatory
accuracy, not permission to enable preparation/persistence or execution.

## B — actual managed request/result correlation tracing

Audit 004 found real `FutureManager` metadata: `_futureId`, `_sendInfos`,
`getFutureId`, `onSendRequest(fuid,msgId)`, `onServerMsgCome(fuid,serverTime)` and
`msgSendInfo`, but the current parser could not resolve altered CIL metadata tokens
through the end-to-end `SendLuaMessage`/receive path.

Inspect existing RDL tooling first. Improve read-only decoding/resolution only
with evidence for the actual metadata/operand encoding. Validate new mappings
against independently constructed fixtures and identifiable artifact methods;
retain raw bytes, table/row tokens, method offsets and unresolved operands.
Do not make the parser silently treat invalid/unknown tokens as convenient names.

Trace actual relevant method bodies, callers and schemas:

1. Lua request serialization -> managed SendLuaMessage;
2. future allocation and association with outbound message/connection;
3. incoming response handling -> onServerMsgCome/pending consumption;
4. raw response conversion -> SFSNetwork/actual Ghost handler table;
5. which identity, if any, survives to the observable result boundary.

Establish whether `fuid` is merely a timing/ack mechanism or can identify a specific
request. Determine actual collision/reuse, disconnect/reset and ordering behavior
where source exists. Trace the separate point-identity push without conflating it
with direct-request completion. Preserve original bytes and new semantic locators.

Use actual decoded body execution with inert stubs if safely feasible, or verified
static instruction/dataflow evidence. Synthetic UUID/future/point injection is not
proof that the real producer emits it. A stronger local parser does not prove real
game xLua binding or live operation. If static recovery blocks, record the exact
unresolved operand/body/schema edge and minimum artifact/witness needed.

Do not add a production correlation abstraction, request queue or real Ghost
transport in this checkpoint. It is recovery of evidence for a later integration
decision; merely having an identifier's name is not sufficient.

## C — focused integration, review and delivery

Run the corrected production-helper tests, canonical Map317/native plunder worker
checks, existing 4 body oracles, 4 blocker body checks, 3 production-Lua safety
checks and applicable new parser/correlation proofs. Keep Dispatch behavior and
Ghost-before-Dispatch rejection intact. Use isolated files/providers only.

Because A changes production source, run Release Desktop and Desktop.Checks builds
and canonical frontend check/build/production-package verification. Reuse the
maintained current validators; old audit B intentionally pins the prior strict
helper/explanation, so preserve it and provide a narrow corrected replay instead
of overwriting historical negative evidence. Review the whole diff for regressions.

Update current work-item/status/ledger/matrix/continuation conservatively, create a
dated worker review and repeatable fresh evidence validator. Report A as corrected
only after actual production proof, and B as exact/partial/unknown per edge. Keep
Home/Map PARTIAL, Treasure/Ghost public methods unavailable and live hold unchanged.

Commit/push coherent checkpoints and verify direct remote SHA. Continue A/B/C
without per-checkpoint relay; B blockage does not prevent A/C delivery. Clean only
verified task-owned temporary roots/processes and prove prior evidence/unrelated
state preserved. Return AWAITING_REVIEW with LR-GHOST-EXPIRY-005 proof, managed
correlation findings, executed checks, commits and precise next evidence needs.

## Non-goals and safety

No inert product shell/placeholder controller, fallback, fake success, new private
schemas or provider capability enablement. No new UI workflows or redesign.
Live/shared-desktop work remains ON_HOLD_BY_OWNER: no game/original launch, attach,
installation instrumentation, owner screenshots/input/focus/config/runtime changes,
updater, protected service/auth bypass or real gameplay/network transport. Read
static installed artifacts and use task-owned copies/isolated inert roots only.
