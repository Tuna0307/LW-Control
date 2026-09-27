# R8-096 — recover multi-entitlement capacity ownership and refresh boundaries

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** host-side multi-entitlement service endpoints, shared entitlement state, `maxProfiles` normalization/consumption, event publication and explicit/automatic refresh ownership. No credentials, private keys or live service calls are used.

## Result

R8-096 closes the previously unknown source/policy for the profile capacity consumed by `profile_create`, `profile_enable_set` and the shared profile-list state.

Original LWBridge does not treat the raw service `maxProfiles` value as an arbitrary quota. The common entitlement parser normalizes it exactly:

- raw `2` -> `2`;
- raw `5` -> `5`;
- every other raw value -> `1`.

The normalized byte is stored in the entitlement source state and later projected as public/shared `maxProfiles`. The shared capacity extractor consumes exactly the public entitlement state's byte at offset `+0xA8`.

This aligns with R8-063, which independently proved that the profile-capacity mutation core accepts only `1`, `2`, or `5`.

## Public commands and service endpoints

The retained frontend and original host expose exact commands:

- `multi_entitlement_get`
- `multi_activate`

`multi_entitlement_get` is handled by `0x14018C614-0x14018CC4D` and performs a real refresh through the common fetch future `0x1400E993A-0x1400E9CD2`.

That future uses exact endpoint:

`/api/multi/entitlement`

and parses the response through the common `EntitlementResponse` parser `0x14023420C-0x14023463B`.

`multi_activate` is handled by `0x14014884F-0x14014964C`, reads exact request field `licenseCode`, calls:

`/api/multi/activate`

and routes the successful response through the same common `EntitlementResponse` parser.

The activation path retains exact `INVALID_LICENSE` vocabulary. R8-096 does not make a real activation request or use a real license code.

## EntitlementResponse normalization

R8-091 recovered exact service response fields:

`planCode, maxProfiles, expiresAt, accountExpiresAt, serverTime`.

R8-096 closes the important capacity transform inside the parser:

```text
raw == 2  -> 2
raw == 5  -> 5
otherwise -> 1
```

The exact branch is at `0x140234488-0x1402344A3`. The normalized byte is written to the source entitlement state at `+0x79`.

This means a malformed, unsupported, zero, oversized or otherwise non-2/non-5 service capacity does not propagate as an arbitrary quota. Native collapses it to one profile.

## Shared/public multi-entitlement state

The full entitlement projection has exact field vocabulary:

1. `phase`
2. `planCode`
3. `maxProfiles`
4. `expiresAt`
5. `accountExpiresAt`
6. `serverTime`
7. `graceExpiresAt`
8. `errorCode`

Public/shared `maxProfiles` is stored at exact offset `+0xA8`.

Recovered phase literals are exactly:

- `single`
- `initializing`
- `authorized`
- `grace`
- `restricted`

These are the multi-entitlement phases and remain distinct from AuthState's `checking/authorized/grace/locked/signedOut` vocabulary.

The phase projector's branch logic is source-locked, but R8-096 deliberately does not assign unsupported semantic names to each internal boolean feeding that branch.

## Public capacity projection

The projector loads the already-normalized capacity byte from the entitlement source state and normally carries it into public `maxProfiles`.

Two internal gate conditions force the public value to `1`:

- the gate associated by control flow with the initializing path;
- the gate associated by control flow with the restricted path.

The exact machine branch is:

`0x140234C82` load normalized capacity -> `0x140234D3C-0x140234D4E` conditional fallback-to-1 -> `0x140234D79` write public `+0xA8`.

R8-096 therefore records the observable rule without overnaming the internal flags:

**public maxProfiles = normalized {1,2,5}, except the two initialization/restriction gates force 1.**

## Event publication

State refreshes publish through exact event:

`bridge://multi-entitlement`

The event owner is `0x140234906-0x1402349BE`, which refreshes the shared projection and publishes the resulting entitlement state.

The original frontend subscribes to this event only while AuthState is `authorized` or `grace`. On an event, ProfileProvider re-runs profile reconciliation with the new entitlement and updates both profile state and entitlement state.

The frontend directly uses entitlement capacity for:

- account-count display;
- profile quota badge;
- create/add-account limit;
- profile capacity reconciliation.

The account dialog also displays entitlement expiry.

## Explicit refresh versus automatic subsystem

`multi_entitlement_get` is an explicit service refresh. It calls the same common fetch future used by the automatic subsystem.

A separate long-lived host subsystem at `0x1400E6E89-0x1400E902D` also calls the same `/api/multi/entitlement` fetch future.

That subsystem is not merely a refresh timer. The recovered function also contains:

- `MULTI_LEASE_RECOVERY_MISSING`;
- lease-recovery rejection handling;
- `leaseProof`;
- `multi_lease_expired`;
- repeated entitlement-state publication.

Therefore automatic entitlement refresh and per-instance multi-lease recovery are coupled in the original lifecycle.

The exact automatic refresh cadence remains **UNKNOWN** and is not guessed in this checkpoint.

## Status mirror

The host's broader status serializer separately exposes a six-field mirror:

- `multiPhase`
- `multiMaxProfiles`
- `multiExpiresAt`
- `multiServerTime`
- `multiGraceExpiresAt`
- `multiErrorCode`

This is a status/config projection and must not be confused with the full eight-field entitlement object.

## Rebuild implications

The old rebuild behavior that fixes `maxProfiles=1` is an equivalent single-profile adaptation, not original authority.

R8-096 now removes the “capacity source unknown” reason for keeping profile-capacity semantics opaque. However, production multi-entitlement is still not enabled because the original service bootstrap/authenticated request path, automatic refresh cadence and per-instance lease recovery/proof lifecycle are not yet fully represented.

Do not simply replace the rebuild constant with a guessed local value. The next implementation step must consume the recovered shared entitlement state produced by the original-equivalent service lifecycle.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_multi_entitlement_capacity.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-096-multi-entitlement-capacity.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production behavior changes in this checkpoint.
