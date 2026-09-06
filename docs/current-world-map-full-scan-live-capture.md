# Current World Map full-scan live proof — 2026-09-06

This checkpoint closes the bounded full-world transport/coverage proof for the
current content-version-12 game build. It extends the earlier one-block,
multi-batch, and concurrent proofs to the recovered normal-map full-grid
contract without adding retry or camera fallback behavior.

## Recovered contract used for the proof

The recovered original `LWC2MapScanner` planner defines a normal `100 x 100`
logical AOI grid with a native transport ceiling of 160 indexes per request.
For full-grid coverage, the clean-room planner produces exactly 65 batches:

- 60 batches containing 160 requested logical blocks;
- 5 tail batches containing 80 requested logical blocks;
- batch 1 is completed serially as a capability check;
- later batches are scheduled with bounded concurrency capped at 8;
- normal-map edge tile coordinates clamp to `WorldSize - 1` (`999`);
- the proof performs zero retries and zero camera/view fallback requests.

These are recovered-original behaviors. The live run below separately proves
that the current build accepts this contract and returns complete correlated
coverage.

## Current-build live result

Candidate `world-full-scan-20260906-1455` was prepared from the exact official
content-version-12 baseline and encrypted round-trip verified before install.
Its package SHA-256 is
`514925be44e3c466dd4e89bc0807698880f81b360b686dafa3e4159752adcd09`;
the injected probe source SHA-256 is
`7ff6063594f40165708c73365958e78a5e04cabe2b54f1015e66826949c4e071`.

The final bounded live run completed at `2026-09-06T06:51:52Z` and proved:

- `10,000 / 10,000` requested logical blocks covered;
- `65 / 65` planned batches completed;
- exact batch distribution `60 x 160 + 5 x 80`;
- peak in-flight request count `8`, never above the recovered cap;
- `65` post-response point captures;
- `16,406` globally deduplicated point identities accumulated across the scan;
- `106` duplicate point observations suppressed by the session dedupe;
- zero retry and zero camera fallback actions;
- the owned response hooks and `isRecvViewPoints` manager flag restored;
- exact protected-file SHA-256 equality before and after the live run.

The response wrapper invokes the game's original handler before the probe
captures `WorldPointManager._pointInfos`. This matters because the loaded point
collection can be replaced as later areas arrive. The full-scan proof therefore
accumulates records after every correlated response instead of treating the
final loaded collection as the full-world inventory.

One additional `world.get.block` protocol response was observed outside the 65
accepted batch completions and was rejected from proof accounting. Completion
depends only on the uniquely matched batch bounds and full logical coverage.

The retained raw live result is
`.codex-live/world-full-scan-20260906-1455/live-result.json` with SHA-256
`f46dc73ba474bdbaa194afa28bd3e54bf2434768f605cd8ed674b1cabc372f47`
and 2,259,053 bytes. `.codex-live` remains local generated evidence and is not
part of the product source commit.

## Proof boundary

**PROVEN current build:** the current game accepts the recovered 65-batch
full-grid request plan; all 10,000 logical blocks can be covered in one bounded
session; concurrency reaches 8 while remaining capped at 8; correlated response
coverage can be tracked independently per batch; point identities can be
preserved across response-driven loaded-area replacement; cleanup restores the
owned hook/manager state and the protected game files exactly.

**RECOVERED original behavior:** the planner shape, 160-index native ceiling,
serial first capability batch, default concurrency of 8, full-grid batch order,
and edge clamp came from the recovered original scanner and were mirrored by the
clean-room probe.

**Still dynamic by nature:** the 16,406 accumulated point identities are a
snapshot of world state during this run. World objects can appear, disappear, or
change after capture. Transport/coverage completeness is proven for the scan;
the captured world contents are not a permanent inventory.

Structured evidence is recorded in
[`current-world-map-full-scan-live-evidence.json`](current-world-map-full-scan-live-evidence.json).
