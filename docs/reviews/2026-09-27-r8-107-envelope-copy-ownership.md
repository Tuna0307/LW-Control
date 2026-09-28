# R8-107 — close package-key-envelope post-response copy ownership

**Date:** 2026-09-27
**Reference:** verified LWBridge 0.3.1
**Scope:** host-side lifetime/persistence of `packageKeyEnvelope` after a successful auth response, plus current-machine surviving-copy checks. No auth request or secret extraction.

## Result

R8-107 proves that the successful auth response does not retain a second copy of `packageKeyEnvelope` in AuthState, SessionV2, or the auth-service object.

Inside `0x23C7EC-0x23CB51`:

- `packageKeyEnvelope` is extracted into a stack-local string at `0x23C95E/0x23C976`;
- `packageKeyEnvelopeExpiresAt` is extracted into a separate stack-local string at `0x23C97B/0x23C990`;
- after validation, the service-owned runtime path is read from `+0x1A0/+0x1A8`;
- the envelope value is formatted with the already recovered LF serialization and passed to shared persistence helper `0x23D8FC` at `0x23CA11`;
- invalid/expired material deletes that same runtime path at `0x23CA57`;
- the expiry and envelope stack locals are explicitly destroyed before return.

An exhaustive direct-memory-reference pass over this function finds exactly eight references to the auth-service object (`rdi`), all read-only. They reference only the authorization-ticket path (`+0x180/+0x188`) and package-envelope path (`+0x1A0/+0x1A8`). No envelope text is assigned into service state.

## SessionV2 exclusion

R8-107 re-verifies the exact seven SessionV2 serializer fields recovered by R8-093:

`version`, `username`, `expiresAt`, `lastHeartbeatAt`, `graceStartedAt`, `encryptedToken`, `encryptedMetadata`.

`packageKeyEnvelope` is not among them. R8-095 additionally proves that `encryptedMetadata` contains only protected `{accessRole, watermarkTraceCode}` JSON, not the auth response.

Therefore SessionV2 is not a backup source for the envelope.

## Durable ownership

Within the recovered original host contract, `package-key.envelope` is the sole post-response durable owner of the server-returned envelope token.

The persistence path uses temporary runtime material and atomic replacement, but the current profile runtime directory contains only `authorization.challenge` and `build.manifest`. A recursive filename search under Local and Roaming AppData for any name containing `package-key.envelope` found no final or temporary sibling copy.

## Other local evidence classes checked

- standard CrashDumps/WER locations contain no dump of the original `lwbridge-0.3.1.exe`; observed LWBridge dumps belong to the reconstructed test executable;
- no File History configuration is present;
- user-visible CIM shadow-copy query returned no copy; `vssadmin` requires elevation, and that permission boundary was not bypassed;
- NTFS USN metadata can be queried, but journal-content reads return `Error 5: Access is denied`; that boundary was not bypassed.

These machine observations do not prove that no inaccessible OS-level historical copy exists. They do establish that no surviving copy is available through the permitted local sources checked in this block.

## Recovery impact

The existing machine state cannot reconstruct `package-key.envelope` from SessionV2, service memory, the runtime directory, ordinary AppData temp siblings, or an original process crash dump.

A future authentic source-population opportunity therefore requires either:

- a legitimately issued new envelope through the original auth lifecycle, initiated by the owner rather than by research probing; or
- a genuinely new authentic artifact/source not currently present.

R8-104 remains the preferred later capture boundary: once authentic package/bootstrap succeeds, preserve the assembled source global rather than retaining the envelope or raw decrypted module table.

## Verification

Tool: `tools/inspect_lwbridge_envelope_copy_ownership.py`

Evidence: `evidence/lwbridge-implementation/2026-09-27-r8-107-envelope-copy-ownership.json`

Reference SHA-256: `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Status

**RECOVERED CONTRACT / CURRENT LOCAL COPY NOT FOUND.**

Map remains **NOT WORKING**. The missing local device key is self-provisionable by authentic login (R8-106), but no current envelope or alternate retained copy is available.