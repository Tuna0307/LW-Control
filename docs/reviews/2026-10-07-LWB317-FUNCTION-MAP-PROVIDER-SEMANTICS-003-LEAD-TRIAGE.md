# MAP-PROVIDER-SEMANTICS-003 — bounded blocker triage

Date: 2026-10-07
Worker checkpoint: `fb70f281bada971e55b9d0643d6b22cd1f8e7b4a`.
State: comprehensive worker acceptance remains **AWAITING_REVIEW**.

## Independent execution

Fresh headless execution passed:

- `tests/map_provider_decoded_body_oracles.py`: 4/4 under the existing task
  Python/lupa 2.8 Lua 5.3 environment;
- `tests/map_provider_semantics_lua_checks.py`: 3/3;
- `python tools/lwbridge317/validate_map_provider_semantics.py`: `ok=true`,
  reference/current package hashes and nine selected module identities matched.

The environment used for both Python test files was
`C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe`.
No game/desktop work occurred. These checks do not independently prove every
claimed original contract, production projection or blocker exhaustiveness.

## Credited bounded findings

The worker added genuine function-body inspection/execution, beyond the earlier
token searches. The Lua production diff now rejects Ghost before the shared
Dispatch manager/hook/send path. Fresh production-module tests verify zero Ghost
sends and preservation of the tested Dispatch path. This is evidence of a useful
safety correction, not evidence that Ghost execution is recovered.

The three public methods remain unavailable. These are specific action/status
dependencies, not a statement that every Home/Map feature or accepted UIUX is blocked.

## Questions still requiring review

**BT-01 — status ownership.** A no-argument public status method can, in principle,
read state established by an earlier claim call, cached inspection or an owned
controller. The lack of UUID in that individual call does not alone disprove
per-target information acquisition by the wider controller. This is an alternative
hypothesis, not permission to invent a cache or its fields. Trace exact original
frontend/host consumers and controller lifecycle before deciding which semantics
remain unavailable.

**BT-02 — preparation versus execution.** In current
`MapActionControlPlane.ScheduleDispatchPlunderAsync`, the provider preparation
returns rows which are normalized/persisted before downstream execution. Its
public signature does not itself perform a steal. Missing executor terminal
correlation therefore does not, alone, prove preparation's row contract is
unrecoverable. An end-to-end capability policy might deliberately fence both;
that policy must be labeled separately from a missing preparation algorithm.
The new strict internal normalizer is not yet accepted as exact original
preparation solely because it passes its own tests.

**BT-03 — response-field evidence.** The observed Ghost handler does not consume a
UUID and incoming messages are fresh objects. This justifies rejecting a design
which assumes request-instance identity. It does not prove the raw response has
no unconsumed field or that all lower transport correlation/queue paths are absent.
Require schema/transport evidence for those broader absence claims. Do not enable
the executor based on an unproven alternative.

## Continuation

Assign a read-only boundary audit through
`LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004`. Produce a separate semantic versus
capability versus live-proof matrix. Preserve the Ghost guard, existing provider
fences, historical evidence, UIUX/R3 acceptance and owner live hold. No provider
enablement or global acceptance is authorized by this triage.
