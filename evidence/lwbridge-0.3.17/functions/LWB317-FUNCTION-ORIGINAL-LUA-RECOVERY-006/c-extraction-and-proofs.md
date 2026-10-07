# Checkpoint C — isolated extraction and inert parser proofs

State: **COMPLETE WITH INPUT BLOCKER FOR ORIGINAL DECRYPT**.

## Isolated extracted root

The exact source-proven embedded resources were carved from the fixed 0.3.17 EXE
into this task-owned root only:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006/c-extracted/`

Files:

- `bridge-scripts.dat` — 1,418,098 bytes,
  SHA-256 `C215B5AA87619F547F2D999E58D1A1CB4780B49FEC7040362732257D86D4FA82`;
- `xlua-proxy-secure.dll` — 619,008 bytes,
  SHA-256 `AE8BBA866DF80E9C924923D1F59D49E824C784C8BA9B47780FE2F58305E00A86`;
- `xlua-proxy-plain.dll` — 620,544 bytes,
  SHA-256 `266129C6C92F3AE89001493D61CCFDF4F5DAC1FE66CFF4FBB4799E6C01A6C902`;
- `xlua-proxy-bundle.json` — 590 bytes,
  SHA-256 `D6B3C71208A658D3D4FF05AB1AF2E5ABEE3F5BAB05839E15D6C97F6D58CFFD1C`;
- `manifest.json` — generated verification manifest.

`tools/lwbridge317/extract_original_lua_resources.py` performs only static
file carving from the hash-gated reference EXE. It verifies every carved hash,
re-parses the LWBP v2 header, parses both proxy DLLs as valid PE images and
verifies the bundle's recorded secure/plain hashes.

No executable or DLL was loaded or launched.

## Original decrypt disposition

Original decryption was **not attempted** because the supplied inputs are not
complete.

The exact missing/required inputs are:

1. A valid signed `package-key.envelope` matching this 0.3.17 package/context.
   No such file exists in the bounded supplied repository/reference-artifact
   roots.
2. The matching persisted P-256 private key in
   `Microsoft Software Key Storage Provider` under key name
   `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`.

The private key is owner-state authentication/entitlement material. This task did
not read it, export it, instrument it, or attempt a bypass.

Static source locators:

- secure proxy runtime envelope path string:
  outer EXE raw `0xABB32B`, secure-proxy raw `0x72190`, proxy RVA
  `0x73790`;
- plain proxy has the same inner raw/RVA envelope-path locator at outer raw
  `0xB5252B`;
- persisted-key open helper: secure proxy RVA `0x44860-0x44920`;
- device-public export/binding helper: RVA `0x44660-0x44860`;
- P-256 agreement/KDF helper: RVA `0x3F2A0-0x3F5BE`;
- signed envelope consumer: RVA `0x40E80-0x4200D`;
- package AES/SHA/build verifier/decryptor: RVA `0x3E800-0x3F295`;
- direct plaintext module-table parser: RVA `0x127D0-0x13620`.

Therefore the blocker is precise: the encrypted package bytes and decrypt
algorithm are present, but the legitimate signed envelope is absent and the
matching private key is intentionally not accessed.

## Inert contract proofs

`tests/original_lua_recovery_contract_checks.py` executes only static evidence
and synthetic plaintext fixtures.

Executed result: **8/8 PASS**.

The tests independently prove:

- all four carved resource hashes match the fixed 0.3.17 identities;
- LWBP version/build/cipher-length arithmetic is self-consistent;
- synthetic valid module tables parse with the recovered grammar;
- missing bootstrap is rejected;
- duplicate bootstrap is rejected;
- invalid module names are rejected;
- trailing package data is rejected;
- zero or >1024 entry count is rejected.

No synthetic fixture is represented as original source.

## Bytecode/source validity boundary

The recovered 0.3.17 post-decrypt parser does not expose another compressed or
bytecode container. It consumes module `source bytes` directly and builds Lua
`package.preload`/`package.loaded` source scaffolding.

Because the original AES plaintext could not legitimately be produced from the
supplied inputs, there are no original module sources available here to compile
or bytecode-validate. Claiming original bytecode validity would therefore be
fabricated.

The correct C result is:

- encrypted/resource extraction: **COMPLETE**;
- integrity verification: **COMPLETE**;
- module-table grammar proof: **COMPLETE (inert synthetic)**;
- original plaintext decode: **BLOCKED BY EXACT MISSING INPUTS**;
- original source/bytecode validity: **NOT OBSERVABLE WITHOUT LEGITIMATE
  DECRYPT**.
