# LWB317-FUNCTION-MAP-BLOCKER-BOUNDARIES-004 — worker review

Status: **AWAITING_REVIEW**
Date: 2026-10-07
Assignment checkpoint: `8ee3b83f4812d017f813b6ac5fe6a4f12aec7332`.
Scope: read-only product audit; derived evidence/tests only; live/shared desktop remained `ON_HOLD_BY_OWNER`.

## Executive disposition

- **BT-01 Treasure status ownership: PARTIAL.** The prior explanation “no UUID argument means per-target status cannot be obtained” is rejected. Exact frontend/host evidence supports provider-owned idle state plus an optional active batch/cache created by earlier claim work. The genuine remaining blocker is the high-level provider/controller storage, transition/reset/error/profile lifecycle and full batch result vocabulary.
- **BT-02 Ghost preparation versus execution: VALID SEPARATION.** Preparation, returned-row validation, persistence and later arm/result execution are distinct stages. Terminal Ghost correlation is a downstream execution restriction, not a semantic prerequisite for preparation. Exact protected preparer transformations remain partially unknown.
- **BT-03 Ghost response correlation: PARTIAL/UNKNOWN.** Current direct handlers do not consume UUID, but that does not prove the raw schema has no identity. Extra raw fields survive unchanged in the actual decoded handler. A separate push path consumes `serverId/pointId/playerInfo`, and managed networking has a real FutureManager pending/future-ID mechanism. Safe durable-job terminal correlation is still not recovered.

## Pinned artifacts

- Original reference EXE SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- Exact frontend index SHA-256: `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
- Exact Map panel SHA-256: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- Current v22 `LWScripts.data` SHA-256: `248F3AEAC712B3F14F86BFF37A0C365E467897A2248403837C44C1B774F05B22`.
- Current `Assembly-CSharp.rdl` SHA-256: `BFB740B4570C58BD2BCC7FB83F9B83D8121CE10FB1BF49040E9FB8B08E958B3E`.

## BT-01 — Treasure status ownership

The exact status wrapper calls `map_treasure_claim_status` without a feature payload, but the generic bridge wrapper injects the **currently selected** `profileId` into every call that does not already carry one. This means “no UUID” is not “no ownership/context.”

The exact Treasure entry invokes status before any claim and consumes `playerUid`, `allianceId`, and `states`; `states` are merged by Treasure UUID. An active `batch` is optional in this idle case.

After `map_treasure_claim` returns `queued > 0`, the frontend polls status every 1,000 ms for at most 1,800 iterations. It merges returned `states` on every poll. Missing `batch` continues polling; `batch.state == running` continues; a present non-running batch stops and triggers refresh.

Original host status handler is `0x115C5D-0x1166BE`; protected `getTreasureClaimStatus` reference is at `0x1160CD`, with 5,000 ms timeout at `0x1160E6`.

**Established:** active batch may legitimately be provider-owned state created by earlier claim work; idle status exists without active batch. **Not established:** where the high-level provider stores/recomputes idle states or batch state, complete batch counters/states, reset after terminal/error/timeout/reconnect, and exact behavior if selected profile changes while an old frontend poll callback is still running.

## BT-02 — Ghost preparation versus execution

Exact `map_dispatch_plunder_schedule` handler is `0x1336A9-0x1350DF`. The frontend already requires decimal UUID, positive `completionTime`, and base `plunderAt`; it owns optional random delay before the host call.

For Ghost rows the host validates input, invokes `prepareGhostPlunderTasks` at `0x133CA8` with 5,000 ms bound, requires returned `rows`, indexes/matches prepared rows by UUID, re-normalizes them, then persists durable identity `ghost:<uuid>`. The later worker independently arms persisted rows and awaits a terminal result.

Field audit:

- `ownerServer > 0` is exact Ghost host admission and a current Ghost request input.
- `completionTime`/base `plunderAt` are exact host scheduling fields.
- current-v22 `protectTime` supports bridge adaptation `plunderAt = completionTime + protectTime*1000`.
- current `stealList` count and template `steal_maxtimes` support declared `stolenCount` / `maxStealCount` aliases.
- unconditional `taskExpireTime > 0` is **not exact**: original host permits non-positive/zero expiry and only constrains a positive expiry to be after `plunderAt`; current `ActGhostreconTaskInfo.__init` explicitly initializes `taskExpireTime = 0`.
- strict alias-equality checks are current consistency guards, not recovered original preparer behavior.

The first genuine preparation gap is the protected preparer's own returned-row transformation/hydration/rejection semantics beyond these host-visible postconditions. No source establishes that terminal steal confirmation is needed for preparation.

## BT-03 — Ghost terminal response correlation

Current `GhostReconStealMessage.HandleMessage` consumes `errorCode`, marks the raw table, and forwards that same table to reward handling and `ActGhostreconManager.GhostReconStealHandler`. The manager consumes `stealTimes` and `reward`; neither body reads UUID/ownerServer/pointId.

The new inert oracle executes the actual decoded v22 body. A synthetic UUID/ownerServer/opaque identity survives unchanged into the manager if present, and no identity is synthesized when absent. Therefore handler non-consumption is not schema absence.

`PushGhostReconStealMessage` is a separate `push.ghost.recon.steal` command. Its downstream current consumer reads `serverId`, `pointId`, and `playerInfo`, proving push-side location identity but not durable task UUID or direct-request pairing.

`SFSNetwork.SendMessage` resolves `GetMsgType(cmd)`, creates/serializes a message, and sends `SendLuaMessage(cmd, bytes)`. Receive again resolves `GetMsgType(cmd)`, creates a fresh empty message instance, and passes the raw table to its handler. No selected Ghost/SFS Lua body uses `getFutureManager`; no Lua request-instance pending queue or source-backed single-flight ordering rule is present.

Managed metadata adds a lower-layer candidate: `NetworkManager` owns `_futureManager`; `FutureManager` owns `_futureId`, `_sendInfos : Dictionary<int,msgSendInfo>`, `getFutureId`, `onSendRequest(fuid,msgId)`, and `onServerMsgCome(fuid,serverTime)`; `msgSendInfo` stores future ID/message ID/send time; the RDL uniquely contains `fuid`. Modified CIL metadata tokens prevent the current parser from naming the SendLuaMessage branch end-to-end, so this is **managed future/pending evidence**, not a promoted Ghost terminal key.

The first missing correlation edge is therefore:

`server/managed direct Ghost response -> Lua raw response table -> stable identity usable by durable Ghost job correlation`.

Static protobuf/managed receive source could settle this without live testing. Otherwise a future owner-authorized raw-response/ordering witness is needed. Until then correlation is UNKNOWN, not impossible and not safe.

## Ready offline implementation candidates

1. **Treasure status/controller shell:** inert profile-scoped idle snapshot plus optional batch polling owner/generation retirement, using injected snapshots only. Do not implement real claim/status acquisition or invent batch counters.
2. **Ghost preparation/execution capability split:** separate preparation capability from arm/result capability; evaluate the existing source-backed one-to-one normalizer only as declared current-v22 adaptation. Keep execution fenced.
3. **Ghost expiry contract correction:** allow zero/non-positive `taskExpireTime`; when positive require it to be after `plunderAt`; preserve owner/timing/capacity guards.
4. **Fail-closed terminal correlation abstraction:** inert tests accepting only explicit UUID/future-ID/point identity and rejecting absent/duplicate/ambiguous/mismatched results. Do not wire real Ghost send/receive.

## Distinguishing checks

- `audit_map_blocker_boundaries.py --checkpoint A`: PASS.
- `audit_map_blocker_boundaries.py --checkpoint B`: PASS.
- `audit_map_blocker_boundaries.py --checkpoint C`: PASS.
- current provider semantic inspector: PASS.
- `tests/map_blocker_boundaries_lua_checks.py`: 4/4 PASS under Lua 5.3.
- `tests/map_provider_decoded_body_oracles.py`: 4/4 PASS under Lua 5.3.
- `tests/map_provider_semantics_lua_checks.py`: 3/3 PASS under Lua 5.3.

Tooling-only negatives retained during the audit: the first A locator used an over-brittle minified substring; the first compact B body dump requested a non-existent presentation key; the first C audit incorrectly expected `GetMsgType` in the method constants rather than the decoded upvalue list. Each failed before changing product/runtime state and was corrected to the actual decoded structure.

## Safety / acceptance

No production behavior was changed. All current provider unavailable states remain intact. `tools/current_overview_bridge.lua` Ghost scheduled-action safety guard remains intact. No Last War/LWBridge launch, attach, desktop input/capture, owner runtime/config mutation, or real network/game action was performed.

Historical SEMANTICS-003 evidence is not rewritten. This review narrows three blocker explanations only; it does not upgrade global acceptance or claim LIVE_PROVEN. Project-lead acceptance remains separate.
