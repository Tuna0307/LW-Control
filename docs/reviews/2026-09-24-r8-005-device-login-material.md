# LWB-R8-005 — recover exact devicePublicKey and launchNonce material

**Date:** 2026-09-24
**Scope:** original client ECDH public-key encoding, persisted device-key identity, authorization challenge generation/reuse, and login-field binding.

## Result

R8-004 proved that the original login request contains `devicePublicKey` and `launchNonce`, but left the exact value encodings open. R8-005 closes those two client-side contracts from the immutable LWBridge 0.3.1 host.

### devicePublicKey

The original host uses:

- provider: `Microsoft Software Key Storage Provider`;
- persisted key name: `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`;
- CNG algorithm: `ECDH_P256`;
- public export type: `ECCPUBLICBLOB`.

The exported blob must be exactly 72 bytes with:

- magic `ECK1` (`0x314B4345`);
- `cbKey = 32`.

The host converts that blob to the 65-byte SEC1 uncompressed point:

`0x04 || X[32] || Y[32]`

and encodes those 65 bytes with the original URL-safe Base64 engine:

`ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_`

The engine configuration is `00 00 02`; the encoder's no-padding branch is selected, so the final `devicePublicKey` is exactly **87 URL-safe Base64 characters with no "=" padding**.

The login future then supplies this recovered value under the exact JSON field name `devicePublicKey`.

### launchNonce / authorization.challenge

The original login flow calls the challenge get-or-create function before constructing the `launchNonce` JSON field.

An existing challenge is reused only when:

- encoded length is exactly **43 characters**;
- every character is ASCII alphanumeric, `_`, or `-`.

If no valid challenge is available, the host creates exactly **32 random bytes**, encodes them through the same URL-safe/no-padding Base64 contract, and produces a 43-character value.

That challenge value is then supplied under the exact login JSON field `launchNonce`.

This matches the surviving original runtime artifact currently present on the machine:

- `C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\bridge-runtime\authorization.challenge`
- file bytes: 44 including trailing LF;
- trimmed value: 43 characters;
- trimmed value decodes to exactly 32 bytes;
- only the expected URL-safe alphabet is present.

The artifact's nonce contents are not copied into repository evidence.

## Source anchors

Reference EXE SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Important original host functions:

- device key open/create: `0x39ECBD-0x39ED6B`;
- device key create/finalize: `0x39ED6B-0x39EE94`;
- public export/encoding: `0x39EE94-0x39F03D`;
- persisted key open: `0x39F0CF-0x39F135`;
- challenge file read/validation: `0x23A111-0x23A24C`;
- challenge get/create: `0x23B408-0x23B8B0`;
- login future: `0xF1C9E-0xF2FBD`;
- Base64 encoder: `0x401E98-0x401FC4`.

Base64 engine descriptors:

- devicePublicKey: RVA `0xCA294E`;
- launchNonce/challenge: RVA `0x836BF0`.

Both carry the same `00 00 02` configuration and URL-safe alphabet.

## Impact on package-key recovery

The original auth exchange is now more tightly specified:

`persisted ECDH_P256 private key -> 65-byte uncompressed public point -> 87-char URL-safe devicePublicKey`

and

`32 random challenge bytes -> 43-char URL-safe launchNonce`.

The server then returns the `LWKE1` package-key envelope recovered in R8-004.

What remains unknown is no longer the client public-key encoding. The critical seam is now specifically the **decoded LWKE1 server/agreement fields**: which field carries the peer P-256 public point and which fields carry the encrypted package-key nonce/tag/ciphertext material.

Once the peer point is recovered, the already-proven secure-proxy helper can derive the shared 32-byte material through ECDH + `TRUNCATE`.

## Restriction boundary

No secure-proxy instruction in historical SB-79 RVA `0x3F8E0-0x40A6D` was inspected, decoded or rerouted.

## Validation

- `python tools\inspect_lwbridge_device_login_material.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-24-r8-005-device-login-material.json` — PASS.
- `python -m py_compile tools\inspect_lwbridge_device_login_material.py` — PASS.
- current surviving `authorization.challenge` independently corroborates the exact 43-char / 32-byte format.
- no authentication, network request, private-key export, or production behavior was performed.
