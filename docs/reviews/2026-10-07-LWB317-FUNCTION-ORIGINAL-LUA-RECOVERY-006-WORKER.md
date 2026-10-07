# LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006 — worker review

Date: 2026-10-07

State: **AWAITING_REVIEW**

Branch: `research/offline-controller`

Starting checkpoint:
`692fd40de1639261ca2c7dda7c7884c527786133`

Checkpoint commits:

- A payload inventory:
  `a9196752ac4148c7b76b04d3af6e17df4da96277`
- B 0.3.17 crypto/input contract:
  `65bde8312dae7ef96d5db0d86d7c73df8b4f4d18`
- C isolated extraction/proofs:
  `00101ba1a6de25f15944a792d64ebeb3ddea4c76`
- D controller semantics boundary:
  `247c40a17fb36ddcbd4c8a56ec5279dc92ab8092`

Final acceptance remains with the project lead.

## Result

The exact original LWBridge 0.3.17 payload location and loader/crypto contract are
now recovered, but the three requested original bridge Lua controller bodies
cannot be legitimately decrypted from the supplied inputs.

This is a precise input blocker, not a generic reverse-engineering failure:

- the exact encrypted `bridge-scripts.dat` is present;
- the exact secure/plain proxies are present;
- the full 0.3.17 envelope/package cryptographic contract is source-recovered;
- a valid signed `package-key.envelope` matching this package/context is absent
  from the bounded supplied artifacts;
- the protocol also requires the matching persisted P-256 CNG private key,
  which is owner-state authentication/entitlement material and was not
  read/exported/bypassed.

No original/game executable was launched and no protected service was contacted.

## A — exact original payload inventory

The reference EXE is fixed at SHA-256
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

The exact embedded resources were located and identity-pinned, including:

- encrypted LWBP v2 `bridge-scripts.dat`;
- secure xLua proxy;
- plain xLua proxy;
- proxy bundle JSON.

The package is genuinely embedded in the 0.3.17 EXE; the loader's references to
`bridge-scripts.dat` are not merely dead strings. The required
`package-key.envelope` is not among the bounded supplied artifacts.

Current Last War v22 Lua is kept distinct from this original bridge payload.

## B — exact 0.3.17 crypto/integrity contract

Historical 0.3.1 ideas were revalidated against the exact 0.3.17 secure proxy,
not by disabling an old hash gate.

Recovered 0.3.17 contract:

- `LWBP`, version 2, exact 22-byte build identifier;
- whole encrypted-package SHA-256 binding;
- build-id binding;
- package AAD `LWBP2|<build>`;
- AES-256-GCM package encryption;
- signed canonical two-segment LWKE1 envelope with exactly 14 pipe fields;
- ECDSA-P256 verification using the embedded public point;
- persisted Microsoft Software KSP P-256 device key;
- device-public SHA-256 binding;
- `NCryptSecretAgreement` plus 32-byte TRUNCATE derivation;
- one-block HKDF-SHA256;
- AES-256-GCM encrypted 32-byte package key;
- direct decrypted module table with **no compression layer**.

The plaintext table is 1..1024 entries of
`u32 nameLen | u32 sourceLen | name | source`, allows only
`A-Z a-z 0-9 . _` names, requires exactly one `bootstrap`, and rejects
trailing bytes.

## C — isolated extraction and proof

The exact encrypted package, proxies and bundle were carved only into the
task-owned evidence root and verified by hash/PE/bundle contract.

`tests/original_lua_recovery_contract_checks.py` runs only static carved
evidence and synthetic plaintext fixtures. Fresh result: **8/8 PASS**.

Original decrypt was intentionally not attempted because the legitimate input
set is incomplete. No synthetic fixture is represented as original source.

Because 0.3.17 consumes source bytes directly after package decrypt, there is no
separate compression or bytecode layer that could be decoded independently of
the missing package key.

## D — original semantics boundary

A complete search of the exact carved package and both proxies finds no plaintext
copy of the three controller names:

- `getTreasureClaimStatus`;
- `claimTreasures`;
- `prepareGhostPlunderTasks`.

The proxies do contain `XluaBridgeHandlePipeMessage`,
`__XluaBridgeLoad`, `__XLUA_BRIDGE_PROFILE_ID`, and
`@bridge-scripts.dat`. Together with the recovered module-table loader, this
places protected provider routing in the loaded bridge Lua plaintext rather than
a second plaintext native implementation.

Actual controller bodies are therefore
`NOT_RECOVERED_FROM_SUPPLIED_INPUTS`.

### Treasure status

Recovered exact outer contract:

- native handler `0x115C5D-0x1166BE`;
- provider reference `getTreasureClaimStatus` at `0x1160CD`;
- 5,000 ms protected bound;
- no feature-specific provider payload;
- observed result surface `playerUid`, `allianceId`, `states`, optional
  `batch`;
- frontend invokes status while idle, then after claim `queued > 0` polls
  once per second up to 1800 iterations;
- frontend merges states by UUID, continues for missing/running batch, and stops
  for present non-running batch;
- generic bridge wrapper injects the currently selected profileId on each
  invocation; the initiating profile is not pinned;
- native persistent Treasure state is profile-owned
  `treasure_claim_states(server_id,player_uid,treasure_uuid,...)`; no native
  batch table is recovered.

Still controller-owned/unknown:

- idle source/cache recomputation;
- exact batch creation/defaults/counter transitions;
- terminal/error/timeout/reconnect/profile-switch retirement/reset semantics.

### Treasure claim

Recovered exact outer contract:

- native handler `0x18EE07-0x190734`;
- provider reference `claimTreasures` at `0x18FCDA`;
- 5,000 ms protected bound;
- scope `single|boxes|season`, target UUID required for single;
- `prioritizeLuckySlots` defaults true;
- host-owned persisted candidate acquisition is nonexpired valid-UUID Treasure
  rows, supplies types 1/3/4 or completed ordinary type 0, ordered
  `point_index ASC`;
- exact provider payload:
  `{serverId,records,claimScope,targetUuid,prioritizeLuckySlots}`;
- result vocabulary:
  `eligible,queued,skipped,directQueued,scoutQueued,scoutDispatched,claimed,`
  `noScoutSkipped,otherAllianceSkipped,failed`.

Still controller-owned/unknown:

- selection/order among already host-selected candidates;
- lucky-slot priority algorithm;
- direct claim versus scout decision/order;
- batch/counter transition meaning, retries and duplicates.

### Ghost preparation

Recovered exact outer contract:

- native schedule handler `0x1336A9-0x1350DF`;
- provider reference `prepareGhostPlunderTasks` at `0x133CA8`;
- 5,000 ms protected bound;
- provider payload contains `rows`;
- result must contain a `rows` array;
- returned rows are identity-matched before persistence rather than accepted by
  provider position;
- host-visible postconditions include UUID/server identity, `taskKind=ghost`,
  positive `ownerServer`, completion/plunder timing, optional-positive expiry
  strictly after plunder, and remaining capacity.

Still controller-owned/unknown:

- whether preparation re-reads/hydrates current task state;
- exact transformations/additional fields;
- exact per-row rejection/drop policy and output reordering.

Preparation remains distinct from later persistence/execution and does not
require terminal Ghost result correlation merely to define its own contract.

## Clone differences retained

No product change was authorized or made.

Current production still fails closed for:

- Treasure claim status;
- Treasure claim;
- Ghost preparation.

The internal current-v22 Ghost helper remains an adaptation, not promoted as the
original preparer.

The 005 numeric/malformed string discrepancy is retained: the original host's
broader integer-like reader accepts/defaults forms that the current helper path
rejects before its later string branch. This task does not fix it.

No assumption that a Ghost response contains no identity was reintroduced, and
no FutureManager mechanism is promoted as a complete durable Ghost correlation
solution.

## Verification

Fresh required 006 checks:

- new 006 Python tools/tests compile;
- B crypto verifier: PASS;
- C extraction replay: PASS;
- C inert module-table checks: 8/8 PASS;
- D controller-boundary verifier: PASS;
- 005 parser regression: PASS;
- six JSON evidence files parse;
- `git diff --check`: PASS.

The inherited 003/004 Lua-oracle tests could not be freshly replayed because no
installed Python interpreter currently has `lupa`. No package was installed
to alter the environment solely for this replay. This is recorded in
`d-final-checks.txt`.

No .NET/frontend product build was required for this checkpoint because no
product, C# product-test, or frontend source changed; changes are bounded to
offline Python recovery tooling/tests, derived evidence, and documentation.

One pre-existing LastWar process was visible during a final process-name check.
It was not launched, attached to, instrumented, controlled, focused or
terminated by this task.

## Acceptance boundary

Home/Map remains **PARTIAL**.

No `LIVE_PROVEN` state is upgraded.

Public Treasure/Ghost provider fences remain in place.

Live/shared-desktop work remains **ON_HOLD_BY_OWNER**.

Exact future static closure requires legitimate recovery of the decrypted
0.3.17 module-table plaintext containing the three bridge provider definitions.
Until then, the host-side contracts in this delivery are the maximum
source-backed semantics available from the supplied artifacts.
