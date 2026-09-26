# LWBridge 0.3.1 — independent LWKE1 / package-key recovery lane

**Date:** 2026-09-24
**Role:** secondary researcher, read-only strict-parity lane
**Repository inspected read-only:** `C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`
**Reference:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**Reference SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`
**Secure proxy SHA-256:** `481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400`

## Hard restriction honored

This lane **did not inspect, decode, invoke, single-step, xref-reconstruct, or reroute through the secure-proxy envelope-consumer body at RVA `0x3F8E0–0x40A6D`**.

The plain proxy was not used as a substitute route around that restriction.

No remote authentication, auth probing, credential access, private-key export/extraction, or production behavior was performed.

All new static work below is from:

- original host code;
- secure-proxy functions outside the restricted RVA;
- enclosing caller/consumer code outside the restricted RVA;
- immutable local runtime artifacts;
- historical repository evidence;
- local caches/backups/logs.

Evidence labels:

- **EXACT_BYTES** — immutable original bytes/strings.
- **EXACT_CONTRACT** — exact externally relevant behavior recovered from permitted instructions/dataflow.
- **RECOVERED** — strong static composition across exact permitted boundaries.
- **PARTIAL** — primitive or caller contract is exact, but an essential field mapping remains unresolved.
- **PROTECTED_UNKNOWN** — the missing fact belongs to the forbidden parser or has no independent surviving evidence.

---

# 1. Executive result

This task materially narrows the R8-006 seam, but **does not recover enough evidence to honestly state a complete independent LWKE1-to-package-key algorithm**.

New exact findings:

1. **ECDH KDF is now exact.** The P-256 agreement helper calls `NCryptDeriveKey(..., L"TRUNCATE", NULL, ...)` with:
   - a **NULL KDF parameter list**;
   - no salt;
   - no info/context;
   - no KDF AAD;
   - exactly 32 output bytes;
   - flags 0.

2. **The generic AES-GCM helper contract is now exact.**
   - key: 32 bytes;
   - nonce: 12 bytes;
   - ciphertext: variable length;
   - tag: 16 bytes;
   - AAD: a distinct explicit string argument;
   - plaintext: output vector.
   The actual **LWKE1 AAD value** remains unknown because the caller mapping is inside the forbidden consumer.

3. **The authorization-ticket context passed into the envelope consumer is now exact at the structural level.**
   The proxy-side `LWAT2` validator has exactly 7 decoded pipe fields:
   - field 0 = `LWAT2`;
   - field 1 = issued-at Unix seconds;
   - field 2 = expiry Unix seconds;
   - field 3 = nonzero numeric claim, semantic name unknown;
   - field 4 = nonzero numeric claim, semantic name unknown;
   - field 5 = exactly 64 lowercase hexadecimal characters and is the device-binding value checked by the `authorization device mismatch` branch;
   - field 6 = exactly 43 URL-safe Base64 characters and is the launch nonce checked by the `authorization launch mismatch` branch.

4. **The exact envelope-consumer authorization input triple is recovered.**
   The enclosing loader copies:
   `{LWAT2 field3, LWAT2 field4, LWAT2 field2 expiry}`
   into the three-qword context passed as consumer argument 2.

5. **The consumer’s first non-key output is identified.**
   The loader reads the raw `package-key.envelope` text before the restricted call. After success, it requires the consumer output-context string at offset 0 to be byte-equal to that same raw token. This is a stale/race-consistency guard.

6. **Build ID and package SHA are not envelope-consumer outputs.**
   They are filled *after* the restricted call by separate permitted function `0x40A70–0x414E0`, which is the build-manifest validator. This corrects a possible over-reading of R8-003/R8-006 composition.

7. **No authentic surviving package-key envelope was found.**
   Both literal-field searches and the stronger encoded-prefix search for the Base64URL prefix of `LWKE1|`, `TFdLRTF8`, were negative in the accessible original runtime/cache/backup locations.

The remaining blocker is therefore very small but still decisive:

> **PROTECTED_UNKNOWN:** exact decoded `LWKE1` field count/order/encoding after the known first three fields, including which field decodes to the 65-byte peer P-256 point, which fields carry nonce/tag/ciphertext, what exact AAD string is supplied to the AES helper, and what the opaque second token segment means.

No guessed field map is included below.

---

# 2. Known outer LWKE1 grammar

## EXACT_CONTRACT — host token validator

From R8-004, original host generic token validator RVA `0x23E465–0x23E72B`:

```text
<canonical URL-safe Base64 first segment>.<opaque second segment>
```

Requirements:

- exactly two dot-separated segments;
- first segment uses URL-safe alphabet
  `ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_`;
- no-padding canonical representation;
- decoder re-encodes and byte-compares the first segment;
- decoded first segment is UTF-8 pipe-delimited;
- at least 3 fields;
- field 0 must be `LWKE1`;
- field 2 is signed decimal expiry **seconds**;
- expiry is converted to milliseconds by the host validation path.

Minimum proven decoded shape:

```text
LWKE1|<field1>|<expirySeconds>|...
```

### Field map that can be claimed

| Index / token part | Meaning | Evidence |
|---|---|---|
| first segment decoded field 0 | literal `LWKE1` | **EXACT_CONTRACT** |
| first segment decoded field 1 | unknown | **PROTECTED_UNKNOWN** |
| first segment decoded field 2 | expiry Unix seconds | **EXACT_CONTRACT** |
| first segment decoded fields 3+ | count/order/meaning unknown | **PROTECTED_UNKNOWN** |
| second dot segment | required opaque segment; meaning unknown | **PROTECTED_UNKNOWN** |

The sibling `LWAT2` second segment is an ECDSA signature, but this **must not be projected onto LWKE1 without independent evidence**.

---

# 3. Client ECDH identity already proven by R8-005

## EXACT_CONTRACT

Persisted device key:

- CNG provider: `Microsoft Software Key Storage Provider`;
- persisted key name:
  `{2D337A4D-7E6C-49EF-9486-54F0A00D8A41}`;
- algorithm: `ECDH_P256`.

Login `devicePublicKey`:

1. export `ECCPUBLICBLOB`;
2. blob must be 72 bytes:
   - magic `ECK1` / `0x314B4345`;
   - cbKey = 32;
3. convert to:
   `0x04 || X[32] || Y[32]`;
4. URL-safe Base64, no padding;
5. resulting login field is exactly 87 characters.

Login `launchNonce`:

- 32 random bytes;
- same URL-safe/no-padding Base64;
- exactly 43 characters;
- persisted as `authorization.challenge`.

No private-key bytes were read or exported in this lane.

---

# 4. New exact ECDH/TRUNCATE derivation contract

## Source

Secure-proxy agreement helper, permitted range:

`0x3DD00–0x3E01E`

This function is entirely outside the forbidden envelope-consumer RVA.

## Peer public point input

**EXACT_CONTRACT.**

The helper requires a 65-byte SEC1 uncompressed P-256 public point:

```text
04 || X[32] || Y[32]
```

It reconstructs a 72-byte CNG `ECCPUBLICBLOB`:

```text
u32 magic = ECK1
u32 cbKey = 32
X[32]
Y[32]
```

and imports it as the peer ECDH public key.

The exact LWKE1 field/index/encoding which supplies these 65 bytes is still **PROTECTED_UNKNOWN**.

## Secret agreement

At the permitted helper, the imported peer key is used with the locally persisted P-256 private key through:

`NCryptSecretAgreement(localKey, peerKey, &secret, 0)`.

## KDF — newly closed

**EXACT_CONTRACT.**

The helper uses KDF name:

`L"TRUNCATE"`.

Two calls are made to `NCryptDeriveKey`.

First call is a size query:

```text
NCryptDeriveKey(
  secret,
  L"TRUNCATE",
  NULL,           // pParameterList
  NULL,           // output
  0,
  &resultSize,
  0
)
```

The result size must be exactly 32.

Second call:

```text
NCryptDeriveKey(
  secret,
  L"TRUNCATE",
  NULL,           // pParameterList
  out32,
  32,
  &resultSize,
  0
)
```

Again the resulting size must be exactly 32.

Permitted instruction anchors:

- secret agreement region: approximately `0x3DE61–0x3DE6F`;
- derive-key size call: `0x3DEA4`;
- derive-key output call: `0x3DEE6`.

### Implementation consequence

There is **no KDF salt/info/context structure** to recover for this helper.

For strict reproduction on Windows, use the exact CNG call rather than inventing a custom ECDH byte-order/truncation conversion.

---

# 5. New exact generic AES-GCM helper contract

## Source

Permitted secure-proxy helper:

`0x3CB60–0x3CFEA`

The helper is outside the forbidden consumer.

## Signature

Recovered effective argument shape:

```text
aes_gcm_decrypt(
    keyVector,        // RCX, exact length 32
    nonceVector,      // RDX, exact length 12
    ciphertextVector, // R8, variable length
    tagVector,        // R9, exact length 16
    aadString,        // stack arg 5
    plaintextVector   // stack arg 6
)
```

### BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO wiring

At `0x3CD54–0x3CD9E` the helper builds a 0x58-byte authenticated-mode info structure:

- `cbSize = 0x58`;
- `dwInfoVersion = 1`;
- nonce pointer + nonce length;
- AAD pointer + AAD length;
- tag pointer + tag length;
- remaining fields zeroed.

The explicit AAD string is loaded at `0x3CD74–0x3CD8B`.

The tag pointer/length is loaded at `0x3CD8E–0x3CD9E`.

`BCryptDecrypt` is invoked at `0x3CDFB`.

### Important boundary

This proves the primitive requires an **explicit caller-provided AAD string**.

It does **not** prove what LWKE1 uses as that AAD, because the envelope helper callsite/field mapping belongs to the forbidden consumer.

It is also not safe to assume the AAD is:

- `LWKE1`;
- the first token segment;
- the decoded pipe payload;
- the authorization launch nonce;
- the raw two-segment token;
- empty.

All remain unproven.

---

# 6. Proxy-side LWAT2 authorization ticket recovered independently

This is a sibling contract that supplies caller context to the envelope consumer.

It is not the LWKE1 parser.

## Validator

Permitted function:

`0x201C0–0x212B9`.

Referenced exact error/status strings include:

- `authorization invalid`;
- `authorization missing`;
- `authorization encoding invalid`;
- `authorization signature invalid`;
- `LWAT2`;
- `authorization clock invalid`;
- `authorization expired`;
- `authorization lifetime invalid`;
- `authorization launch mismatch`;
- `authorization device mismatch`;
- `authorization payload invalid`;
- `authorization format invalid`;
- `authorization valid`.

## Exact decoded first-segment field count

At `0x20860–0x20875`, the decoded pipe field vector length must equal exactly:

`7`.

Field 0 must equal literal `LWAT2`.

Fields 1–4 are parsed as numeric values through the same decimal parser.

Fields 3 and 4 must both be nonzero.

## Exact field semantics recovered

| Index | Contract | Evidence |
|---:|---|---|
| 0 | `LWAT2` | exact compare |
| 1 | issued-at Unix seconds | clock check against current time; lifetime calculation |
| 2 | expiry Unix seconds | explicit expired check and lifetime calculation |
| 3 | nonzero numeric claim; semantic name unknown | exact parse/nonzero check |
| 4 | nonzero numeric claim; semantic name unknown | exact parse/nonzero check |
| 5 | 64-character lowercase-hex device-binding value | exact length/charset and `authorization device mismatch` comparison |
| 6 | 43-character URL-safe Base64 launch nonce | exact length/charset and `authorization launch mismatch` comparison |

Clock behavior:

- issued-at more than 300 seconds in the future => clock invalid;
- expiry <= current time => expired;
- expiry must be greater than issued-at;
- lifetime `expiry-issuedAt` must be <= 1200 seconds.

## Second segment semantics — sibling only

Permitted signature verifier:

`0x212C0–0x214AF`.

The decoded second segment must be exactly **64 bytes**.

The verifier:

1. hashes the **decoded first-segment payload bytes** with SHA-256 through helper `0x1F640–0x1F7F6`;
2. constructs a fixed P-256 ECDSA public-key blob with magic `ECS1` and 32-byte coordinate size;
3. imports that embedded public key;
4. verifies the 64-byte signature using CNG.

This proves:

`LWAT2 decoded payload + 64-byte ECDSA-P256 signature`.

It does **not** prove the LWKE1 second segment uses the same scheme.

---

# 7. Exact authorization context passed to the LWKE1 consumer

R8-006 left consumer argument 2 semantically unnamed.

The permitted LWAT2 validator/caller composition now recovers its exact claim sources.

The enclosing loader takes authorization result fields:

- result offset `+0x58` = LWAT2 field 3;
- result offset `+0x60` = LWAT2 field 4;
- result offset `+0x50` = LWAT2 field 2 expiry;

and copies them into the 24-byte consumer context in this order:

```text
offset 0x00 = LWAT2 field 3
offset 0x08 = LWAT2 field 4
offset 0x10 = LWAT2 expirySeconds
```

The semantic names of field 3 and field 4 remain unknown.

Therefore do not rename them `userId`, `licenseId`, `entitlementId`, etc. without new evidence.

---

# 8. Envelope caller/output boundary refined

## Pre-read raw token

Before the restricted call, the enclosing loader constructs/reads the `package-key.envelope` path using the permitted text reader.

Relevant permitted loader anchors:

- path construction around `0x1C443`;
- bounded text read at `0x1C45B`;
- exact diagnostic `key envelope missing` referenced at `0x1C59E`;
- exact diagnostic `key envelope expired` referenced at `0x1C677`.

## Restricted call boundary

R8-006 callsite:

`0x1C8E2 -> 0x3F8E0`.

Only the caller-side arguments are used here:

- arg1 RCX: package-key.envelope path;
- arg2 RDX: 24-byte authorization context described above;
- arg3 R8: output context object, initialized empty;
- arg4 R9: output vector, initialized empty;
- arg5: error/output string;
- additional stack integer: 1.

The consumer body remains completely uninspected.

## Key output

R8-006 already proves the exact output vector supplied as arg4 becomes the package AES key.

The downstream package AES helper requires it to be exactly **32 bytes**.

## New: context offset 0 is the raw envelope token

Immediately after successful consumer return, permitted loader range `0x1C8EB–0x1C92A` compares:

- the consumer output-context string at offset 0;

against:

- the exact pre-read `package-key.envelope` text and length.

Mismatch rejects the path.

Therefore:

> **EXACT_CONTRACT:** consumer output context offset 0 is expected to reproduce the exact raw envelope token text used for the call.

This is a consistency/race guard, not a decoded field semantic.

---

# 9. Build manifest context is separate from the envelope

A subtle but important correction:

The output context later passed to the package function contains:

- offset `+0x20`: expected build ID string;
- offset `+0x40`: expected lowercase package SHA-256 string.

However these are **not proven outputs of the envelope consumer**.

After the envelope call, the loader invokes separate permitted function:

`0x40A70–0x414E0`

at loader callsite `0x1CC14`.

Its exact diagnostics include:

- `build manifest missing`;
- `build manifest invalid`;
- `build manifest mismatch`.

The loader passes:

- context `+0x20` as one output;
- context `+0x40` as another output.

Only after that manifest validation does it call the package function at `0x1CC3A`.

The package function `0x3D260–0x3DCF5` then:

- compares full package SHA-256 against context `+0x40`;
- compares embedded 22-byte package build ID against context `+0x20/+0x30`;
- receives the 32-byte key as its separate third argument.

So the proven context composition is:

```text
context +0x00 = raw package-key.envelope token, from envelope consumer
context +0x20 = expected build ID, filled by build-manifest validator
context +0x40 = expected package SHA-256 hex, filled by build-manifest validator
```

Do not infer build ID/SHA fields exist inside LWKE1 from this context.

---

# 10. Final package-key consumption remains exact

From R8-003/R8-006:

Package:

- magic `LWBP`;
- version 2;
- build ID `9BupJXpEgm34lybhNhbbcQ`;
- nonce: 12 bytes;
- package ciphertext: 1,172,657 bytes;
- package tag: 16 bytes;
- package AAD:
  `LWBP2|9BupJXpEgm34lybhNhbbcQ`.

The 32-byte vector produced by the restricted envelope consumer is passed unchanged as the package AES key.

This package AAD is **not evidence for the LWKE1 envelope AAD**. The two AES-GCM operations are separate layers.

---

# 11. Surviving-artifact search

## Current original runtime roots checked

Accessible original/legacy locations inspected include:

- `C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game\bridge-runtime`;
- `C:\Users\chimw\AppData\Local\LastWar-xLua-Bridge`;
- `C:\Users\chimw\AppData\Roaming\LastWar-xLua-Bridge`;
- `C:\Users\chimw\AppData\Roaming\lwbridge`;
- legacy WebView cache under `local.lastwar.xlua.bridge\EBWebView`;
- `LWBridgeRebuild` data;
- local Backup;
- CrashDumps;
- Last War updater/temp data;
- Codex local cache;
- user Temp;
- OneDrive;
- Downloads;
- Documents.

The live FunFly `bridge-runtime` currently contains:

- `authorization.challenge`;
- `build.manifest`.

It does **not** contain:

- `authorization.ticket`;
- `package-key.envelope`.

The nonce contents of `authorization.challenge` were not copied into this report.

## Encoded-prefix search

Because a real canonical first segment begins with Base64URL of `LWKE1|`, its text prefix is:

`TFdLRTF8`.

This prefix was searched directly across the accessible runtime/cache/history roots above.

Result:

**0 authentic matches found.**

This is stronger than searching for literal `LWKE1`, because an actual envelope file contains the encoded token rather than decoded payload text.

## Old builds

No second original `lwbridge-0.3.1.exe`/older original LWBridge build was found.

A Codex-local cached copy of the extracted runtime bundle exists, including `xlua-proxy-secure.dll`, but it contains no authorization ticket or package-key envelope.

## Qualification

Some broad home-directory searches report unrelated permission-denied paths. The targeted application/runtime/cache roots listed above were accessible and searched successfully.

Conclusion:

> No surviving authentic `package-key.envelope` was found in the accessible local evidence set.

---

# 12. What an independent implementation can safely implement now

The following pieces are implementation-ready without guessing.

## 12.1 Outer token validation

Implement:

- exactly two dot segments;
- canonical URL-safe/no-padding Base64 first segment;
- UTF-8 decode;
- pipe split;
- minimum 3 fields;
- field 0 exact `LWKE1`;
- field 2 signed-decimal expiry seconds.

Do not assign meanings to other fields yet.

## 12.2 Device-key access/public formatting

Implement exact persisted CNG key identity and exact 65-byte SEC1 public point encoding from R8-005.

Do not export the private key.

## 12.3 Peer-key agreement helper

Once an independently recovered field yields the peer 65-byte point:

- require length 65;
- require first byte `0x04`;
- construct ECK1/32 ECCPUBLICBLOB;
- import peer point;
- `NCryptSecretAgreement`;
- `NCryptDeriveKey(..., L"TRUNCATE", NULL, ..., 32, ..., 0)`.

## 12.4 Generic envelope-AES primitive wrapper

A strict helper can already be implemented with:

- key length 32;
- nonce length 12;
- tag length 16;
- explicit AAD string;
- variable ciphertext;
- AES-GCM via BCrypt.

But it **must not yet be wired to LWKE1 field indexes**.

## 12.5 Authorization-binding input

The envelope consumer caller context can be reproduced structurally as:

```text
claim3_u64
claim4_u64
authorization_expiry_seconds
```

The first two semantic names remain unknown.

---

# 13. What cannot be implemented as strict parity yet

The following remain **PROTECTED_UNKNOWN**:

1. exact total decoded LWKE1 field count;
2. semantic meaning of decoded field 1;
3. exact index and textual encoding of the 65-byte peer P-256 public point;
4. exact index/encoding of the 12-byte envelope nonce;
5. exact index/encoding of the envelope ciphertext;
6. exact index/encoding of the 16-byte envelope tag;
7. exact LWKE1 AES-GCM AAD string, including whether it is empty;
8. exact meaning/verification contract of the second dot segment;
9. whether decrypted envelope plaintext is directly the 32-byte package key or a structured plaintext from which the consumer selects/copies 32 bytes;
10. how the two opaque LWAT2 numeric claims are used internally by the forbidden consumer.

Because items 3–9 are still missing, **a complete independent package-key derivation recipe is not yet evidence-supported**.

Any implementation that guesses these fields would no longer be strict parity.

---

# 14. Partial algorithm boundary

The strongest evidence-supported flow is:

```text
auth login:
  persisted ECDH_P256 device key
  -> devicePublicKey = Base64URL(04 || X || Y)
  + launchNonce

server:
  -> packageKeyEnvelope (LWKE1 two-segment token)

host:
  -> validate canonical first segment
  -> check LWKE1 magic and field2 expiry
  -> persist package-key.envelope

proxy permitted caller:
  -> validate LWAT2 authorization ticket
  -> produce caller context:
       numericClaim3
       numericClaim4
       authorizationExpirySeconds
  -> pre-read raw package-key.envelope
  -> call forbidden consumer

forbidden consumer:
  -> exact field map UNKNOWN
  -> emits raw-envelope identity string
  -> emits exact 32-byte package key

separate permitted manifest validator:
  -> expected build ID
  -> expected package SHA-256

permitted package decrypt:
  -> validate SHA/build
  -> AES-GCM bridge-scripts.dat using exact 32-byte package key
```

A likely cryptographic primitive chain exists in the binary:

```text
peer P-256 point
 -> ECDH secret agreement
 -> NCryptDeriveKey("TRUNCATE", NULL params) => 32 bytes
 -> AES-GCM helper(key32, nonce12, ciphertext, tag16, explicit AAD)
```

However, without a permitted mapping from actual LWKE1 fields into those inputs, this chain must remain **PARTIAL**, not promoted to a complete envelope algorithm.

---

# 15. Sibling-token warning

The recovered LWAT2 signature format is useful evidence about the product’s auth design:

```text
Base64URL(decoded pipe payload).Base64URL(64-byte ECDSA-P256 signature)
```

But **do not reuse this as the LWKE1 second-segment contract**.

The host’s LWKE1 outer validator only proves that a second segment exists.

No independent surviving source in this lane proves it is:

- ECDSA;
- HMAC;
- MAC;
- signature over decoded first segment;
- signature over encoded first segment;
- or anything else.

---

# 16. Best next evidence routes that preserve the restriction

The remaining seam can be closed without entering `0x3F8E0–0x40A6D` if any of these become available:

1. an authentic expired/original `package-key.envelope` from a backup, old machine, disk image, or user-controlled archive;
2. auth-service source/schema documentation already owned by the project;
3. a captured historical auth response already stored locally outside credentials/private key material;
4. a symbol/source-cache artifact that names the envelope fields;
5. another independently documented original-client contract that defines the LWKE1 payload.

For an authentic envelope sample, useful non-secret static analysis would be:

- decode canonical first segment;
- record field count;
- classify each field by length/alphabet;
- identify any 87-char URL-safe candidate decoding to 65 bytes beginning `0x04`;
- identify 16-char / 22-char / 43-char / 64-char / other fixed-width candidates;
- compare decoded byte lengths to exact permitted helper requirements;
- compare field 2 to expiry metadata;
- compare any launch nonce candidate to the existing challenge only if that historical sample is from the same authorization session.

Do not:

- authenticate merely to obtain a fresh envelope;
- export private key material;
- inspect/reroute the forbidden consumer;
- assume plain-proxy parity to bypass the restriction.

---

# 17. Exact source anchors for new findings

## Secure proxy, permitted

- authorization validator: `0x201C0–0x212B9`;
- decoded field-count check = 7: `0x20860–0x20875`;
- LWAT2 magic check: `0x2087B–0x208A6`;
- numeric fields 1–4 parsing: `0x208AC–0x20912`;
- field3/field4 nonzero checks: `0x20918–0x20928`;
- 64-char lowerhex field5 validation: `0x2092E–0x20992`;
- field6 URL-safe validation/copy: `0x20992–0x20A16`;
- device-binding compare/failure path: `0x20A16–0x20A73` -> device mismatch branch;
- launch-nonce compare/failure path: `0x20A79–0x20AC6` -> launch mismatch branch;
- clock check begins `0x20ACC`;
- expiry/lifetime checks: `0x20CA2–0x20D48`;
- authorization success write: `0x20D4E`.

- SHA-256 helper: `0x1F640–0x1F7F6`;
- LWAT2 signature helper: `0x212C0–0x214AF`.

- AES-GCM helper: `0x3CB60–0x3CFEA`;
- authenticated-mode info wiring: `0x3CD54–0x3CD9E`;
- BCrypt decrypt call: `0x3CDFB`.

- ECDH agreement/derive helper: `0x3DD00–0x3E01E`;
- `TRUNCATE` KDF with NULL parameter list;
- size query around `0x3DEA4`;
- 32-byte derivation around `0x3DEE6`.

- package function: `0x3D260–0x3DCF5`.

- build-manifest validator, immediately after forbidden range: `0x40A70–0x414E0`.

## Enclosing loader, permitted

- package-key text read: around `0x1C443–0x1C45B`;
- `key envelope missing` diagnostic reference: `0x1C59E`;
- `key envelope expired` diagnostic reference: `0x1C677`;
- restricted callsite only: `0x1C8E2 -> 0x3F8E0`;
- post-success raw-envelope identity comparison: `0x1C8EB–0x1C92A`;
- build-manifest validator call: `0x1CC14 -> 0x40A70`;
- package function call: `0x1CC3A -> 0x3D260`.

No instruction inside the forbidden target was decoded.

---

# 18. Read-only / Git-state note

No `LW-Control` source, test, documentation, evidence, Git index, commit, or remote branch was modified by this helper.

The repository was already under active concurrent work when this lane started and changed again during this research session. The final observed branch remained:

`research/offline-controller`.

Final observed repository modifications included active main-researcher work in:

- `.github/workflows/csharp.yml`;
- `src/LWBridge.Desktop/LWBridgeBackend.cs`;
- `src/LWBridge.Desktop/ManualMapScanCommandService.cs`;
- Map Data frontend/tests/tools;
- untracked Map Scan Clear parity/check files.

Those are not this helper’s changes.

This report itself is intentionally outside the repository under `LW Helper Finding`.

## Final concurrent-worktree verification

A final read-only `git status --short` still showed active main-researcher changes on `research/offline-controller`, now including the R8-008 Map Scan Clear strict-parity lane (`2026-09-24-r8-008-map-scan-clear-strict-parity.*`, `MapScanClearParityChecks.cs`, and `check_map_clear_r8008.cjs`) plus related source/docs changes.

No listed repository change was created by this helper.

# 19. Final deleted/residue-source exhaustion pass

After the initial handoff, one additional read-only local-recovery pass was performed to answer whether any independent source remained unsearched.

Checked:

- C:\$Recycle.Bin for package-key.envelope and authorization.ticket;
- Windows Error Reporting roots under ProgramData and the current user profile for LWBridge/package-key residue;
- non-elevated shadow-copy enumeration;
- non-elevated NTFS USN-journal text query for package-key.envelope, authorization.ticket, and LWKE1.

Results:

- Recycle Bin: no matching envelope/ticket files.
- User WER: no LWBridge/package-key matches.
- ProgramData WER: no matches in accessible files; some unrelated WER paths were permission-restricted.
- No shadow-copy objects were available through the non-elevated query.
- The USN query produced no usable matches/results under the current non-elevated account.

No elevation was attempted.

Therefore the independent local-artifact route is exhausted under the task restrictions. The remaining LWKE1 field/AAD map is evidence-limited and remains PROTECTED_UNKNOWN; no further strict-parity claim is justified without a new independent artifact/source.
