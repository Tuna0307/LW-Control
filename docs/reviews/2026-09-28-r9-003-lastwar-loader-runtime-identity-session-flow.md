# R9-003 — LastWar.Loader runtime identity / launch-session flow

**Date:** 2026-09-28
**Status:** STATIC FLOW RECOVERY / VERSION-PARITY SUPPORTED

## Managed start request

The current 0.3.34 managed UI sends only:

```csharp
host.Execute("startGame", new { accountId })
```

Therefore the additional native launch arguments are resolved/produced inside `LastWar.LoaderHost.dll`.

## Native StartGame argument mapping

Recovered 0.3.34 signature:

```text
bool LocalLoaderStore::StartGame(
  const string& arg1,
  const string& arg2,
  const string& arg3,
  const RuntimeModuleRemoteFile& arg4,
  unsigned int& processId,
  string& status,
  const function<bool(uint64,uint64)>& progress,
  bool* flag,
  RegistryRestartContext* restartContext
)
```

At the unique pre-launch handler:

- arg1 is the managed `accountId`.
- arg2 is produced by protected helper B.
- arg3 is produced by protected helper B.
- arg4 is the `RuntimeModuleRemoteFile` object. It is populated by protected helper C only on the branch where the local boolean check does not already satisfy the runtime condition.
- arg5 is the process-id output.
- arg6 is the shared status/error string.
- arg7 is the runtime-download progress callback.
- arg8 is a boolean output.
- arg9 is `RegistryRestartContext`.

## Helper-B output #1

The first protected-helper-B output becomes StartGame arg2.

After the game is created suspended, arg2 is passed to function `0x1C7C20` together with:

- the process handle,
- a 60,000 ms timeout,
- the shared status/error string.

`0x1C7C20` imports:

- `GetProcessId`
- `WaitForSingleObject`
- `select`
- `accept`
- `getpeername`
- `ntohl`
- `send`
- `closesocket`

This establishes that the value participates in the local internal-client socket/session handshake.

Inside `0x1C7C20`, the arg2 string is explicitly packaged as a string view together with values parsed from the connected client/session state and passed into validation function `0x17DCD0`. Success from that validation is required for the connection path to continue.

Because the statically linked crypto layer prevents clean primitive identification from imports alone, this checkpoint does **not** assert that `0x17DCD0` is cryptographic proof verification. The evidence does support calling arg2 a **local launch-session authenticator / expected session value**, rather than a filesystem path.

## Helper-B output #2

The second helper-B output becomes StartGame arg3.

After the internal client connects, StartGame copies:

- `accountId`
- arg3

and passes them into `0x1D0CD0`, which forwards them to `0x1D06A0`.

RTTI for:

```text
LaunchSessionRegistry::Session::Impl
```

anchors the same code region.

The session-object construction at `0x1D06A0` stores:

- process ID at object offset +0x10,
- `accountId` string at +0x18,
- helper-B arg3 string at +0x38,
- process/session handles at later fields.

Therefore arg3 is **launch-session identity metadata bound to the process/account**, not a path.

An exact semantic field name is not asserted yet.

## RuntimeIdentity callback

RTTI emitted from the `LocalLoaderStore` constructor encodes a callback equivalent to:

```text
bool(
  unsigned int processId,
  const std::string& accountId,
  const RuntimeIdentity& identity,
  std::string& out1,
  std::string& out2
)
```

0.3.34 callback body:

- thunk: `0x1ECB50`
- body: `0x1EC500–0x1EC7E1`

The callback:

1. looks up local launch state by process ID;
2. validates local state under a mutex/map;
3. checks the leading string-like field of the supplied `RuntimeIdentity`;
4. requires that field length to be between 6 and 32 characters;
5. requires every character in that field to be decimal `0-9`;
6. consults additional local identity/registry state;
7. fills two output strings only after the relevant identity checks succeed.

The callback is installed during `LocalLoaderStore` construction through a `std::function` wrapper.

## 0.3.29 parity

The same callback type and structure existed in 0.3.29:

- 0.3.29 callback thunk: `0x1EC4E0`
- 0.3.29 callback body: `0x1EBE90`
- 0.3.34 callback thunk: `0x1ECB50`
- 0.3.34 callback body: `0x1EC500`

Both versions retain the same `RuntimeIdentity` callback RTTI/signature and the same 6–32 decimal-digit validation pattern.

## Interpretation

The local launch path is not simply:

```text
authenticated = true → StartGame
```

It contains a stable, process-bound internal-client identity/session protocol:

```text
accountId
   ↓
protected pre-launch materialization
   ├─ local launch-session authenticator
   ├─ process/account session identity metadata
   └─ optional RuntimeModuleRemoteFile
   ↓
CreateProcessW(CREATE_SUSPENDED)
   ↓
local runtime handoff
   ↓
internal client connects over local socket
   ↓
session/authenticator validation
   ↓
RuntimeIdentity callback
   ↓
LaunchSessionRegistry binds process + account + identity metadata
   ↓
resume game
```

No bypass is claimed. No login, session, subscription, license, device identity, executable bytes, or runtime state was modified.

## R9-004 correction: RuntimeModuleRemoteFile is mandatory on the observed path

The earlier description of arg4 as being populated by helper C only when a local runtime condition is unsatisfied was structurally correct but incomplete.

The intervening check is a constant-false stub:

- 0.3.29 `0x13C750`: `xor al, al; ret`
- 0.3.34 `0x13B0F0`: `xor al, al; ret`

Consequently protected helper C always executes on the recovered startGame path and supplies/materializes the `RuntimeModuleRemoteFile` object before `StartGame`.
