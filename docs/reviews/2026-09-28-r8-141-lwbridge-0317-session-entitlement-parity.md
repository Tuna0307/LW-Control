# R8-141 — LWBridge 0.3.17 session / entitlement schema parity

**Date:** 2026-09-28
**Status:** RECOVERED 0.3.17 SESSION/ENTITLEMENT SCHEMA PARITY

## Goal

Continue the 0.3.17 authorization comparison below the frontend without sending an auth request, reading credential contents, or modifying the executable.

## AuthState contract remains recognizable

The 0.3.17 binary still contains the same eight public AuthState field names:

- `phase`
- `username`
- `accessRole`
- `watermarkTraceCode`
- `expiresAt`
- `lastHeartbeatAt`
- `lockedUntil`
- `errorCode`

It also retains `bridge://auth-state`, exact `STATE_UNAVAILABLE`, and exact message `authorization state is unavailable`.

Executable-section immediate-string scanning places the likely phase-builder family in a compact `0x3A6Cxx-0x3ABFxx` neighborhood containing `authorized`, `signedOut`, `checking`, `grace`, and `locked` fragments. Direct disassembly of those auth-builder instructions was rejected by the current tool layer, so this checkpoint does not claim exact native emitter addresses.

## SessionV2 schema

0.3.17 contains exact generated type text `struct SessionV2 with 7 elements` at RVA `0xD4FD2D`.

Six direct field literals remain identical to the 0.3.1 schema:

- `username` — `0xD4FCE0`
- `expiresAt` — `0xD4FCE8`
- `lastHeartbeatAt` — `0xD4FCF1`
- `graceStartedAt` — `0xD4FD00`
- `encryptedToken` — `0xD4FD0E`
- `encryptedMetadata` — `0xD4FD1C`

0.3.1 source-locks the seventh field as `version` with required value `2`. This checkpoint does not assume the exact 0.3.17 placement of that field because its literal was not located in the same direct cluster.

## EntitlementResponse schema

0.3.17 retains the generated `struct EntitlementResponse with 5 elements` type and the same five fields recovered from 0.3.1:

- `planCode`
- `maxProfiles`
- `expiresAt`
- `accountExpiresAt`
- `serverTime`

The corresponding literal cluster is around RVA `0xD54DA2-0xD54E3E`.

## Storage/configuration parity

The binary retains the legacy/v2 session and credential filenames, cleanup marker, challenge/ticket/envelope/manifest names, `LWBRIDGE_AUTH_URL`, default service URL `https://auth.songunity.com`, and `heartbeatIntervalSeconds`.

## Current live state

Current `AppData\Roaming\lwbridge` contains `auth-credentials.v2.json` but no `auth-session.v2.json`. Credential contents were not read.

Combined with R8-139's untouched baseline (`signedOut`, `profile_list -> AUTH_REQUIRED`, `multi_entitlement_get -> SESSION_INVALID`), this is consistent with no usable persisted v2 session being available to the current 0.3.17 startup path.

## Conclusion

0.3.17 preserves the 0.3.1 frontend orchestration, public AuthState vocabulary, SessionV2 type family, entitlement response schema, storage filenames, service configuration names, and runtime authorization artifact names.

The remaining high-value delta is below those schemas: exact native transition implementation and successful service/runtime material lifecycle. No authorization bypass is claimed by this checkpoint.

The pre-0.3.17 backup at `AppData\Local\Temp\lwbridge-pre-0317-20260928` has the same pattern: `auth-credentials.v2.json` is present but neither v2 nor legacy session file is present. This shows the missing persisted session predates the 0.3.17 launch and was not caused by its startup migration.
