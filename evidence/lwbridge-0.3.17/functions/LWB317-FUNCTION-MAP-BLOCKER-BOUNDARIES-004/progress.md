# Progress — LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004

Starting HEAD: `8ee3b83f4812d017f813b6ac5fe6a4f12aec7332`.

## A — complete

BT-01 is PARTIAL. Exact frontend/native locators prove the status call is selected-profile scoped even though it carries no Treasure records. Idle Treasure entry consumes playerUid/allianceId/states before any claim; after a claim returns queued>0, status polls optional provider-owned batch state until batch.state leaves running. Therefore no-UUID does not disprove per-target status. The genuine missing edge is the high-level provider/controller storage, transitions, reset/error/profile lifecycle and full batch result vocabulary.

Checkpoint B is active. No provider/action was enabled.

## B — complete

BT-02 is VALID. Exact frontend/host/current control-plane evidence separates row
preparation from later arm/result execution. The current public preparation fence
is preserved, but its terminal-correlation rationale is downstream rather than a
preparation-semantic dependency.

Field audit: ownerServer>0 is exact host admission/current request input;
plunderAt and steal-count aliases are source-backed current-v22 bridge
adaptations; unconditional taskExpireTime>0 is disproven as exact/current-domain
semantics because current TaskInfo initializes expiry to 0 and exact host permits
0/non-positive expiry.

Checkpoint C is active.

## C — complete

BT-03 is PARTIAL/UNKNOWN. The actual decoded direct Ghost success path does not consume UUID, but distinguishing execution proves arbitrary raw fields survive unchanged when present and are not synthesized when absent. Separate push handling consumes serverId/pointId/playerInfo. Lua SFS dispatch is command -> GetMsgType with fresh receive instances and no Lua pending queue. Managed NetworkManager owns a FutureManager with future IDs and pending msgSendInfo state; modified current-RDL CIL tokens prevent promotion of that mechanism to an end-to-end Ghost terminal identity.

All requested checks are green. Status is AWAITING_REVIEW. No product/provider behavior was enabled or changed.
