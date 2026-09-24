# Deep binary / protected package handoff — strict parity priority

**Current through:** `LWB-R8-010`, 2026-09-24.

Protected original implementation recovery is now P0 because the project goal is exact LWBridge 0.3.1 parity.

## Primary target

Recover the complete original `bridge-scripts.dat` plaintext/package contents and the exact host/proxy dispatch contracts required to understand and reproduce them.

Reference EXE:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`

SHA-256:

`2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

## Already recovered

Existing R6-039 through R6-046 evidence establishes substantial surrounding architecture:

- LWBP package version 2.
- runtime `package-key.envelope` path.
- bounded envelope reader and trailing CR/LF behavior.
- Microsoft Software Key Storage Provider.
- persisted key identity `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`.
- ECCPUBLICBLOB/ECK1 public-key structure.
- 65-byte uncompressed public form `04 || X || Y`.
- CNG agreement/KDF path producing 32 bytes.
- AES-GCM boundary with 32-byte key, 12-byte nonce and 16-byte tag.
- package-side AES consumer and `LWBP2|` / integrity / build validation markers.
- **R8-003:** exact LWBP2 binary layout: `LWBP`, version, build-ID length/build ID, 12-byte nonce, u32 ciphertext length, ciphertext and final 16-byte GCM tag.
- **R8-003:** exact AAD `LWBP2|<buildId>`, package SHA-256/build-ID validation, and package AES call ownership: original third argument is the 32-byte key; parsed nonce/ciphertext/tag are supplied directly; decrypted bytes return through the original fourth argument.
- **R8-004:** host-side key-envelope transport: response fields `packageKeyEnvelope`/`packageKeyEnvelopeExpiresAt`, runtime `package-key.envelope`, exact-two-segment token framing, canonical URL-safe Base64 first segment decoding to `LWKE1|<field1>|<expirySeconds>|...`, plus login `devicePublicKey`/`launchNonce` and auth header/endpoints.
- **R8-005:** exact client login material: persisted CNG `ECDH_P256` key `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`, 65-byte uncompressed public point encoded to 87 URL-safe/no-padding characters for `devicePublicKey`, and a reusable/generated 32-byte challenge encoded to 43 URL-safe/no-padding characters for `launchNonce`.
- **R8-006:** caller-side ownership around the still-opaque envelope consumer: its fourth output argument (`rsp+0x48`) is passed unchanged as package arg3, which R8-003 proves is forwarded as the 32-byte AES key for `bridge-scripts.dat`. The restricted consumer body remains uninspected.

## Missing chain

R8-003 closes package layout/AES ownership. R8-004 proves exact outer `LWKE1` framing. R8-005 closes the client ECDH/login side. R8-006 proves which opaque-consumer output becomes the exact 32-byte package key. The remaining critical chain is now narrower:

`decoded LWKE1 agreement field(s) -> peer public key / encrypted key material -> already-recovered ECDH/TRUNCATE helper -> 32-byte package key -> decrypt known LWBP2 ciphertext -> post-decrypt container/entries/scripts`

The exact original script handlers are then to be indexed and mapped back to UI/host services. Machine evidence: `evidence/lwbridge-implementation/2026-09-24-r8-003-lwbp2-package-layout.json`; verifier: `tools/inspect_lwbridge_proxy_package_layout.py`.

## Historical SB-79

SB-79 records one exact-target operation rejected by a previous environment. Preserve that record. Do not replay or reroute a prohibited operation merely by changing tools.

The underlying recovery goal remains active. Use genuinely distinct permitted methods: non-executable metadata, other static artifacts, exported symbols/strings, current runtime-owned files, lawful debugger/trace capabilities when available, loader behavior observable without crossing a restriction, package-format reconstruction, or other evidence-backed approaches.

Do not call the package “unrecoverable” until the permitted method space is actually exhausted and documented.

## Return criteria

A successful protected-package checkpoint must provide durable evidence for:

- decrypted package bytes or a reproducible extractor;
- package/container structure;
- per-entry names/types/compression/encryption if present;
- hashes of extracted original scripts;
- exact script handlers/commands and their contracts;
- unresolved edges clearly separated from recovered facts;
- implementation impact on the parity matrix.

Do not immediately rewrite production behavior from guesses. First preserve and document the original bytes/contracts.
