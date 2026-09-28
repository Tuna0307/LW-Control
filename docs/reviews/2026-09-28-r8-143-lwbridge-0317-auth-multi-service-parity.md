# R8-143 — LWBridge 0.3.17 auth / multi-service parity

**Date:** 2026-09-28
**Status:** RECOVERED 0.3.17 AUTH/MULTI SERVICE SURFACE PARITY

## Result

The 0.3.17 binary preserves the broad 0.3.1 authorization-service architecture. Static, read-only comparison found no evidence of a new top-level licensing subsystem replacing the old password-challenge, device-bound login, authorization-ticket, package-key-envelope, and multi-entitlement paths.

## Auth service surface

0.3.17 retains the same endpoint family:

- `/api/password-challenge` — RVA `0x82E5A0`
- `/api/login` — `0x82E5F8`
- `/api/register` — `0x82E8FA`
- `/api/renew` — `0x82E630`
- `/api/heartbeat` — `0x82EA18`

It also retains the established auth request/configuration vocabulary including `passwordVerifier`, `passwordVersion`, `passwordIterations`, `passwordSalt`, device/build/auth-policy headers, and the same auth service configuration already recovered from 0.3.1.

The full strings `devicePublicKey` and `launchNonce` are not stored contiguously in either design. The old 0.3.1 inspector proved they were constructed from immediate chunks. Those same chunks survive in 0.3.17, including `devicePu`, `ublicKey`, and `launchNo` at multiple auth-request builder sites. Therefore their absence as full strings is not a protocol delta.

The numeric value `600000` also remains present in the 0.3.17 auth-code neighborhood, consistent with the previously recovered password-challenge iteration count. Direct instruction-level semantics are not claimed because the tool layer rejected auth-builder disassembly.

One native-only term, `AUTH_CHALLENGE_INVALID` at RVA `0xD55616`, was not found anywhere in the retained 0.3.1 research tree. This is classified only as **newly observed in 0.3.17**, not definitively version-new, because the original 0.3.1 executable is no longer available for a complete binary search.

## Multi-entitlement surface

0.3.17 retains the old multi-license service architecture:

- `multi_entitlement_get`
- `/api/multi/entitlement`
- `multi_activate`
- request field `licenseCode`
- `/api/multi/activate`
- `bridge://multi-entitlement`

The same public entitlement vocabulary remains present: `phase`, `planCode`, `maxProfiles`, `expiresAt`, `accountExpiresAt`, `serverTime`, `graceExpiresAt`, and `errorCode`.

The same phase set is present: `single`, `initializing`, `authorized`, `grace`, and `restricted`.

The old status-mirror and lease vocabulary also survives:

- `multiPhase`
- `multiMaxProfiles`
- `multiExpiresAt`
- `multiServerTime`
- `multiGraceExpiresAt`
- `multiErrorCode`
- `MULTI_LEASE_RECOVERY_MISSING`
- `leaseProof`
- `multi_lease_expired`
- `/api/multi/leases/`
- `/release`
- `/heartbeat`
- `SERVICE_UNAVAILABLE`

This closely matches R8-096's 0.3.1 contract.

## Live relationship

The current untouched 0.3.17 state remains:

- native AuthState `signedOut`;
- profile commands rejected with `AUTH_REQUIRED`;
- `multi_entitlement_get` rejected independently with `SESSION_INVALID`;
- no persisted v2 or legacy session file exists.

So local profile admission and server-backed multi entitlement remain separate gates, but both sit downstream of usable authorization/session material. Simply making the frontend look authorized would not satisfy the entitlement path.

## Limits

No auth or entitlement network request was made. No credential contents were read. No license/session/ticket/envelope data was synthesized or changed. No authorization bypass is claimed.
