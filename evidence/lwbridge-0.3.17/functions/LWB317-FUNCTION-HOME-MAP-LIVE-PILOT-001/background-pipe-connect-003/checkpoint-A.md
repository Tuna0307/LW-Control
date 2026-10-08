# LWB317-FUNCTION-HOME-MAP-BACKGROUND-PIPE-CONNECT-003 — checkpoint A

Status: **DIAGNOSED / REPAIRED AT CURRENT-CLIENT PIPE ENTRY**.
Starting reviewed HEAD: `f65555116eccc6aa279bde2ab77444ea7eab2c69`.
Scope: the current-game *rebuild* adapter, not unrecovered original encrypted
Lua, account/license services or general Map behavior.

## Source-backed failing boundary

`tools/current_overview_bridge.lua`, in `load_pipe_adapter` and
`ensure_pipe_hello` (source at this commit), loads
`LWBridge.GamePipe.PipeClientAdapter.Connect` from a reflected field.
The field type is .NET `Action<string,string,string>`.
Old code entered the reflected `MethodInfo.Invoke` branch when Lua
`type(connect) ~= "function"`, then counted `pcall` success as
`hello_sent`. That label was **not** an acknowledged native call.

The first fresh actual-game diagnostic run
`diagnostic-attempt-001.json`, `receipts-001/` proves:
- xLua exposes the actual Connect delegate as `userdata`, not Lua function.
- The chosen route was `reflection`, and `pcall` returned.
- The game-side state was `hello_sent` / `clientConnected=false`;
  no `pipe-adapter-state.txt` (the state written immediately by native
  `BeginConnect`). Host native listener remained without accepted or
  rejected sessions. The unchanged original installed scripts were restored.
- Absence of the **opt-in** native receipt alone is insufficient: the
  original trace guard initially contained a duplicate trailing separator.
  The separate existing mandatory adapter-state absence and live
  route comparison remain the significant distinguishing evidence.

With no authentication, token, PID, path or host behavior altered, replaced
the reflected `MethodInfo.Invoke` branch with **one** direct delegate
invocation `connect(control.controlPipePath, hello, root)` under `pcall`.
The return of void/nil is not interpreted as a failure, so no second Connect
is invoked. The original adapter loader and its independent direct
`ReadRuntimeSnapshot` path are preserved.

## Actual counterproof of other hypotheses

Fresh same-build Last War `retest-attempt-002.json`, `receipts-002/`:
- Same xLua delegate type `userdata`, now `delegate_direct_once`.
- New actual `pipe-adapter-state.txt=connected` and Lua
  `pipe-transport.json state=connected,clientConnected=true`.
- Production host showed **one authenticated session, one connected route,
  zero rejected handshakes**; actual game profile/session/PID identity
  matched the isolated runner. Current server 2212 observed in canonical
  production Map context, but `isInWorld=false`.
- This same-root connected counterexample disfavors wrong root, stale
  packaged DLL, no native worker or a persistent native pipe-open failure.
  It does **not** prove all builds and states are supported.

Final `rpc-retest-attempt-003.json`, `receipts-003/` additionally proves
the direct delegate entered the **real game-side native adapter** with
correlated timestamp/PID/session receipts in sequence:
`begin_connect_entry`, `native_startup`, `worker_created`,
`worker_start_called`, `worker_entry`, `wait_named_pipe_success`,
`native_pipe_opened`, `hello_frame_written`, `worker_connected`.
Native `runtimeDirMatches=True`; `pipe-adapter-state.txt=connected`;
Lua reports `delegate_direct_once`, `type=userdata`, and a connected
state; production host independently confirms authenticated route.
No token/hello bytes or private material appears in receipts.

`src/LWBridge.GamePipeAdapter/PipeClientAdapter.cs` gains only optional
`LWBRIDGE_PIPE_DIAGNOSTIC=1` receipts, restricted to a child of the system
Temp root and the session-scoped overview runtime, with PID/timestamp,
known stage name and limited error class/Win32 code. It neither sends
extra traffic nor changes authentication, timeout, worker concurrency,
read grammar, existing lease/failure/Stop mechanics or production defaults.
The checks-owned runner enables this flag only within its own process.

## Negative controls and proof limits

`tests/pipe_delegate_current_checks.py`, run under existing
lupa-enabled Python, checks Lua compilation, the **single call** source
structure, callable userdata with nil return (success *exactly once*)
and exception (failure *exactly once*). These 4 tests are **inert**;
the authoritative direct-userdata behavior and native entry are the
independent real Last War receipt pairs listed above.

No original-Lua A-to-A acceptance is inferred; the current-client
internal connection mechanism is a source-backed integration repair.
The exact original protected controller remains unrecovered.
