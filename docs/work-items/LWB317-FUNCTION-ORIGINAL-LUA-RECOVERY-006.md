# LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006

Status: **ASSIGNED FOR MANUAL OWNER RELAY — AFTER 005 LEAD ACCEPTANCE**
Date: 2026-10-07
Branch: `research/offline-controller`.
005 delivery `8f6730ef5e46fc602c6331d21e25d206b2e95ca4` is lead-accepted for
its assigned scope. Start from the latest descendant containing that delivery
and the 005 lead review; record exact starting SHA. Never reset to an older commit.
Read `docs/reviews/2026-10-07-LWB317-FUNCTION-MAP-EXPIRY-CORRELATION-005-LEAD.md`.
Retain its parser-proof limits and numeric-string preparation discrepancy when
comparing recovered original semantics; this task does not authorize product fixes.

## Goal

Prioritize the exact original LWBridge 0.3.17 function payload: establish where its
Lua/package lives, extract and decode it offline if the supplied artifacts and
legitimately available inputs support that, and recover the missing original
Treasure/Ghost controller semantics from actual code. Do not assume decoding the
Last War game package reveals LWBridge's own orchestration.

## Known starting facts and limits

- Current Last War v22 LWScripts.data has already been decoded: 18,741 entries.
  Selected actual Lua 5.3 bodies are parsed and executed under isolated stubs.
  This supplies game APIs/behaviour, not necessarily original bridge controllers.
- Older LWKE1/package-key research and inspectors target LWBridge 0.3.1. Their
  algorithms/offsets/constraints are historical hypotheses until revalidated.
- Fresh lead read-only inventory of the exact 0.3.17 EXE finds literal references
  to `bridge-scripts.dat`, `package-key.envelope`, `LWBP2|`, `LWKE1` and the three
  missing controller names. This is evidence of references only: it does not prove
  the payload is embedded or that a usable decryption input is present.
- Reference EXE SHA-256:
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Lead preflight script/results:
  `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006/lead-preflight/`.

Read AGENTS.md, current A->A owner clarification, AI_WORK_PROTOCOL, current
Home/Map masters, 003/004/005 evidence and reviews, exact-reference loader/action
evidence, and historical package-crypto research for hypotheses. Apply current
owner/access rules rather than blindly carrying old version-specific assumptions.

## A — exact-original payload inventory

Inspect the supplied 0.3.17 executable's resources, embedded file/PE ranges,
overlay/package tables, compression/manifest and extraction/loader code. Inspect
immutable supplied/archived research artifacts and known task-owned extracted
copies; do not broadly rummage through owner configuration/credentials or modify
owner runtime files. Inventory candidate files with origin, exact hashes and
headers; distinguish original payloads, frontend JS, current-game Lua and clone
scripts. Resolve literal file offsets to actual consumers/producers.

Determine whether each required function body is embedded, independently bundled,
available in existing supplied artifacts, generated locally or obtained elsewhere.
Treat absent local payload as a specific missing artifact, not automatically an
encryption problem. Preserve evidence for rejected candidates and source paths.

## B — actual 0.3.17 extraction/crypto contract

Trace exact-original package reads, transforms, integrity/build checks, bytecode
loader and required key/envelope inputs using hash-gated static analysis. Verify
every format/field/encoding/KDF/nonce/tag/AAD/compression claim against 0.3.17;
do not merely disable a 0.3.1 inspector's hash gate.

Identify legitimately available input dependencies and document their availability
without logging credentials/private keys. Do not guess missing fields, invent
keys or label a failed integrity check as successful decoding. Distinguish payload
decryption from bytecode disassembly/decompilation; decoded compiled code still
requires semantic analysis.

## C — offline payload recovery where inputs are complete

If a complete source-backed transform and supplied/authorized inputs exist, extract
and decrypt/decompress into a task-owned isolated root without launching the
original/game or touching installed/runtime files. Validate exact build/integrity,
entry tables and bytecode syntax. Pin encrypted and decoded identities and tools.
Use narrow derived function evidence; do not duplicate entire packages in Git.

If the needed payload/key/input is absent or obtaining it requires unavailable
authorized access, report that precise dependency with source locators. Do not
bypass authentication/licensing/entitlement, access protected services, export
someone else's credentials, fabricate original-service state or cross the owner
live/shared-desktop hold. Required dependency research under AGENTS.md section 6
remains allowed; touching auth-related local code alone is not a reason to stop.

## D — original semantics and single delivery

If recovered, trace actual original `getTreasureClaimStatus`, `claimTreasures` and
`prepareGhostPlunderTasks` bodies, their owned state/cache, inputs, selection/order,
batch counters/transitions, timeout/reconnect/profile lifetime, preparation rows,
scout completion and action result correlation. Write exact function/PC/source
locators and executable isolated oracles where feasible. Compare findings with
existing clone implementations; report concrete A->B differences for assignment.
Do not enable/change product providers in this recovery task.

Create reproducible artifact/tool validators, a dated worker review and per-function
recovered/unknown matrix. Preserve older negative attempts and evidence. Commit/
push coherent medium checkpoints, verify direct remote SHA, continue ready branches
automatically, and return AWAITING_REVIEW with recovered payload/function coverage
or exact missing inputs. No promise that one package contains all program logic:
native, managed, runtime and externally produced parts remain separate if proven.

Work alone without subagents, other AI chats or GPT Work/Codex delegation. Live/
desktop activity remains ON_HOLD_BY_OWNER. No visible windows, game/original launch,
attach, install instrumentation, owner capture/input/focus/runtime/config mutation,
updater, protected-service access or real game transport. Static artifacts and
isolated offline code only. Final acceptance stays with the lead.
