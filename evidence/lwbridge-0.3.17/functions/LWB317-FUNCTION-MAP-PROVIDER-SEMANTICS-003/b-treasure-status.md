# Milestone B — Treasure status

State: **BLOCKED public provider; recovered current producers are EXACT_CONTRACT offline/static.**

## Original high-level boundary

The exact 0.3.17 service `0xDD807-0xDE902` calls protected
`getTreasureClaimStatus` at `0xDD8D0` with no feature payload and a 5 s host
bound. The host returns that provider JSON unchanged. The recovered frontend
consumes `playerUid`, `allianceId`, `states`, and, while a claim batch is
running, `batch.state`; it polls the no-argument status call after
`claimTreasures` reports `queued > 0`.

This is a bridge-specific aggregate/status surface. It is not the same operation
as the accepted read-only `inspectTreasureStates(records, refresh=true)` lane.

## Current-v22 producers recovered

Exact installed package SHA-256:
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.

`Net/Msgs/RadarCenter/DetectEventGetTreasureClaimInfoMessage.luac`
decoded SHA-256
`a8216497185f8a12ccf5d9608400c0070f6cae692d0a2007b2adf770f1f853ff`:

- `OnCreate`, root/child[0], lines 11-23: serializes `uuid` as long,
  `source` as int (default Chat), and `targetServer` as int (default
  `LuaEntry.Player:GetSourceServerId()`).
- `HandleMessage`, root/child[1], lines 25-32: any `errorCode` takes the
  error/UI path and returns; only error-free responses call
  `RadarCenterDataManager:GetDetectEventTreasureClaimInfo(msg)`.

`DetectEventGetTreasureClaimInfo.luac`, decoded SHA-256
`cb3df2e7ad2c34155cbb49c0d234e1d57a6320e6a64badb76b8af74825bafefe`:

- `InitData`, root/child[3], lines 37-88: maps `cfgId -> eventId`,
  `num -> remainNum`, point/owner data, player `array`, reward,
  `createTime`, and `targetServer`.
- `GetSelfClaimState`, root/child[4], lines 91-104: scans
  `treasureClaimPlayerInfoList` for the current player UID and returns
  `DoubleClaim`, `NormalClaim`, or `NoClaim`.
- player-specific big-reward/lucky-buff helpers are root/child[5-7],
  lines 107-145.

`RadarCenterDataManager.GetDetectEventTreasureClaimInfo`, decoded manager SHA
`cb2a7830902addb3a2a75619474df0cead2092ac6e2d14277f84a43fb055bba8`,
root/child[74], lines 1000-1017, replaces one singleton
`detectEventTreasureClaimInfoData`; Chat broadcasts it and World opens its view.

Global player budget/reset data is separately recovered from
`ActDetectTreasureDataManager.luac`, decoded SHA
`04a5b7f426b65bb9af1d1bed9855a37ccf0fe33acbdc0e50d02e2df79dfd3645`:

- `OnGetDigTimesMsg` root/child[4], lines 70-117 updates claim/dig budget data,
  including `treasureRewardLimit`;
- `CheckTreasureReachDailyLimit` root/child[5], lines 119-134 uses `dailyGot`;
- `OnPassDay` root/child[6], lines 136-138 resets the daily cache;
- current/max dig-time functions are root/child[8]/[10], lines 148-210 and honor
  event-expiry timing.

## Competing explanations tested

1. **Global budget alone is the no-argument status object** — insufficient:
   the original frontend consumes per-row `states` and an active `batch`.
2. **Status issues one fresh per-target claim-info request** — unsupported:
   the original protected call has no record/UUID input, while the current
   request constructor requires one UUID.
3. **The current singleton claim-info model is the batch cache** — disproven by
   control flow: every successful response creates/replaces one
   `detectEventTreasureClaimInfoData`; no current body defines the original
   provider's multi-target/batch result.

## First unresolved edge

`current per-target claim-info + global budgets + provider-owned active claim controller`
**-> exact original no-argument**
`{playerUid, allianceId, states, batch...}`.

The active batch producer, its state vocabulary/defaults and correlation are not
present in the inspected game package or repository history. Enabling the public
method would require inventing that bridge-specific state machine.

Exhausted local sources include the exact 0.3.17 host/strings, current frontend,
Map action pass-through, all current claim-info/model/UI cross-references, budget
manager bodies, prior history and prior DEEP-RECOVERY candidates.

Minimum evidence to unblock: the actual protected high-level status body or an
equivalent current source that defines the returned status/batch object and its
lifecycle; otherwise a future explicitly authorized live witness of that provider
result. Live work remains on hold.

Disposition:
`GetTreasureClaimStatusAsync` remains `GAME_PROVIDER_UNAVAILABLE`.
