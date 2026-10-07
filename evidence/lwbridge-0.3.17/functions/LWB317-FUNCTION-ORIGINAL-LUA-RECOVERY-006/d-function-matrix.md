# D — per-function recovered / unknown matrix

Reference: LWBridge 0.3.17
SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

The original bridge Lua controller plaintext is **not recovered from the supplied
inputs**. The fixed 0.3.17 host contracts below are recovered independently and
hash-gated by `tools/lwbridge317/audit_original_lua_controller_boundaries.py`.
No current-game Lua or historical 0.3.1 body is substituted for the missing
0.3.17 bridge controller source.

| Function | Recovered exact 0.3.17 host contract | Controller-owned semantics still unknown | Current clone difference | Closure evidence |
| --- | --- | --- | --- | --- |
| `getTreasureClaimStatus` | Public handler `0x115C5D-0x1166BE`; protected provider name at `0x1160CD`; 5,000 ms bound; no feature-specific payload at provider-method boundary. Provider result is observed as `playerUid`, `allianceId`, `states`, optional `batch`. Frontend calls status while idle and, after claim returns `queued > 0`, polls once per second up to 1800 iterations. Poll continues when batch is absent or `batch.state == running`, stops when a batch exists and is non-running, and merges states by UUID. Each frontend call receives the **currently selected** profileId; initiating profile is not pinned. Native profile store has `treasure_claim_states(server_id,player_uid,treasure_uuid,...)`; no native batch table exists. | Internal idle-state source; whether state is recomputed or cached; batch allocation/default state; exact batch counters/state vocabulary; terminal/error/timeout/reconnect retirement; profile-switch/reset semantics inside the Lua controller. | Public current-client provider returns `GAME_PROVIDER_UNAVAILABLE`; current local control-plane method does not fabricate batch/state. | Legitimate recovered 0.3.17 module-table plaintext containing the provider implementation, or a future owner-authorized original-provider witness. |
| `claimTreasures` | Public handler `0x18EE07-0x190734`; provider name at `0x18FCDA`; 5,000 ms bound. Host validates server and scope `single|boxes|season`, requires target UUID for single, defaults `prioritizeLuckySlots=true`, and prepares exact provider payload `{serverId,records,claimScope,targetUuid,prioritizeLuckySlots}`. Host owns persisted candidate acquisition: nonexpired valid-UUID Treasure rows, supplies types 1/3/4 or completed ordinary type 0, ordered `point_index ASC`. Returned result vocabulary includes `eligible,queued,skipped,directQueued,scoutQueued,scoutDispatched,claimed,noScoutSkipped,otherAllianceSkipped,failed`. | Selection/ordering among those already host-selected records; exact lucky-slot priority algorithm; direct-claim versus scout branching; scout completion ordering; batch creation/update ownership; duplicate/retry/error semantics and exact counter transition meanings. | Public current-client provider returns `GAME_PROVIDER_UNAVAILABLE`; current clone preserves outer validation/request shape but does not invent executor behavior. | Legitimate recovered 0.3.17 provider body or future explicitly authorized original-provider result/sequence witness. |
| `prepareGhostPlunderTasks` | Schedule handler `0x1336A9-0x1350DF`; protected provider name at `0x133CA8`; 5,000 ms bound; payload object contains `rows`; result must expose a `rows` array. Returned rows are identity-matched before persistence rather than trusted positionally. Host revalidates Ghost rows: UUID/server identity, `taskKind=ghost`, positive `ownerServer`, positive completion, `plunderAt >= completionTime`, positive expiry only when strictly after plunder, and remaining capacity when max is positive. Ghost persistence/execution remains a later separate phase. | Whether preparer re-reads/hydrates current game task state; exact row transformation/additional fields; exact rejection/drop policy; output ordering; any controller-owned normalization beyond host-visible postconditions. | Public current-client provider returns `GAME_PROVIDER_UNAVAILABLE`. Internal `PrepareGhostPlunderRows` is only a declared current-v22 adaptation and is not promoted as original semantics. The retained 005 numeric/malformed-string reader discrepancy remains: original host accepts/defaults those forms where the helper rejects before its later string branch. | Legitimate recovered 0.3.17 provider body; no execution/result-correlation evidence is required merely to recover preparation semantics. |

## Original body availability

The three controller names occur in the outer 0.3.17 host because the host sends
those protected provider calls. They occur **zero times** in each exact carved
resource:

- encrypted `bridge-scripts.dat`;
- `xlua-proxy-secure.dll`;
- `xlua-proxy-plain.dll`.

That negative does not mean the functions are absent from the encrypted
plaintext: AES-GCM ciphertext does not preserve plaintext strings.

The secure/plain proxies do contain the fixed Lua bootstrap/runtime markers
`XluaBridgeHandlePipeMessage`, `__XluaBridgeLoad`,
`__XLUA_BRIDGE_PROFILE_ID`, and `@bridge-scripts.dat`. Combined with the
recovered loader, this places provider/function dispatch in the loaded bridge
Lua module table, not in a second plaintext proxy implementation.

## Exact remaining dependency

The encrypted package and complete 0.3.17 decrypt algorithm are available. The
legitimate decrypt inputs are not complete:

1. a valid signed `package-key.envelope` for this package/context is absent
   from the bounded supplied artifacts;
2. the matching persisted P-256 CNG private key is owner-state
   authentication/entitlement material and was not read, exported, or bypassed.

Therefore actual controller-body recovery is blocked by exact missing legitimate
inputs, not by an unknown cipher or package format.

No provider was enabled and no product behavior changed.
