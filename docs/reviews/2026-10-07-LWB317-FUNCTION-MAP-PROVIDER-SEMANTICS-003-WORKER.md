# LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003 — worker review

Date: 2026-10-07

State: **AWAITING_REVIEW**

Branch: `research/offline-controller`

Starting assignment HEAD:
`b48b3bdbacd94566019679026126f9a2e4ae8ba0`

Pushed checkpoints:

- A semantic toolchain:
  `012deb4b84ce4d012c640715e3d9c5e138102889`
- B-G semantic recovery / implementation / adversarial validation:
  `57ceb7288fe4c5873f3d4b827078364022b91d84`

Final acceptance remains with the project lead.

## Result

The assignment completed the requested offline/static/inert recovery campaign,
including actual current-v22 function-body decoding, caller/response tracing,
selected actual decoded-body execution under isolated Lua 5.3, canonical
production-module testing, source-backed product corrections and full regression
gates.

None of the three public high-level methods has a complete current operation and
terminal-correlation contract. They therefore remain fail-closed:

| Method | Worker disposition |
|---|---|
| `GetTreasureClaimStatusAsync` | **BLOCKED / GAME_PROVIDER_UNAVAILABLE** |
| `ClaimTreasuresAsync` | **BLOCKED / GAME_PROVIDER_UNAVAILABLE** |
| `PrepareGhostPlunderTasksAsync` | **BLOCKED / GAME_PROVIDER_UNAVAILABLE** |

This is not the same result as the prior name/string-only recovery. The campaign
now has exact current body ranges, request fields, producer mutations, competing
explanations, executable body proof and the first unresolved edge for each public
provider.

## A — semantic inspection toolchain

The exact installed package remains:

- fileVersion 3;
- contentVersion 22;
- 18,741 entries;
- SHA-256
  `248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.

The reference 0.3.17 EXE remains:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

The decoded game chunks are Lua 5.3 version byte `0x53`, format byte `0x01`,
canonical LUAC data/int/number sentinels, and the compact serialized-size header
`04 04 08 08`. The current format omits the ordinary Lua-5.3 `size_t` header
byte while retaining the standard prototype/string/constant/instruction layout.

`tools/lwbridge317/inspect_map_provider_semantics.py` is hash-gated to the exact
current package and provides:

- compact-chunk parsing;
- register-aware root closure-to-method mapping;
- prototype paths;
- source-line ranges;
- instruction PCs/raw words;
- scratch-only compact -> standard Lua-5.3 normalization.

The older adjacent `CLOSURE`/next-`SETTABLE` naming assumption was proven
unsafe because modules batch closures before table assignment. The new mapper
tracks the closure value register.

A broad current package pass parsed 373 Treasure/Ghost/Scout candidates with zero
parse errors.

An independently compiled `lupa.lua53 string.dump` fixture was converted to the
observed compact form, parsed, normalized back byte-for-byte to the original
standard chunk and executed with identical outputs. The game package was never
rewritten.

## B — Treasure status

Current `DetectEventGetTreasureClaimInfoMessage` is exact:

- decoded SHA
  `a8216497185f8a12ccf5d9608400c0070f6cae692d0a2007b2adf770f1f853ff`;
- `OnCreate` root/child[0], lines 11-23:
  `uuid`, `source`, `targetServer`;
- `HandleMessage` root/child[1], lines 25-32:
  error first; successful responses update
  `RadarCenterDataManager.GetDetectEventTreasureClaimInfo`.

The current claim-info model is also recovered:

- `InitData` maps event ID, remaining count, owner, claim-player array, rewards,
  creation time and target server;
- `GetSelfClaimState` scans claim players for the current UID and returns the
  current game normal/double/no-claim state;
- current-player big-reward/lucky-buff helpers are recovered.

Global daily/dig producers are separately exact from
`ActDetectTreasureDataManager`:

- successful claim data updates `treasureRewardLimit`;
- `dailyGot` participates in daily-cap evaluation;
- `OnPassDay` resets that cache;
- current/max dig budgets honor event expiry.

However, the exact 0.3.17 host calls protected
`getTreasureClaimStatus` with **no feature payload**, and the frontend consumes
an aggregate object containing player/alliance identity, per-treasure states and
an active `batch` lifecycle.

Competing explanations were tested and rejected:

- global budget only is incomplete;
- issuing fresh per-target claim-info requests is unsupported because the
  protected status call supplies no UUID/records;
- the current singleton claim-info model cannot be the batch cache because each
  response replaces one singleton object.

First unresolved edge:

`current per-target claim-info + global budgets + provider-owned active batch`
-> exact original no-argument
`{playerUid, allianceId, states, batch...}`.

Minimum evidence to unblock: the protected high-level status body/equivalent
current batch producer, or a future explicitly authorized provider-result witness.

## C — direct Treasure claim

Current
`Net/Msgs/RadarCenter/DetectEventClaimTreasureMessage.luac`:

- decoded SHA
  `ecceef817b99de8ce94518e910e3d4c773dbf1d0825a4878c87d00d59b0b4499`;
- `OnCreate` root/child[0], lines 10-22;
- serializes long `uuid`;
- defaults target server to `LuaEntry.Player.GetSourceServerId()` when absent;
- serializes int `targetServer`;
- serializes optional int `type`.

`HandleMessage`, root/child[1], lines 24-71:

- a response `errorCode` is terminal rejection and skips success mutation;
- successful responses process rewards/lucky presentation;
- successful responses update
  `ActDetectTreasureDataManager.OnGetDigTimesMsg`.

The current local `UIUtil.GetDetectTreasureReward` was identified from debug
local metadata as root/child[119], source lines 4528-4706. Its body performs
current point/type/cross-server/authority/player/reward gates before choosing
direct claim, claim-info, sharing/tips or no action.

A sent request is not counted as successful claim.

## D — Supplies scout / aggregate orchestration

Current `UIWorldPointBtn.WorldSupplies`, decoded module SHA
`d2f099d11172a80e7c8eda63fd410d3360fea965d8269691061be5a7d8f54d79`,
root/child[21], lines 3432-3451:

- reads current Supplies point detail;
- requires `rewardCount < rewardMax`;
- executes `CheckBtnState`;
- allowed state launches
  `MarchUtil.LaunchScout(MarchTargetType.SCOUT_SUPPLIES,...)`.

Current `MarchUtil`, decoded SHA
`80effa59728e223053bca2b99ddfdd963cc0c98d2ec420972a5100a5e23990b4`:

- `LaunchScout` root/child[59], lines 1740-1797;
- obtains a free scout formation;
- builds formation/hero arrays;
- enters generic `StartMarch` / `SendCreateMarchToServer`.

The exact 0.3.17 protected batch call still owns
`single|boxes|season`, `prioritizeLuckySlots`, direct-vs-scout selection and
the observable counters:

`eligible, queued, skipped, directQueued, scoutQueued, scoutDispatched, claimed,
noScoutSkipped, otherAllianceSkipped, failed`.

No current package body defines those bridge-specific aggregate transitions.
Package-wide `SCOUT_SUPPLIES` cross-reference exposes admission/launch but no
separate Supplies-specific terminal reward callback.

First unresolved edges:

1. selected records + scope/lucky policy -> exact lane/order/initial counters;
2. accepted `SCOUT_SUPPLIES` march -> authoritative reward completion and
   terminal aggregate counters.

Minimum evidence: protected `claimTreasures` high-level controller/equivalent
source plus the Supplies completion edge, or a future authorized correlated
witness.

## E — Ghost preparation

Current Ghost task/template semantics establish:

- `CheckCanSteal` uses
  `(serverTime - completionTime) / 1000 >= protectTime`;
- `GetPointStealType` owns protection, steal-list count, per-task max, current
  player prior steal and global daily capacity.

The current fast Ghost record now projects the source-backed scheduler aliases:

- `plunderAt = completionTime + protectTime * 1000`;
- `stolenCount = stealListCount`;
- `maxStealCount = stealMaxTimes`.

The internal canonical `PrepareGhostPlunderRows` performs a strict one-to-one
identity/timing/counter validation and clone. It rejects mutated UUID/server,
counter aliases, pre-protection timing and expiry. The public provider remains
fenced.

Current
`Net/Msgs/Ghostrecon/GhostReconStealMessage.luac`, decoded SHA
`db52842471d2d276dc374236f274214657b70d22770722f4b2883cf6a756a1a3`:

- request `OnCreate`, root/child[0], lines 11-15 serializes `uuid` and
  `ownerServer`;
- terminal `HandleMessage`, root/child[1], lines 18-29 gates on
  `errorCode`, updates reward state and calls `GhostReconStealHandler`;
- that terminal handler does **not** consume `uuid` or `ownerServer`.

Current `Net/SFSNetwork.luac`, decoded SHA
`f9c2762ef603df799da3b502a0c63a50454f97a5a8a6949a39f5948cf0a8fc36`,
constructs a fresh `msgType.NewEmpty()` for incoming responses before invoking
the handler. Request-instance identity therefore cannot supply correlation.

The first attempted Ghost executor design in this campaign depended on an echoed
response UUID. This was disproved before checkpointing and the attempted runtime
rewrite was reverted.

A source-backed safety correction remains: the pre-existing shared scheduled
runtime accepted `kind=ghost` but otherwise used the Dispatch path. It now
fails Ghost in `dispatch_plunder_runtime.begin` before manager lookup, hook
installation or transport. Existing Dispatch behavior is unchanged.

First unresolved edge:

`GhostReconSteal(uuid, ownerServer)` -> incoming terminal response -> exact
scheduled task identity.

Minimum evidence: a current response schema/body with a task correlation key, an
authoritative single-flight/queue contract, or a future authorized witness.

## F — executable proof

`tests/map_provider_decoded_body_oracles.py` executes selected **actual decoded
current-v22 bodies** after only the A-proven header normalization, in isolated
lupa 2.8 / Lua 5.3 with explicit stubs and no game/network access.

PASS 4/4:

1. Ghost request and terminal handler;
2. Ghost protection boundary;
3. Treasure claim request/terminal handler;
4. Treasure claim-info request/terminal handler.

`tests/map_provider_semantics_lua_checks.py` executes the complete production
`current_overview_bridge.lua` under inert seams.

PASS 3/3:

- Ghost fails before send/pending/hook;
- the Ghost guard is independent of Dispatch manager availability;
- the Dispatch path still arms, sends, correlates by its proven response UUID,
  terminalizes and restores the hook.

C# `MapProviderSemanticsChecks` covers valid normalization plus counter/timing/
identity mutations.

## G — adversarial / regressions / build

All executed checks pass:

- decoded-body oracle: 4/4;
- production-module Lua oracle: 3/3;
- provider semantic validator;
- `home_runtime_file_ownership_checks.py`: 4/4;
- `home_runtime_lease_lua_checks.py`: 6/6 under Lua 5.3;
- `--map317-plunder-worker-boundary-check`;
- `--map-campaign-canonical-check`;
- `--profile-runtime-owner-check`;
- `--overview-bridge-lifecycle-launch-binding-check`;
- `--overview-bridge-host-transport-check`;
- `--overview-bridge-normal-composition-check`;
- current-client runtime contract;
- current Map compatibility.

Release build:
`dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj -c Release --no-restore`

Result: **0 warnings / 0 errors**.

The build re-ran the canonical production frontend/package gates successfully:

- `LWB317_PRODUCTION_UI_BUILD_OK`;
- `LWB317_PRODUCTION_UI_PACKAGE_OK`.

No Last War/LWBridge process was present before or after the phase-1 validation,
and the final process inventory count was 0.

## Product changes

Only complete source-backed subcontracts/safety corrections were changed:

1. project Ghost scheduler aliases from current v22 task/template semantics;
2. add strict internal Ghost row normalization;
3. fence scheduled Ghost before the historically shared Dispatch transport when
   no source-backed Ghost terminal correlation exists.

No Treasure action transport was added. The accepted read-only Treasure
inspection lane remains read-only.

## Proof limits / remaining dependencies

No claim is made for:

- exact original protected Treasure batch-controller internals;
- a current high-level no-argument Treasure status producer;
- exact bridge lucky ordering/aggregate counters;
- Supplies scout terminal reward correlation;
- Ghost terminal task correlation;
- real current-game xLua crossing/readiness/scheduling;
- positive live Treasure/Ghost gameplay;
- exact original protected wire/traversal equivalence beyond the recovered
  boundaries.

Live/shared-desktop work remains **ON_HOLD_BY_OWNER**.

Home/Map overall remains **PARTIAL** and no `LIVE_PROVEN` state is upgraded.

## Evidence

Primary machine-readable matrix:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003/contract-matrix.json`

Repeatable validator:

`python tools/lwbridge317/validate_map_provider_semantics.py`

Detailed A-G evidence, body dumps, check logs and compatibility results are under:

`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-MAP-PROVIDER-SEMANTICS-003/`

## Delivery disposition

**AWAITING_REVIEW**.

The project lead should review the three blocker boundaries and the Ghost
fail-closed safety correction before any future live/provider-positive work.
