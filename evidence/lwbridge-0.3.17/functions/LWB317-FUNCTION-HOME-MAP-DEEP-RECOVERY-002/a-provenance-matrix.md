# Milestone A — provenance and real-gap freeze

Date: 2026-10-07
Baseline: `6bc72c0493ac0dffa11432713840f0de96800647`

## Evidence classes

| Class | Meaning in this campaign | Examples |
| --- | --- | --- |
| EXACT_0317 | Recovered from the exact 0.3.17 executable/frontend or a hash-gated 0.3.17 static inspector with a traced semantic contract. | Map public commands, SQLite schema/query/store behavior, scan start/status/stop services, scheduled job lifecycle, Treasure host claim/status envelope, Ghost preparation boundary. |
| CURRENT_V22_STATIC | Verified against the exact installed v22 package/RDL/game/xLua artifacts. This proves present model/message/API structure only to the strength of the inspected bytecode/metadata. | World/Map structures, Dispatch/Ghost/Treasure/Train lower-level fields and modules, current compatibility anchors. |
| CLONE_ADAPTATION | Current-client implementation policy used to reproduce an established high-level contract. It is not original protected wire/traversal parity. | `CurrentClientMap317ScanProvider`, command-file probe rendezvous, R3 exclusive runtime ownership, provider retry mechanics, internally derived reward keys. |
| HISTORICAL_031_HYPOTHESIS | Older 0.3.1/v19/v21 evidence that has not been independently promoted for the exact 0.3.17/current-v22 claim being made. | Old proxy RVAs, protected traversal internals, action orchestration inferred only from earlier client versions. |
| UNKNOWN_OR_LIVE | Not established by permitted static/inert evidence. | real game xLua external delegate crossing, positive current populations, exact protected traversal/wire parity, missing high-level Treasure/Ghost current orchestration. |

## Exact 0.3.17 authority

The exact reference executable is
`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`,
SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
The executable identity and the current installed identities are frozen in
`a-source-hashes.json`.

The following are retained as exact 0.3.17 host/local contracts, not inferred from
the rebuild:

- eight public Map kinds: city/resource/monster/truck/railway/dispatch/ghost/treasure;
- scan start/status/stop state, normal=8 / fast=20, staging/publication, failure
  preservation, cancellation and local query ownership;
- exact per-profile Map SQLite schema/query/filter/sort/options/export semantics;
- Treasure host status/inspection/claim request and result envelope, including the
  five-second protected-call boundaries and claim candidate ownership;
- Ghost schedule admission through `prepareGhostPlunderTasks`;
- Dispatch/Ghost and Truck durable scheduler status/result/retry/cancel/clear/history
  behavior recovered in RE-MAP-005.

These facts come from the exact 0.3.17 reviews and their hash-gated machine-readable
evidence under `evidence/lwbridge-0.3.17/map/`. Historical 0.3.1 RVAs are not
promoted.

## Current-v22 static authority

Installed content is v22. The whole package has 18,741 entries and the campaign
successfully decoded all 18,741 without an entry decode error.

A whole-package name/content search found **no** exact occurrence of the original
high-level provider names:

- `claimTreasures`;
- `getTreasureClaimStatus`;
- `prepareGhostPlunderTasks`.

The same name searches against current `Assembly-CSharp.rdl` produced no matching
member report. This strengthens the existing fail-closed compatibility result but
does not prove no semantically equivalent lower-level composition exists.

Lower-level current primitives are present and recorded in
`a-current-v22-lower-level-action-hits.json`, including:

- direct Treasure message key `detect.event.claim.treasure`;
- `LaunchScout`, `SCOUT_SUPPLIES`, `StartMarch`;
- `IsHaveGetReward` and the UUID-correlated
  `WorldBuildTopBubbleTreasureGet` push route;
- Ghost task/config fields `stealList`, `protect_times`,
  `steal_maxtimes` and `GhostreconPointInfo`;
- Train/Railway model paths and `robTimes` / `protectTime`.

These are partial primitives only. They are not promoted to the missing batch/status
or Ghost preparation orchestration because the required request composition,
correlation, error/result normalization, cancellation and ownership chain has not
been established.

## Clone adaptation policy that must remain distinguishable

The accepted rebuild owns several explicit adaptations:

- `CurrentClientMap317ScanProvider` maps current-client acquisition into the exact
  Map317 host plane; traversal/capture mechanics are current-client policy.
- `current_live_resource_probe.lua` uses an isolated command-file rendezvous and
  current runtime reflection. This is not the original protected wire protocol.
- RECOVERY-003 exclusive shared-runtime identity transactions and typed busy
  handling are accepted safety/current-client policy, not recovered original
  native protocol.
- provider block retry mechanics stay below the exact 0.3.17 host surface.
- Truck/Railway reward projection can derive an internal rebuild key
  `reward:<type>:<itemId>`; the original LWBridge `currentGoods` key producer is
  explicitly unrecovered.

The OFFLINE-HOST-CONTRACT lead erratum remains authoritative: the .NET adapter
bounds UTF-16 `String.Length` and honors BOM detection; Lua metadata parsers
independently bound string bytes. No adapter redesign is implied.

## New/remaining actionable branches

| Branch | State after A | Reason |
| --- | --- | --- |
| B eight-kind original backend/projection | READY | Exact host/local semantics exist; per-kind protected producer semantics must be separated from current adaptation. |
| C current-v22 producer mapping | READY | Lower-level current models/messages exist and can be mapped to the existing projection/ingestion layer statically/inertly. |
| D Treasure claim status | BLOCKER CANDIDATE, continue deeper inspection | exact 0.3.17 host contract exists; no current high-level sender/result chain found in whole-package/RDL name search. |
| D Treasure claim execution | BLOCKER CANDIDATE, continue deeper inspection | direct claim + scout primitives exist but protected scope/lucky/scout batching/terminal orchestration is not established. |
| D Ghost preparation | BLOCKER CANDIDATE, continue deeper inspection | task/config primitives exist but no equivalent of exact high-level preparation result is established. |
| E scheduled jobs | READY | exact 0.3.17 worker/store contract and current implementation exist; compare and adversarially exercise. |
| F Home/profile lifetime | READY | preserve accepted R3; change only on a newly demonstrated backend integration discrepancy. |
| G integration/self-review | READY after B-F | actual services/store/controlled providers; no live checks. |
| Live xLua/positive populations | BLOCKED_BY_OWNER_HOLD | requires future owned live witness; synthetic/static evidence must not promote it. |

Milestone A found no production defect by itself. No product or test-source change is
justified at this checkpoint.
