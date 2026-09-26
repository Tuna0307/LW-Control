# LWB-R8-003 — recover LWBP2 package layout and AES argument ownership

**Date:** 2026-09-24
**Scope:** original `bridge-scripts.dat` binary layout, integrity/build checks, AES-GCM AAD and package callsite ownership.

## Result

A new hash-locked read-only inspector closes a major gap left by R6-045 without touching the historically restricted SB-79 consumer.

The verified original `bridge-scripts.dat` is exactly:

```text
0x00  4 bytes   "LWBP"
0x04  u32 LE    version = 2
0x08  u32 LE    build ID length = 22
0x0C  22 bytes  build ID = 9BupJXpEgm34lybhNhbbcQ
0x22  12 bytes  AES-GCM nonce
0x2E  u32 LE    ciphertext length
0x32  N bytes   ciphertext
...   16 bytes  AES-GCM authentication tag
```

For the verified 1,172,723-byte package, ciphertext length is 1,172,657 bytes and the total-size relation is exactly `66 + ciphertextLength`.

## AAD and integrity

The package function builds AES-GCM AAD exactly as:

`LWBP2|9BupJXpEgm34lybhNhbbcQ`

The package function also computes SHA-256 over the package and lowercase-hex encodes the digest before comparing it to an expected value in its manifest/context object. It separately compares the embedded build ID to the expected build ID before decrypting.

## AES-GCM call ownership

At secure-proxy RVA `0x3DA4A`, the recovered AES helper receives:

- RCX: original package-function third argument, a 32-byte package-key vector;
- RDX: parsed 12-byte nonce;
- R8: parsed ciphertext;
- R9: parsed 16-byte authentication tag;
- stack argument 5: AAD string `LWBP2| + buildId`;
- stack argument 6: plaintext output vector.

The helper independently enforces key length 32, nonce length 12 and tag length 16 before `BCryptDecrypt`.

On success, the package function copies decrypted bytes into its original fourth argument. Its fifth argument carries error text on failure.

## Source identity

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

Embedded package:
- host RVA `0x8699E0`
- raw offset `0x868DE0`
- SHA-256 `a1e23419c25c76bee16942922a643381e0d38a857902569d927f22ef02c99c8d`

Embedded secure proxy:
- host RVA `0x987F74`
- raw offset `0x987374`
- SHA-256 `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`

The RVA/raw distinction matters: earlier asset tables store outer-host RVAs, then PE-map them to raw file offsets.

## Restriction boundary

SB-79 is preserved. This checkpoint does not disassemble, query or route around secure-proxy RVA `0x3F8E0-0x40A6D`.

All new claims come from the previously permitted package function, AES helper, SHA-256 helper and immutable package bytes.

## Validation

- `python tools\inspect_lwbridge_proxy_package_layout.py ..\LW\lwbridge-0.3.1.exe --output evidence\lwbridge-implementation\2026-09-24-r8-003-lwbp2-package-layout.json` — PASS.
- `python -m py_compile tools\inspect_lwbridge_proxy_package_layout.py` — PASS.
- machine evidence parses as JSON.
- no production behavior is enabled by this checkpoint.

## Remaining P0 seam

The package format is no longer the primary unknown. The next key gap is the independent `package-key.envelope` path:

`envelope grammar -> peer public key / encrypted package-key fields -> agreement helper 0x3DD00 -> 32-byte package key`

Once that 32-byte package key is recovered lawfully from original evidence, the now-recovered package layout gives every remaining AES-GCM input required to decrypt and preserve the original script plaintext.
