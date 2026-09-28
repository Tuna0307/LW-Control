# R8-112 — recover native proxy pipe-to-Lua dispatch boundary

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** secure/plain native proxy handling of already-framed host→proxy messages below the recovered R7-097 command/call wire. No live game, no pipe command transmission, no auth/network work, no protected envelope-consumer analysis.

## Result

R8-112 closes the native portion of original host→proxy call dispatch.

R7-097 already proves that the host's generic Lua-call request is a version-1 `command` envelope whose payload contains:

```text
id
kind = "call"
fn
args
createdAt
```

R8-112 proves the native xLua proxy **does not inspect or route `payload.fn`**.

Instead, both verified proxies drain 32-byte string records and deliver every record to one fixed Lua global:

```text
XluaBridgeHandlePipeMessage
```

At `0x1F940-0x1F94D`:

- `r8 = rbx` — address of the 32-byte message string object;
- `rdx = "XluaBridgeHandlePipeMessage"`;
- `rcx = Lua state`;
- call helper `0x13890`;
- loop advances by exactly `0x20` bytes per record.

## `0x13890` call contract

The helper receives:

```text
(LuaState, globalName, std::string argument)
```

It resolves the named global through `xlua_getglobal`, pushes the string argument through `lua_pushlstring`, then invokes:

```text
lua_pcall(L, 1, 0, 0)
```

On failure it obtains stack `-1` through `lua_tolstring`, formats/logs the Lua error, and restores the Lua stack through `lua_settop`.

Secure resolved slots:

- `xlua_getglobal` `0x90E38`
- `lua_pushlstring` `0x90E68`
- `lua_pcall` `0x90DD8`
- `lua_tolstring` `0x90E58`
- `lua_settop` `0x90E50`

The plain proxy has the identical code contract with this slot block shifted by `+0x1000`.

## String ownership

The helper treats the queued record as the same MSVC 32-byte string layout recovered elsewhere:

- capacity at `+0x18`, SSO threshold `15`;
- logical length at `+0x10`;
- inline bytes at object base when small;
- otherwise object base contains the heap pointer.

Therefore native passes the **entire queued serialized message string** to `XluaBridgeHandlePipeMessage`; it does not extract `fn` first.

## Native vocabulary boundary

Neither verified native proxy image contains exact native literals:

```text
"kind":"call"
"fn"
```

`XluaBridgeHandlePipeMessage` itself appears once at RVA `0x721C0` and is referenced by the update loop at `0x1F943`.

This establishes the ownership split:

```text
host R7-097 command/call JSON
        ↓
native pipe/frame queue
        ↓
fixed native handoff
XluaBridgeHandlePipeMessage(serializedMessage)
        ↓
UNRECOVERED bridge Lua
        ↓
payload.fn/provider/function routing
```

## Impact on the R8-111 scratch lead

R8-111 scratch recovered the original Lua globals `__XluaBridgeLoad` and `__XluaBridgeEvalHook`, but its verifier execution was blocked and R8-111 remains **NOT PROMOTED**.

R8-112 independently proves that the host wire cannot be assumed to reach `__XluaBridgeLoad` merely by setting:

```text
payload.fn = "__XluaBridgeLoad"
```

The missing fact is precisely `XluaBridgeHandlePipeMessage`'s Lua dispatch contract: it may use an allowlist, provider table, explicit branches, global lookup, or some other routing policy. Until that original Lua source is recovered, invoking generic `call_lua` as an eval/source-loader shortcut would be an invented behavior and violates the project's no-fallback/no-guess rule.

R8-112 does **not** prove that the encrypted bridge Lua forbids `__XluaBridgeLoad`; it proves only that native code does not establish that route.

## Verification

Tool:

`tools/inspect_lwbridge_proxy_pipe_dispatch_boundary.py`

Evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-112-proxy-pipe-dispatch-boundary.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Both secure and plain proxy hashes are re-verified.

## Status

**RECOVERED DISPATCH BOUNDARY.**

Map remains **NOT WORKING**. R8-112 narrows original script-dispatch ownership but does not recover `XluaBridgeHandlePipeMessage`, its provider table/allowlist, `XluaBridgeMapScanTick`, or original Map acquisition logic.