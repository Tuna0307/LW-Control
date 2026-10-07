# Milestone D — scout lane and claim orchestration

State: **scout admission/march creation EXACT_CONTRACT; bridge batch orchestration and claim-completion accounting BLOCKED.**

## Current-v22 Supplies scout lane

`UI/UIWorldPoint/Component/UIWorldPointBtn.luac`, decoded SHA
`d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79`.

`WorldSupplies`, root/child[21], lines 3432-3451:

- reads `GetWorldSuppliesPointDetailData`;
- requires `rewardCount < rewardMax`;
- calls detail `CheckBtnState`;
- only an allowed state calls
  `MarchUtil.LaunchScout(MarchTargetType.SCOUT_SUPPLIES, position, uuid)`;
- exhausted/blocked states show current game tips.

`Util/MarchUtil.luac`, decoded SHA
`80effa59728e223053bca2b99ddfdd963cc0c98d2ec420972a5100a5e23990b4`.

`LaunchScout`, root/child[59], lines 1740-1797:

- obtains `ArmyFormationDataManager:GetFreeScoutFormation()`;
- no free formation terminates through the current game error/tip path;
- captures formation UUID/current-server context;
- creates the formation/hero SFS arrays;
- invokes the generic `StartMarch` path.

The generic march creation is recovered through `StartMarch`,
root/child[63] lines 1840-1952, and `SendCreateMarchToServer`,
root/child[64] lines 1954-2013.

`OnLaunchMarchSuccess`, root/child[58], lines 1709-1737, handles generic scout
launch success categories but contains no `SCOUT_SUPPLIES` token. Full-package
cross-reference found `SCOUT_SUPPLIES` only in EnumType, UIWorldPointBtn and
MarchUtil; no separate Supplies-specific completion/return provider was found.

## Exact 0.3.17 bridge batch contract

Public handler `0x18EE07-0x190734` calls protected `claimTreasures` at
`0x18FCDA`, 5 s, with
`{serverId,records,claimScope,targetUuid,prioritizeLuckySlots}`.

Scopes are `single|boxes|season`; single requires a target UUID. Original
observable counters are:

`eligible, queued, skipped, directQueued, scoutQueued, scoutDispatched, claimed,
noScoutSkipped, otherAllianceSkipped, failed`.

The frontend displays initial `eligible/queued/skipped`; if `queued>0` it
polls claim status until `batch.state` leaves `running`.

## First unresolved edges

1. Exact original selected-record/scope/lucky ordering
   **-> per-record direct versus scout lane and every aggregate initial counter**.
2. `SCOUT_SUPPLIES` march accepted/launched
   **-> authoritative reward/claim completion and provider-owned
   `scoutDispatched/claimed/failed` transitions**.

The current game proves that Supplies launch uses a free scout formation and a
real march. It does not prove that a created march equals a claimed Treasure, nor
does it define the bridge-specific aggregate counters. Treating request/march
emission as success would violate the assignment.

Exhausted local sources: exact 0.3.17 handler/result strings, current frontend
scope callsites, UIUtil Treasure dispatcher, WorldSupplies, LaunchScout,
StartMarch, SendCreateMarchToServer, OnLaunchMarchSuccess, package-wide
`SCOUT_SUPPLIES` references, current/prior repository history and DEEP-RECOVERY
evidence.

Minimum evidence: the protected high-level `claimTreasures` body or equivalent
current controller that defines selection/order/counter transitions and the
Supplies completion edge; otherwise future explicitly authorized live witness
correlating one scout target through authoritative reward completion.

Disposition:
`ClaimTreasuresAsync` remains `GAME_PROVIDER_UNAVAILABLE`.
