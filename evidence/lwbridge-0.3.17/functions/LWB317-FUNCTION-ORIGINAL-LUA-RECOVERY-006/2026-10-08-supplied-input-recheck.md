# 2026-10-08 supplementary supplied-input check (after accepted 006)

Status: **NO NEW LEGITIMATE DECRYPT INPUT**. This is a bounded follow-on
filename/provenance recheck, not a replacement for the accepted 006 review or
a repeated ciphertext/crypto analysis.

## Newly checked scope

At starting checkout `3634daa30a036eaaa567553dea44e2aed65e4f71`,
the previous 006 review, `a-payload-inventory.md`,
`b-crypto-contract.md`, `c-extraction-and-proofs.md`, and
`d-function-matrix.md` were read first. A fresh **exact filename** enumeration
(including hidden project worktrees) was then performed over the supplied
`LW-Control` repository, including `.scratch-lwb317`, `.codex-live`,
`evidence`, `src`, `tools`, `tests`, `docs`; and separately over the
supplied sibling `Github/LW` reference-artifact folder. Matches searched:
`package-key.envelope`, `bridge-scripts.dat`, `authorization.ticket`.
This is a filename-only local artifact inventory; it is not a scan of runtime
credentials or a proof that no differently named item exists.

The sole exact-name hit was the **already inventoried** extracted ciphertext
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006/c-extracted/bridge-scripts.dat`.
No new exact-name envelope, ticket, source module or standalone package was
identified. No owner config/key/credential location or protected service was
accessed. The sibling reference folder yielded no exact-name matches.

## Four separate input/recovery states

| Item | State | Reproducible provenance and limitation |
| --- | --- | --- |
| Original encrypted package | **AVAILABLE, EXACT_BYTES** | 0.3.17 EXE SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`; carved LWBP v2 at EXE raw `0x8EED88`, length 1,418,098, SHA-256 `C215B5AA87619F547F2D999E58D1A1CB4780B49FEC7040362732257D86D4FA82`; accepted 006 A/C manifests |
| Matching signed package-key envelope | **NOT SUPPLIED/NOT VERIFIED** | Accepted B: secure proxy envelope consumer RVA `0x40E80–0x4200D`, ECDSA verifier `0x43E50–0x440D8`. Current filename recheck found no new matching signed material. A superficially named file would still need successful signature, context/build and whole-package SHA bindings |
| Matching persisted device key | **DEPENDENCY, AVAILABILITY UNKNOWN** | Accepted B: secure proxy CNG persisted-key-open RVA `0x44860–0x44920`, public-binding `0x44660–0x44860`, P-256 secret agreement `0x3F2A0–0x3F5BE`. This is owner-state entitlement/key material; not opened, queried, used or exported |
| Original decrypted module plaintext | **NOT RECOVERED** | Package decrypt/whole-hash verifier RVA `0x3E800–0x3F295`, plaintext parser RVA `0x127D0–0x13620` and bootstrap requirement; no valid decrypt inputs and no original module table / Treasure/Ghost controller source |

## Original producer/consumer boundary

Accepted 006 static evidence places the outer EXE resource marker at
`0xA490FA`, secure-proxy runtime envelope path marker at EXE raw
`0xABB32B`, and outer-host auth/runtime `LWKE1`/`package-key.envelope`
markers at raw `0xD54528`/`0xD545E8`. The loader verifies the signed,
build/hash-bound envelope, uses the **matching CNG device state** to obtain
the package AES key, and then parses the decrypted source module table.
These are consumers and validators, **not evidence that the missing
envelope was issued or persisted among supplied artifacts**. The
envelope issuer/input producer and legitimate device-key availability remain
unverified; no replacement inputs were invented and no auth/entitlement
route was attempted.

The controller source status from 006 remains
`NOT_RECOVERED_FROM_SUPPLIED_INPUTS`. No Treasure/Ghost public provider
was enabled; historical 006 findings and A-to-A limitations remain intact.
Only newly supplied matching, verifiable authorized inputs would justify
reopening plaintext recovery.
