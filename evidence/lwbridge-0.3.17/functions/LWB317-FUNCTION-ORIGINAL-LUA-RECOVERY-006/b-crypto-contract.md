# Checkpoint B — 0.3.17 extraction, crypto and integrity contract

State: **COMPLETE**.

This checkpoint revalidates the historical 0.3.1 hypotheses against the exact
0.3.17 secure proxy identified in A:

- secure proxy SHA-256
  `AE8BBA866DF80E9C924923D1F59D49E824C784C8BA9B47780FE2F58305E00A86`;
- fixed reference EXE SHA-256
  `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

The machine-readable contract is `b-crypto-contract.json`. Bounded disassembly
extracts used to derive it are retained as `b-*.txt`.

## Package contract

The package decrypt body is secure-proxy RVA `0x3E800-0x3F295`.

The outer format is source-confirmed:

1. `LWBP` magic.
2. little-endian u32 version, exactly `2`.
3. little-endian u32 build-id length, exactly `22`.
4. 22 build-id bytes.
5. 12-byte AES-GCM nonce at package offset `0x22`.
6. little-endian u32 ciphertext length at offset `0x2E`.
7. ciphertext at offset `0x32`; implementation bounds it to
   `1..0x1000000`.
8. final 16-byte GCM tag.

Before decrypt, the function computes SHA-256 over the complete encrypted
package, hex-encodes it with lowercase `0123456789abcdef`, and compares it
against the expected digest supplied by the validated envelope context. It also
compares the package's 22-byte build identifier against the envelope-derived
expected build.

Package AES-GCM AAD is exactly:

`LWBP2|<22-byte build id>`

The AES helper is RVA `0x3E100-0x3E58A` and enforces:

- 32-byte key;
- 12-byte nonce;
- 16-byte tag;
- BCrypt `AES`;
- BCrypt `ChainingModeGCM`;
- `BCryptGenerateSymmetricKey`;
- `BCryptDecrypt`.

## Envelope contract

The envelope consumer is RVA `0x40E80-0x4200D`.

The token has exactly two canonical Base64URL segments separated by one dot.
The decoded second segment is exactly 64 bytes. RVA `0x43E50-0x440D8`
verifies it as an ECDSA-P256 signature over SHA-256(decoded first segment) using
the hard-coded public point recorded in `b-crypto-contract.json`.

The decoded first segment splits on `|` into exactly 14 fields:

- field 0: literal `LWKE1`;
- fields 1/2: decimal u64 validity-window values; static code enforces bounded
  current-time relationships including +300 and <=1200-second constraints;
- field 3: exactly 22-character build id;
- field 4: exactly 64 lowercase hexadecimal characters;
- fields 5/6/7: nonzero decimal u64 context values. Their caller bindings are
  source-visible, but no safe semantic names are assigned here without a
  definitive static name;
- field 8: Base64URL => exactly 32 bytes;
- field 9: Base64URL => exactly 65 bytes and byte 0 must be `0x04`;
- field 10: Base64URL => exactly 32 bytes;
- field 11: Base64URL => exactly 12 bytes;
- field 12: Base64URL => exactly 32 bytes;
- field 13: Base64URL => exactly 16 bytes.

The implementation opens the persisted local CNG key using:

- provider: `Microsoft Software Key Storage Provider`;
- key name: `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`.

This audit did **not** read or export that owner-state private key.

Field 8 is compared in constant time against SHA-256 of locally exported device
public material. Field 9 is imported as an uncompressed P-256 peer point and is
used with the persisted private key in `NCryptSecretAgreement`. The CNG
agreement helper (RVA `0x3F2A0-0x3F5BE`) invokes
`NCryptDeriveKey(..., "TRUNCATE", ...)` and requires a 32-byte raw output.

The key-derivation helper at `0x40210` performs a one-block HKDF-SHA256 shape:

- extract: HMAC-SHA256 with field 10 as key/salt over the 32-byte agreement
  output;
- expand: HMAC-SHA256 with the PRK as key over
  `canonicalInfo || 0x01`;
- output: exactly 32 bytes.

The canonical info/AAD string is constructed from validated envelope fields in
this observed order:

`field0 | field3 | field5 | field6 | field7 | field2 | field8`

using literal `|` separators.

The resulting 32-byte HKDF output is the AES-256-GCM key used to decrypt field
12. Field 11 is the nonce, field 13 is the tag, and the same canonical info is
used as AAD. Successful envelope decryption must return exactly 32 plaintext
bytes: the package AES key. The validated context also exports field 3 as the
package build id and field 4 as the expected package SHA-256.

## Plaintext format: no compression layer

After package AES-GCM succeeds, the loader passes the plaintext directly to
module-table parser RVA `0x127D0-0x13620`. No gzip/zlib/zstd/other decompressor
exists in this recovered path.

The plaintext starts with a little-endian u32 module count constrained to
1..1024. Each entry then contains:

`u32 nameLength | u32 sourceLength | name bytes | source bytes`

Module names must be nonempty and use only:

`A-Z a-z 0-9 . _`

Exactly one entry named `bootstrap` is required. A duplicate bootstrap is
rejected; missing bootstrap is rejected; any trailing bytes after the declared
table are rejected.

The loader constructs Lua source around non-bootstrap module sources beginning
with:

`local __bridge_preload = package.preload\n`

and `package.loaded`/`package.preload` scaffolding, while retaining the
bootstrap source separately for the execution stage. At this layer the
decrypted entries are source byte strings; there is no additional compressed
or bytecode container to peel.

## 0.3.1 hypothesis disposition

Historical evidence is preserved unchanged. Revalidation against 0.3.17:

- LWBP v2 structural layout: **CONFIRMED SHAPE**, but package identity/build are
  different;
- package AAD `LWBP2|<build>`: **CONFIRMED**;
- whole-package SHA-256 binding: **CONFIRMED**;
- 22-byte build binding: **CONFIRMED**;
- AES-256-GCM package encryption: **CONFIRMED**;
- signed two-segment key envelope: **CONFIRMED AND EXPANDED**;
- device-bound P-256 agreement: **CONFIRMED AND EXPANDED**;
- HKDF-SHA256 derivation: **CONFIRMED**;
- post-decrypt module table: **CONFIRMED**;
- compression before module table: **REJECTED** for 0.3.17.

## Required-input boundary

Supplied artifacts contain the exact encrypted package, but do not contain a
`package-key.envelope`. The protocol also requires the matching persisted
device private key. That private key is owner-state authentication/entitlement
material and was not accessed or exported.

Therefore C may safely extract/hash-gate the encrypted resources and exercise
the recovered parsers with inert synthetic data, but it may not legitimately
decrypt the original package from the currently supplied inputs. This is a
missing-input boundary, not a crypto failure, and no bypass is attempted.
