# Milestone C — direct Treasure claim

State: **exact current low-level request/acknowledgement recovered; high-level public batch provider remains blocked by D.**

## Current-v22 request

`Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac`, decoded SHA-256
`ecceef817b99de8ce94518e910e3d4c773dbf1d0825a4878c87d00d59b0b4499`.

`OnCreate`, root/child[0], lines 10-22:

1. serializes `uuid` with `PutLong`;
2. if `targetServer` is nil, obtains `LuaEntry.Player:GetSourceServerId()`;
3. serializes `targetServer` with `PutInt`;
4. when the optional world-Treasure type is non-nil, serializes `type` with
   `PutInt`.

The independently identified current caller is the local
`UIUtil.GetDetectTreasureReward` prototype:
`Util/UIUtil.luac` root/child[119], source lines 4528-4706. Root debug locals
name that child exactly. Its current body performs world-point/type, cross-server,
authority, current-player/reward and per-type gates before choosing direct claim,
claim-info, sharing/tips or no action. Direct branches call
`SFSNetwork.SendMessage(MsgDefines.DetectEventClaimTreasure, uuid, serverId)`.

## Terminal response is not send success

`DetectEventClaimTreasureMessage.HandleMessage`, root/child[1], lines 24-71:

- `errorCode` takes an error/UI path and skips success mutation;
- successful responses process reward/lucky-buff presentation;
- successful responses update RewardManager and call
  `ActDetectTreasureDataManager:OnGetDigTimesMsg(msg)`;
- season/FlowerTrain branches may perform their own proven follow-up updates.

The selected actual decoded request and response bodies are executed directly in
`tests/map_provider_decoded_body_oracles.py`. The oracle distinguishes explicit
target server versus source-server default, optional type, successful mutation,
and a rejected response that must not update reward/dig state.

`PushDetectTreasureClaimMessage.luac`, decoded SHA
`e14bafd7b714c7703cd52d35ab60acc5b6f10aec3721184866ba1f590cc103ef`,
has separate root/child[0] OnCreate lines 9-12 and root/child[1] HandleMessage
lines 14-28; it is not promoted as a substitute for the direct request
acknowledgement.

## C disposition

The direct current request/acknowledgement contract is source-complete as a
subcontract. It is **not** sufficient to enable `ClaimTreasuresAsync` because
that public method owns multiple records, scopes, direct-vs-scout selection,
lucky ordering and aggregate counters. Those unresolved controller semantics are
documented in D.

No blind direct-claim transport was added. The accepted read-only Treasure
inspection lane remains read-only.
