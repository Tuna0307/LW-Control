# R8-109 — close package-key-envelope host exposure surfaces

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** exhaustive exact-literal/RIP-relative host ownership for `packageKeyEnvelope`, its expiry field, runtime envelope path, and envelope-expired status. No auth request, credentials, private-key access, or protected proxy analysis.

## Result

R8-109 closes the possibility that the original host exposes the package-key envelope through a second command, IPC result, log/debug surface, or alternate persisted owner.

The exact host literal inventory is:

- `packageKeyEnvelope`: byte occurrences at RVA `0xC80114` and as the prefix of `packageKeyEnvelopeExpiresAt` at `0xC80126`; code references only from auth-response ingest at `0x23C95E` and `0x23C97B`;
- `packageKeyEnvelopeExpiresAt`: RVA `0xC80126`; sole code reference `0x23C97B` in auth-response ingest;
- `package-key.envelope`: RVA `0xC80240`; sole code reference `0x23D130` in auth-service runtime-path construction;
- `KEY_ENVELOPE_EXPIRED`: RVA `0xC80356`; sole code reference `0x23EF2C` in the static error/status mapping builder.

## Owner classification

`0x23C95E/0x23C97B` are the response-field extraction points already recovered in R8-004/R8-107. They feed the stack-local envelope/expiry values that are validated and persisted.

`0x23D130` belongs to the auth-service constructor. It builds the service's `package-key.envelope` PathBuf alongside the other runtime paths. It is not an output or logging route.

`0x23EF2C` belongs to a table-building function that assembles named auth/status errors. `KEY_ENVELOPE_EXPIRED` is therefore an error/status label, not an envelope-bearing data surface.

## Consequence

There is no separate original-host surface to retrieve the actual envelope value through:

- frontend/API command output;
- host IPC/provider result;
- log/debug message;
- status/error payload;
- alternate service-state owner;
- alternate persisted host artifact.

Combined with R8-107, the original host lifecycle is now closed as:

`auth response -> stack-local envelope -> package-key.envelope file -> local response strings destroyed`.

R8-109 therefore eliminates host-side envelope exposure as a distinct permitted recovery route. Future work should not repeat searches for an envelope-returning host command or debug surface unless genuinely new evidence changes the literal/xref inventory.

## Verification

Tool: `tools/inspect_lwbridge_envelope_exposure_surface.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-109-envelope-exposure-surface.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Status

**RECOVERED NEGATIVE SURFACE.**

Map remains **NOT WORKING**. This checkpoint removes another speculative recovery route but does not recover the envelope, package key, or original Lua source.