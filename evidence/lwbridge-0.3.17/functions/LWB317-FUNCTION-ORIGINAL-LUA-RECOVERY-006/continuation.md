# Continuation — LWB317-FUNCTION-ORIGINAL-LUA-RECOVERY-006

State: **AWAITING_REVIEW**.

Completed checkpoint commits:

- A: `a9196752ac4148c7b76b04d3af6e17df4da96277`
- B: `65bde8312dae7ef96d5db0d86d7c73df8b4f4d18`
- C: `00101ba1a6de25f15944a792d64ebeb3ddea4c76`
- D: `247c40a17fb36ddcbd4c8a56ec5279dc92ab8092`

Lead review should start with:

- `a-payload-inventory.md/json`;
- `b-crypto-contract.md/json`;
- `c-extraction-and-proofs.md` and `c-extracted/manifest.json`;
- `d-controller-boundaries.json`;
- `d-function-matrix.md/json`;
- `d-final-checks.txt`;
- dated worker review.

Exact remaining dependency for original controller bodies:

1. a valid signed `package-key.envelope` matching this 0.3.17 package/context;
2. matching persisted P-256 CNG private-key state.

The encrypted package and decrypt algorithm are recovered. The envelope is not
present in the supplied bounded artifacts, and owner-state private key material
was intentionally not read/exported or bypassed.

Do not treat current Last War v22 Lua, the current clone helper, or historical
0.3.1 source as the missing 0.3.17 bridge-controller body.

Until new legitimate evidence exists:

- keep Treasure status/claim public current-client provider methods fenced;
- keep public Ghost preparation fenced;
- keep Ghost scheduled execution safety guard/fences;
- retain 005 numeric/malformed string reader discrepancy as a known clone
  difference;
- retain status profile-poll edge: frontend injects currently selected profile
  on each poll call rather than pinning the initiating profile;
- do not claim exact Treasure batch lifecycle, claim/scout ordering, or Ghost
  preparation transforms.

Live/shared-desktop remains `ON_HOLD_BY_OWNER`.
Final acceptance remains with the project lead.
