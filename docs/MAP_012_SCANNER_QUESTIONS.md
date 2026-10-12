# MAP-012 — owner scanner questions and continuation (2026-10-12)

## Owner MAP-013 finite method selection — 2026-10-12

**Decision for the admitted v24 game: retain the existing serial,
camera-assisted shared LOD0 AOI scanner (QUALIFIED among tested
coverage-bearing methods), with its present type-specific enrichment
and explicit multi-server view transitions. Do not change Normal/Fast.**
This is **not** a claim of globally minimum latency, perfect recall,
another-server passive full-map reading, or complete recovery of the
original commercial application's scanner. The method decision for
accessible proven cases is supported; the overall MAP-012/013 objective
is still **PARTIAL** because source-owned second-region send→reply/
segments→cache identity and cross-server attribution remain unresolved.
The accepted baseline is preferred over a faster-looking method that
returns no independently verified objects.

The historical R15/R16 **original** JSON receipts have now been
replayed through the fail-closed
`tools/map_009_offline/map013_native_view_audit.py` (not a new
game observation). In both, the **first** region at (455,425) had
28 real native Resource point rows and 26 Monster march rows. In the
**second** region at (545,495), `_curViewIndex`/`_addViewIndex`
locally selected 80 LOD0 AOIs including 4954, but the two actual
receiver snapshots showed `_msgViewIndex` length **9** with target
**absent**. Native ready flags subsequently became true at roughly
219 ms (R15) / 194 ms (R16); **all three target point/march
families remained at zero**. The seven retained native
`ResPointInfo` candidates were each rejected as **outside the
second selected AOI**. R16 additionally called the source
`SendAoiRequest` explicitly, yet its immediate global send
count did not advance (203→203). Crucially, this is a demonstrated
**divergence between locally selected AOIs, observed message-view
indices, and populated target cache**, not proof that the server
refused or never received the request, nor proof those seven old
records are all the game held. No matching native outgoing packet,
response header, segment generation or insertion callback was saved.
The first **observable** failed boundary is the missing second-region
source/cache population; the first **causal** failed branch remains
unidentified. R14 independently proves that logical game-camera
movement without actual cameraAnchor movement can retain an old
first-region subscription. The R15/R16 custom-anchor route fixes
*local geometry* but not verified fresh reception.

### Finite evaluated method families

These are scoped classifications, not universal existence/impossibility
claims. The full per-kind direct-source boundary is in the table
below and in `MAP_009_OFFLINE_ACQUISITION_CONTINUATION.md`.

| Route / useful variants | Current classification | Observation and exact boundary |
| --- | --- | --- |
| Existing shared serial camera-assisted LOD0 discovery with target→home restoration | **QUALIFIED** for the admitted current-server available-kinds native sweep and context-switched multi-server delivery | Genuine 340 app→Lua probes, 10,000 recorded AOIs, 2,500 logical blocks, independent sampled native fields and completed dataset publication. Retains one active camera request. AOI coverage is not universal object recall. |
| Consecutive camera-assisted regions, one home restoration | **REJECTED_FOR_OBSERVED_CASE** R8–R16, broader support **UNRESOLVED_WITH_EXACT_EDGE** | R13/R14 logical tile changed while anchor/old nine AOIs persisted; R15/R16 target AOIs advanced to 80 but `_msgViewIndex` remained nine old AOIs, 7 stale/out-of-AOI candidates, no fresh target rows/segments. Need target-scoped outbound→receiver→insert witness and exact full camera/manager restoration. |
| Camera-independent `SendAoiRequest` / `SendViewRequest` / `StartViewRequest` variants | **REJECTED_FOR_OBSERVED_CASE** R4/R5/R10/R16; untested native subscription variants **UNRESOLVED_WITH_EXACT_EDGE** | Correct local AOIs/return values and generic traffic do not demonstrate source-accepted target reply. Source's 160-AOI split path and manager/current-server state make bare caller args insufficient. Match wire request and incoming native segments before retry. |
| Coarse LOD2 world/filtered monster, resource and nearest/official searches | **COMPLEMENTARY_ONLY** | Coarse LOD2 specialized Zombie Boss and nearest/filtered Resource/Monster/Dispatch queries lack generic all-family, all-coordinate unknown-object census/paging proof. Useful to locate or enrich selected kinds, not replacement for full LOD0 coverage. |
| Official Train/Railway lists, march positions and activity/season lists | **COMPLEMENTARY_ONLY**; Ghost-positive and seasonal Treasure **OWNER_DEFERRED** | Train list refresh assists Railway enrichment but previously omitted Truck witnesses; known march positions require UUID, while event/season task lists are scoped and may be absent. No complete unknown-map census. |
| Known City/User, Point, Truck, Treasure, Resource and Ghost details | **COMPLEMENTARY_ONLY** for known-identity enrichment | Actual v24 request constructors exist and can provide fields for known targets. Caches are not guaranteed world-complete. Do not identify a new object by querying unknown UUIDs or copy a requested server into a response. |
| Remote known-point detail without active server switch | **UNRESOLVED_WITH_EXACT_EDGE** | R12 active 2212 / target 2213 returned matching pointId but native parsed `serverId=0`; `WorldPointDetailManager` caches solely by pointId. Need raw `scrServer`/callback/connection lineage and same-coordinate wrong-server, stale and retired-connection inverses; absence of literal server echo alone is inconclusive. |
| Remote *region* and complete *unknown map* without switching | **UNRESOLVED_WITH_EXACT_EDGE** separately | `SendAoiRequest` lacks an explicit target server argument and uses active manager/view state. The existence of explicit serverId in known-object requests cannot prove remote AOI subscription or full-map discovery. Require verified remote response generation and complete region then world coverage. |
| Existing shared mixed pass + type-specific enrichment | **QUALIFIED** for admitted selected kinds with positive witnesses | Mixed City/Resource/Monster/Truck/Railway/Dispatch extraction reuses one AOI pass and merges per-type dictionaries. Known Railway/Truck lists and details remain kind-specific; sample-positive Railway scope must not be assumed where absent. |
| New hybrid shortcuts, list-based census or skip-restoration acceleration | **UNRESOLVED_WITH_EXACT_EDGE** (not promoted) | Reusing known UUID/list metadata can be complementary; no independently qualified incremental time/coverage improvement or new native sent-call reduction was measured. Missing rows are not fast results. |
| Passive two-server scan via actual view switching and exact return | **QUALIFIED** for previously tested bounded examples only, no speed comparison | Archived 2212→2213→2212 Truck+Railway+Dispatch Fast 384.380s / City Normal 383.255s, both n=1 and transition/return overhead separately recorded. This switches world context; it is **not** a camera-free or remote-without-switch route. |

**Per-kind selection:** City/player, Resource, Monster, Truck,
Railway and Dispatch retain the shared LOD0 discovery and the
conditional corresponding detail/list/march enrichment. Their
single-kind historical times are shown below. Mixes use that same
discovery, never a sweep per selected kind. Ghost positive
and full seasonal Treasure remain deferred; neither is silently
recorded as positively census-qualified. Current and other-server
scans must use authoritative active context and verified server-view
switch/return; no direct remote without-switch source is enabled.

**Performance:** the genuine historical Normal/Fast all-eight
medians were 209.278s and 207.142s respectively, **n=2 per
route**; this is not an accepted comparative advantage. 340 probes,
one actual inflight camera request, a native LOD0 10,000-AOI
union and 2,500 publication blocks apply to the admitted full
1000×1000 current world. Roughly 90s of per-region restoration
was observed across a historical whole scan, but is *not*
recoverable speed savings until safe consecutive region reception
and exact restoration qualify. Type-specific detail and publish
costs are in historical proof receipts; exact source-owned native
wire calls and paired per-kind marginal costs remain unavailable.
No candidate passed a new two/three-fresh-populated-region accuracy
gate, so there are **no defensible candidate n≥3 alternating
benchmarks** or speedup percentages. Existing measured result ages,
enrichment, publication, switch/return and Stop fences are preserved.
Accuracy is sampled (25,393 compared native values, zero errors
across seven genuinely populated kinds), not perfect recall.

**Owner-visible behavior:** no production algorithm, Normal/Fast
settings, saved results, profile owner boundaries, localized
names, Home or Auto Stop-before-return changed. Full scans
continue to take minutes and require the currently supported
camera/view restoration. No diagnostic-only native
code or Ghidra artifacts ship with the application.

**Next exact discriminating observation:** one task-owned,
hash-compatible, source-owned **passive** event/payload trace at
the real `WorldPointManager.Init` registered native message IDs
1433/661/2821011/2821014, `SendAoiRequest`/transport dispatch,
`HandleViewPointsReply` entry/accepted segments and
`AddPointInfo`/march insertion. Time-bracket first populated
region and second independently populated region with the
request's server/world/LOD/manager generation, native payload
AOI IDs, and resulting cache keys. Source-side native delegate
matching to those event IDs remains unresolved; recording just
their counts or registering an unscoped listener would not
make a reply attributable. Compare an identical coordinate
on another server and a retired-owner packet for remote
qualification. This must distinguish **not sent**, **sent
without a reply**, **reply discarded/misbinding**, **cache
insertion elsewhere**, and **valid empty**. Another identical
R16 synthetic anchor/flag trial cannot do so. Only then
reconsider restoration removal/remote region modes and
their alternating n≥3 performance campaign.

**MAP-013 monitored continuation:** successful native child/PID/CPU/memory
control and one 1,800s-allowance original-function attempt, safely
interrupted at 659s due to owner-machine memory pressure; useful
decompiler-phase progress is **OBSERVABILITY_INSUFFICIENT**. Exact
request/reply/cache attribution remains unobserved. See
`MAP_013_REA_NATIVE_RECOVERY.md` for immutable experiment locators.
No candidate met the fresh second-region native-positive gate;
Normal/Fast and product behavior remain unchanged.

**MAP-013 continuation:** see `MAP_013_REA_NATIVE_RECOVERY.md` and its compact
proof. Project-local Ghidra 12.1.4 / JDK 21 are checksum-verified; REA genuinely
returned original-associated x64 PE assembly, pseudocode and references.
Original EXE full auto-analysis and four large Rust functions timed out, but
selective Ghidra decompiled **five smaller original EXE functions** including
the `map_scan`-related state branch and linked original call paths.
**MAP-013 lead corrections:** `0x140191253` is now retained in full (16,815
characters), source-range checked, with explicit excerpt truncation; four
large functions were retested with verified boundaries and 75s each but
still timed out (not an impossibility conclusion). Hash-locked
managed v24 request/handler bodies and R15/R16 receipts still lack independently
correlated second-target native reply segments.
**2026-10-12 MAP-013 causal observer update:** eight new exact-owned
live instrumentation attempts are now recorded in
MAP_013_NATIVE_CAUSAL_TRACE_CONTINUATION.md, with raw SHA256 audit in
proof/MAP-013-NATIVE-OBSERVER-2026-10-12.json. Actual v24
GameEntry.Event was reached through the static CS proxy; source EventIds
1433, 661, 2821011, 2821014 were additively registered and a **661
numeric-payload event** was observed. This is **not** an AOI wire reply.
One official WorldReady transition was independently observed on
server 2212/world 0; one private observer reflection error was fixed.
A later attempted first-region native trial did **not** reach region
dispatch because WorldReady was unavailable, and a fail-closed runner
error was corrected. All owned games exact-exited and scripts restored.
No first/second-region native packet/segment/cache correlation,
population witness or performance candidate is new. **The Q1–Q8
answers and production disposition below remain PARTIAL.**
Original negative trial history remains retained. Same branch and background-only
authority; leave Normal/Fast unchanged.

**Disposition: PARTIAL.** The strongest tested accurate method remains the
existing shared, camera-assisted v24 LOD0 AOI acquisition. Native receiver
identity for successive regions, authoritative remote-server response
attribution and a universally fastest coverage-complete alternative remain
open. No route which failed its small-data correctness gate was enabled.
Normal/Fast controls, completed data, Home, owner-keyed Map, native names,
mixed acquisition and Auto Stop-before-return are untouched. This is the
single current Q1–Q8 answer matrix for PR #9; acceptance is the lead's
separate decision.

## Q1–Q8 answer matrix

| Question | Plain conclusion and product consequence | Native/source support and limits |
| --- | --- | --- |
| **Q1: how request/receive/cache/retire?** | Camera/viewport determines native LOD/AOI subscription. The manager builds add/current lists, sends split-capable view/AOI messages, receives point segments, updates its caches and processes update/removal notices; expired/out-of-view records are later retired. World marches, train/event and detail managers add other type sources. App-owned results are staged and published separately. | Hash-locked v24 WorldPointManager sender/parser IL plus GameEntry.get_Event and EventComponent.Subscribe. **New live positive:** four actual additive local event subscriptions and numeric event 661, fresh successful WorldReady server 2212/world 0. **Still missing:** correlated AOI wire ID, response segments and cache insertion; these local event IDs must not be mistaken for native reply IDs. Details in MAP_013_NATIVE_CAUSAL_TRACE_CONTINUATION.md. |
| **Q2: complete unknown map without moving camera?** | **Not established for City, Resource, Monster, Truck, Railway, Dispatch, Ghost, Treasure or any mixture.** Direct details, scoped lists and coarse LOD can help known targets or enrichment but are not proven full-population enumeration. Not proven impossible in principle. | R4 explicit SendAoiRequest and R5 direct SendViewRequest timed out; specialized zombie-boss coarse LOD is not generic Monster, and direct Train list omitted previously seen Trucks. See per-kind table. |
| **Q3: adjacent regions with one final home restore?** | **Technically plausible, not accuracy-qualified.** Camera position, native AOI selection and a *fresh populated reply* are independent tests. Keep the conservative validated production target/home-response cycle. | R13 official navigation reached second tile yet nine first-region AOIs remained. **R14:** even after explicit anchor refresh + view update the four native camera-anchor corners remained at first region. **R15:** reversible second-region anchor rectangle yielded 80 correct AOIs and restored home once, but returned **zero** second point/march rows; the private diagnostic's proven status was misleading. **R16:** explicit native SendAoiRequest for those 80 AOIs still returned zero target rows and was rejected. Native response and source field witnesses remain missing. |
| **Q4: another server without switching?** | **Three distinct questions:** known object = explicit-target request exists but actual authoritative remote response **inconclusive**; arbitrary remote region = **not proven**; remote *entire unknown map* = **not proven**. The active/current server uses the existing view without a server switch. Other full-map scans presently switch context and return. | WorldGetDetail request serializes point + serverId + worldId, but R12 active 2212/target 2213 found known point with reported detail server 0: a missing literal echo alone neither establishes nor refutes remote origin. Point-ID cache can be stale/ambiguous. Need wrong-server/same-coordinate/retired-connection controls and response transport/callback identity. |
| **Q5: fastest accuracy-preserving route?** | **Existing shared, serial native AOI sweep is the only coverage-qualified complete route among tested alternatives.** Neither Normal nor Fast is demonstrated consistently faster. No candidate qualified for three alternating full benchmarks, so do not claim absolute optimality. | 340 serial app→Lua AOI probes, 10,000 LOD0 AOI cells and 2,500 logical blocks per archived all-eight pass; live peak simultaneous camera requests = **1** (not UI admission 8/20). All-eight Normal median 209.278s, Fast median 207.142s, n=2 each; only one comparison pair had independently positive sampled witnesses across populated kinds. See measurements. |
| **Q6: verify accurate and complete?** | **10,000-cell native footprint union proves coverage of regions, not census recall or receipt of populated rows.** Cross-check independent native identities, fields, server/world, age and type-specific enrichment; fail closed on missing native provenance. | R15 demonstrates a real experimental failure: **80 correctly selected AOIs, ready flags, zero second native rows** and a misleading private diagnostic completion status. R16 now fails explicitly on absent source rows. Historical witness **25,393 compared values / zero discrepancies** across seven populated kinds remains sampled, not universal. Inert audit covers deliberately corrupted missing/extra/duplicate/stale/wrong server/field cases. |
| **Q7: types shared efficiently?** | **Yes:** a compatible mixed selection traverses the map once, extracts selected point/march families together, merges per-type dictionaries and then performs type-specific enrichment. There is no full AOI sweep per kind. Marginal type time/request count is **not statistically determined** by current unmatched n=1 runs. | One FastFullWorldResumeState.Covered and common ProbeFastCityBatchAsync; one Railway official list refresh when selected; Truck metadata post-processing; conditional Resource detail refresh and Monster protection hydration; City/Resource/Dispatch/Ghost/Treasure point extraction plus Monster/Train march extraction. Restoration per region is measured expensive, but safe elimination unqualified. |
| **Q8: what will the owner test?** | **Production scanner remains as before**: Normal and Fast retain their serial camera-assisted sweep and per-region restoration; Auto retains Stop-before-return. Expect multi-minute full-map scans, no promise Fast always wins. | R4–R16 alternatives not promoted; R15's fast-looking 80-AOI snapshot is explicitly **unqualified** because it had zero source rows. Ghost live positive and full seasonal Treasure deferred. No account-changing actions, merge or release. |

## How v24 coordinates and manager lifecycle connect (Q1/Q3)

* The installed v24 Lua SceneUtils.TileToWorld converts world tile
  (x,y) to Unity x/z via TileSize and a +0.5 tile-center offset;
  actual nine-palaces server offset can be added. World tile is not
  packed point ID, global index or AOI index. SceneUtils world
  tile index is **1-based** (y * worldTileCount + x + 1); EncodeWorldPos
  packs x and y as two 32-bit halves. For city scenes, CityTileCount is
  different and requires a separate bound. Neither coordinate encodes
  server/world ownership.
* The managed WorldPointManager.PositionToAoiBlock halves Unity world
  positions and divides by current _lwAoiBlockSize; AoiBlockToIndex
  is y * _lwAoiBlockCount + x. At the **observed v24 normal-world
  LOD0** that is 10-tile AOIs and 100 columns: target (455,425)
  maps to **4245**, target (545,495) maps to **4954**. An AOI list is
  meaningful only alongside native LOD, size/count, server/world and
  the request/view generation. Coarser LOD2 can use one 1000-tile
  block rather than the LOD0 10,000-cell grid. The app projects four
  10-tile AOIs into each 20×20-tile logical block, 50×50 blocks.
* UpdateViewRequest(true) enters native AOI update, while
  StartViewRequest enables/start-gates manager request behavior;
  SendViewRequest has tile/LOD/server arguments and SendAoiRequest
  accepts explicit AOI indexes but **no explicit serverId argument**.
  Manager viewport/camera anchors and cached _curViewIndex,
  _addViewIndex, _msgViewIndex and _splitLastAOIRequest control
  request selection, outstanding segments and current view state.
  Send success or a receive-ready flag is not a response generation.
* HandleViewPointsReply parses the native SFS payload, dispatches
  point data and updates view state; HandleViewUpdateNotify dispatches
  update, fold-up and removal handling. _pointInfos and view/out-of-view
  dictionaries retain items; DeleteOutOfAoi, DeleteTimeOutPoints,
  ViewCulling and RemovePointInfo can later evict/cull them, while
  WorldMarch/Train/activity/detail managers maintain other type-specific
  data. A matching cached point ID can be old or from a different
  requested source.
* App Lua request IDs, profile ID, session challenge, PID, target and
  timestamp authenticate the **task-owned local producer and result**;
  these are not claimed to be game-server echoed identifiers. The
  sender's R11 manual camera shift, R10 StartViewRequest restart, and
  R13 GotoWorldPos full callback each failed to deliver a second
  independently attributable response. R13 proves its nine native
  current IDs are **LOD0 first-region indexes**, not the requested
  second LOD footprint. It does **not** prove why the game manager
  failed to issue/consume the new second-region view request.

## Per-kind camera-free scope and correct source (Q2)

| Type | Full-discovery source currently retained | Available direct/list/cache mechanism; why not a replacement |
| --- | --- | --- |
| City/player | Native LOD0 player-base point AOIs | WorldGetDetail for known point, GetUserInfoMulti for known UID; not an unknown-player global roster |
| Resource | Native LOD0 ResourcePointInfo AOIs plus quantities | FindResource/FindResourcePoint filtered search and known point detail; no unfiltered all-world coverage/paging guarantee |
| Monster | Native LOD0 WorldMarch plus Doomsday points | FindMonster nearest/filter; coarse LOD2 may serve specialized Zombie Boss but misses generic RunningMonster/Doom Walker families |
| Truck | Native Train/march AOIs and Truck metadata | TryGetTrainList(true) is a scoped train cache/list; previously failed full Truck presence even when it returned data |
| Railway | Native march AOIs, merged once with alliance train refresh | TryGetTrainList(true) allianceTrainList and GetMarchPos by known UUID/server/world; no global railway census guarantee |
| Dispatch | Native LOD0 dispatch points | DispatchFindNearestPoint is nearest point, not a global list |
| Ghost | Native LOD0 GhostreconPointInfo when event available | Ghostrecon task/self/alliance lists; no positive all-map event population or complete scope proof |
| Treasure | Native treasure/radar point AOIs for available families | Known UUID treasure/claim detail and self/alliance exchange lists; full seasonal scope unverified |
| Mixed | One native shared AOI sweep and selected parsers | Combining several partial lists does not make any unknown-object population exhaustive |

The official native lookup and scope signatures above are inventoried in
docs/MAP_009_OFFLINE_ACQUISITION_CONTINUATION.md and
docs/proof/MAP-009-OFFLINE-NATIVE-SOURCE-LOCATORS-2026-10-12.json;
hash-locked inspection entrypoints are under tools/map_009_offline.

## Native response provenance and competing explanations (Q3/Q4)

R11 (native receiver waited for a target-owned completed second AOI, 8
seconds): first (455,425) had **30 AOIs, 28 Resource rows**; actual
second camera settled (545,495); manager _curViewIndex/_addViewIndex
had nine AOIs, _msgViewIndex 0, split flag false, but target 4954
absent. Retaining 455 flags-ready observations did not solve the issue.
Prior R8 took the second request on the same tick; R9 waited for
camera settle; R10 invoked StartViewRequest and observed unrelated
network packet-count increases; R11 waited for receiver rather than
prematurely rejecting its first stale view. All failed their *second*
AOI qualification.

**R13 was a distinct official navigation attempt** rather than another
manual SetCameraPos or synthetic request generation. Source-backed
GoToUtil.GotoWorldPos was invoked for second tile with native
completion callback. At second tile manager reported svLod 0,
_lwAoiBlockSize=10 and _lwAoiBlockCount=100. The nine retained AOIs
were specifically row 42–44, columns 44–46—near the *first*
request, not row 49/column 54. Both native flags were ready but did
not identify a new target-owned segment; the complete eight-second
second wait failed closed. The task-owned game was stopped in a
finally block, no publication occurred; raw game records remain
ignored and are not committed.

Competing hypotheses and distinguishing evidence:

* **H1: same-tick manual camera movement only.** R9 different game
  pump, R11 delayed receiver and R13 official completed navigation
  are counter-evidence; these do not prove all camera lifecycles work.
* **H2: second camera is at another LOD or AOI encoding.** R13 live
  LOD0,size10,count100 and recovered IndexToAoiBlock rule refute that
  explanation for its nine old IDs. A nine-cell set is **not**
  automatically the requested target's footprint.
* **H3: StartViewRequest alone re-establishes the generation.**
  R10 restarted successfully yet lacked the requested AOI; no
  server-owned reply correlation was established.
* **H4: the native manager did not submit a new second target
  viewport, or submitted it but consumed unrelated/old/out-of-order
  segments, or its subscription/cache remained bound to first view.**
  All remain live hypotheses; no native request header, response
  association or subscription generation was captured to distinguish
  them. Receipt flags, request send success and unscoped packet totals
  cannot replace that evidence.

### Continued investigation: R14–R16 (same v24, new owner-approved isolated trials)

**R14, completed official navigation + anchor refresh + view update:**
Second game WorldScene tile reached (545,495), manager LOD0 size10/count100.
RefreshCameraAnchor and UpdateViewRequest(true) returned success, but four
camera-anchor corners stayed at first-region projected x=892.28–929.71,
z=857.87–871.14. The native current AOIs remained first-region
[4244–4246, 4344–4346, 4444–4446], excluding required 4954,
through 465 ready-without-target observations in eight seconds.
This specifically localizes one cause of earlier observations: **the
logical scene tile can change without updating the native viewport
anchor that generates the manager's AOI subscription**. Native request
and receive provenance is a separate remaining dependency.

**R15, native source-derived rectangle (not shipped):** The task
saved the original four corners, then set a reversible WorldScene
6×4-cell anchor rectangle around the second target (cells x51–56,
y47–50) and called UpdateLWAoi_Normal(true). The actual point manager
eventually reported 80 second-region native AOIs containing 4954,
LOD0 size10/count100. First region provided **28 real Resource and
26 Monster rows**, but the **second region returned 0 point, 0 Train,
0 Monster rows**. The private driver labeled the result *proven*
because its experimental sequence gate checked reply flags + AOI
geometry without requiring independently attested source data. That
label was **not an accurate scan completion**. Original home tile
(498,63) was reported returned once; exact final camera anchor,
manager generation and native field/segment restoration were not
independently proven.

**R16, explicit second-region SendAoiRequest (not shipped):**
After that same new native 80-cell add-view set was computed, an
explicit reflected SendAoiRequest was invoked with the active
server's LOD0, target tile, original world bounds, source
_addViewIndex and native block geometry. Invocation succeeded and
the second index 4954 belonged to 80 current AOIs, but native
source rows remained **zero across all three point/march families**.
The second-stage acceptance condition now failed closed as
**r16_second_source_rows_empty_despite_native_footprint**.
Unscoped immediate send counters were 203→203; neither they nor
four later unrelated received packets attribute a reply to target.
Do not infer that the server returned an authoritative empty
population, or that no packet could arrive by another protocol.

**Independent archived population clue (not live exact-time truth):**
The saved genuine server-2212 Map317 records covering the second
6×4-cell anchor area contain **104 City, 26 Resource, 31 Monster and
1 Treasure**. A smaller central rectangle contains **17 City, 8
Resource, 10 Monster and 1 Treasure**. Their timestamps are not a
newly refreshed R16-native field witness, so dynamic movement or
relocation cannot be excluded. They establish a strong reason to
question the all-zero experimental snapshot, not a formal same-instant
missing-count measurement. The R15/R16 route fails its mandatory
positive second-region and source-provenance gate regardless.

**Source-backed distinction for the next experiment:** In v24
UpdateLWAoi_Normal's 160-AOI-bounded send branch, a changed
cameraAnchor can *locally* populate current/add AOI sets and
invoke SendAoiRequest. R15/R16 current=80 and add=80 alone do not
show a target response: the native message AOI set still reported
**nine old first-region entries**, and no returned source data
could be correlated. Recover real outbound command IDs/arguments
and incoming HandleViewPointsReply raw headers/segments and
their manager subscription generation, before repeating a camera
or anchor trial. An AOI index response flag is not a native positive.
The task-owned runs each exact-stopped, Auto stayed off, no recovery
journals or pending account action jobs remained; installed Chinese
game scripts and the production Desktop C#/Lua were returned to their
original hashes. No unrelated processes were stopped.

The real current-game remote detail response handler was decoded
again for this task: Net/Msgs/WorldGetDetailMessage.HandleMessage
passes its raw message to WorldPointDetailManager.UpdateDetail,
then broadcasts WorldPointDetail. WorldPointDetailManager.UpdateDetail
parses the raw fields and caches by **pointId alone** in
worldPointDetailList. Its WorldPointDetailData.ParseData assigns
serverId specifically from incoming **scrServer**, only when present;
it does **not** copy the requested targetServerId. Therefore
R12's native detail.serverId=0 does not license setting it to
2213, and a matching pointId is not proof of source server.
The unmodified response handler does not expose an authoritative
request/connection/server envelope to this probe. Further
known-point correlation requires capturing the real response fields
and connection/request callback context, plus a *different*
independent native witness from server 2213 at the same logical
coordinate versus active 2212, before changing production.

## Measured accuracy-qualified speed (Q5/Q7)

Previous real v24 all-eight trials (Normal,Fast,Normal,Fast) lasted
**210.651, 207.424, 207.905, 206.860 seconds**. Normal:
median **209.278s**, range **207.905–210.651**, n=2.
Fast: median **207.142s**, range **206.860–207.424**, n=2.
Only one pair independently sampled positive actual populated kinds
on both sides. Thus a faster Fast result is an *observation*, not
an accepted repeatable speedup; at least three alternating per-route
runs with comparable positive witnesses would be required to
claim one. Both run 340 serial Lua region probes, **10,000**
AOIs and 2,500 blocks. Home-view restoration costs ~90 seconds
per full-world scan by the historical phase receipts, but R8–R16
show that omitting it is currently unqualified; this is potential,
not earned, savings. Packet counters were global and unscoped:
**actual raw network packet count per scan is unknown**.

Archived single-kind Normal/Fast run times, **seconds**:

| Individual kind | Normal | Fast | Scope |
| --- | ---: | ---: | --- |
| City/player | 191.898 | 191.122 | Positive sampled native fields; n=1 per mode |
| Resource | 198.575 | 195.871 | Positive sampled amount/detail; n=1 per mode |
| Monster | 192.040 | 193.410 | Positive sampled Monster; active protection not witnessed |
| Truck | 189.398 | 189.235 | Sampled goods/expiry; n=1 per mode |
| Railway | 189.832 | 190.637 | Individual positive native field witness **not qualified** at that time |
| Dispatch | 188.584 | 188.675 | Sampled expiry/state; n=1 per mode |
| Ghost | — | — | Positive live event deferred |
| Treasure | — | — | Full seasonal positive deferred; ordinary Treasure has separate mixed positive |

Archived representative mixed selections:

| Selection | Normal (s) | Fast (s) | Limitation |
| --- | ---: | ---: | --- |
| City+Resource+Monster | — | — | No exact matched independent campaign; cannot infer from six/all-eight |
| Truck+Railway+Dispatch | 184.107 | 184.924 | n=1 each, positive Truck/Dispatch; Railway absent |
| City+Truck | — | 187.824 | n=1, sampled positive City/Truck |
| City+Resource+Monster+Truck+Railway+Dispatch | — | 208.874 | n=1; positive City,Resource,Monster,Truck,Dispatch; Railway not positive |
| All eight available | 209.278 (median) | 207.142 (median) | n=2 each; one fully matched sampled-positive pair |

Bounded earlier 2212→2213→2212 **two-server cycles with exact
return**: Truck+Railway+Dispatch Fast total **384.380s** (one
cycle; combined switch/return/orchestration **5.566s**); City
Normal total **383.255s** (one cycle; combined
switch/return/orchestration **5.179s**). Those are not n=3
head-to-head comparisons nor remote scanning without a switch.
Previously recorded source acquisition, enrichment, projection,
publication, positive field counts and oldest result age are in
docs/proof/MAP-008-NATIVE-METHOD-BENCHMARKS-2026-10-11.json.
The per-run source-age clock is necessarily older at the end of a
multi-minute 340-region sweep. Explicit Stop latency for a *new
method* is not measured because no new method passed small data.

The marginal full-world **discovery** expense of adding a kind to
an existing mixed sweep is not a second 340-probe pass. After the
shared AOI pass, marginal work can be: City's main-base parse;
Resource's record accounting and idle amount detail request(s);
Monster's march/Doomsday parse and eligible protection requests;
Truck's lightweight march parse and post-sweep metadata;
Railway's one Train list refresh and UUID merge; Dispatch/Ghost/
Treasure point-family parsing and any event-specific detail.
The exact marginal milliseconds/packet counts *per type* remain
unmeasured with matched stable populations; do not subtract unrelated
n=1 scan totals to manufacture them. The existing production
resumed-band path has already avoided repeated 2,500-block re-filtering.

## Independent accuracy checks and proof boundary (Q6)

The current source requires owner/session/profile/challenge/PID,
server/world, result age, LOD, block dimensions, fresh target AOIs,
native response/restoration flags and no pending split before
merging. It deduplicates rows by stable RecordKey and checks per-kind
source counts, full 10,000 AOI union, then maps to 2,500 logical
blocks. MapDataStore stages blocks and selected records with
transactional completed-run publication; incomplete or Stop/failed
runs must not replace the last completed user-visible data.

That proves the scanner visited each *recorded* native AOI
footprint, not that the game returned every object at every instant.
Test recall against **independent** native known-positive dense,
boundary, sparse and negative regions, not the scanner's own
derived total. For each fixed time bracket, join
(kind,server,world,stable identity) and detect:
missing/extra keys, same-key duplicates, wrong-server/world,
stale/future capture time, level/owner/name/amount/expiry/goods/
protection and other native field discrepancies. Time-bracket
moving trucks, expiring resources/monsters and seasonal objects;
unbracketed nonmatches are **inconclusive**, not automatic
false positives or fabricated negative witnesses.

The historical independent audit observed **25,393 native-value
comparisons, zero discrepancies across seven actually populated
kinds**, which is meaningful sampled agreement, not universal
100% recall. This task adds
tools/map_009_offline/scanner_accuracy_012.py and
test_scanner_accuracy_012.py (pure, unshipped) to reject deliberate
missing, extra, duplicate, stale, wrong-world/server, field and
coordinate/AOI corruption. They distinguish moving/expiring items
that need a native time bracket. Offline fixtures establish *audit
sensitivity*; they cannot be claimed as new live positive coverage.

## Exact open gates and next defensible experiment

1. **Q1/Q3 native lifecycle:** trace actual outbound **second**
   AOI/view command with native viewport, LOD, world/server and
   manager subscription generation, plus HandleViewPointsReply
   inbound reply segment/window/connection and _msg/_add/_cur view
   transitions. Distinguish new target not sent, target reply
   dropped/misbound, stale cached first view, or wrong manager
   subscription. Use official camera navigation from a uniquely
   owned game and two or three independently populated separated
   regions. Prove each native point + march snapshot before one
   final restoration of original tile, zoom, FOV, aspect and manager
   view subscription, including Stop/timeout/owner retirement.
2. **Q4 remote known object/region/full world:** inspect the
   WorldGetDetail native response **scrServer**, raw envelope and
   callback/transport ownership against distinguishing wrong-server
   same-coordinate and stale/retired connection controls, comparing
   fields to independently refreshed server-specific witnesses.
   Only positive authoritative known-point provenance permits a
   *known-object* claim; full region requires genuine region AOIs,
   and entire map requires fresh complete coverage without changing
   live context. A serverId request argument or helper ID alone is
   not evidence.
3. **Q5 best speed:** only after such a candidate passes the small
   correctness/Stop gate, alternate **at least three** eligible
   baseline/candidate runs per route for each available kind,
   City+Resource+Monster, Truck+Railway+Dispatch, City+Truck,
   all kinds and bounded two-server cycles. Measure median/range,
   attributable native request work, cache/enrichment, acquisition,
   publication, switch/return, result age, Stop latency and
   independently time-bracketed field-level missing/extra/duplicate
   sets. No full benchmark is justified for already failed R13.

## Current cleanup and delivery

Only a fresh isolated MAP-012 owned profile was enabled for R13;
its AutoLaunch, AutoReconnect and GameDesiredRunning remained false.
One read-only trial game was started and exact-owned stopped in
finally, with no error on Stop, no task recovery journal and zero
account-changing actions. The temporary Desktop trial command/C#
source was removed, the two built Lua runtime copies restored to
the exact canonical source, installed originals verified unchanged,
and no unrelated Chrome/game process was touched. R13 raw records
and diagnostic Lua remain ignored under artifacts/map-012 rather
than shipping/committing. The canonical runtime scanner, normal/
fast UI and previous MAP-011 extracted release are unchanged.
The lead review and accepted Home/Map evidence stay intact.
