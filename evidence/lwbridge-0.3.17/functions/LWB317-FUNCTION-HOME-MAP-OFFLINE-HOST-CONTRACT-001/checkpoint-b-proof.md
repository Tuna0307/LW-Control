# Checkpoint B — distinguishing offline production-seam proof

No production defect was demonstrated. No product source, test source, dependency,
runtime configuration, installed client file, or historical evidence was changed.
This checkpoint adds evidence only.

## Executed seams

### 1. Real packaged .NET reader / Windows ownership

Existing Release check binary:
`tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll`.

`--profile-runtime-owner-check`: **PASS**.

This check reaches `HomeRuntimeFileOwnershipChecks`, which loads the packaged
`OverviewBridge/LWBridge.GamePipeAdapter.dll` by reflection without calling
`Connect`, casts the public `ReadRuntimeSnapshot` field to
`Func<string,string>`, and invokes the real delegate. The check proves:

- exclusive Windows publication prevents partial reads/replacement;
- a valid same-owner lease blocked by publication waits and then validates;
- foreign, stale, malformed and valid-looking-plus-malformed leases are rejected;
- a real `FileShare.None` collision returns exact `busy\n`;
- contention cannot extend a previously validated lease beyond its five-second
  freshness horizon;
- missing metadata returns exact `unavailable\n`;
- pending Close/cancellation preserves foreign cancellation metadata.

The separate Python Windows ownership suite reports **4/4 PASS**.

### 2. Full production Lua ownership/result consumers

First attempt under default `python`: **environment failure**,
`ModuleNotFoundError: No module named 'lupa'`. This is preserved in
`checkpoint-b-native-checks.txt`; nothing was installed.

Retry used the already-existing RECOVERY-003 task venv:
`C:\Users\chimw\.codex\tmp\lwb317-recovery003-lua\Scripts\python.exe`,
Python 3.12.14, lupa 2.8, Lua 5.4.

`tests/home_runtime_lease_lua_checks.py`: **6/6 PASS**.

Those tests execute complete `current_overview_bridge.lua` and
`current_live_resource_probe.lua` modules with isolated temporary roots. They
distinguish busy from unavailable/error/missing, preserve queued work during a
same-owner busy interval, refuse foreign/malformed metadata, enforce the
non-extendable TTL boundary, terminalize all admitted lanes after ownership
retirement, and fence stale callbacks. Their injected read delegate is a Lua
function, so this result does **not** prove .NET/xLua marshalling.

### 3. Host/binding and retired-owner native boundaries

All executed against the existing Release check binary:

- `--map317-native-boundary-check`: **PASS**.
- `--home-campaign-lifecycle-check`: **PASS**.
- `--overview-bridge-launch-binding-check`: **PASS**.
- `--overview-bridge-lifecycle-launch-binding-check`: **PASS**.
- `--overview-bridge-host-transport-check`: **PASS**.
- `--overview-bridge-normal-composition-check`: **PASS**.
- `--map-campaign-canonical-check`: **PASS**.

Notable distinguishing results include stale/foreign runtime ownership rejection,
same-binding launch-registration refresh, unregister on successful stop/terminal
failure, different-identity listener rejection, application-owned shutdown, and
canonical Map execution with `providerMode=inert-local` and
`externalProviderCalls=0`.

## xLua conversion boundary

Checkpoint A static evidence from the exact installed
`Assembly-CSharp.rdl` establishes the current build contains the generic
one-argument/one-result `XLua.DelegateBridge.Func<TArg,TResult>(TArg)`,
delegate translator/cache methods and reflection wrapper machinery.

That fact plus the passing real .NET delegate test is deliberately **not**
promoted to an end-to-end xLua claim. The following exact operation remains
UNKNOWN offline:

`FieldInfo.GetValue(null) -> external packaged Func<string,string> -> current
game xLua value -> current_overview_bridge.lua reader:Invoke(path) -> returned
string`.

A different xLua runtime or a lupa function would not close that dependency.

## Finding disposition

- **B-01 reader signature/result grammar:** established on the actual packaged
  .NET delegate for type plus busy/unavailable outcomes; success-prefix handling
  remains source/Lua-consumer backed. Real xLua crossing remains UNKNOWN.
- **B-02 same-owner refresh contention:** actual Windows publisher/reader
  contention and no-TTL-extension behavior established offline.
- **B-03 retired owner:** native provider/lifecycle and full Lua lane
  terminalization checks pass; no stale owner gains admission.
- **B-04 nil/error/non-string consumer behavior:** production Lua module tests
  pass in the isolated harness; real xLua conversion exceptions remain part of
  the unresolved crossing above.

No demonstrated defect justified a production change or fallback. The accepted
RECOVERY-003 ownership, Stop, compatibility, Auto Launch and Auto Scan semantics
remain untouched.
