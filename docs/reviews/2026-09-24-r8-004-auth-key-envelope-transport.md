# LWB-R8-004 — recover original key-envelope transport grammar

**Date:** 2026-09-24
**Scope:** host-side authorization/key-envelope response fields, token framing, canonical payload encoding, expiry semantics, auth endpoints and login device-key binding.

## Result

A new hash-locked read-only inspector recovers the original host transport contract around `package-key.envelope` without authenticating, reading credentials/private keys, making network requests, or entering the historically restricted secure-proxy SB-79 body.

The key envelope returned by the auth service uses:

- response field `packageKeyEnvelope`;
- companion field `packageKeyEnvelopeExpiresAt`;
- runtime artifact `package-key.envelope`;
- accepted decoded payload magic `LWKE1`.

The generic host token validator requires exactly **two dot-separated segments**. The first segment is decoded with the same canonical URL-safe Base64 engine used to re-encode and byte-compare the input. The decoded UTF-8 payload is pipe-delimited, requires at least three fields, requires field 0 to match the allowed magic, and parses field 2 as signed decimal expiry seconds, returning milliseconds.

Therefore the recovered minimum key-envelope shape is:

```text
<canonical URL-safe Base64 of "LWKE1|<field1>|<expirySeconds>|...">.<opaque second segment>
```

The host validator establishes the second segment's presence through exact-two-segment framing but does not establish its cryptographic meaning in this function. Signature/MAC semantics remain unresolved.

## Canonical Base64

The exact host engine is at RVA `0x836BF0`.

Observed config/alphabet:

```text
config bytes: 00 00 02
alphabet: ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_
```

The linked binary contains the `base64-0.22.1` general-purpose decoder source path. The wrapper decodes with this engine, re-encodes with the same engine, then length/memory-compares against the incoming first segment, enforcing a canonical representation.

## Authorization-ticket sibling format

The same validator is used for `authorization.ticket`:

- response fields `authorizationTicket` / `authorizationTicketExpiresAt`;
- accepted decoded magics are exactly `LWAT1` and `LWAT2`;
- the same exact-two-segment framing and payload-expiry grammar applies.

## Auth response ingestion

Original host function `RVA 0x23C7EC-0x23CB51` extracts both authorization-ticket fields and both package-key-envelope fields, validates their token/date expiry paths, writes/replaces the owned runtime artifacts, removes stale/invalid artifacts, and exposes the exact errors `AUTHORIZATION_TICKET_INVALID`, `AUTHORIZATION_TICKET_STORAGE_FAILED` and `KEY_ENVELOPE_INVALID`.

Pair-valid function `RVA 0x23CB51-0x23CBB8` passes the two authorization magics or one envelope magic into generic token validator `RVA 0x23E465-0x23E72B`.

## Auth API/device-key linkage

The original login flow sends these exact JSON field names:

- `username`;
- `passwordVerifier`;
- `devicePublicKey`;
- `launchNonce`.

The auth client also uses:

- default base URL `https://auth.songunity.com`, override `LWBRIDGE_AUTH_URL`;
- `/api/password-challenge`;
- `/api/login`;
- `/api/register`;
- `/api/renew`;
- `/api/heartbeat`;
- unbind and watermark endpoints recorded in machine evidence;
- headers `Accept: application/json`, `X-Device-Fingerprint`, `X-Client-Build-Id`, and `X-Client-Auth-Policy: 2`.

The password challenge is pinned to `passwordVersion=1`, `passwordIterations=600000`, and a decoded 16-byte salt.

This proves the auth exchange explicitly carries a device public-key field before the client later ingests a package-key envelope. It does **not** yet prove the exact encoding of that request value or which envelope payload field holds the peer agreement material.

## Restriction boundary

SB-79 remains untouched. No secure-proxy instruction in RVA `0x3F8E0-0x40A6D` is inspected, decoded, invoked or rerouted by this checkpoint.

## Validation

- `python tools\inspect_lwbridge_auth_key_envelope_contract.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-24-r8-004-auth-key-envelope-transport.json` — PASS.
- `python -m py_compile tools\inspect_lwbridge_auth_key_envelope_contract.py` — PASS.
- no network requests, credentials, private keys or production behavior are involved.

## Remaining P0 seam

R8-003 established every package AES-GCM input except the 32-byte key. R8-004 now establishes the outer envelope transport grammar and device-key login binding.

The remaining key-recovery seam is:

`LWKE1 decoded field(s) -> peer public key / envelope payload -> already-recovered ECDH/TRUNCATE agreement helper -> 32-byte package key -> decrypt known LWBP2 ciphertext`.

Exact decoded `LWKE1` field semantics must be recovered through a distinct permitted source such as an original envelope artifact, other non-restricted static dataflow, or another independently observable original contract.
