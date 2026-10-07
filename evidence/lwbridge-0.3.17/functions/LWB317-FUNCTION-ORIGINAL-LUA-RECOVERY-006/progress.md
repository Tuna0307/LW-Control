# Progress — LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006

Starting HEAD/remote: `692fd40de1639261ca2c7dda7c7884c527786133`, clean.

## A — COMPLETE

Exact 0.3.17 encrypted LWBP v2 package/resource layout is hash-gated. Secure and
plain loader proxies plus bundle are identified. No standalone
`package-key.envelope` is present in the bounded supplied roots.

Commit:
`a9196752ac4148c7b76b04d3af6e17df4da96277`.

## B — COMPLETE

0.3.17 loader/crypto/integrity/input contract is source-recovered:

- signed two-segment 14-field LWKE1 envelope;
- ECDSA-P256 signature check;
- device-bound persisted P-256 CNG agreement;
- HKDF-SHA256;
- AES-256-GCM envelope/package decrypt;
- package whole-SHA/build binding;
- direct module table after decrypt with **no compression stage**.

Commit:
`65bde8312dae7ef96d5db0d86d7c73df8b4f4d18`.

## C — COMPLETE WITH EXACT INPUT BLOCKER

Exact encrypted package, secure/plain proxies and bundle were carved into the
task-owned `c-extracted/` root and hash/PE/bundle verified. Original decrypt
was not attempted because a valid signed `package-key.envelope` is absent and
the matching persisted CNG private key is owner-state authentication/entitlement
material that this task did not read/export/bypass.

Inert recovered-format tests: **8/8 PASS**.

Commit:
`00101ba1a6de25f15944a792d64ebeb3ddea4c76`.

## D — COMPLETE WITH EXACT INPUT BLOCKER

No duplicate plaintext body for `getTreasureClaimStatus`,
`claimTreasures` or `prepareGhostPlunderTasks` exists in the exact carved
encrypted package or either proxy DLL. The proxies do contain the fixed bridge
Lua bootstrap/dispatcher markers, placing the missing provider definitions in
the loaded module-table plaintext rather than a second plaintext native body.

All independently recoverable 0.3.17 host semantics are now hash-gated in
`d-controller-boundaries.json` and summarized per function in
`d-function-matrix.{md,json}`.

Recovered host-side boundaries include:

- Treasure status: one no-feature-payload protected call, 5 s host bound,
  result fields `playerUid/allianceId/states` plus optional `batch`, exact
  frontend idle/poll behavior and current-profile-per-call ownership.
- Treasure claim: exact host candidate query/order, exact five-field provider
  request, scope/lucky default, 5 s bound and result-counter vocabulary.
- Ghost preparation: exact `{rows}` provider request, required returned
  `rows`, 5 s bound and post-return identity/timing/owner/capacity validation.

The still-missing semantics are specifically controller-owned transitions and
transformations that require the original decrypted module-table plaintext.
No current-game Lua or 0.3.1 body was substituted.

D checkpoint commit:
`247c40a17fb36ddcbd4c8a56ec5279dc92ab8092`.

## Verification / negatives

Fresh required 006 checks pass:

- 006 tool/test `py_compile`;
- B exact crypto verifier;
- C exact extraction replay;
- C inert module-table tests: 8/8;
- D exact controller-boundary verifier;
- 005 parser regression;
- JSON evidence parse;
- `git diff --check`.

Inherited 003/004 Lua-oracle replay was not rerun because none of the installed
Python interpreters currently provides `lupa`; no dependency was installed.
This is an environment-only limitation and does not affect the 006 validators.

One pre-existing `LastWar.exe` process was observed read-only during a final
process-boundary check and left untouched. This task did not launch, attach,
instrument, control, focus or terminate it.

No product provider was enabled or changed. Home/Map remains PARTIAL and no
LIVE_PROVEN state is promoted.

Status: **AWAITING_REVIEW**.
