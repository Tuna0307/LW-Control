# R8-105 — current-machine authentic source reachability

**Date:** 2026-09-27
**Reference contracts:** R8-005, R8-006, R8-102, R8-104
**Scope:** current-machine readiness for the original package/bootstrap path; no auth request, no private-key export, no production behavior change.

## Result

R8-105 does not recover a new protocol. It applies already recovered original contracts to the machine's current runtime state and answers whether the R8-104 assembled-source capture window is presently reachable.

The answer is **no**.

Current profile runtime root:

`C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\bridge-runtime`

Observed files:

- `authorization.challenge` — present; 44 file bytes including LF, 43 URL-safe characters after trimming, decodes to exactly 32 bytes. Its contents are not recorded in repository evidence.
- `build.manifest` — present; two segments, 64-byte decoded signature, `LWBM2` payload with 10 fields.
- `authorization.ticket` — absent.
- `package-key.envelope` — absent.
## Build identity

The `LWBM2` payload matches the exact extracted original build through the fields whose semantics are already evidenced:

- build ID `9BupJXpEgm34lybhNhbbcQ`;
- version `0.3.1`;
- host SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`;
- proxy-bundle composite SHA-256 `6a9296268ffeec941a05ae6c723469f7e740c982f70bf142f5a5c37365e21403`;
- package SHA-256 `a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d`;
- launcher SHA-256 `8f42adb9ed678445425e529cdee8f12a097c63053f9d4de314a758a8dfe362de`;
- multi-hook SHA-256 `a8b48120bbe3fb125d50c046acbd4f7c440a4d5e5775b8156fc291d01bd69d2d`.

The extracted runtime files match those package/launcher/hook hashes and the proxy-bundle JSON carries the matching composite hash.

Manifest field 9 remains deliberately uninterpreted. The manifest's 64-byte signature is structurally present, but R8-105 does not independently reverify that signature cryptographically.

## Device-key readiness

R8-005 recovers the exact persisted device-key identity:

- provider: `Microsoft Software Key Storage Provider`;
- key name: `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`;
- algorithm: `ECDH_P256`.

R8-105 opens only the provider and named key. It does not export or read private-key material.

Current result:

- provider open status: `0x00000000`;
- named key open status: `0x80090016`;
- named key open succeeded: **false**.
## Reachability conclusion

Under the already recovered proxy contract, the current machine is not ready to populate the R8-104 assembled-source window.

Two current missing prerequisites are observed:

1. the persisted ECDH device key is not currently openable under the recovered original identity;
2. `package-key.envelope` is absent.

This checkpoint classifies those as **current at-rest prerequisite gaps**, not as proof that each gap is permanently unrecoverable through the original host lifecycle. R8-106 subsequently closes the local-key ambiguity: normal login selects open-or-create mode and self-provisions the missing persisted key before public export.

`authorization.ticket` is also absent, but this checkpoint records that only as observed auth/runtime state rather than asserting it as a direct proxy package-decrypt blocker.

The present `authorization.challenge` and build-matching `build.manifest` are therefore insufficient by themselves to reach the authentic package decrypt/source-population path.

R8-105 does not create a replacement key, synthesize an envelope, guess LWKE1 fields, use credentials, contact the auth service, or launch LWBridge merely to force those materials.

## Verification

Tool: `tools/inspect_lwbridge_current_runtime_reachability.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-105-current-runtime-reachability.json`

The verifier records challenge structure without challenge contents, build identity/hash matches, artifact absence, and CNG key-open status only.

## Status

**CURRENT-MACHINE AT-REST PREREQUISITES: INCOMPLETE.**

Map remains **NOT WORKING**. R8-106 proves the missing persisted key is locally self-provisionable by authentic login; `package-key.envelope` remains absent and is the current missing package-decrypt material.