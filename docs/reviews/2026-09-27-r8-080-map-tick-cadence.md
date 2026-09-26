# R8-080 — recover outer MapScanTick cadence

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** embedded secure/plain xLua proxy pump cadence. No production scanner change.

## Result

Both verified embedded proxies use the same outer elapsed-time gate for `XluaBridgeMapScanTick`.

The proxy pump is unwind-bounded at `RVA 0x1F800-0x2014F`. At `0x1F9C5` it calls `KERNEL32!GetTickCount64` through IAT RVA `0x6F180`, so the cadence constants are milliseconds.
For Map tick:

- `0x1FB37`: subtract previous Map-tick timestamp at scheduler offset `+0x28`;
- `0x1FB3B`: compare elapsed time with `0x32` = 50 ms;
- `0x1FB3F`: skip the call when elapsed is below 50 ms;
- `0x1FB41`: store current `GetTickCount64` value back to `+0x28`;
- `0x1FB45`: load exact name `XluaBridgeMapScanTick`;
- `0x1FB4F`: invoke the shared Lua-call helper.

The timestamp is updated before the named call.
## Related pump cadence

The same pump also gates:

- `XluaBridgeInputPoll` at 16 ms using scheduler offset `+0x20`;
- `XluaBridgeNativeUpdate` at 200 ms using `+0x18`;
- `XluaBridgePoll` at 500 ms using `+0x30`.

These constants and locators are identical in the verified secure and plain proxies.

The Map gate itself contains no Normal/Fast branch. This does not prove that work performed inside `XluaBridgeMapScanTick` is mode-independent.
## Semantics and limits

The recovered 50 ms value is a **minimum elapsed-time gate serviced by the proxy pump**, not proof of an independent exact-period 20 Hz timer. If the pump is serviced late, the tick is correspondingly late.

R8-080 does not recover:

- how many logical blocks or game requests one tick advances;
- traversal order or coordinate construction;
- mode-specific work inside the tick;
- retry/backoff rules;
- point/march/removal queue drain or completion policy.

Therefore the rebuild must not equate the recovered 50 ms outer gate with complete request pacing.
## Verification

Hash-locked verifier:

`tools/inspect_lwbridge_map_tick_cadence.py`

Durable machine evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-080-map-tick-cadence.json`

Reference executable SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production scanner code changes in this checkpoint.
