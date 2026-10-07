# Milestone E — Ghost preparation and downstream execution

State: **current row normalization implemented as an internal subcontract; public preparation BLOCKED; unsafe Ghost scheduled execution now fails closed.**

## Before/after row semantics recovered

The current fast Ghost acquisition row already preserves task/detail/template
data including `completionTime`, `taskExpireTime`, `ownerServer`,
`stealListCount`, `protectTime` and `stealMaxTimes`.

Current v22 proves:

- `ActGhostreconTaskTemplate.CheckCanSteal`, decoded SHA
  `015d156c9fc4a3d245718669c113afb23ce04e35b44b057e9abf52d405e865c8`,
  root/child[8], lines 196-199:
  `(serverTime - completionTime) / 1000 >= protectTime`.
- `ActGhostreconManager.GetPointStealType`, root/child[28], lines 563-588,
  counts `stealList`, checks current player membership,
  `stealMaxtimes`, global `GetStealTimesFull`, protection time and show rules.

Therefore the current adaptation now projects, before 0.3.17 host validation:

- `plunderAt = completionTime + protectTime * 1000`;
- `stolenCount = stealListCount`;
- `maxStealCount = stealMaxTimes`.

The frontend may add only its nonnegative random delay to `plunderAt`. The exact
0.3.17 host validates Ghost rows before calling
`PrepareGhostPlunderTasksAsync`, then requires one prepared row per UUID and
validates again before persistence. That rules out eligibility filtering as the
preparer's observable responsibility.

`CurrentClientMap317ActionProvider.PrepareGhostPlunderRows` is an internal
source-backed verifier/identity normalizer: it rejects mutated UUID/server,
counter aliases, pre-protection timing and expiry, and otherwise clones the row
one-to-one. It is independently exercised by
`MapProviderSemanticsChecks.cs`.

## Current Ghost steal request and terminal blocker

`Net/Msgs/Ghostrecon/GhostReconStealMessage.luac`, decoded SHA
`db52842471d2d276dc374236f274214657b70d22770722f4b2883cf6a756a1a3`:

- `OnCreate`, root/child[0], lines 11-15 serializes long `uuid` and int
  `ownerServer`;
- `HandleMessage`, root/child[1], lines 18-29 gates success on no
  `errorCode`, updates rewards and calls `GhostReconStealHandler`;
- critically, that response handler does **not** consume `uuid` or
  `ownerServer`.

`Net/SFSNetwork.luac`, decoded SHA
`f9c2762ef603df799da3b502a0c63a50454f97a5a8a6949a39f5948cf0a8fc36`:

- `SendMessage`, root/child[1], lines 25-34 creates a request message with
  `NewMessage(...)`;
- incoming `HandleMessage`, root/child[2], lines 36-54, nested lines 39-43,
  creates a **fresh** `msgType:NewEmpty()` and calls that new instance's
  `HandleMessage(response)`.

Thus request-instance identity cannot correlate a scheduled Ghost result, and no
inspected current module proves an echoed Ghost UUID in the terminal response.

This disconfirmed the campaign's first attempted implementation, which had
temporarily planned to hook Ghost HandleMessage and correlate `message.uuid`.
That attempted runtime rewrite was reverted before checkpointing.

## Source-backed safety fix

The existing shared scheduled runtime accepted `kind=ghost` but then used the
Dispatch manager/message/send path. Because the actual Ghost terminal correlation
is unresolved, the production runtime now rejects Ghost in
`dispatch_plunder_runtime.begin` before manager lookup, hook installation or
transport. It returns the existing translated
`DISPATCH_PLUNDER_MANAGER_UNAVAILABLE` failure rather than sending
`DispatchSteal` for a Ghost row. Dispatch behavior is unchanged.

The full production module is exercised under isolated Lua 5.3 by
`tests/map_provider_semantics_lua_checks.py`: Ghost sends zero requests and
creates no pending/hook state, while the same runtime still performs/correlates
the Dispatch path.

## First unresolved edge

`GhostReconSteal(uuid, ownerServer) -> incoming success/error response -> exact scheduled task identity`.

Minimum evidence: a current response schema/body that proves an echoed task
identity, an authoritative serialized request/result queue that proves ordering
and single-flight ownership, or an explicitly authorized future live witness.
None is currently available offline.

Disposition:
`PrepareGhostPlunderTasksAsync` remains `GAME_PROVIDER_UNAVAILABLE`.
The row projection/normalization is implemented but is not used as permission to
enable an incomplete action lane.
