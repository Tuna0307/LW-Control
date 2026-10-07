# Blocker boundary matrix

| Stage | Missing observable semantics | Bridge-owned adaptation | Downstream capability restriction | Terminal-result correlation | Offline proof | Future witness |
|---|---|---|---|---|---|---|
| Treasure idle status | High-level provider source/cache/freshness for idle `states` | Profile ownership/generation and DTO/cache shell can be modeled without provider behavior | None from claim execution | N/A | `a-status-audit.json`, `a-treasure-status-ownership.md` | Static provider body or optional idle freshness witness |
| Treasure active batch | Batch storage, full fields/counters, terminal vocabulary, reset/reconnect/profile lifecycle | Fail-closed profile-scoped batch owner/generation shell | Real claim execution still unavailable | Provider-owned batch terminal state; underlying per-target transport correlation unrecovered | A evidence | Protected controller source or idle->running->terminal/reset witness incl. profile change |
| Ghost preparation | Exact protected returned-row transform/hydration/rejection beyond host postconditions | `plunderAt`, steal-count aliases, consistency checks are current-v22 adaptation | Later Ghost arm/result execution unavailable independently | Not required for prepare/persist | `b-preparation-audit.json`, decoded bodies | Static preparer source; live not required to split capabilities |
| Ghost expiry | Exact preparer may alter expiry, but zero allowance itself is established | Existing unconditional `taskExpireTime>0` is conservative and can be corrected | None | N/A | B evidence | None needed for zero-allowed correction |
| Direct Ghost response | Complete raw schema and stable identity field | Do not invent; future adapter may preserve explicit identity | Scheduled Ghost execution stays fenced | **UNKNOWN**; handler non-consumption != schema absence | `c-correlation-audit.json`, inert decoded-body test | Static schema/receive source or raw direct response witness |
| Ghost push | Whether push pairs with direct steal; uniqueness/order of `serverId+pointId` | No pairing invented | Push cannot terminal-complete durable task yet | Location identity exists; durable task identity not proven | C evidence | Static pairing source or future direct+push trace |
| Managed FutureManager | Whether `fuid` reaches Lua Ghost raw response and is bridge-usable | Explicit-fuid adapter can be tested but not connected | Real Ghost execution remains fenced | Managed pending mechanism exists; end-to-end Ghost correlation **UNKNOWN** | C audit + managed CIL evidence | Resolvable managed/protobuf source or future raw witness |

## Ready offline implementation candidates

1. **Treasure status/controller shell** — inert profile-scoped idle snapshot + optional batch polling owner/generation retirement. No real provider acquisition and no invented counters.
2. **Ghost preparation/execution capability split** — separate preparation from arm/result capability; evaluate the existing current-v22 one-to-one normalizer only as declared adaptation.
3. **Ghost expiry contract correction** — allow zero/non-positive `taskExpireTime`; when positive require `taskExpireTime > plunderAt`; preserve owner/timing/capacity guards.
4. **Fail-closed Ghost correlation abstraction** — deterministic inert tests for explicit UUID/future-ID/point identity and rejection of missing/ambiguous/mismatched results; do not wire real action send/receive.

All four are implementation candidates for a follow-up task, not changes made by this audit.
