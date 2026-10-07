# Live attempt 3 — delegate method not surfaced by xLua

Date: 2026-10-07
Disposition: FAILED_SAFE / RESTORED

A third fresh canonical Home session ran after zero selected game/launcher
processes. Helper session was `ee746cef7c2b4280a8cd6f30bb100b6d`;
the helper-owned game PID was 34172 and launcher root PID was 8556.

The adapter bootstrap itself succeeded far enough to obtain the packaged .NET
reader field. The real game reached authenticated current-server state
(`serverId=2212`) under the isolated root, but xLua did not expose the delegate
instance method as a directly callable member. The exact heartbeat/error was:

`pipe_adapter_read_failed:DataCenter.Global.LuaEntry:423: attempt to call a nil value (method 'Invoke')`

No Map scan was started. The helper timed out, closed only game PID 34172,
restored the exact candidate backup, cleared recovery, and left zero
LastWar/launcher processes. `helper-start-cleanup.json` reports `ok=true`
and `recoveryCleared=true`.

The next correction does not guess a new protocol. It reuses the current-client
reflection pattern already present in the Resource probe: obtain the delegate
type from the reflected static field, obtain its `Invoke` MethodInfo, create a
`System.Object[]`, and call `MethodInfo.Invoke(delegate,args)`. The same
source-proven route is applied to both the one-argument runtime reader and the
three-argument Connect delegate.

No protected-original service, authentication automation, updater action,
gameplay action, cross-server movement, recurring Auto scan, claim, share, march,
combat, plunder or spend occurred.
