# R8-056 - fence watermark_lookup at authorization-role network boundary

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the retained `watermark_lookup` public input/network/result boundary without reconstructing owner-excluded authorization, role, session or token behavior.

## Native authority

Primary evidence:

- `watermark_lookup` handler `0x1401277C4-0x1401285CB`;
- authorization-state helper `0x1400DC58F-0x1400DC79E`;
- role gate `0x140237398-0x14023743F` (`ROLE_REQUIRED`, `accessRole`);
- optional-string field extractor `0x14023F10C-0x14023F1C0`;
- trim helper `0x1400385A4`;
- JSON-string converter `0x1402AD5D7-0x1402AD647`;
- shared HTTP request helper `0x1400F5347-0x1400F643A`;
- generic JSON result converter `0x1402BB816`;
- retained frontend wrapper `watermark_lookup({traceCode})`.

## Public input behavior

The frontend sends `{traceCode:<value>}`.

Native reads `traceCode` through its optional-string extractor. Missing, null, or non-string values become an empty string rather than an immediate `INVALID_REQUEST`. The string is trimmed and then serialized directly as JSON; there is no separate host-side nonempty validator in this path.

The outbound JSON body therefore contains the normalized `traceCode` string, including `""` when the field is absent/non-string/blank after trimming.

## Authorization and role admission

Before the service request, native awaits shared authorization state.

Unavailable state uses exact:

- code: `STATE_UNAVAILABLE`
- message: `authorization state is unavailable`

The handler then applies a role gate against authorization-state `accessRole`. When the required role is not present, native raises exact code `ROLE_REQUIRED`.

The exact role value/policy and the authorization/session/token material used by the request are owner-excluded and intentionally not reconstructed.

## Network boundary

The handler builds a JSON request for exact path:

`/api/watermark/lookup`

The shared HTTP helper owns generic service/network behavior. Native vocabulary recovered from that helper includes:

- `REQUEST_TIMEOUT`;
- `NETWORK_ERROR`;
- `SERVICE_UNAVAILABLE`;
- `SESSION_INVALID`;
- service error extraction from `/error/code` and `/error/detail`;
- JSON accept/content handling and client/device/auth-policy headers.

R8-056 does not recreate these credentials/session headers because they are part of the excluded authentication/account state.

## Success result

After a successful HTTP response, native sends the service value through the shared generic JSON converter and returns that JSON value. The host does not construct a fixed watermark DTO in this handler.

## Why no production implementation is added

`watermark_lookup` is functionally retained but its native admission/request transport is inseparable from owner-excluded authorization-role/session state. Implementing only the endpoint with guessed headers, hard-coded role access, or unauthenticated HTTP would be an observable and security-relevant deviation.

R8-056 therefore makes no runtime-code change.

## Evidence classification

- optional-string/trim `traceCode` behavior: `EXACT_NATIVE`;
- `/api/watermark/lookup` path and JSON request ownership: `EXACT_NATIVE`;
- authorization unavailable error: `EXACT_NATIVE`;
- `ROLE_REQUIRED` access-role gate: `EXACT_NATIVE`;
- generic HTTP error vocabulary: `EXACT_NATIVE`;
- generic JSON success return: `EXACT_NATIVE`;
- exact role/session/token/header material: `OWNER_EXCLUDED_UNKNOWN`;
- runtime implementation: `FENCED`.