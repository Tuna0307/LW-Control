# DEEP-RECOVERY-002 — lead assessment for the next assignment

Date: 2026-10-07

Worker delivery: `a7c4a03cbd87db5d45030c8117a199d6653729e4`.

## Disposition

The worker delivery remains **AWAITING_REVIEW** for comprehensive acceptance.
This is a bounded, independent provenance and evidence-depth assessment, not a
claim that all A-H suites have been independently rerun or that Home/Map is done.
The next assignment addresses the unresolved provider semantics directly.

The clean starting checkout and direct remote branch both matched the delivered
SHA. The diff from `6bc72c0493ac0dffa11432713840f0de96800647` contains documentation
and evidence changes, with no production/test-source correction. That is consistent
with the worker's stated delivery boundary; fast completion alone is not a defect.

## Fresh independent checks

Executed:

```text
python evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-DEEP-RECOVERY-002/lead-review-2026-10-07/verify-static-boundary.py
```

Result: `LWB317_DEEP002_LEAD_STATIC_BOUNDARY_OK artifacts=5 sources=9 modules=3`.

The reference EXE, current package/game/xLua/assembly and all nine pinned current
source inputs match the worker's provenance records. Read-only decoding of three
selected current-v22 modules matches the saved module hashes. Their header contains
`1b4c75615301` (Lua signature, version byte `0x53`, and following format byte
`0x01`). This establishes available
function bytecode; it does not establish interpreter compatibility or semantics.
The new script imports container/decoder helpers only and never runs the live
probe entry point. No desktop/game/installation/runtime mutation occurred.

## What the evidence supports and what remains open

- Existing host boundaries and fail-closed provider states are recorded explicitly.
- The D packet identifies useful current message/model modules. Its selected
  evidence consists of strings and offsets; the packet does not provide an
  executable, function-body-level oracle for the missing provider compositions.
- Absence of original high-level provider names in the current game is not proof
  that an adapter cannot be composed from the game's lower-level operations.
- The no-argument status provider must be investigated without assuming its
  source is a collection of single-target requests. Local/global player state is
  another hypothesis requiring actual caller/producer analysis.
- Ghost preparation could involve host normalization plus current task data;
  lack of an identically named current API does not settle that question.
- Exact original private algorithms and a source-backed current-client adaptation
  are separate claims. The latter may be possible even when the former is unknown,
  provided the recovered observable contract is sufficiently complete.

No newly reproduced product defect is asserted by this review. No blocked provider
is accepted as implemented. Existing UIUX/R3 acceptance and the live hold remain.

## Next action

Owner relay of `LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003`, one solo campaign with
sequential checkpoints. Reconstruct actual function bodies and response/state
paths, build independent inert semantic proofs, and implement only contracts that
meet their evidence gate. Preserve all DEEP-RECOVERY-002 records. Home/Map remains
PARTIAL; actual xLua binding, populations and actions remain UNKNOWN/live-held.
