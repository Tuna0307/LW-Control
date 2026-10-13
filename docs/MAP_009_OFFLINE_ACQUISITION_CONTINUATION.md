# MAP-009 — current-game native acquisition, offline continuation

**Lead update, 2026-10-12:** delivered offline scope at `56e472c8` is accepted
for source inventory, selected-body/model tests and the reviewed small CPU grouping
change. This is not a new native-route/speed/release acceptance. Owner now allows
background testing without mouse or window-state manipulation. Continue the
specific native trials under `work-items/LWB317-MAP-BACKGROUND-ACQUISITION-010.md`.
The original offline delivery narrative below remains historical.

**Status:** READY_FOR_LEAD_OFFLINE_REVIEW, subject to verification of the final
commit and independent lead acceptance. This is a source-led, offline investigation
on `codex/map-action-providers-007`, existing PR #9. Whole Map functionality,
remote whole-server reads, final-only restoration, and globally optimal scan
speed remain unqualified. The MAP-008 R5 locale/release gates remain separate.

## Evidence boundaries and reproducibility

Current installed **LWLF format 3 / content 24 / 18,741 Lua entries**, package
SHA-256 `d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495`;
current `Assembly-CSharp.rdl` SHA-256
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
Exact module encoded/decoded hashes, original Lua function source lines,
bytecode instruction hashes, WorldPointManager metadata tokens/signatures and
reproduction command live in
[`MAP-009-OFFLINE-NATIVE-SOURCE-LOCATORS-2026-10-12.json`](proof/MAP-009-OFFLINE-NATIVE-SOURCE-LOCATORS-2026-10-12.json).

`tools/map_009_offline/inspect_current.py` verifies the current package hash
and decodes selected actual v24 Lua, including original source lines and PC
disassembly. `inspect_managed.py` checks the installed RGMD hash before
managed IL inspection. Only parser primitives were selected from the historical
`research/offline-controller` tools; original v19/v22 hash gates were not
altered. All raw/decoded/disassembled third-party material stays untracked and
ignored under `artifacts/map-009-offline/`. Inert body execution uses
`execute_selected.py` with explicit Python callbacks, no client/server I/O.
Do not treat an inert test callback or an inspection string as a server witness.

## A. Chat coordinate: actual route and limits

1. `UI/UIChatNew/Component/ChatItem/ChatItem.luac`,
   `ExecuteChatEvent` original line 229, PCs 22–33: a clickable post broadcasts
   `ChatEventEnum.CHAT_SHARE_EXECUTE_CMD` and the structured `_chatData`.
   `Chat/Controller/ChatController.luac`, `OnExecuteChatEvent` root/38,
   source line 654, PCs 0–3 calls `executeChatData`.
2. `Chat/Other/ShareDecode.luac` root router PCs 173–178 maps
   `PostType.Text_PointShare` and `Text_PointShare_Alliance` to root/0.
   Root/0 original lines 15–98, PCs 1–8 requires `x` and `y`; PCs 198–225
   builds the display using the localization keys
   `POSITION_COORDINATE_CROSS(sid,x,y)` or `SHOW_POS(x,y)`.
   Therefore `[Warzone 2172 #X:550 Y:573]` is an example *rendered label*:
   clicking the share does not require parsing that literal string.
3. `Chat/Other/ShareExecute.luac` root router PCs 12–17 also selects root/0
   for these two post types. Its root/0 lines 13–76, PCs 0–12 requires world
   navigation to be allowed and non-null `x`,`y`; PCs 13–40 converts
   `x`,`y`,`worldId` (default 0),`sid` (default self server) to numbers.
   PCs 40–54 computes `SceneUtils.TilePosToIndex` and `TileToWorld` for
   `ForceChangeScene.World`.
4. That same body PCs 55–124 branches for nonzero `worldId`/other-world
   context: `GoToUtil.GotoDragonPos` or
   `BattleFieldUtil.TryEnterBattlefieldFromShare` with
   `{worldId,worldType,pointId,serverId,allianceId,actDragonGroup}`, plus
   chat-close callbacks. PCs 125–157 go through
   `GoToUtil.GotoWorldPos(worldPos,zoom,focus,callback,sid)` on the ordinary
   world route; other-world return/error branches follow.
5. `Util/GoToUtil.luac` `GotoWorldPos` root/85 lines 3332–3380,
   PCs 0–19 resolves self/current/selected server, PCs 27–58 handles the
   season/battlefield branches, PCs 58–62 calls `GoToServerPreCheck`, and
   PCs 73–81 initiates a cut-scene/callback for the cross-server route.
   `Util/CrossServerUtil.luac` `JumpToServerByServerId` root/22
   lines 430–486, PCs 75–120 constructs a jump context with `serverId`,
   `type` and `JumpServerMode.CrossServerMoveCity`, follows target info/
   world navigation. This is a **server/world navigation transition**;
   it does not demonstrate a background remote-server full-map read.

Boundary: the above recovers the dispatch, payload, coordinate conversion and
high-level navigation branches. The obfuscated lower-layer server-join transport
and the authoritative post-jump server response/cache freshness still require
bounded native tracing. No live coordinate link was clicked during MAP-009.

## B. Coordinates, known targets and direct queries

**Executed actual v24 bodies under inert fixtures:**

| Actual source / original Lua lines | Contract | Tested values or negative |
| --- | --- | --- |
| `SceneUtils.EncodeWorldPos`, 1266–1272; `DecodeWorldPos`, 1253–1264 | Encodes `(x << 32) | y`; decodes upper/lower 32-bit fields; nil encodes 0 / decodes (0,0) | Roundtrips (0,0), (1,1), **(550,573)**, (999,999), (1000,0), high unsigned low-half; swapped X/Y inverse |
| `SceneUtils.TileXYToIndex`, 303–320; `IndexToTilePos`, 244–280 | For selected `WorldTileCount` or `CityTileCount` N: valid tile 0..N-1 and index `y*N+x+1` (1-based); invalid tile returns 0 | World N=1000 and city N=50 roundtrips; x=-1, y=-1, x/y=1000 rejected, city (550,573) rejected |
| `Railway/GetMarchPosMessage.OnCreate`, 11–17 | Serializes `serverId:int`, `worldId:int` (default 0), `marchUuid:long`, `marchType:int` (default 2) | Exact serialized fields for target server 2172 and alternate 2231, with optional defaults |
| `WorldGetDetailMessage.OnCreate`, 10–25 | Serializes known `point`, `serverId`, `worldId`, detail options and optional `uid` | Exact point + server/world keys and inverse selected server 2173 |

The 1,000/50 bounds above are explicit **test inputs**, not proof all special
worlds have those dimensions. `TileToWorld` additionally uses scene/world
conversion; independently recover its floats, projection and clamping before
substituting a handwritten camera position. Packed position alone does not
encode serverId/worldId; preserve those independently in every cache key.

`Railway/GetMarchPosMessage.HandleMessage` root/1 source lines 19–29
consumes an accepted reply and calls
`GoToUtil.MoveToWorldMarchAndOpen(positionId,marchUuid,serverId,worldId)`.
`WorldGetDetailMessage.HandleMessage` root/1, lines 26–42 updates
`WorldPointDetailManager` then broadcasts `EventId.WorldPointDetail`.
`GetWorldMarchCurPosMessage.OnCreate` lines 6–8 serializes one `uuid` and
`HandleMessage` lines 10–17 broadcasts `WorldMarchGetCurPos`.
`GetUserInfoMultiMessage` and `GetNewUserInfoMessage` similarly address
named/known user identities; **a guaranteed positional response for arbitrary
UID has not been established**.

Direct requests are supported **as serialized client messages** without any
world sweep parameter. Actual remote server acceptance, per-server permissions,
connection routing and server-side completeness are unobserved. None discovers
every unknown UUID/point or replaces AOI enumeration.

## C. Native AOI, request/response and why our probe returns home

The current `Assembly-CSharp.rdl` declares `WorldPointManager` fields
`_curViewIndex`, `_addViewIndex`, `_splitLastAOIRequest`,
`isRecvViewPoints`, `_cachedServerId`, native point/march caches, and
an AOI send cap `once_max_request_count=160`. Relevant current IL methods:

| Managed token | Method | Source-established boundary |
| --- | --- | --- |
| `0x06003565` | `UpdateViewRequest` | View-associated state update, follows camera traversal |
| `0x06003568` | `StartViewRequest` | Entry into native view request process |
| `0x0600351d` | `SendAoiRequest` | Explicit indices `List<int>`, LOD and block bounds, **no explicit serverId argument** |
| `0x0600356a` | `SendViewRequest` | `(Vector2Int,int,int)`: tile, view LOD, serverId input; IL 0x21–0x33 mixes explicit arg and current state |
| `0x0600351b` | `UpdateLWAoi_Normal` | Camera-anchor-derived visible AOI selection |
| `0x06003551` | `HandleViewPointsReply` | Response/point-cache consumption; split and receive-state qualification |
| `0x06003557` | `HandleViewUpdateNotify` | Native update notifications |

`tools/current_live_resource_probe.lua` lines 5410–5600 holds an internally
shifted camera, widens FOV/aspect for coverage, resets response flags, and
ultimately invokes `UpdateViewRequest(true)` for the currently shipped
coverage route. Its isolated diagnostic `requestMode=direct` at 5558–5597
populates `_addViewIndex` and invokes private `SendAoiRequest`, but **still
operates with camera/manager state**, so it does not qualify camera-free reads.
`request.viewLevel>=0` calls `SendViewRequest(tile,level,serverId)`, but
this probe path also holds a camera shift. Previous direct-list/region negative
attempts do not prove game-wide impossibility.

The active 1000×1000 normal-world coverage has 100×100 LOD0 native AOI
indices (10,000). Native snapshot/footprint tests and the final 10,000-index
union check are in `CurrentClientMapBlockSource.FastCity.cs` 1380–1450
and 500–505; arbitrary other worlds, LODs or omitted monster families do
not inherit that qualification. The native split flag, cache evictions,
unmatched updates and response arrival order all limit what can be inferred
from a single response flag. No global request-ID/owner-generation semantics
for native replies were established from obfuscated managed dispatch.

Normal world navigation advances successive AOI view requests and cached
indices without first returning to a home tile. Our probe instead resets
flags for one temporary target, waits for point and march snapshots and only
then restores home. `tools/current_live_resource_probe.lua` 5722–5754
waits for **home-view** response flags and no pending split;
`CurrentClientMapBlockSource.FastCity.cs` 813–816 and 1422–1429 validates
`deferRestoreUntilResponse`. This came from earlier missing spatial rows on
same-tick restore and is a conservative **probe contract**, not a native rule
that game movement must restore home after every target. Removing these
restore cycles is promising, but **not yet safe to ship** without equivalent
bounded response and Stop/error/restoration proof.

**Remote-server conclusion:** a chat jump navigates by serverId and a known
march/point detail request carries serverId/worldId; one native
`SendViewRequest` overload accepts an explicit serverId. Conversely,
`SendAoiRequest` itself has no server argument and consumes global
WorldPointManager state. This mixed evidence leaves full arbitrary remote AOI
without active world switch **unresolved**, neither proved possible nor
globally impossible. Investigate connection-bound routing/authoritative
response server identity before enabling it.

## D. Per-kind source and completeness matrix

"Coverage-qualified" refers to an admitted region/identity/LOD strategy and
its native AOI union, **not perfect ground-truth whole-world recall**.
"Partial" means the alternative lists/searches cannot substitute for a
complete census on the available client evidence.

| Kind | Complete-enumeration candidate | Other **native** acquisition routes | Alternative scope/limits and enrichment |
| --- | --- | --- | --- |
| **City** | LOD0 native point AOI coverage + client identity checks | `WorldGetDetailMessage(point,serverId,worldId,uid)` for a *known* point, `GetUserInfoMulti` for known UIDs | No proven whole-server player list. Detail is per-known-index; unknown city positions still require discovery. UID position/routing unresolved |
| **Resource** | LOD0 AOI `ResourcePointInfo` + detail hydration of idle/used nodes | `FindResourceMessage`; `FindResourcePointMessage` | Filtered level/type/position search returns point candidates, no whole-server count or pagination witness. Resource quantity/status detail may need separate refresh |
| **Monster** | LOD0 AOI point/march union including special types | `FindMonsterMessage(pointId,level,isBoss,searchRange,monsterType)`; LOD2 coarse map for *zombie boss only* | A nearest match is partial. LOD2 cannot be assumed to include seasonal RunningMonster/Doom Walker; active protection details remain limited |
| **Truck** | Native AOI march coverage, post-sweep reward/expiry enrichment | `LWTrainDataManager.TryGetTrainList(true)` truck `ls` response/cache | Visible/event list, refresh cadence and server-wide cap unknown; no proven complete truck universe outside its event list. Expiry and loot enrichment retained |
| **Railway** | AOI march capture + refreshed native Train alliance list merged by exact UUID | `LWTrainDataManager.TryGetTrainList(true)` alliance train response; `GetMarchPos(serverId,worldId,UUID,type)` | Alliance/list scoped; cannot assert server-wide completeness. Known-UUID position available with server/world selectors. Existing private direct-only Train list route remains deliberately unused |
| **Dispatch** | LOD0 AOI dispatch point capture | `DispatchFindNearestPointMessage` | Nearest/event/owner-specific point, no exhaustive list or paging evidence; expiry/state may change |
| **Ghost** | LOD0 AOI `GhostreconPointInfo` (when world/event has it) | `GhostreconGetTaskList`, `GhostReconGetAllianceTaskList`; `ActGhostreconManager.GhostreconGetTaskListHandler` + bubble positions | Self/alliance event task scope, not all unknown global Ghosts; refreshing/expiry and season population unqualified by live positives |
| **Treasure** | LOD0 AOI treasure/radar point capture (available ordinary subset) | `DetectEventGetTreasureClaimInfoMessage(uuid,source,targetServer)` for known item; DispatchTreasure self/alliance exchange-info lists | Known UUID/detail or separate self/alliance exchange scope, not whole-world treasure. Seasonal families and timestamps unqualified |

Evidence for alternative message serializers and response handlers is indexed
with actual v24 hashes/lines in the locator proof. Examples:
`FindMonsterMessage.OnCreate` lines 11–40 sends `pointId,level,isBoss,
searchRange,monsterType`; `HandleMessage` lines 41–55 broadcasts
`END_SEARCH(pointId,uuid)`. `GhostreconGetTaskListMessage.HandleMessage`
lines 16–25 forwards to `ActGhostreconManager`; the manager's
`GhostreconGetTaskListHandler` lines 87–111 consumes `taskList`,
not a global unfiltered point universe. `DetectEventGetTreasureClaimInfoMessage
.HandleMessage` lines 25–32 forwards an accepted detail reply to
`RadarCenterDataManager`. Pagination, unknown exclusions, refresh and server
authorization have **no complete witness** for these alternate lists.

## E. Selection and acquisition candidate table

| Route | City | Resource | Monster | Truck | Railway | Dispatch | Ghost | Treasure | Combined/full-world decision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Existing LOD0 AOI + kind parsing | Candidate | Candidate | Candidate | Candidate | Candidate | Candidate | Conditional | Conditional | **Preserve active production**; one 10k-AOI sweep shared between selected spatial kinds |
| Exact known UID/point/UUID detail | Partial | Partial | Partial | Partial | Partial | Partial | Partial | Partial | Faster for *known* targets, cannot discover all unknown objects |
| Official train list | — | — | — | Partial | Partial | — | — | — | May merge scoped railway once after sweep; direct-only full replacement fails completeness qualification |
| Other native search/task/exchange lists | — | Partial | Partial | — | — | Partial | Partial | Partial | No established whole-server scope/pagination/coverage |
| Explicit region `SendAoiRequest` | Unverified | Unverified | Unverified | Unverified | Unverified | Unverified | Unverified | Unverified | Method exists; need camera independence, split/correlation, omission and remote-routing witnesses |
| Consecutive targeted regions + one final restore | Prototype | Prototype | Prototype | Prototype | Prototype | Prototype | Prototype | Prototype | **Inert, not shipped** until native response-owner freshness, positive values and exact Stop restoration qualify |
| All kinds/remote server from active home | Unresolved | Unresolved | Unresolved | Unresolved | Unresolved | Unresolved | Unresolved | Unresolved | Explicit IDs in some messages do not prove server connection/whole-world access |

**Supported production improvement:** resumed region-band
`CaptureBatchAsync` now indexes pending blocks into AOI groups once instead
of filtering the full 2,500-block list for every group. Group membership,
projection order, native request count/arguments, response validation, existing
home restoration and failure handling remain the same. This is a CPU lookup
reduction for partial/resumed batches; no legitimate live seconds or recall
increase can be assigned to it. Mixed selections already co-acquire selected
kinds in one AOI visit; additional list hydration should remain after coverage.

**Inert next-route prototype:** `consecutive_region_trial.py` models a
per-owner/session/PID/generation/server/world request, each region's complete
point/march reply segments, union AOIs, idempotent duplicates, list totals,
cancel/owner replacement and exact home tile/zoom/FOV/aspect on terminal.
Actual v24 native replies are *not known* to contain synthetic trial
`request_id` or `generation`; a real adapter must derive and attest those
from owned calls, not trust invented server fields. Its model tests include
distinguishing inverses and fail closed. It does not send a request or touch a
game process, and is not hooked into the scanner.

## F. Validation and next bounded native witnesses

- `python -m unittest discover -s tools/map_009_offline -p 'test_*.py' -v`:
  **11 passed** (four selected real-bytecode executions; seven adversarial
  inert final-restoration/response models).
- `python tools/map_009_offline/build_source_inventory.py`: independently
  reproduces 26 current Lua module hashes/method locators and 12 current
  managed method signatures, rejecting changed installed hashes.
- `dotnet build tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj
  -c Release -p:SkipCanonicalProductionUiBuild=true --no-restore`:
  **PASS, 0 warnings / 0 errors**.
- `dotnet tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll`:
  **HOME_FEATURE_DELIVERY_CHECKS_OK**, existing headless Home/Map provider,
  owner/Stop/publication/name cases; zero game launches.

When owner presence restrictions end and the owner explicitly authorizes a
bounded live phase, do these **specific** witnesses before any production
change: (1) record official chat click's active-server/world cache, outgoing
route and authoritative completion for own vs other server; (2) instrument
consecutive same-server regions with controlled distinct object positives,
split/late reply, AOI union, replacement owner, cancellation and one exact
final restore; (3) compare explicit `SendViewRequest` / `SendAoiRequest`
against camera-assisted snapshots, including LOD omission and all selected
kinds; (4) for a fixed known point/UUID, inspect explicit remote server detail
acceptance while the active context remains unchanged; (5) *only if allowed
and valid* attempt independent full remote AOI coverage, verify per-reply
server/world identity, no active server change and same-valued positives;
(6) test train/task/exchange list scope, caps, pagination/expiry and missing
counterexamples before calling any list complete; (7) measure alternating
individual and mixed selections with real clocks and matching positive
coverage/Stop/error gates. No such live work was performed here.

Historical R5 Normal/Fast all-eight medians **209.278s / 207.142s**
(n=2 each; one fully sampled pair) and approximately 90s of cumulative
per-region restore are archival observations, not proof of a removable
90-second speedup. Whole-world recall, best-in-class speed, complete Ghost/
seasonal Treasure and globally camera-free/remote-server acquisition are
not accepted. Existing PR #9 must still pass independent lead review.
