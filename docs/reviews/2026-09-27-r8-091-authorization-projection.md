# R8-091 — recover authorization projection and correct Automation request fields

> **Follow-up (R8-092, 2026-09-27):** the shared public AuthState schema, phase emitters, `bridge://auth-state` publication, grace clock and snapshot-only `auth_state` command are now recovered. The remaining blocker is the SessionV2/secure-storage/service/entitlement source feeding that producer, not the producer shape itself.

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT / CORRECTION
**Scope:** original host-side authorization admission, role membership, entitlement response serde, and provider-request construction for retained Automation/Inspect commands. No credentials/private keys are read and no auth network request is made.

## Main correction

R8-051 and R8-068 treated the adjacent raw metadata cluster `taskoptionspremiumadmin` as evidence that generic Automation/Inspect provider requests contained authorization-derived `premium` / `admin` fields.

Direct handler tracing shows that inference was wrong.

The original provider request shapes are:

- `configureAutomationTask` -> `{task, config}`;
- `startAutomationTask` -> `{task, config}`;
- `stopAutomationTask` -> `{task, options}`;
- `inspectAutomationTask` -> `{task}`.

The four handlers contain no direct reference to the recovered `premium` or `admin` field descriptors. Those strings belong to adjacent auth/role metadata and other request metadata, not fields appended by these handlers.

This checkpoint supersedes only the old request-field claim. The handlers still await shared authorization state before game routing, so they are not yet live-unfenced.

## Shared authorization-state admission

The non-role authorization-state accessor at `0x14023DE19` accepts the normal authorized path when the phase string is exactly `authorized`.

It also accepts a `grace` path only when:

- the stored grace timestamp is positive; and
- `now - graceStartedAt < 900001` milliseconds.

If session expiry has passed, the exact recovered public code is `ACCOUNT_EXPIRED`.

When there is no usable stored authorization error/state, the helper constructs exact fallback code `AUTH_REQUIRED`.

This is the original host admission policy used beneath the shared future reached by retained commands. It does not yet recover the producer that loads/renews/decrypts the state.

## Role admission

The shared role gate reads the authorization-state role member at fixed offset `+0x310` and compares it against a caller-provided list using exact string membership.

Failure uses exact code:

`ROLE_REQUIRED`

with field vocabulary `accessRole`.

For `watermark_lookup`, the recovered caller policy is:

1. an initial allowed-role set exactly `["premium", "admin"]`;
2. a later explicit `["admin"]` gate in the same handler before the lookup request.

This proves the role values are strings used for admission; it does not justify inventing a role in the rebuild.

## Entitlement response

Original serde metadata declares:

`struct EntitlementResponse with 5 elements`

The exact five field descriptors are:

- `planCode`;
- `maxProfiles`;
- `expiresAt`;
- `accountExpiresAt`;
- `serverTime`.

This closes the response shape used by the entitlement/capacity path. It does not yet close the service request lifecycle or local cache/renew rules that produce the current entitlement.

## SessionV2

Original serde metadata declares:

`struct SessionV2 with 7 elements`.

Six direct field names are source-locked:

- `username`;
- `expiresAt`;
- `lastHeartbeatAt`;
- `graceStartedAt`;
- `encryptedToken`;
- `encryptedMetadata`.

The seventh serde field is not directly identified by this pass and remains `UNKNOWN`. It must not be guessed as `accessRole` merely because that vocabulary is nearby in `auth.rs`.

## Rebuild implications

The current rebuild still does not have a native authorization-state producer. Its frontend local provider presently uses placeholder `accessRole: "normal"`, and profile entitlement is not original authority.

Therefore R8-091 does **not** wire Automation/Inspect or role-gated commands live yet.

The corrected blocker is now:

`persisted/auth service state -> original authorization-state producer -> accessRole / entitlement projection -> retained command admission`

rather than:

`invent premium/admin provider request booleans`.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_authorization_projection.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-091-authorization-projection.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production behavior changes in this checkpoint.
