# R8-093 — recover SessionV2 persistence and restore source

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Status:** RECOVERED CONTRACT
**Scope:** host-side session persistence, v2 restore precedence, version admission, secure-storage handoff, restored authorization projection, and auth-service configuration. No real credentials, encrypted session payloads, device keys, private keys, or live auth requests are read or executed.

## Result

R8-091 left the seventh `SessionV2` field unknown. R8-092 then recovered the public AuthState producer but correctly left its persisted/service source open.

R8-093 closes most of the persisted-session side of that source.

The seventh SessionV2 field is exact:

`version`

The original v2 session is serialized with:

`version: 2`

and the restore path performs a semantic version gate. A deserialized SessionV2 whose version byte is not exactly `2` is rejected through the `SESSION_INVALID` path.

This is a correction to R8-091's “seventh field unknown” statement.

## Exact SessionV2 structure

The typed SessionV2 object is 0x88 bytes.

The original serializer writes exactly seven fields in this order:

| # | Field | Typed object offset |
|---:|---|---:|
| 1 | `version` | `+0x80` |
| 2 | `username` | `+0x00` |
| 3 | `expiresAt` | `+0x48` |
| 4 | `lastHeartbeatAt` | `+0x60` |
| 5 | `graceStartedAt` | `+0x78` |
| 6 | `encryptedToken` | `+0x18` |
| 7 | `encryptedMetadata` | `+0x30` |

The save builder explicitly writes byte value `2` into the typed object's version slot before invoking the SessionV2 serializer at `0x14023EB78`.

The generated seven-field deserializer is reached through the startup wrapper at `0x1402A812E`, which calls the generated SessionV2 serde body at `0x140315E6D`.

After successful deserialization, the restore path checks the copied version byte against exact value `2`. Any other version follows the `SESSION_INVALID` route.

## Storage paths

The auth-service constructor creates path ownership for both legacy and v2 storage:

- `auth-session.json`;
- `auth-credentials.json`;
- `auth-session.v2.json`;
- `auth-credentials.v2.json`;
- `device-key-cleanup.pending`;
- `authorization.challenge`;
- `authorization.ticket`;
- `package-key.envelope`;
- `build.manifest`.

These names are service-owned host paths, not frontend state.

## Restore precedence

The centralized restore function at `0x140236567` first uses the service object's active v2-session path slice at internal offsets `+0x100/+0x108`.

Its precedence is:

1. test the v2 session path;
2. if present, read and deserialize the v2 session;
3. if the v2 path is absent, test the legacy session path at `+0xC0/+0xC8`;
4. use the legacy restore/migration path when that older file is present;
5. if there is no usable restored session, the supervisor resets the grace clock and publishes normal `signedOut`.

The exact legacy migration internals below that fallback remain open; R8-093 does not guess them.

## Centralized startup ownership

The SessionV2 restore function has one direct host caller in the recovered code:

the heartbeat/session supervisor at `0x1400F6F41-0x1400F7F47`.

The supervisor calls restore at `0x1400F7C08`.

This means disk/session reconstruction is centralized. Retained commands consume the produced authorization state; they do not individually load/decrypt the persisted session.

When the restore result reports no usable session, the same supervisor:

1. resets the internal grace clock through `0x14023CBB8`;
2. publishes the normal signed-out state through the R8-092 emitter `0x14023855D`.

## Encrypted token and metadata boundary

The v2 restore path does not store `accessRole` as a standalone SessionV2 field.

Instead:

- the persisted `encryptedToken` is decoded through host helper `0x14039F5CE`;
- persisted `encryptedMetadata` is decoded through the same host helper;
- decode failure on either restored item is normalized to exact `SESSION_INVALID`;
- successful decoded metadata is parsed and then supplied to the common authorization-role projection at `0x14023B9EB`;
- persisted identity/timing state accompanies that projection.

Therefore the recovered ownership is:

`SessionV2 identity/timing + decoded auth metadata -> common role/authorization projection -> public AuthState`

rather than:

`SessionV2.accessRole -> public AuthState`.

This materially narrows the remaining implementation work: do not add a synthetic role field to the persisted session.

## Persistence path

The current v2 save path uses:

- typed SessionV2 builder;
- exact serializer `0x14023EB78`;
- active v2-session path;
- shared host filesystem helper `0x14023D8FC`.

The helper ensures/uses the target's parent and performs the file write lifecycle. R8-093 source-locks this helper ownership but does not assign unsupported atomicity guarantees to every internal filesystem call.

## Auth service configuration

The same service constructor recognizes exact environment variable:

`LWBRIDGE_AUTH_URL`

and embeds default service URL:

`https://auth.songunity.com`

R8-093 records this only as original service configuration. No network call is made by the research checkpoint.

## Heartbeat interval configuration

The auth service defaults its heartbeat interval to:

`120 seconds`

The recovered `heartbeatIntervalSeconds` configuration is accepted only when the JSON value is an integer in the exact inclusive range:

`30 <= heartbeatIntervalSeconds <= 300`

Values outside that range do not replace the 120-second default through this helper.

This corrects an exploratory off-by-one wording during R8-093; the upper bound is 300, not 301.

## Lower-level secure/device-key error vocabulary

The host keeps distinct lower-level error vocabulary including:

- `SECURE_STORAGE_UNAVAILABLE`;
- `DEVICE_KEY_MISSING`;
- `DEVICE_KEY_MISMATCH`;
- `DEVICE_KEY_UNAVAILABLE`;
- `KEY_ENVELOPE_EXPIRED`.

Those codes remain relevant to secure-storage/device-key setup and service paths.

The restored SessionV2 token/metadata decode boundary described above does not expose those low-level decode failures directly; it normalizes failed restored material through `SESSION_INVALID`.

R8-093 does not reconstruct the low-level key algorithm and does not inspect a live key.

## Remaining auth-source work

R8-093 removes these blockers:

- unknown SessionV2 seventh field;
- unknown SessionV2 version value;
- unknown version admission;
- uncertainty over v2 versus legacy session precedence;
- uncertainty over session-restore owner;
- uncertainty over whether `accessRole` is directly persisted in SessionV2;
- uncertainty over auth heartbeat interval default/bounds.

Still open before any auth-dependent action can be called LIVE-WORKING:

- exact secure-storage/device-key acquisition lifecycle needed to reproduce the original decode source safely;
- legacy v1 -> v2 migration details where relevant;
- authorization ticket and package-key-envelope lifecycle;
- exact login/renew/heartbeat service response-to-session mutations beyond the producer transitions already recovered;
- entitlement/capacity persistence and refresh ownership;
- rebuild implementation and then real current-client/live validation.

No credentials, roles, entitlement, capacity or premium/admin state should be invented.

## Verification

Hash-gated verifier:

`tools/inspect_lwbridge_session_v2_source.py`

Machine-readable evidence:

`evidence/lwbridge-implementation/2026-09-27-r8-093-session-v2-source.json`

Reference SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

No production behavior changes in this checkpoint.
