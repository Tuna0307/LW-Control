# MAP-OFFLINE-ACQUISITION-009 — recover native map acquisition and optimization paths

Assigned by lead, 2026-10-12, following explicit owner request for useful
reverse engineering while the owner uses the computer. Use the existing checkout
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control`, branch
`codex/map-action-providers-007`, PR #9. Preserve product checkpoint `10b73c4a` and
subsequent lead instructions. No new branch/worktree or bulk research import.

## Authority and goal

**This assignment is OFFLINE ONLY.** Read installed game files, decode supplied
client artifacts, inspect managed IL/Lua control flow, implement supported offline
improvements and run headless/inert tests. No Windows-MCP/RDC desktop input or
captures, visible application windows, game/launcher/Desktop UI starts, live RPC,
server switch/scan, claims/plunder/sharing/marches/spending, installation changes,
network fault manipulation or owner-state mutation. No timed automatic live resume.
Existing live permissions remain historical context, not authority to run them
inside this owner-present offline task. Ask no repeated permission for ordinary
file analysis; complete ready offline work. The game language remains Chinese.

Work solo: no subagents, other AI chats, GPT Work or Codex delegation.

Primary goal: determine how the admitted game itself locates and requests map
objects, whether complete acquisition can avoid camera movement or active server
switching, and which per-kind/combined strategies can reduce verified work without
losing scope, values or freshness. Owner's coordinate example:
`[Warzone 2172 #X:550 Y:573]` -> click -> target-server location.
Recover that native workflow instead of assuming our adapter's restrictions are
the game's restrictions. A known coordinate/UID lookup is not whole-map discovery.

Read AGENTS.md, MAP_008_SCAN_METHOD_CONTINUATION.md, latest R5 lead review,
MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md, MAP_007_ACTION_PROVIDER_HANDOFF.md,
the MAP-008 work item and the lead source-inventory proof for this task.
Accepted main/v0.6.0 and Home remain preserved. MAP-008 final locale/UI capture
gates are separate; offline string/catalog fixes may continue, but no captures now.

## A — source inventory and reusable toolchain

Current installed inputs, read only:

- Game root: `C:\Users\chimw\AppData\Local\FunFly\Last War-Survival Game`.
- Managed metadata/IL: `Game\LastWar_Data\Assemblies\Assembly-CSharp.rdl`, plus
  relevant networking/xLua/world assemblies when actual call flow requires them.
- xLua: `Game\LastWar_Data\Plugins\x86_64\xlua.dll`.
- Lua package: `C:\Users\chimw\AppData\LocalLow\FunFly\Last War-Survival Game\lwScripts\LWScripts.data`
  and companion metadata/version files. Current lead read: LWLF format 3, content
  version 24, 18,741 entries; selected navigation entries decode to compact Lua 5.3.
- Installed locale/configuration sources where names, type IDs, dimensions or
  event/list scopes require them. Raw copies and decoded third-party bodies stay
  ignored under a task-local `artifacts/map-009-offline/` directory.

Existing current decoder: `tools/run_live_resource_probe.py` read_lwlf/decode_lenc.
Import/call only read-only helpers, never that script's live main flow. Confirm
current xlua/package/assembly hashes before accepting decoding or old findings.

Historical reusable tools available with `git show research/offline-controller:...`:
`tools/inspect_lastwar_rdl_metadata.py`,
`tools/inspect_current_dispatch_plunder_sources.py`,
`tools/lwbridge317/inspect_map_provider_semantics.py`.
Read imports and copy only needed parser functionality with recorded lineage into
task-local analysis tools; do not merge research or disable v19/v22 hash gates.
Write a current-build wrapper/manifest. Lead found dnfile/dncil/capstone available
in default Python; lupa absent there, not proof that offline Lua execution is
unavailable. Inspect existing runtimes or use isolated task dependencies if needed.
Do not install globally or open GUI decompilers while the owner uses the desktop.

## B — trace the native chat coordinate path end to end

Lead found decoded current package seeds:
`Chat/Other/ShareDecode.luac`, `Chat/Other/ShareExecute.luac`,
`Chat/Controller/ChatController.luac`, `UI/UIChatNew/Component/ChatItem/*`,
`Util/GoToUtil.luac`, `Util/SceneUtils.luac`, `Util/CrossServerUtil.luac`.

Find the actual rich-link/structured-share click caller. Trace decode -> payload
fields -> server/world/tile conversion -> navigation -> native request -> response
handler -> active scene/server/cache changes. The display label may be generated
from structured metadata rather than parsed as the literal example string.

For every edge record exact module/prototype/PC or managed method token/IL offset,
arguments, return/callback and conditions. Explain whether the operation changes
camera, active world, network routing or just asks for an object's position.
Check own-server/remote-server and known world/coordinate boundaries with actual
decoded-body/IL evidence. Names/constants are leads, not semantic proof.

## C — coordinate, identity and direct-position contracts

Trace SceneUtils EncodeWorldPos/DecodeWorldPos, TileToWorld/index helpers and
caller-selected serverId/worldId/worldType. Establish valid ranges, offsets,
rounding and spatial key identities; do not assume one 1000x1000 formula fits all
world types. Reproduce source-backed roundtrips/boundaries offline.

Trace known-object queries separately from enumeration. Current seeds:
`Net/Msgs/Railway/GetMarchPosMessage.luac`,
`Net/Msgs/GetUserInfoMultiMessage.luac`, `Net/Msgs/GetNewUserInfoMessage.luac`,
`Net/Config/MsgDefines.luac`, and GoToUtil's position/city/march paths.
Recover actual request fields, response values, cache scope, freshness and identity.
Does a known UID/UUID yield coordinates without a full scan? Does it require
target-server context, or can a genuine server selector route it directly?
Do not invent account IDs, endpoints or server responses.

## D — actual region/AOI and cross-server request semantics

Inspect managed WorldPointManager/WorldScene, UpdateViewRequest, SendAoiRequest,
_curViewIndex, native point/march caches, response flags, split replies and callback
ordering. Follow callers that normal gameplay uses when moving the view repeatedly.
How does the game keep consecutive requests fresh without returning to home after
each visible movement? Which home resets are our probe policy versus actual game
requirements? Can an official request carry explicit region/server independent of
camera? Recover exact coverage geometry, limits/paging and native LOD/type omissions.

Trace network serializer/dispatch and response handlers through relevant managed
assemblies (including networking/future machinery only when reached). Establish
whether route is chosen from an explicit field, active world connection or global
state. Future IDs must be connection/generation scoped; do not assume FIFO or
globally durable IDs. Distinguish client branch behavior from unobserved server
acceptance, authorization or completeness. Static absence in one path does not
prove a whole-game impossibility.

## E — recover per-kind complete versus partial producers

Cover City, Resource, Monster, Truck, Railway, Dispatch, Ghost and Treasure.
For each inspect normal UI searches/lists and region handlers, not only scan Lua.
Additional actual package seeds:

- `Net/Msgs/FindEnemyPointMessage.luac`, `FindResourceMessage.luac`,
  `FindResourcePointMessage.luac`,
  `Net/Msgs/DispatchTask/DispatchFindNearestPointMessage.luac`.
- `DataCenter/LWRailway/Train/LWTrainDataManager.luac`.
- `DataCenter/LWDoomsday/LWDoomsdayManager.luac`.
- `DataCenter/WorldMarchDataManager/WorldMarchDataManager.luac` and proxy/data types.
- `DataCenter/ActivityListData/ActGhostrecon/*` and reached read-only handlers.
- Reached Radar/Treasure/Supplies point/list/status sources and templates.

Recover complete-population limits: nearest-N, alliance/owner/event/match-server
scope, refresh requirements, caps, pagination, expiry, hidden types and detail
hydration. A fast nearest/list response is not a full-world substitute. Offline
Ghost/seasonal source analysis may proceed while positive live tests remain deferred.
Never implement an attack/claim/share because its request appears in inspected code.

## F — turn recovered semantics into supported improvements

Maintain a candidate table for each type and combinations: full scoped list,
known-object position, explicit-region request, native view-assisted request,
consecutive target regions with final restoration, and shared/hybrid scheduling.
Only include methods with actual source support. Rank eligibility before cost.

First prototype the most promising actual source-supported improvement offline:
consecutive target responses with final Stop/error restoration, or another producer
if recovery shows a stronger complete route. Execute selected actual decoded bodies
under isolated Lua or controlled managed boundaries, recording outbound arguments
and delivered callbacks. Test stale/out-of-order/split/duplicate/missing responses,
wrong server/world, cancellation, owner replacement, restoration and incomplete
lists. Add distinguishing inverses; do not merely test a hand-written assumption.

Implement source-backed CPU/serialization/lookup/planner improvements where current
production semantics and meaningful offline evidence suffice. New game-dependent
acquisition paths lacking genuine qualification stay a separate inert prototype,
not a silently enabled product route or a fake success/provider fallback. Keep one
canonical product implementation and current verified acquisition intact.

Simulated latency/work counts may compare algorithmic request cost, but are not
genuine speed/accuracy benchmarks. Identify exact native witnesses needed later,
with a bounded plan for individual, mixed and multi-server comparisons after the
owner explicitly resumes live work. Do not run that plan now.

## G — evidence, checks and independent handoff

Keep a compact call graph, per-kind capability/coverage matrix, coordinate vectors,
candidate ranking and one continuation. Persist hashes, locators and reproducible
decoder/tests, not giant raw packages/disassembly dumps or duplicated evidence.
Separate current-source recovered behavior, own adaptation, offline actual-body
execution, historical live observation, and unverified server/runtime behavior.

Run source/parser validators and actual offline-body/inverse tests; run appropriate
frontend/native headless regressions/builds for any product changes. Protect owner
data and installed artifact hashes. Preserve prior negatives and unrelated work.
Do not produce a new ZIP per research checkpoint. Commit coherent progress, push
normally, verify direct remote SHA and update the existing PR's precise scope.

Continue ordinary recovery/test failures independently: new supported hypothesis,
retest, self-review, next ready item. Do not stop because a string-only search finds
nothing or original LWBridge's separate protected controller is unavailable.
When a genuine dependency remains, document the exact missing edge, finish other
ready work and distinguish an offline-complete investigation from a working new
native feature. No repeated blind scans, unrelated automation or commercial login
recovery. Independent lead acceptance remains separate.

Return **READY_FOR_LEAD_OFFLINE_REVIEW** when the assigned offline investigation,
supported changes and checks are complete; otherwise precise PARTIAL with actual
continuation. Answer in simple language: what current-game code proves about
camera dependence, remote-server context, direct coordinates, each type's complete
acquisition route, combined efficiency, justified improvements and remaining live
questions. Do not claim the fastest possible scanner or completed whole Map.
