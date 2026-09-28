# R9-004 — LastWar.Loader local startup/proof boundary

**Date:** 2026-09-28
**Status:** STATIC LOCAL-PROTOCOL RECOVERY

## Constant-false pre-launch branch

The pre-launch wrapper contains a branch that appears to permit skipping protected helper C when a local boolean check returns true.

The check is actually a constant-false stub in both retained versions:

```text
0.3.29  0x13C750  xor al, al
                    ret

0.3.34  0x13B0F0  xor al, al
                    ret
```

Therefore helper C is effectively mandatory on the recovered `startGame` path in both versions.

Helper C receives the output object later passed as `const RuntimeModuleRemoteFile&` to `LocalLoaderStore::StartGame`.

This corrects the earlier interpretation that `RuntimeModuleRemoteFile` might be optional depending on a local-runtime-present condition.

## Local startup-envelope construction

Function `0x17DCD0–0x17EA37` directly references the local startup protocol constant block and uses the Windows BCrypt API through local import thunks.

Resolved BCrypt thunks:

| Stub | Import |
|---:|---|
| `0x3D9618` | `BCryptOpenAlgorithmProvider` |
| `0x3D961E` | `BCryptGetProperty` |
| `0x3D9624` | `BCryptSetProperty` |
| `0x3D962A` | `BCryptCloseAlgorithmProvider` |
| `0x3D9630` | `BCryptGenerateSymmetricKey` |
| `0x3D9636` | `BCryptEncrypt` |
| `0x3D963C` | `BCryptDestroyKey` |
| `0x3D9642` | `BCryptCreateHash` |
| `0x3D9648` | `BCryptHashData` |
| `0x3D964E` | `BCryptFinishHash` |
| `0x3D9654` | `BCryptDestroyHash` |
| `0x3D965A` | `BCryptGenRandom` |

The same constant block contains:

```text
BCrypt SHA-256 provider is unavailable.
Could not query the SHA-256 provider.
Could not calculate the local protocol authenticator.

LWLOCAL-STARTUP-V2

AES
ChainingModeGCM
ChainingMode

AES-GCM is unavailable.
Could not query the AES provider.
Could not create the local startup key.
Could not create local protocol randomness.
Could not create the startup nonce.
Could not encrypt the startup payload.

type
startup
cipher
aes-256-gcm
tag

LWLOCAL-PROOF-V2
```

Within `0x17DCD0`, direct references include the AES/GCM provider/property names, startup-key/randomness/nonce/encryption failures, and the `type=startup / cipher=aes-256-gcm / tag` serialization labels.

This supports classifying `0x17DCD0` as the local startup-envelope construction path.

## Separate protected proof path

The exact `LWLOCAL-PROOF-V2` marker at RVA `0x48D7B8` is referenced from a different function:

```text
protected function: 0xC30C1B–0xC30EE7
marker reference:    0xC30C96 -> 0x48D7B8
```

This function resides in the high-entropy protected executable section and does not decode meaningfully under linear x86-64 disassembly.

The evidence establishes that startup-envelope creation and `LWLOCAL-PROOF-V2` handling are separate code paths. The exact proof direction (generation versus verification) is not asserted without stronger evidence.

## Updated launch model

```text
accountId
   ↓
protected pre-launch helper A/B
   ↓
protected helper C
   ↓
RuntimeModuleRemoteFile materialized
   ↓
LocalLoaderStore::StartGame
   ↓
CreateProcessW(CREATE_SUSPENDED)
   ↓
construct authenticated/encrypted LWLOCAL-STARTUP-V2 envelope
   ↓
localhost internal-client session
   ↓
protected LWLOCAL-PROOF-V2 handling
   ↓
RuntimeIdentity / LaunchSessionRegistry binding
   ↓
ResumeThread
```

No bypass is claimed. No login, subscription, session, license, device identity, executable bytes, or runtime state was modified.
