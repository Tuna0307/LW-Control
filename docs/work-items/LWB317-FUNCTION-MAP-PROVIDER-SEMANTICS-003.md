# LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003

Status: **ASSIGNED FOR OWNER RELAY**
Date: 2026-10-07
Owner: one solo worker; project lead reviews final delivery.
Branch: `research/offline-controller`.
Product/evidence baseline: `a7c4a03cbd87db5d45030c8117a199d6653729e4`.
Start from the latest descendant containing this work item and lead assessment;
record its exact SHA. Do not reset to the product baseline.

## Goal

Resolve the remaining three Map provider contracts through function-body recovery
and executable offline evidence, then implement any complete source-backed
current-client adapters. Preserve accepted Home behavior and UIUX. This is one
large campaign with eight sequential medium checkpoints and one final return.
Work quality and completeness determine its duration; there is no minimum number
of hours, time quota, fixed assertion count or requirement to manufacture changes.

Methods in scope, in `src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs`:

- `GetTreasureClaimStatusAsync`;
- `ClaimTreasuresAsync`;
- `PrepareGhostPlunderTasksAsync`.

Current methods fail with `GAME_PROVIDER_UNAVAILABLE`. Keep each unavailable until
its complete public input/result and current operation/correlation contract is
established. Recover independently: a blocked claim branch need not block a
provable status or Ghost branch.

## Mandatory reading and inputs

Read `AGENTS.md`, `task.md`, `docs/AI_WORK_PROTOCOL.md`, this work item, current
Home/Map masters, feature ledger and parity matrix. Read the lead assessment:
`docs/reviews/2026-10-07-LWB317-FUNCTION-HOME-MAP-DEEP-RECOVERY-002-LEAD.md`.

Inspect actual files, not just the previous worker summary:

- `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-DEEP-RECOVERY-002/`,
  especially A provenance, D candidates/strings/blockers and G limits;
- exact 0.3.17 Map action/claim/scheduling evidence under
  `evidence/lwbridge-0.3.17/map/` and hash-gated `tools/lwbridge317/` inspectors;
- `src/LWBridge.Map-0.3.17/` action interfaces, claim normalization, scheduling,
  storage and provider-result consumers;
- `src/LWBridge.Desktop/CurrentClientMap317ActionProvider.cs`, related block-source
  action/state files, and their actual transport/ownership implementation;
- `tools/current_live_resource_probe.lua` and `tools/current_overview_bridge.lua`;
- accepted RECOVERY-003 and OFFLINE-HOST-CONTRACT lead reviews. Preserve the reader
  erratum: .NET bounds UTF-16 `String.Length` and honors BOM detection; Lua metadata
  readers separately bound byte length.

Reference EXE:
`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`.
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Current v22 package:
`C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts\LWScripts.data`.
Expected SHA-256: `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.
Check actual artifacts before claims. A changed artifact is a separate provenance
event; do not silently repin accepted gates. Older 0.3.1/v19 findings are hypotheses.

## Operating boundary

Work alone: no subagents, other AI chats, GPT Work or Codex delegation/execution.
Use your connected command/file tools for headless work. Do not request the owner
to relay each checkpoint; continue all ready assigned branches automatically.

Live/shared-desktop work remains **ON_HOLD_BY_OWNER**. Do not launch, attach to,
control or instrument Last War or LWBridge; modify the game installation; capture
the owner's desktop; send input/change focus; touch owner runtime/configuration;
use protected services, credentials, updater or real transport/gameplay. Reading
installed artifacts is allowed. Use isolated temporary copies, explicit roots,
inert processes/providers and headless test rendering only.

Do not bypass original authentication/entitlements. Static analysis of supplied
artifacts and required state dependencies is allowed within AGENTS.md section 6.
A local Lua runtime with explicit stubs can prove bytecode behavior, never real
in-game scheduling, xLua delegate crossing, readiness or provider-positive actions.

## Milestone A — reproducible semantic inspection toolchain

Create a campaign-owned evidence directory, queue, progress, continuation and input
manifest. Freeze the starting production tree and previous evidence hashes.

Identify the actual bytecode format/version from headers and instruction structure.
Build or adapt a reusable read-only parser/disassembler, and a decompiler if useful.
Inspect existing tools first; record new tools' source/version and compatibility.
Validate the tooling against small independently compiled fixtures with the same
format and against selected decoded game functions. Do not assume the existing
lupa Lua 5.4 environment consumes the `0x53` chunks correctly.
The observed next format byte is `0x01`; inspect the actual chunk layout and,
if necessary, static current-xLua loader/VM code. Do not edit headers or remap
instructions to force loading without proving the transformation preserves semantics.

For relevant functions produce prototype paths, byte ranges/program counters,
constant/upvalue/register mappings, control-flow edges, callers and callees.
Cross-check decompiler output against instructions, especially conditionals and
closure captures. Unsupported instructions or imports are explicit proof limits.
Pin package/entry/raw/decoded hashes; retain derived analysis and narrow source
slices without copying whole installed packages into the repository.

## Milestone B — Treasure status, actual producers to host result

Trace the original host call and every consumed result field/default/error path.
Then trace current player/global counters, reset/timing sources, claim-info objects,
UI consumers, message constructors, response handlers and state updates.

Starting current candidates:

- `Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac`;
- `DataCenter/RadarCenterDataManager/DetectEventGetTreasureClaimInfo.luac`;
- `DataCenter/RadarCenterDataManager/DetectEventTreasureClaimPlayerInfo.luac`;
- relevant radar/player/world-point producers and UI controllers discovered from
  real callers, not only the prior candidate list.

Test competing hypotheses: global/local player budget versus per-target info;
cached versus refreshed state; nil versus zero; server/day reset and identity.
Do not assume no-argument status requires a batch of target queries. Build a
field-by-field input/producer/result table with instruction-level locators.

## Milestone C — Treasure direct-claim request and acknowledgement

Trace current world-point UI entry/eligibility through the exact request builder,
network-response handler, correlated push, model updates and terminal observation.
Start with `DetectEventClaimTreasureMessage`, `PushDetectTreasureClaimMessage`,
`UIWorldPointBtn`, actual caller controllers and affected data managers.

Recover target/server/type identity, serialization types, eligibility and limits,
game-error versus transport-error paths, duplicate and late observations, reward
updates, and what actually establishes a completed claim. An emitted request is
not an acknowledged or completed action. Absence of an original provider name is
not an impossibility proof.

## Milestone D — scout lane and complete claim orchestration

Trace actual `LaunchScout`, `SCOUT_SUPPLIES`, formation/queue/march ownership,
availability, return/replacement, and corresponding callbacks/state transitions.
Locate bodies via callers and references across the package, not arbitrary module
name guessing. Establish which original host-selected records/scopes/lucky order
map to direct claims versus scout work, and each aggregate result counter.

Separate exact original host behavior from explicitly justified current-client
adaptation. A different internal composition is permitted only when it reproduces
the established observable contract without guessing missing product semantics.
Record every remaining ambiguity and its exact missing producer/result edge.
Do not repurpose the accepted read-only Treasure inspection lane into an action
lane. Preserve its prohibition of claim/scout/march calls.

## Milestone E — Ghost pre-persist preparation

Trace original schedule input -> protected preparation call -> returned rows ->
validation/persistence -> execution. Recover all observable transformations and
reject/skip/error boundaries obtainable from the exact 0.3.17 host.

Trace current Ghost task/detail/list refresh, template/eligibility, UUID/server,
timing/protection/counters and row producers through actual functions. Starting
candidates include `ActGhostreconManager`, `ActGhostreconTaskInfo`, task/template
modules, `GhostreconGetTaskListMessage` and actual UI callers. Investigate whether
preparation is local normalization, data hydration, a request, or a composition.
Keep preparation distinct from the already implemented steal executor.

Document the exact before/after row schema and persistence timing. Do not infer
the preparation algorithm from the existence of `CheckCanSteal` alone.

## Milestone F — executable oracles and gated production implementation

Run selected actual decoded function bodies in a compatible isolated Lua process,
with explicit data/Unity/message stubs and all real IO/network/process/game access
unavailable. Capture constructed messages and callbacks as inert observations.
If execution genuinely cannot work, document the exact tool/format failure and
provide a verified instruction/dataflow proof for the blocked function; never
label a rewritten clone model as execution of the original bytecode.

For every recovered provider field/branch, distinguish source execution, static
proof, clone policy and unresolved assumptions. Use those independently recovered
oracles to exercise actual production handlers and boundaries.

When a method's gate is satisfied, implement it in the canonical provider and
separate action lane with the established request/result correlation, session,
profile, lease, cancellation/replacement, retirement and cleanup semantics. Never
enable the real provider path for a partial contract. Do not add alternate UI,
automatic fallback, placeholder success or blind sends. Pure internal helpers may
be added for proven subcontracts while the incomplete public method stays fenced.

For a still-blocked method deliver the first exact unresolved edge, inspected
function/PC ranges, competing interpretations, disconfirming evidence, exhausted
local source paths and minimum evidence that would settle it. A name/token search
or a rerun of old passing suites is insufficient to close this milestone.

## Milestone G — adversarial complete-boundary proof

Add distinguishing tests that use actual production services/provider boundaries
and controlled inert transport. Test applicable wrong UUID/server/profile,
duplicate/out-of-order acknowledgements, stale push, zero/nil/malformed fields,
partial batch failure, cancellation, disconnect/restart and replacement, without
inventing original timeouts/retry policies. Exercise scout-queued versus claimed
counts and Ghost prepare-before-persist only where their contracts were recovered.

Show that deliberately broken correlation/aggregation/ownership implementations
are detected. Preserve immutable baseline failures. Compare expected behavior to
the independent recovered bodies/contract, not a second copy of the clone.

If production changes, run canonical frontend check/build/package verification,
Release Desktop/Checks builds, canonical Map317 and affected native/Home/Auto/
runtime ownership/restart checks. Use maintained current harnesses, preserve
historical extractors and negatives. If no production changes, run applicable new
semantic evidence/tool tests and focused seams; explain skipped build gates.
Use isolated mounted/headless packaged controls if wiring changes; never bring a
visible app/window to the foreground. Do not manufacture large test counts.

## Milestone H — review, cleanup and single delivery

Review the full diff yourself: regressions, missing fields, edge cases, incorrect
assumptions, ownership and intended-design conflicts. Resolve every valid finding
and rerun affected distinguishing checks. Work alone; no optional delegated audit.

Create a dated worker review, machine-readable per-method contract matrix and
repeatable current evidence validator. Update Home/Map masters, feature ledger,
parity matrix and handoff conservatively. Preserve UIUX acceptance and R3 fixes.
Use EXACT_BYTES / EXACT_CONTRACT / IMPLEMENTED_NOT_VALIDATED / UNKNOWN / BLOCKED;
no LIVE_PROVEN upgrade from this campaign.

Checkpoint A-H coherently: review diff, run applicable checks, commit/push, verify
direct remote SHA and immediately continue ready assigned work. Keep durable
queue/progress/continuation, exact commands/results and negative attempts. Before
ending, clean only verified task-owned temporary roots/processes/listeners; prove
owner state and prior evidence were preserved. Never force-push/reset unrelated work.

Return once at **AWAITING_REVIEW**, with each method's actual state, newly recovered
semantics, production changes, oracle proof limits, meaningful inverse tests,
commits/direct remote SHA, cleanup and exact remaining dependency. If interrupted,
leave a runnable continuation; stopping because a milestone passed or elapsed time
alone is not completion. If all remaining branches genuinely block, deliver their
precise blockers rather than waiting for live permission. Final acceptance stays
with the project lead; live pilot remains ON_HOLD_BY_OWNER.
