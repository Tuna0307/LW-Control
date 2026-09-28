# R8-115 — recover legitimate package-key-envelope lifecycle trigger

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED LIFECYCLE SYNTHESIS
**Scope:** cross-check the already hash-locked host contracts that create, refresh, delete, and later consume the runtime package-key envelope. No credentials, network requests, private-key export, protected proxy-envelope-body inspection, or production behavior change.

## Result

The authentic source-recovery prerequisite is no longer an unspecified "auth state". The original host has three successful response paths that can populate or replace `package-key.envelope`:

1. login/register through the common projector `0x239937-0x239BF2`, whose normal branch calls artifact ingest `0x23C7EC` at `0x239A11`;
2. renew, which calls the same artifact ingest at `0xF4D3B`;
3. heartbeat, which calls the same artifact ingest at `0xF6980`.

R8-004 proves those are the only direct `.text` callers of artifact ingest and that the corresponding service endpoints include `/api/login`, `/api/register`, `/api/renew`, and `/api/heartbeat`.

## Fresh-login trigger

Normal login is the clean first-envelope route on a machine with no persisted device key. R8-106 proves login selects device-key operation `0`: open-or-create the persisted `ECDH_P256` key, then export its public point. R8-005 proves that exported point becomes the request field `devicePublicKey`; the login request also carries `launchNonce` from `authorization.challenge`.

A successful non-expired login response enters the common projector, ingests `authorizationTicket` plus `packageKeyEnvelope`, publishes `authorized`, and saves SessionV2. `expired=true` instead cleans ticket/envelope and publishes locked `ACCOUNT_EXPIRED`.

## Existing-session refresh trigger

The centralized startup/session supervisor restores SessionV2. R8-106 proves supervisor key mode `1` is open-existing-only. Successful renew and heartbeat responses both re-enter the same artifact-ingest owner and therefore can replace the runtime ticket/envelope with newly returned values.

Heartbeat service cadence defaults to 120 seconds, with configured `heartbeatIntervalSeconds` accepted only in the inclusive range 30..300 seconds. This is a refresh opportunity, not proof that every heartbeat response necessarily changes the envelope value.

## No-session behavior

No usable SessionV2 is not a source-population path. R8-093/R8-094 prove the supervisor best-effort deletes `authorization.ticket` and `package-key.envelope`, then publishes normal `signedOut`.

R8-105 directly proves the current machine has `authorization.challenge` plus matching `build.manifest` while `authorization.ticket` and `package-key.envelope` are absent. That state is insufficient for the authentic package/source path. Separately, if the supervisor restores no usable SessionV2, its recovered behavior is cleanup + `signedOut`, not envelope creation.

## Transition into authentic package/source processing

Once a legitimate host lifecycle has produced a valid runtime envelope, the next authentic proxy bootstrap/package refresh remains the direct Map-source route. R8-104 pins the bootstrap call at `0x153E2`, the nonzero assembled-source gate at `0x1547A`, compiler read at `0x15A08-0x15A3E`, and source zeroization at `0x16183-0x1618A`. R8-114 pins the typed file inputs used by that package/auth path.

Therefore the practical order is:

`fresh login OR valid-session renew/heartbeat -> ticket/envelope artifact ingest -> authentic proxy package/bootstrap -> assembled Lua source -> XluaBridgeMapScanTick`.

The remaining unresolved step is operational reachability of a **legitimately authorized** original host/session. This checkpoint does not perform that login, contact the service, fabricate account/session state, synthesize envelope/key material, or cross the protected proxy consumer boundary.

## Current-machine decision

Do not launch the game merely to test source recovery while SessionV2/ticket/envelope remain absent. A live launch becomes useful only after a legitimate original host lifecycle has populated the required runtime artifacts, at which point return immediately to the R8-104 source-capture target.

## Status

**MAP: NOT WORKING. HOME: NOT WORKING. WHOLE LWBRIDGE: NOT READY.**

R8-115 narrows the next action from "find some way to obtain an envelope" to the exact authentic lifecycle choices above. It does not recover `XluaBridgeMapScanTick` and does not count as live proof.
