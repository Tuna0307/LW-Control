# MAP-014 Truck / Train march freshness — source-causal offline qualification

**Scope / handoff:** worker-1, 14 October 2026, `codex/map-owner-fixes-014` at clean `339e241c`. This is an **offline, read-only diagnosis**, not a new game witness and not a production fix. All edits are isolated to this document and `tools/map_014_march_research/`. No app/game session, installed script mutation, network request, packaging or commit was performed.

## October 14 10:26 SGT owned passive-native receipt review (checkout 62f45143)

**Actual native evidence, not synthetic:** `artifacts/map014-lead-live/native-march-source-20261014-102638/live-resource/bulk-aoi-diagnostic-result.json`, `march-source-before-camera.json`, `final.json`; reviewed alongside producer `tools/current_live_resource_probe.lua:3537-3593,5755-5787,6097-6113`, host `src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:906-917`, and static producer checks `tools/map_014_lead_native/test_march_source_telemetry.py`. All were **read**, not edited.

The game-produced diagnostic successfully reached point readiness (`isRecvViewPoints`, `hasReceiveViewPointsReply`, `responseFlagsTransitioned` all true) and **actually observed 60 unique uncapped** `WorldMarchDataManager.GetAllMarchesByCS` UUIDs at point readiness (`marchDictionaryCount=60`, `marchUuidSampleCapped=false`, exactly 60 listed). `viewRect` was x = [-5, 2092.5632], y = [-5, 157]; `cameraViewRect` was x = [0, 2087.5632], y = [0, 152]. These are broad native March-manager rectangles for **this actual baseline-FOV120 probe** (`coverageFov=120`), **not** confirmed FOV20 request/response coverage. There were **zero** extracted `train_march_records` despite the 60 manager UUIDs. This is an **unclassified all-March dictionary**; it does not imply 60 valid Trucks/Trains or any absence of a specific Train. Native point AOI membership is a different domain.

**Critical negative, not papered over:** `marchSourceBefore` is **absent** in the actual `bulk-aoi-diagnostic-result.json`. The misleadingly similar standalone `march-source-before-camera.json` is a `mode=camera_checkpoint` camera-geometry receipt, with no march dictionary/queue/timestamp snapshot. Its `requestId` differs from the bulk request, and its captured timestamp is ~114 seconds earlier; it cannot substitute for the missing pre-shift March state. The recovered producer currently captures `request.details.marchSourceBefore` before shifting (Lua ~5775-5777), and producer serializes/re-attaches it in its current version. The separately owned `test_march_source_telemetry.py` has **four** passive-observer/inverse tests passing, including a regression for an earlier details-reconstruction loss, but **tests on a repaired source do not retroactively change this archived failed native receipt**.

The point-ready stage reports `pendingQueuePresent=true`, `pendingQueueCount=0`, `mergedQueueCount=0`, `observedAtRuntimeSeconds=140.54324565013`. This is **not** an audited drained March queue: the installed metadata type is `GameKit.Base.StepProcessQueue<T>`, and the producer merely reads `safe_get(queue,"Count") or reflected_value(queue,"Count")` (Lua ~3582-3585). There is no source/type signature proof that this property represents all pending work or is the correct read-only getter, even though a numeric zero was returned. A production eligibility gate must reject unverified `StepProcessQueue.Count` provenance. The retained manager's `_lastGetBlockMsgTime` and `_lastHandleBlockMsgTime` appear as strings such as `10/14/2026 2:29:13 AM: -417184563`, whereas numeric `lastGetBlockMessageTicks` / `lastHandleBlockMessageTicks` are absent. Treat the displayed suffix as **opaque**; never parse it as valid C# DateTime ticks or infer receive-before-handle interval from it. No correlated native `WorldGetRectMarchInfosMessage` request, reply, or `TryExcuteMarchMessage` completion is captured, and there is no independent same-time UUID/lifecycle oracle.

**Independent archived UUID overlap (read-only):** `tools/map_014_march_research/audit_passive_march_receipt.py` compares the 60 real manager UUID strings only against `record_key` and `data_json.uuid` of archived `truck` / `railway` rows in the six `native-benchmark-mixed-20261014-021149/benchmark-{0..5}.db` databases, enforcing stored server consistency and emitting **aggregate counts only**:

| Historical mixed round | Archived Truck rows | Archived Railway rows | Overlap with 60 later all-March UUIDs |
| --- | ---: | ---: | ---: |
| 0 (baseline) | 103 | 2 | 0 for each kind |
| 1 (FOV20) | 39 | 0 | 0 for each kind |
| 2 (baseline) | 95 | 1 | 0 for each kind |
| 3 (FOV20) | 39 | 1 | 0 for each kind |
| 4 (baseline) | 89 | 2 | 0 for each kind |
| 5 (FOV20) | 36 | 1 | 0 for each kind |

**Important:** the old scan completions were approximately **29,024–29,636 seconds (8.06–8.23 hours) earlier** than the later source capture. The later manager sample is not Truck/Train-classified and has no `TrainData.arriveTs` or position attached. Therefore **zero UUID overlap is not proof of loss, expiration, manager completeness, or FOV20 failure at that later moment**. It is an identity comparison across changing, time-separated populations, not a same-time native negative or matching population oracle.

### Read-only receipt gate and concrete fail-closed result

Run:

```text
python tools/map_014_march_research/audit_passive_march_receipt.py artifacts/map014-lead-live/native-march-source-20261014-102638/live-resource/bulk-aoi-diagnostic-result.json --archived-mixed artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149
python -m unittest discover -s tools/map_014_march_research -p 'test_*.py' -v
```

The checker returns `receiptHasActualPointReadyMarchManagerSnapshot=true`, `observedMarchDictionaryCount=60`, **`mobileFreshnessQualified=false`**, **`globallyQualified=false`**. It specifically rejects missing **same-run pre-camera March source** and after-processing source, no exact-server/world March attribution, absent correlated request/reply/process events and source rectangle match, queue count without independently proven read-only `StepProcessQueue.Count` getter semantics, missing numeric `DateTime.Ticks`, and absent contemporaneous independently supported active-March identities. A standalone camera checkpoint is never accepted as a march snapshot.

For a **future, independently produced** opt-in trace to satisfy even the *single-region* gate, the checker documents the following test-only receipt contract (it does not create an observer or issue requests):

* `marchSourceBefore`, `marchSourceAtPointReady`, and `marchSourceAfterProcess` each must have distinct increasing `observedAtRuntimeSeconds`, typed `viewRect` and `cameraViewRect`, uncapped deduplicated `marchUuids` exactly matching `marchDictionaryCount`, and positive `lastGetBlockMessageTicks` / `lastHandleBlockMessageTicks` (native .NET DateTime ticks, not ambiguous formatted strings).
* Native timestamp ticks must be plausible and align within **five minutes of the recorded UTC receipt** (deliberately conservative admission policy for the observed source clock, **not** a wait; if a future client uses a different DateTime timezone, independently establish a trustworthy clock mapping first). Source receive/process tick snapshots cannot move backwards; an exact post-point-ready March handler must advance the handle timestamp.
* Queue provenance must identify actual installed `_messages` field `0x04002561`, confirmed `StepProcessQueue` runtime type, zero-argument read-only `Count` getter and independent exact-type verification. Numeric zero without this proof fails; both queue and merge backlog must be zero.
* `marchRectEvents` must contain exactly one source-observed `WorldGetRectMarchInfosMessage.CSSetData` **request**, one `WorldGetRectMarchInfosMessage.CSHandleResponse` **reply**, and one `WorldMarchDataManager.TryExcuteMarchMessage` **process** for a common independently witnessed correlation identifier, same server/world, identical and applicable active March rectangle, monotonic runtime/native timestamps, and processing after point readiness while target camera is retained. An app-local point `requestId` is **not** a native network match.
* A distinct same-time independent `independentActiveMarches` oracle must tie each inspected UUID to actual server/world, final `TrainData.arriveTs` still in the future relative to witnessed native server time, and observation within a one-second configured skew. Its set must be contained in the after-process all-March dictionary. Missing UUID, stale observation, expired final arrival, duplicate or server/world mismatch fails. **Movement-leg `endTime` never substitutes for final arrival**.

This strict receipt schema is a conservative **offline qualification proposal**, not proof that the existing native producer exposes or reliably correlates all such events. A synthetic complete trace passing the test still returns `globallyQualified=false`. The new isolated inverse suite covers the actual missing before source, camera-only substitution, absent/mismatched request/reply/process, stale rectangle/server/clock, ambiguous `Count=0`, queue pending, stale/expired/duplicate UUID, missing native .NET ticks, and time-separated archived replay.

**Result:** the actual October 14 passive result positively establishes one WorldMarch manager population snapshot and camera restoration, but **does not advance Truck/Train FOV20 qualification**. The prime's separate owner-only native instrumentation would need the missing native request/response/processing/geometry and lifecycle sequence to distinguish absent subscription, late queue consumption, and point-AOI-filter mismatch. Keep baseline for Truck/Railway/mixed mobile; do not add sleeps or guessed requests.

## Decision

**REJECT FOV20 for Truck/Train and mixed mobile scans.** Preserve the guarded serial LOD0 method; keep the existing City-only optimization independent. The observed point acquisition barrier and 10,000/10,000 point AOIs **do not** certify WorldMarchDataManager's separate moving-march rectangle, net response, queue consumption or native availability. More passive evidence is needed before any fix that changes mobile scan admission. Neither an arbitrary sleep nor a manual `WorldGetRectMarchInfos` request is a supported substitute.

## Archived native-to-SQLite inverse (not hypothetical)

Source: `artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149/{benchmark.json,benchmark-[0-5].db,benchmark-[0-5]-probe-metrics.json,audit-result.json}`. Replayed with `tools/map_014_march_research/audit_march_pairs.py`: 3 serial baseline→FOV20 pairs on world 0/server **2212**; every run 2,500 completed logical blocks, **zero failed blocks**, and 10,000 independently counted **point** AOIs. Baseline median **168.075 s**, candidate median **90.754 s**, but the faster route **fails mobile accuracy**.

| Pair | Truck baseline→FOV20 | Shared Truck UUID | Baseline UUID absent in candidate | Of absent: baseline native `TrainData.arriveTs` after approximate candidate end | Other missing classification | Railway baseline→FOV20; missing future-arrival UUID |
| --- | --- | ---: | ---: | ---: | --- | --- |
| 0→1 | 103→39 | 11 | **92** | **90** | 2 reached/passed arrival deadline | 2→0; **2** |
| 2→3 | 95→39 | 3 | **92** | **88** | 4 reached/passed arrival deadline | 1→1; **1** (different UUID) |
| 4→5 | 89→36 | 6 | **83** | **82** | 1 reached/passed arrival deadline | 2→1; **1** |

The later scan also has 28/36/30 **new** Truck UUIDs relative to the prior baseline, as expected from a changing population; a mere total-count comparison cannot explain the much larger unaccounted identity losses. The saved rows are keyed by SQLite `(server_id,kind,record_key)`, checked against native `data_json.uuid`. The `arriveTs` comparison accepts plausible Unix seconds **or milliseconds** and uses `terminal.startedAt + seconds` as an *approximate local wall-clock completion bound*, **not** a native server-clock witness. The separate March `endTime` is a **movement-leg** end, not a demonstrated Truck/Train final lifetime. Thus future native arrival excludes the simplest expiration explanation but cannot alone prove still-loaded objects at the second native manager.

**No global census or perfect recall asserted:** these are six sequential time-separated snapshots. Point AOI completion and stored-row validation alone never prove March refresh completion or that a missing UUID was still visible at the later time.

## Exact installed-source and archived method/field references

The locally installed `Game/LastWar_Data/Assemblies/Assembly-CSharp.rdl` was verified **read-only** against SHA-256 `bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`, matching hash-gated archived JSON. Method and field names/token indices below are from its RGMD metadata, not invented Lua fields. Reproduce by `python tools/map_014_march_research/trace_installed_metadata.py`, which rejects differing hashes/tokens; source decoder is `tools/map_009_offline/rgmd_il.py` (no game launch).

| Installed `WorldMarchDataManager` field | Metadata token | Concrete type / observer relevance |
| --- | --- | --- |
| `allMarches` | `0x0400253a` | `Dictionary<long,WorldMarch>` — snapshot population, distinct from point cache |
| `viewBounds` | `0x0400254e` | `UnityEngine.Bounds` — geographic filtering boundary |
| `viewRect` | `0x0400254f` | `UnityEngine.Rect` — march rectangle |
| `lastCameraViewRange` | `0x04002550` | `Vector4` — previous camera-related range |
| `cameraViewRect` | `0x04002551` | `UnityEngine.Rect` — march-specific current camera footprint |
| `_messages` | `0x04002561` | `StepProcessQueue<(WorldMarchMessageType,ISFSObject)>` — **separate** message-processing queue |
| `_messagesMergeDict` | `0x04002562` | `Dictionary<long,(ISFSObject,WorldMarchMessageType)>` — merge backlog |
| `_lastGetBlockMsgTime` | `0x04002566` | `DateTime` — name suggests block-message receive marker; causal meaning must be observed |
| `_lastHandleBlockMsgTime` | `0x04002567` | `DateTime` — name suggests block-message handle marker; not itself a source-matched acknowledgement |

| Method | Token | Role indicated by method name, signatures and archived IL |
| --- | --- | --- |
| `WorldMarchDataManager.OnUpdate(float)` | `0x0600326c` | Per-frame update, invokes several obfuscated members; not necessarily a request-completion barrier |
| `UpdateViewRect():bool` | `0x0600326e` | Constructs/compares camera rectangle, includes **5.0** scaled margin in IL; cannot infer FOV-independent coverage |
| `IsInView(Vector3)` / `IsLineInView(Vector3,Vector3)` | `0x0600326f` / `0x06003270` | Point **and line-segment** view tests; march visibility may differ from a current-point AOI |
| `IsRectInView(Vector3,int)` / `IsMarchInView(WorldMarch)` | `0x06003271` / `0x06003272` | March-owned geometrical membership |
| `UpdateLastMarchMessage` / `TryExcuteMarchMessage` / `HandleMessage` | `0x06003275` / `0x06003276` / `0x06003277` | Queue receipt/consumption candidates; require observed ordering |
| `HandleWorldMarchGet` / `HandleWorldGetRectMarchInfos(ISFSObject)` | `0x06003278` / `0x0600327b` | Distinct handlers; rect handler manipulates several collections and flags in archived IL |
| `WorldGetMarchInfos():void` | `0x060032aa` | Contains outbound-construction IL; do not assume every camera move triggers it |
| `GetAllMarchesByCS()` | `0x060032c9` | Returns `Dictionary<long,WorldMarch>`; snapshot read used by current Lua |
| `WorldGetRectMarchInfosMessage.CSSetData(object[])` | `0x060019a6` | Serializes a two-coordinate rect message with additional message fields |
| `WorldGetRectMarchInfosMessage.CSHandleResponse(ISFSObject)` | `0x060019a7` | Checks error and dispatches an obfuscated call; attribution to manager requires passive correlation |

Metadata references: `artifacts/map-009-offline/map014-march-acquisition-methods.json`, `map014-march-viewrect-il.json`, `map014-march-request-il.json`, `map014-rect-march-protocol.json`, all tied to the same pinned assembly hash. The IL's member targets are often protected/nonstandard tokens (e.g. `OnUpdate IL_001d 0xA7965722`, `HandleWorldGetRectMarchInfos IL_009a 0x67932A82`, `WorldGetMarchInfos IL_0055 0xA79C9342`). Their field/method *names cannot safely be inferred solely from those operands*. Specifically, do not assert that `_lastHandleBlockMsgTime` advances after full queue drainage without passive confirmation.

Separate Lua evidence: `artifacts/map-009-offline/map014-march-message-source.json` locates `Net/Msgs/WorldGetMarchInfosMessage.luac` `OnCreate` (root/0, lines 8–13) encoding `x`, `y`, `needCross`, and `HandleMessage` (root/1, lines 15–23) invoking `DataCenter.WorldMarchDataManager.WorldMarchGetHandle`. **That message is not assumed identical to the managed `WorldGetRectMarchInfosMessage`**. Protocol hook `CSHandleResponse` is identified by token but no real FOV20 request/reply or server-matched correlation ID has been captured in this offline review.

## Where the existing scanner joins the wrong completion boundary

1. `tools/current_live_resource_probe.lua:6347-6373` accepts `WorldPointManager.isRecvViewPoints` and `WorldScene.hasReceiveViewPointsReply` as **point** readiness, and `_splitLastAOIRequest` completion for requested point footprints. They are not fields of `WorldMarchDataManager`.
2. After that separate milestone, `tools/current_live_resource_probe.lua:6255-6264` calls `train_march_aoi_records` from `3532-3694`. This enumerates `WorldScene.MarchDataManager.GetAllMarchesByCS` (or `allMarches`), obtains `GetMarchCurPosIndex` with `targetPos` fallback, translates **the march's current position** to the **point** `selected_lookup` AOI, and reads `train.trainData.arriveTs` (`3605`), `march.startTime` / `march.endTime` (`3666-3667`). There is **no** march-response / queue-drained / rectangle freshness prerequisite.
3. `src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs:1964-2055` checks trainType Truck=1/Railway=2, server/world/coordinates, record AOI membership and UUID before storing. These correctly prevent malformed rows, **but cannot invent a missing native march**. A moving segment can intersect the native view rectangle while its current position lies outside the selected point-AOI set; exact geometry and time need a separate witness.
4. The official Railway list refresh supplements its native rows in other routes, but its own server-specific list cannot be substituted for `WorldMarchDataManager` whole-world Truck/Train coverage without an independent identity inverse (`docs/MAP_SCAN_METHOD_ASSESSMENT_2026-10-11.md`).

**Highest-priority causal hypotheses, not yet proved:** (A) FOV20/moving camera does not change `viewRect` / emit a correctly attributed `WorldGetRectMarchInfos` request; (B) it emits a request, but the point-ready transition precedes March response and `_messages` / `_messagesMergeDict` consumption, so snapshot+restoration races March insertion; (C) native March visibility is line/rectangle based and the point-indexed position filter rejects valid intersecting marches. A direct no-request witness supports A; reply/post-process UUID gain supports B; same-time manager UUID present but rejected by point-AOI postfilter supports C. Multiple causes can coexist.

## Safe passive distinguishing observation for the exclusive native owner

Do **not** run anything here on the user's account without the owning agent's separately managed native session. In one approved session, use existing game-generated camera movement only; observer must be passive, source-named, no `Send`/manual `WorldGetMarchInfos` / direct claim/march interaction. Preserve task-owned startup/stop, native script hashes and exact camera restoration.

For each **target region** and both legacy/FOV20 modes, observe at four boundaries: **before camera move**, **on point-manager reply**, **on observed March rect reply**, **after March queue consumption while camera remains at target**. The same server/world and camera geometry are mandatory.

At each boundary record `WorldMarchDataManager.viewBounds`, `viewRect`, `cameraViewRect`, `lastCameraViewRange`, `allMarches` count plus ephemeral UUID digest, `_messages` pending count (only if safely exposed by manager), `_messagesMergeDict` count, `_lastGetBlockMsgTime`, `_lastHandleBlockMsgTime`, manager flags, current native clock and `WorldPointManager` response flags. Observe outgoing **actual** `WorldGetRectMarchInfosMessage` request with its effective rect/server and actual reply/handler; correlate using observed per-session ordering/request identity, not guessed token IDs or merely a `WorldGetMarchInfos` name. Read only world/TrainData `uuid`, `serverId`, `worldId`, `trainType`, `arriveTs`, `startTime`, `endTime`, `GetMarchCurPosIndex`, `targetPos`, current view-rectangle membership and line intersection; keep raw player/UUID data inside ignored owned receipts (not this document or source repo).

**Reflection safety detail for that observer:** the existing read-only Lua helper `reflected_value(component,key)` (`tools/current_live_resource_probe.lua:219-235`) invokes `GetField` or `GetProperty` with `GetValue` behind protected calls, so `reflected_value(manager,"viewRect")` and `reflected_value(manager,"cameraViewRect")` are appropriate read-only typed `UnityEngine.Rect` field reads. On the returned Rect, read `xMin`, `yMin`, `xMax`, `yMax` or `x`, `y`, `width`, `height` only if those actual properties resolve; do not call setters. `_messagesMergeDict` is a Dictionary, whose `Count` is a suitable read-only candidate after verifying the reflected instance. By contrast, `_messages` is `GameKit.Base.StepProcessQueue<T>`, **not** a Dictionary or List: neither the installed field signature nor archived IL establishes that a `Count` getter exists. Inspect its actual reflected type/property list first, accept `Count` only if exposed as a parameterless, nonmutating getter, otherwise mark queue length **unavailable** and do not claim queue-drained readiness. The same applies to any timestamp that cannot be read. No assumed private backing-field layout.

**Decisive controlled inverses:**

* A — point reply passes but **no** march request/rect change is source-observed: reject candidate; diagnose `UpdateViewRect` and camera footprint first; a wait cannot create a missing subscription.
* B — point reply passes while March reply/processing is pending, and the same still-future-arrival UUIDs appear only after exact March handler + queue drain **without moving/restoring camera**: demonstrated premature snapshot; candidate can be redesigned to wait on independently validated March completion, not an arbitrary timeout.
* C — exact manager UUID exists at the candidate snapshot, but the point-derived `selected_lookup` omits it while `IsMarchInView` / line segment intersects the active march rectangle: demonstrated point/march geometry conflation; fix membership in a separate guarded production task with no overlap/dedup regressions.
* Negative: old-region replies, unmatched server/world/rect, pending queue, unchanged `_lastHandleBlockMsgTime`, missing UUID, arrival already passed, stale owner, explicit Stop, and incomplete native request must all **fail** qualification. Delayed replies after camera restore may not be attributed to the next target.

Only after a same-time independent native march population/control matches UUIDs (with arrival/server/rect validity), and after source-matched queue transitions, multiple complete engine→SQLite pairs, train/Truck movement, Stop/retirement and camera restoration pass, may a per-mobile-kind optimization be reconsidered. One locally clean rectangle does **not** establish whole-map accuracy.

## Offline qualification tools and result

* `python tools/map_014_march_research/audit_march_pairs.py artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149`: read-only archived SQLite/JSON comparison; returns `mobileAccuracyQualified=false`.
* `python tools/map_014_march_research/trace_installed_metadata.py`: pin/version verified method + field contract, no app/game execution.
* `tools/map_014_march_research/march_freshness_gate.py`: pure evaluator of **hypothetical later** observed native receipts. Requires independent request/reply/process correlation, exact server/world/rect, monotonic observation and drained queue, plus independent UUID inverse recorded within a conservative one-second **observer skew limit** (test admission policy, not a native server guarantee or proposed sleep); refuses global mobile accuracy even on a single clean synthetic rectangle. **Not a native positive.**
* `python -m unittest discover -s tools/map_014_march_research -p 'test_*.py' -v`: isolated no-game regression including missing-future-arrival, stale-region reply, mismatched owner, pending queue, snapshot-before-process and false AOI completeness.

**Conservative release policy:** continue baseline for mobile selections and **mixed selections containing Truck or Railway**, regardless of City-only FOV20 speed; do not claim final Train lifetime or universal Truck expiry; preserve prior genuinely verified Truck Follow/Stop. March request/response/queue causal tracing remains **PARTIAL** until an actual source-matched passive native witness exists.
