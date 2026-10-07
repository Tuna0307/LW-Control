# Milestone F — executable oracles and implementation gate

State: **COMPLETE offline.**

## Actual decoded-body execution

`tests/map_provider_decoded_body_oracles.py` uses the exact installed v22
package, LENC decoding, the A-proven compact Lua-5.3 header normalization and the
pre-existing isolated `lupa.lua53` runtime. No real network/game/process/files
are exposed to the selected bodies.

Passing actual decoded bodies:

1. `GhostReconStealMessage.OnCreate/HandleMessage`:
   exact uuid+ownerServer serialization, success reward/manager mutation and
   error path.
2. `ActGhostreconTaskTemplate.CheckCanSteal`:
   300-second protection boundary proves millisecond completionTime versus
   seconds protectTime.
3. `DetectEventClaimTreasureMessage.OnCreate/HandleMessage`:
   uuid/server/type serialization, source-server default, success-only
   reward/dig updates, rejection mutation fence.
4. `DetectEventGetTreasureClaimInfoMessage.OnCreate/HandleMessage`:
   uuid/source/server serialization/defaults and success-only model update.

Latest isolated run: 4 tests PASS under lupa 2.8 / Lua 5.3.

## Production-module oracle

`tests/map_provider_semantics_lua_checks.py` executes the complete production
`tools/current_overview_bridge.lua` with inert file/network/game seams.

It proves:

- a Ghost scheduled request fails before hook/manager/send and writes a terminal
  failed result with `requestSent=false`;
- that guard does not depend on the Dispatch manager;
- the Dispatch path in the same runtime still arms, sends DispatchSteal,
  correlates its authoritative `message.uuid`, terminalizes, and restores its
  hook.

Latest isolated run: 3 tests PASS under lupa 2.8 / Lua 5.3.

## C# production-boundary distinctions

`MapProviderSemanticsChecks.cs` exercises the canonical internal Ghost
normalizer against valid, delayed, counter-mutated, premature, expired and
invalid-identity rows.

The existing `CurrentClientMapBlockSourceChecks` now requires the canonical
Ghost scan projection to contain the recovered scheduler aliases.

## Implementation gate result

No public method has a complete current operation/correlation contract:

- `GetTreasureClaimStatusAsync`: BLOCKED, unchanged fail-closed;
- `ClaimTreasuresAsync`: BLOCKED, unchanged fail-closed;
- `PrepareGhostPlunderTasksAsync`: BLOCKED, fail-closed with precise reason.

Implemented source-backed subcontracts only:

- current-v22 Ghost schedule-field projection;
- strict one-to-one Ghost row normalization helper;
- scheduled Ghost execution safety fence before the previously shared Dispatch
  transport path.

The accepted read-only Treasure inspection lane was not modified into an action
lane. No fallback or fabricated success was added.
