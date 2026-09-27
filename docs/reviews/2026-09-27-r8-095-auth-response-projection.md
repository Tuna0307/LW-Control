# R8-095 — recover auth-service response projection into AuthState and SessionV2

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** successful login/register/renew/heartbeat response fields, normalization/inheritance rules, AuthState publication, and the plaintext source of SessionV2 protected token/metadata. No real credentials, tokens, encrypted session payloads, private keys, or live auth requests are read.

## Result

R8-091 through R8-094 closed command admission, the public AuthState producer, SessionV2 persistence/restore, and runtime ticket/envelope cleanup.

R8-095 closes the missing service-response-to-state mutation layer.

The recovered chain is now:

`auth service JSON -> artifact ingestion / field normalization -> shared AuthState -> SessionV2 protected persistence`

rather than a synthetic frontend role/capacity projection.

## Common login/register projector

Successful login and register service flows share one host projector:

`0x140239937-0x140239BF2`

Direct callers are:

- login future at `0x1400F20DF`;
- register future at `0x1400F3AD9`.

The corresponding endpoints are exact:

- `/api/login`;
- `/api/register`.

### token

The projector extracts exact field:

`token`

through the host string extractor at `0x14023F10C`, then replaces the service raw-token string at internal `+0x398`.

If the response field is missing, not a string, or empty, the resulting raw-token length is zero and the projector takes exact error:

`SESSION_INVALID`

This is the raw in-memory service token. It is not the SessionV2 field `encryptedToken`.

### expired

The projector looks up exact field:

`expired`

It recognizes expiration only when the JSON value has the native boolean tag and boolean value true.

When `expired=true`:

1. shared runtime cleanup `0x14023CBB8` removes `authorization.ticket` and `package-key.envelope` best-effort;
2. new ticket/envelope ingestion is skipped;
3. the login-result emitter chooses phase `locked`;
4. exact public error `ACCOUNT_EXPIRED` is installed.

When `expired` is false, missing, or not a boolean, this expiration flag is false and the normal artifact-ingestion / authorized branch is used.

This is stricter than merely treating a truthy JSON value as expired.

### identity/authorization fields

The common projector additionally reads:

- `username`;
- `accessRole`;
- `watermarkTraceCode`;
- `expiresAt`.

It also applies the response's optional `heartbeatIntervalSeconds` through the R8-093 service-config helper.

The resulting projection is published by login-result emitter `0x140237F05`, then persisted through SessionV2 save builder `0x14023743F`.

## accessRole normalization

The original role normalizer `0x14023ED23` has exactly three output strings:

- `normal`;
- `premium`;
- `admin`.

Input `premium` and `admin` are recognized by exact string comparison.

Any missing, non-string, or other string value normalizes to:

`normal`

Therefore the rebuild must never infer a role from boolean premium/admin flags. The original authoritative role is a normalized string.

## watermarkTraceCode normalization

The original watermark normalizer `0x14023E72B` accepts a code only when:

- it is a string;
- length is exactly 12;
- every character belongs to exact alphabet:

`23456789ABCDEFGHJKLMNPQRSTUVWXYZ`

Missing, non-string, wrong-length, or invalid-character input normalizes to the empty string.

This value is the same normalized field later written into public AuthState and protected SessionV2 metadata.

## Renew success projection

After successful renew HTTP parsing:

1. ticket/envelope response artifacts are ingested through `0x14023C7EC`;
2. optional heartbeat interval is applied;
3. `username` is extracted through helper `0x14023F4AB`, which receives the current username slice as fallback;
4. `expiresAt` is extracted as an optional string through `0x14023DF8E`;
5. `accessRole` is normalized;
6. `watermarkTraceCode` is normalized;
7. shared `authorized` emitter `0x1402391D4` publishes the state;
8. SessionV2 save builder `0x14023743F` persists it.

Important inheritance rule:

- omitted/mistyped renew `username` retains the current username;
- omitted/mistyped renew `expiresAt` has **no prior-expiry fallback** before the authorized emitter.

The authorized emitter itself always replaces the public expiry slot with the supplied projected value. Therefore an absent/mistyped renew expiry becomes no expiry value rather than silently retaining the old expiry.

## Heartbeat success projection

Heartbeat shares the same general projection but differs for expiry.

It:

1. ingests ticket/envelope artifacts;
2. extracts `username` with existing-username fallback;
3. extracts optional `expiresAt`;
4. if heartbeat `expiresAt` is absent/mistyped, explicitly clones the current AuthState expiry from service offset `+0x338`;
5. normalizes role and watermark;
6. publishes `authorized`;
7. persists SessionV2.

Therefore heartbeat preserves prior expiry when the service omits it, while renew does not.

This difference is original behavior and must not be normalized away in a parity implementation.

## SessionV2 protected-field source

R8-093 recovered SessionV2 disk fields:

- `encryptedToken`;
- `encryptedMetadata`.

R8-095 closes their plaintext source.

### encryptedToken

The SessionV2 save builder reads the current service raw-token buffer from the `+0x398` string slot and passes its data/length members `+0x3A0/+0x3A8` to helper:

`0x14039F4C5`

Existing R7 evidence already source-locks this helper as the host `CryptProtectData` + Base64 protection path.

Therefore:

`service raw token -> host protection -> SessionV2.encryptedToken`

### encryptedMetadata

Before protecting metadata, the SessionV2 save builder constructs one JSON object with exactly two insertions:

1. `accessRole` from current normalized AuthState;
2. `watermarkTraceCode` from current normalized AuthState.

There are exactly two object-insertion calls in the save builder at:

- `0x1402374E9`;
- `0x14023758C`.

The object is serialized through `0x1405A24DB`, then protected through the same `0x14039F4C5` helper.

Therefore the exact persisted plaintext ownership is:

`{accessRole, watermarkTraceCode} -> JSON -> host protection -> SessionV2.encryptedMetadata`

It is **not** the whole auth-service response JSON, and SessionV2 does not carry a standalone public `accessRole` field.

On restore, R8-093 already proves `0x14039F5CE` decodes both protected fields and decoded metadata feeds the common role projection.

## Activation relationship

The native `auth_activate` handler does not define a third response projection format. Its recovered flow invokes the existing login future and then the existing renew future.

R8-095 therefore does not invent an activation-specific persisted/auth-state schema.

## Impact

The auth/session source gap is now materially smaller.

Source-locked:

- raw service token ownership;
- token-invalid error precedence;
- expired-account branch;
- login/register common projector;
- renew and heartbeat response fields;
- username fallback;
- different renew-vs-heartbeat expiry inheritance;
- role normalization;
- watermark normalization;
- exact SessionV2 metadata plaintext;
- protected token/metadata source.

Still open before live auth-dependent commands can be called exact:

- credential/device-key acquisition/provisioning implementation in the rebuild;
- real service request execution and safe account-session bootstrap;
- entitlement/capacity response ownership and refresh lifecycle;
- remaining service-error/retry/lockout scheduling details;
- live proof against a legitimately authorized account/session.

No production behavior changes in this checkpoint.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_auth_response_projection.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-095-auth-response-projection.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`
