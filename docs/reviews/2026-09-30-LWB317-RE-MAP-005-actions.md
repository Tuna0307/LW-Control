# LWB317-RE-MAP-005 — 0.3.17 Map actions and plunder control plane

Date: 2026-09-30

## Result

State: `EXACT_CONTRACT` for the current 0.3.17 public action surface, local
scheduled-job persistence, result envelopes and provider boundaries. Game-side
effects are intentionally provider-owned and require current-client/live proof.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Machine-readable action evidence:

- `evidence/lwbridge-0.3.17/map/action-handler-discovery.json` —
  `179383662A142F71D8B57E2E8318886DDDFABDF2BD75C39EB6B9C088B9934DCC`;
- `evidence/lwbridge-0.3.17/map/action-handler-summary.json` —
  `AABCFD0546FD9937D8550679A14C2D819A3884449C9D53A7E7B121CFE6C01134`.
- `evidence/lwbridge-0.3.17/map/plunder-worker-surface.json` —
  `CAD4667CA43769BAFB3A7F205601F4965FF7E2A8A23D9EDEB4912146830EE8A3`;
- `evidence/lwbridge-0.3.17/map/plunder-worker-functions.json` —
  `8412F10E315EC8E72319BF73AFAC10A16AB84C304260C724846ABCC9BC9441FC`.

## Navigation

`map_coordinate_jump` handler `0x13F4F3-0x1408EA` requires positive server,
X and Y; invalid input returns
`INVALID_COORDINATE / server ID and coordinates must be positive integers`.
The protected call is exact `gotoWorldCoordinate` at `0x13FDB6` with a 5 s
host timeout. Success returns the requested `{serverId,x,y}`.

`map_march_follow` handler `0x1249C8-0x125A89` requires positive server and a
march UUID; invalid input returns
`INVALID_MARCH / server ID and march UUID are required`. It calls exact
`gotoWorldMarch` at `0x125273` with a 5 s timeout and returns
`{serverId,marchUuid}`.

These commands stay behind the high-level action provider; the 0.3.17 local
plane does not invent the current Last War navigation grammar.

## Server jump

Public handler `0x182112-0x182C0D` validates integer server `1..99999` with
`INVALID_SERVER_ID / server ID must be an integer from 1 to 99999`.

Current service `0xDCCDC`:

1. reads `getCurrentServerId`;
2. if already at target, returns `changed=false` without travelling;
3. otherwise calls protected `gotoServer` at `0xDD16B` with 5 s call timeout;
4. polls current server every 500 ms for up to 10 s;
5. if the target never becomes authoritative, returns
   `SERVER_JUMP_TIMEOUT / the game did not switch to the target server`.

Success envelope is exactly consumed as
`{changed,previousServerId,serverId}`. Game operations are serialized and
conflicts expose `GAME_OPERATION_IN_PROGRESS / another game operation is
already in progress`.

## Treasure state / claim

The current bridge service is `0xDD807-0xDE902`.

- `getTreasureClaimStatus` is called at `0xDD8D0`, 5 s timeout;
- `inspectTreasureStates` is called at `0xDDE02`, 5 s timeout;
- exact inspect payload is `{records,refresh:true}`;
- the response projects player UID, alliance ID and per-treasure states into
  the recovered `treasure_claim_states` cache.

`map_treasure_state_refresh` and `_all` enforce
`SERVER_MISMATCH / current game server does not match map data server`.
`_all` reads the persisted season-supplies Treasure population before the
provider inspection; the single refresh path uses supplied rows.

`map_treasure_claim` handler `0x18EE07-0x190734` requires server `1..99999`,
scope `single|boxes|season`, and a target UUID for `single`. Invalid scope is
`INVALID_TREASURE_CLAIM_SCOPE / treasure claim scope is invalid`; lucky-slot
priority defaults true.

The exact persisted candidate query selects nonexpired Treasure rows that are
Supplies types `1,3,4` or complete ordinary Treasure rows, with nonempty/nonzero
UUID, ordered by point index. The protected call is `claimTreasures` at
`0x18FCDA`, 5 s, with
`{serverId,records,claimScope,targetUuid,prioritizeLuckySlots}`.

The current result vocabulary includes
`eligible,queued,skipped,directQueued,scoutQueued,scoutDispatched,claimed,
noScoutSkipped,otherAllianceSkipped,failed`. The current frontend relies on at
least `eligible`, `queued` and `skipped`, then polls claim status while queued.

## Dispatch alliance share

`map_dispatch_share_alliance` handler `0x10C27F-0x10D552` accepts 1..200 rows.
Each row requires decimal UUID plus positive `serverId,x,y,cfgId`; invalid
count/data use current `INVALID_REQUEST` messages. Rows are shared sequentially
through exact protected `shareDispatchTaskToAlliance` at `0x10CD18`, 5 s.

Current frontend success contract is `{shared,failed,sharedUuids}`. No current
frontend `failedUuids` contract is promoted.

## Unified plunder list

`map_plunder_jobs_list` handler `0x10E53B-0x10F6AF` combines Dispatch/Ghost
store read `0x3D1A59` with Truck/history read `0x3CDEEE`, yielding
`{dispatchJobs,truckJobs}`. Optional protected `getMapPlunderServerDay` at
`0x10EB44` can refresh server-day history/pruning, but list remains locally
readable when the game provider is unavailable.

## Dispatch/Ghost scheduling

`map_dispatch_plunder_schedule` handler `0x1336A9-0x1350DF` accepts 1..200
rows with per-row `taskKind=dispatch|ghost`. The recovered UI explicitly adds
that row field when selecting a Dispatch or Ghost task.

Dispatch rows require positive server, decimal UUID, positive completion time,
`plunderAt>=completionTime`, positive expiry strictly after `plunderAt` when
present, and remaining steal capacity when max is positive. The frontend owns
random-delay calculation before the request; the host does not add a second
random delay.

Ghost rows first pass protected `prepareGhostPlunderTasks` at `0x133CA8`, 5 s,
then persist through the same scheduling store. Persisted Ghost job identity is
`ghost:<uuid>` while row JSON retains `taskKind=ghost` and original UUID.

Current 0.3.17 materially changed the historical conflict update. Store
`0x3D778F` reschedules existing rows in
`scheduled|waiting_connection|failed|cancelled|expired`, explicitly sets
`status='scheduled'` and clears `last_error`. `running` and `succeeded` are not
overwritten. This supersedes the old scheduled/waiting-only hypothesis.

Dispatch cancel changes only `scheduled|waiting_connection` to `cancelled`; a
missing/noncancellable job is `NOT_FOUND / scheduled plunder job not found`.
Clear removes terminal `succeeded|failed|cancelled|expired` rows older than the
required cutoff, optionally filtered by Ghost/Dispatch identity. Mutations emit
`bridge://dispatch-plunder-changed`.

## Truck scheduling

`map_truck_plunder_schedule` handler `0x17555F-0x1761C1` accepts 1..200 rows
with positive server, decimal UUID, positive execute time, positive
`maxLootCount` and `robTimes < maxLootCount`.

Current store `0x3D4BB9` archives a prior terminal attempt, refuses a running
replacement, resets attempts for a new terminal replacement, strips stale
`battleWon`/`plunderRewards`, and creates a fresh job ID. Cancel operates only
on `scheduled|waiting_connection`. Clear removes terminal current jobs and
archived history older than the cutoff. Mutations emit
`bridge://truck-plunder-changed`.

## Exact scheduled worker execution

The durable scheduler is not terminal-on-provider-return. Exact 0.3.17 uses an
**arm -> pending -> separate result event** lifecycle. Production therefore
owns independent Dispatch/Ghost and Truck worker loops at approximately
`100 ms`; one slow Dispatch arm must not block Truck execution.

Dispatch/Ghost execution (`0xF0655-0xF2A09`):

- up to `200` armable rows are selected at a time, with a `10,000 ms` arm lead;
- each valid durable row transitions to `running` and increments `attempts`
  exactly once before the batch arm call;
- corrupt persisted targets fail closed before provider arm and do not consume
  an attempt;
- the protected batch arm call is bounded at `5,000 ms`;
- an accepted arm is explicitly non-terminal: the durable row remains
  `running` and is correlated in memory by kind/server/task UUID;
- the exact post-arm result horizon is `15,000 ms`, not the historical
  `30,000 ms` hypothesis. A connected expiry becomes
  `failed / DISPATCH_PLUNDER_RESPONSE_TIMEOUT`;
- a connection-loss arm/result path becomes
  `waiting_connection / DISPATCH_PLUNDER_GAME_DISCONNECTED` without consuming a
  second attempt;
- a terminal result event alone owns normal `succeeded|failed` persistence;
- `DISPATCH_PLUNDER_DAILY_LIMIT_REACHED` fanout occurs only while processing a
  matching terminal Dispatch result. It is not an arm-failure side effect and
  does not fan out to Ghost rows;
- a result-provided `serverDayStartAt` is applied at result handling and drives
  the recovered server-day cache/pruning path;
- process restart recovers in-flight durable rows to
  `waiting_connection / DISPATCH_PLUNDER_CLIENT_RESTARTED` while preserving the
  already-consumed attempt.

Truck execution (`0xE9DBA-0xEBF1C`):

- the same `10,000 ms` arm lead and `5,000 ms` protected arm timeout apply;
- the scheduled row's public `uuid` is the provider `trainUuid`; there is no
  second scheduled Truck identity and no caller-supplied march UUID;
- the arm payload is correlated by
  `{serverId,trainUuid,jobId,executeAt,robTimes,maxLootCount}`;
- arm success is non-terminal and leaves the row `running` with one consumed
  attempt;
- matching terminal results require the current `jobId`; stale job IDs are
  ignored;
- the exact post-arm horizon is `30,000 ms`. A connected timeout becomes
  `failed / server response timeout`, then performs best-effort
  `clearMapPlunderPending` under its own `5,000 ms` protected bound;
- disconnect becomes `waiting_connection / game disconnected`; restart becomes
  `waiting_connection / client restarted`, preserving the consumed attempt;
- a malformed persisted target fails before provider arm with zero attempts;
- result persistence may merge only the recovered result fields:
  `battleWon`, `plunderRewards`, `jobId`, `robTimes`,
  `remainingLootCount`, and `dailyRobCount`. The worker does not synthesize
  these from schedule input.

No exact 0.3.17 evidence was recovered for `plunderRewardsComplete` or a
`RewardNormalizationComplete` persistence flag. Neither is part of this plane.

## Native-only compatibility commands

- `server_jump_history_get` reads the profile setting and exposes current
  `INVALID_SETTING` for corrupt JSON;
- `map_dispatch_plunder_list` returns the local Dispatch/Ghost list;
- `map_dispatch_plunder_retry` requeues only `failed|expired`, clears error and
  emits Dispatch changed;
- `map_truck_plunder_list` returns current Truck/history list;
- `map_truck_plunder_retry` accepts only `failed|expired`, sets `executeAt=now`,
  re-enters the current Truck scheduling/archiving path and emits Truck changed.

These are native compatibility surface, not frontend-used commands in 0.3.17.

## Implementation boundary

`src/LWBridge.Map-0.3.17/MapActionControlPlane.cs` implements current public
validation/result semantics, exact `5,000 ms` protected action bounds and local
scheduled-job/cache behavior. `MapPlunderWorker.cs` owns exact durable
Dispatch/Ghost/Truck due selection, arm/pending/result timeouts, restart and
disconnect recovery. `MapActionProvider.cs` exposes only the protected
high-level names required by current evidence. Provider arm results never stand
in for terminal game results.

`UnavailableMapActionProvider` fails explicitly with
`GAME_CONNECTION_UNAVAILABLE`; it never synthesizes navigation, game claims,
shares or plunder execution. The production Desktop path uses a real
current-client provider; current-client features whose high-level orchestration
is not source-proven must remain explicitly unavailable rather than falling
back to this fake-looking boundary or to historical 0.3.1 behavior.

The deterministic action checks use a fake provider only to prove the local
contract and persistence, including exact arm non-terminal behavior, the
`15 s`/`30 s` result horizons, result-only Dispatch daily-limit fanout, Truck
`jobId` correlation/clear, disconnect and restart handling. Runtime proof is a
separate current-client/live requirement and is not claimed by those tests.
