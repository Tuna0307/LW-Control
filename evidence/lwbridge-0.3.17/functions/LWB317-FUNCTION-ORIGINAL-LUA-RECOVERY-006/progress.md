# Progress — LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006

Starting HEAD/remote: `692fd40de1639261ca2c7dda7c7884c527786133`, clean.

## A — COMPLETE

Exact 0.3.17 encrypted LWBP v2 package/resource layout is hash-gated. Secure and
plain loader proxies plus bundle are identified. No standalone
package-key.envelope/bridge-scripts.dat/authorization.ticket is in the bounded
supplied roots.

Checkpoint commit:
`a9196752ac4148c7b76b04d3af6e17df4da96277`.

## B — COMPLETE

0.3.17 secure-proxy static recovery confirms:

- LWBP v2 + 22-byte build;
- whole-package SHA-256 and build binding;
- package AAD `LWBP2|<build>`;
- AES-256-GCM (32/12/16 key/nonce/tag);
- signed two-segment canonical Base64URL envelope;
- 14 pipe fields;
- ECDSA-P256 signature verification with hard-coded public key;
- device-public SHA-256 binding;
- persisted Microsoft Software KSP P-256 private key;
- NCrypt P-256 secret agreement + 32-byte TRUNCATE output;
- HKDF-SHA256 one-block derivation;
- AES-GCM encrypted 32-byte package key;
- direct decrypted module table; no compression stage;
- 1..1024 entries, `u32 nameLen/u32 sourceLen/name/source`, allowed
  `A-Z a-z 0-9 . _`, exactly one bootstrap, no trailing bytes.

No private key, runtime auth material or protected service was accessed.

## C — ACTIVE

Extract only the source-proven encrypted/resources into task-owned evidence,
verify hashes/PE validity, and build inert parser/oracle checks. Do not decrypt:
the signed envelope is not supplied and the matching persisted private key is
owner state.
