# R9-001 — LastWar.Loader 0.3.34 start-game trust boundary

**Date:** 2026-09-28
**Specimen:** `LastWar.Loader.exe` 0.3.34
**SHA-256:** `D8140C997DB145473D30D8C0882659C72214EE8EBF8E247E9531E7A49BFA6B26`

## Current live baseline

The loader auto-updated from 0.3.29 to 0.3.34 and currently initializes successfully with IPv6 still enabled. The live UI reports `初始化完成，请登录` and the process has reached the service at `43.169.12.139:443` over IPv4. No `HKCU\Software\Musys\Auth` state exists and no `LastWar.Loader/Auth` Credential Manager entry exists.

0.3.34 changed `LastWar.Auth.json` from `https://apiv2.188666.com.cn` to `https://api.188666.com.cn`.

## Recovered StartGame body

MSVC RTTI for the internal `lambda_6` preserved the enclosing signature. The recovered signature is:

```text
bool lastwar::loader::LocalLoaderStore::StartGame(
  const std::string&,
  const std::string&,
  const std::string&,
  const RuntimeModuleRemoteFile&,
  unsigned int&,
  std::string&,
  const std::function<bool(uint64_t,uint64_t)>&,
  bool*,
  RegistryRestartContext*
)
```

The function is bounded by PE unwind metadata at RVA `0x1EA7F0–0x1EC4EC`.

The singleton passed as `this` is the global object at RVA `0x5F5440`, identifying that object as the `LocalLoaderStore` instance.

## Unique caller chain

Direct xref recovery gives:

```text
LwlExecuteCommand
  callsite 0x1B8D1B
      ↓
startGame adapter
  function 0x1AFCD0–0x1B016E
  callsite 0x1AFEC1
      ↓
protected pre-launch handler
  function 0x1AF7C0–0x1AFCCE
  callsite 0x1AF9F0
      ↓
LocalLoaderStore::StartGame
  function 0x1EA7F0–0x1EC4EC
```

`StartGame` itself has only one direct caller in the module.

## Pre-launch checks

Before `LocalLoaderStore::StartGame` is entered, the handler performs a sequence of boolean preparation/check calls. Three important helpers are unique to this handler:

- `0x186F30`
- `0x183F20`
- `0x189110`

They are invoked on the global object at RVA `0x5F5100`, which is strongly consistent with the authorization/runtime coordinator.

An auxiliary preparation call at `0x1E8FC0` uses global RVA `0x5F5340`.

The handler also calls `0x13B0F0` on the `LocalLoaderStore` singleton before entering `StartGame`.

The three security-sensitive helpers enter the executable high-entropy section `.E\"w` almost immediately. Examples of protected targets include RVAs `0x7F9547`, `0x9C9DF7`, `0xA68613`, and `0xC31D42`.

The protected section is:

```text
section: .E"w
RVA:     0x638000
size:    0x62D2F8
entropy: 7.332
flags:   executable
```

This is strong evidence that the author intentionally protected/virtualized the pre-launch authorization/materialization logic while leaving the mechanical local launch code comparatively readable.

## Local process/runtime handoff

Inside `LocalLoaderStore::StartGame`:

- `CreateProcessW` is called at RVA `0x1EB86B`.
- The creation flags include `0x404`, consistent with `CREATE_SUSPENDED | CREATE_UNICODE_ENVIRONMENT`.
- A local handoff helper at RVA `0x1BFEC0` is called before the game thread is resumed.
- A descendant path `0x1BFEC0 -> 0x3E0830` imports `OpenProcess`, `WriteProcessMemory`, and `WaitForSingleObject`.
- `WaitForSingleObject` is used with a 5000 ms timeout at RVA `0x1EBE69`.
- `ResumeThread` occurs at RVA `0x1EBEBF` only after the local handoff path succeeds.

This establishes that the loader starts the game suspended, performs a runtime/process handoff, waits for a local synchronization condition, and only then resumes the game thread.

## Local secure-startup protocol

Both 0.3.29 and 0.3.34 retain:

- `LWLOCAL-STARTUP-V2`
- `LWLOCAL-PROOF-V2`
- SHA-256
- AES-256-GCM
- explicit startup-key / nonce / encryption failure paths

The local secure-startup design therefore survived the 0.3.34 update.

## Interpretation

The readable `LocalLoaderStore::StartGame` body is downstream of the meaningful authorization decision. The authorization/runtime materialization step lives in the unique pre-launch handler and its protected helpers, not in the final `CreateProcessW` mechanics.

No authorization bypass is claimed. No login, activation, subscription, token, device identity, or license material was generated or modified during this checkpoint.
