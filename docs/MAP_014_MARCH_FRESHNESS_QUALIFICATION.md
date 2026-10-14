# MAP-014 Truck / Train march freshness — source-causal offline qualification

## 2026-10-14 worker-7 source-event / cohort causal inverses

**New reproducible synthetic false admissions, then GREEN.** In the earlier
`audit_passive_march_receipt.py`, all three self-reported March events could
point at the *smaller cameraViewRect* even when the actual
`WorldMarchDataManager.viewRect` was different. It also admitted a changed
manager rectangle between point-readiness and queue-consumption snapshots,
because a coincidentally matching camera-rect oracle could hide the mismatch.
Native event `DateTime.Ticks` a day older than the declared capture clock were
accepted if the separate request/response/process stamps stayed ordered and
the process stamp still preceded the last manager handle tick. Merely setting
the `method` string to a familiar name passed even when `sourceClass` and
installed method tokens were inconsistent. These cases were demonstrated
using **synthetic mutations**, with actual RED assertions before the fix;
none is an observed Last War event.

The hash-pinned installed `Assembly-CSharp.rdl` inventory was independently
replayed **read-only**, yielding SHA-256
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
It contains *separate* `WorldGetRectMarchInfosMessage.CSSetData(object[])`
`0x060019a6`, `CSHandleResponse(ISFSObject)` `0x060019a7`,
`WorldMarchDataManager.HandleWorldGetRectMarchInfos(ISFSObject)`
`0x0600327b`, and `TryExcuteMarchMessage(WorldMarchMessageType,ISFSObject)`
`0x06003276`. Archived installed IL for `CSHandleResponse` has an early return
at `IL_000e`, before its downstream dispatch path. Thus method **entry**
alone cannot certify receipt by the separate March manager. A new source
contract requires **four** independently source-observed, same-correlated
events in request → response → manager handler → queue-processing order,
each bearing its pinned declaring class and method token, matching
server/world and the active `WorldMarchDataManager.viewRect`, with strictly
applicable event/stage timestamps. The manager `viewRect` and
`cameraViewRect` must remain unchanged until the post-processing snapshot.
The request rectangle is the proposed observer's **effective March request
footprint**, which must actually be recovered and tied to the installed
source; merely relabelling a point-request rectangle is insufficient.
Source class/token strings by themselves do not authenticate an observer.
The source method `TryExcuteMarchMessage` has a `bool` return but the
obfuscated IL has not established that a particular return value proves
successful ingestion; the four-event contract remains only a necessary
structural screen, never independent proof of processed population.

The earlier `audit_compare_ready_march.py` also accepted an
**arbitrarily long typed cohort collection interval** so long as it
overlapped the after-process oracle. A post-process cohort extending from
runtime second `100.4` to `400` could compare as identical to a second
`100.49–100.57` snapshot, despite no evidence that every March existed
simultaneously. It could even combine a server clock at second `100.51`
with a later independent cohort extending to `103`, while final
`TrainData.arriveTs` was only two seconds after that clock. Both were RED
false passes. All typed observation intervals now have a one-second maximum
duration matching the declared maximum observer skew, in addition to the
existing exact stage brackets, after/oracle overlap and one-second
`arriveTs` margin. This conservative rule may reject slow but valid capture
sessions until an independently justified tighter sample-time proof exists.

**GREEN verification:**
`python -m unittest discover -s tools/map_014_march_research -p 'test_*.py' -q`
passes **55 tests**. The new missing/early/reordered manager-handler,
source-class/token, stale event-tick, wrong active rectangle, premature
rectangle change and multi-minute cohort mutations are all rejected. The
original synthetic complete fixture still passes the *structural* contract,
while both global-accuracy and independently verified authenticity flags
remain **false** by construction. No production code, Lua, original scripts,
installed game, live session, commit or PR changed.

**Exact actual archived replay, read-only:**
`artifacts/map014-lead-live/native-march-source-20261014-102638/live-resource/bulk-aoi-diagnostic-result.json`
still supplies the genuine **60 uncapped, unclassified point-ready March
UUIDs**, but no separately identified request, CS response, manager handler,
queue process, complete before/after March stages, numeric native DateTime
ticks, verified queue count getter, trusted server clock or simultaneous typed
oracle. `mobileFreshnessQualified=false` and
`sameSessionCompareContractPass=false`; 60 cannot be counted as 60 Trucks or
Trains. Its historical intersection with each of the six saved mixed scan
rounds is **zero**, across 29,023.874–29,635.991 seconds (8.06–8.23 hours)
of separation, with **no** inference about recall or expiry. Independently
replayed `native-benchmark-mixed-20261014-021149`: baseline→FOV20 Truck
UUID losses **92/92/83**, including **90/88/82** with recorded final
`arriveTs` after the *approximate* subsequent local scan finish; Railway
future-arrival losses **2/1/1**. These receipts are sequential and do not
prove whether missing Marches were still in the native manager. Missing live
source stages remain an actual same-session request→CS reply→manager
handler→queue-consumption witness, independent current typed identity/position
oracle, server clock, native queue getter semantics and retained rectangle
geometry. **FOV20 mobile and mixed remain unqualified; WinError5 is a hard
boundary.**

## 2026-10-14 ce9fedf2 independent stale-terminal hardening

**Proved RED before fix / GREEN after fix.** The existing
`audit_compare_ready_march.py` checked that the **post-processing** typed
manager cohort and independent oracle agreed, but it never compared the
**point-ready** cohort's entire UUID set with the post-processing cohort. A
genuine point-ready Truck with a still-future `TrainData.arriveTs` could
disappear from both later cohorts, leaving two agreeing incomplete sets and
`sameSessionCompareContractPass=true`. The same bug permitted a train UUID
to be rebound to a **different** March UUID between snapshots: collisions
were checked only *within* one cohort. These are **demonstrated offline
checker false positives using synthetic adversarial fixtures**, not claims
that the live game actually did either.

Five initial tests failed under the original gate: disappeared unexpired
point-ready UUID; cross-stage train-UUID rebound; point-ready typed UUID
missing from the recorded manager snapshot; point-ready cohort timestamp
interval not containing the actual manager observation; and an absent typed
`count` treated as acceptable. The fixed contract now rejects those
cases, as well as overlapping/reversed point-ready versus processed cohort
capture windows, and near-expiring final arrivals inside a **one-second
observer/clock uncertainty margin**. A disappearing point-ready UUID
cannot be legitimized by guessing expiry from the movement-leg
`WorldMarch.endTime`; the gate fails closed without an independently
source-observed lifecycle/retirement event. This **may reject legitimate
departures** but cannot silently publish unproven terminal freshness.

**Native method semantics that exposed an additional hole:** read-only,
hash-pinned `Assembly-CSharp.rdl` metadata confirms
`WorldMarch.GetMarchCurPosIndex():int32` at `0x06003208` and
`WorldScene.IndexToTilePos(int32):UnityEngine.Vector2Int` at
`0x060039c3`. The existing Lua producer first obtains that **integer
position index**, then calls `WorldScene.IndexToTilePos`
(`tools/current_live_resource_probe.lua:467-477,3617-3620`);
`GetMarchCurPosIndex` is **not** a method yielding a trusted x/y pair
or a signed movement/retirement event. The offline typed contract now
requires each claimed x/y to bind to an explicitly source-observed
`pointIndex` and those exact method/conversion tokens; the x/y formula
`pointIndex = y*1000+x+1` is required for this proven 1,000-wide
world domain. A mismatch, unidentified conversion or index-only
substitution is rejected. A movement transition must also bind the
before/after raw indices, not just two free-floating x/y pairs. This
provides a **structural** inverse only: the offline checker cannot
authenticate a self-reported source method or infer actual movement
from an integer getter alone. The game owner would need a genuine
contemporaneous observation of both stages to use it.

**Read-only actual March 102638 replay:** captured
`2026-10-14T02:29:14Z` with point reply ready and exactly **60
unique, uncapped, unclassified manager UUIDs**. No
`marchSourceBefore` or `marchSourceAfterProcess` is present;
`train_march_records=[]`; no separately correlated march rect
request/response/process events, numeric `lastGetBlockMessageTicks`
or `lastHandleBlockMessageTicks`, verified `StepProcessQueue.Count`
getter, or native server clock. The reported queue/merge zeros cannot
prove native completion. Both `mobileFreshnessQualified=false` and
`sameSessionCompareContractPass=false` remain enforced. A previous
camera-only checkpoint is not a before-March snapshot; the earlier
mixed mobile receipt comparison is **eight hours apart**, not a
same-time lifecycle inverse. The later `103048` WinError 5 recovery
journal denial remains a **hard stop**; no recovery/native/game
operation was made.

**Independent Resource six-round timestamp re-audit** (read-only external
lead auditor, no edits to its script or original SQLite): preserved
`native-benchmark-resource-20261014-024146` has **7,989 /
7,995 / 8,002 / 7,998 / 7,993 / 7,990** native-hydrated
resource rows across alternating FOV120/FOV20 rounds. Every row's
`updated_at` is a **local point-capture time** validated within its
camera-before/after UTC window, strictly ordered across rounds;
the windows progress from **[1791916945000,1791917096000]** ms
in round 0 to **[1791917548000,1791917622000]** ms in round 5.
In baseline→FOV20 pair 0→1, **19 baseline-only**
(16 unoccupied, 3 occupied) and **25 candidate-only**
(24 unoccupied, 1 occupied) were observed. Another same-AOI
Resource appears in the opposing capture for **11/19** missing
and **11/25** extra, with 11/19 and 11/25 stable anchors
across baseline/candidate/following-baseline. Pairs 2→3
and 4→5 lose **4 occupied** and **3 occupied** points,
respectively, with opposing same-AOI cohorts for 4/4 and 2/3.
These native records and clocks corroborate **sequential sampling**
and field fidelity, **not** server spawn/destruction timestamp,
unoccupied Resource terminal transition, cache freshness or
simultaneous resource presence/absence. Re-audit still returns
`resourceFov20AccuracyQualified=false` and
`genuineSpawnDestructionOrScannerMissAttribution="unknown"`
for every pair. In particular do not explain away the sixteen
previously unoccupied missing Resource points as occupation,
and do not call the twenty-five candidate-only observations
baseline misses.

**Validation:** `python -B -m unittest discover -s
tools/map_014_march_research -p 'test_*.py' -v` runs **47
passing isolated Python tests**. The hash-gated metadata tracer
reports both pinned method tokens. These are offline structural
checks; even a synthetic compare contract pass returns
`sourceAuthenticityIndependentlyVerified=false` and
`globalMobileAccuracyQualified=false`. **Maintain baseline for
Truck/Railway/mixed selections; Resource FOV20 stays
unqualified**. Only the assigned March research folder and this
document were edited; no game/production/Lua/PR/commit or
WinError5 recovery attempts occurred.

## 2026-10-14 checkout 342b3737: stricter source-grounded comparison proposal

At clean Git checkpoint `342b3737` the preserved installed `Assembly-CSharp.rdl` still hashes to
`bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e`.
Hash-gated `tools/map_014_march_research/trace_installed_metadata.py` confirms the **actual
native contract**: `WorldMarchDataManager.allMarches` is a
`Dictionary<long,WorldMarch>` (`0x0400253a`); `viewRect`/`cameraViewRect`
are **different** `UnityEngine.Rect` fields (`0x0400254f`/`0x04002551`);
`_messages` (`0x04002561`) is a
`StepProcessQueue<(WorldMarchMessageType,ISFSObject)>` rather than a list;
`_messagesMergeDict` (`0x04002562`) is an independent dictionary; and
`_lastGetBlockMsgTime`/`_lastHandleBlockMsgTime` (`0x04002566/67`) are
`System.DateTime`, not Unix timestamps.

**IL semantic limits:** archived hash-pinned `UpdateViewRect` `0x0600326e`
computes a camera-based rectangle and includes `IL_016b: ldc.r4 5.0` and
`IL_0170: mul` before a difference comparison and conditional `true`
return. Its two named rectangle fields are not interchangeable, and the exact
dimension transformed by the protected calls cannot safely be inferred from
their opaque operands. `OnUpdate` `0x0600326c` calls several protected
members, but its `IL_001d: call token(0xA7965722)` is **not proof** of a
completed request. `HandleWorldGetRectMarchInfos` `0x0600327b`
clears/iterates native collections and writes multiple boolean flags
(`IL_0098-IL_00af`), but a response handler invocation alone is **not**
a correlated queue-drained and spatially complete March barrier.
`WorldGetRectMarchInfosMessage.CSSetData` `0x060019a6` serializes two
rectangle-related fields, whereas `CSHandleResponse` `0x060019a7`
short-circuits on a response condition before dispatching an obfuscated
handler. These metadata observations **do not** identify a reliable
`StepProcessQueue.Count` getter, a typed server-time provider, or an
exact request correlation ID. No arbitrary waits, assumed token mapping or
manual request is warranted.

The latest preserved `native-march-source-20261014-103048/final.json`
reports **`PermissionError [WinError 5] Access is denied`** on a recovery
journal temporary-file rename and contains **no** new
`live-resource/bulk-aoi-diagnostic-result.json` March observation.
This is a **hard denial**; no game/recovery/installed-script operations were
attempted by this worker. The earlier `102638` file remains the only
current source-positive March manager receipt under review and still has
60 **untyped** cached UUIDs at point readiness, with no before/processed
pair, no numeric clock, and no validated March reply sequence.

### Additional offline compare-ready contract

New **pure read-only** `tools/map_014_march_research/audit_compare_ready_march.py`
provides `audit_compare_ready(receipt)`. It first requires all existing
`audit_receipt` March request/response/manager-handler/process/typed queue/tick prerequisites,
then requires a **single-session** `compareReadyEvidence` bundle. It
requires an observed target `viewRect` distinct from the before-camera
rect; the same camera/manager target footprint after processing; pinned
installed-assembly SHA; selected native TrainType **int 1 Truck / int 2
Railway**; source-labelled `WorldMarch.train` and an exact
`marchUuid=uuid` and `trainUuid` join; typed server/world/ownerServer,
0–999 coordinates and an uncapped unique per-cohort March identity set;
independent **native server-clock** provenance with integer epoch
milliseconds; and final `TrainData.arriveTs` strictly later than that
server time. The movement-leg `WorldMarch.endTime` is never substituted
for final arrival.

For each UUID the candidate after-processing cohort and independently
observed native cohort must **overlap in monotonic runtime time ranges**
and exactly match `uuid`, `trainUuid`, TrainType, owner server,
position and final arrival. Source-unknown typed values, duplicated UUIDs,
one train UUID assigned to competing marches, mismatched cached
position/type/arrival, stale/coincident old rectangles, stale server clock,
and missing selected IDs **fail closed**. An eligible March may change
position between the two native stages only if one exact source-observed
`GetMarchCurPosIndex` movement transition covers the previous and
current positions within that stage interval. Owner/type/final-arrival
repurposing is never silently treated as movement.

**Evidence limitation:** `sameSessionCompareContractPass=true` is possible
for a **fully synthetic fixture** solely to test inverse logic. The
checker always reports
`sourceAuthenticityIndependentlyVerified=false` and
`globalMobileAccuracyQualified=false`, since source-method authenticity,
native clock provenance and global same-time March completeness cannot be
established by Python assertions or the existing archived receipts.
Only the game owner's independently captured, hash-gated native witnesses
could qualify those premises. On the **real 102638 receipt**,
`sameSessionCompareContractPass=false`: it lacks the base chain and
any same-session typed/clock/position cohort. An archive from eight hours
earlier cannot be supplied as a substitute.

The new `test_audit_compare_ready_march.py` adds explicit falsifying
tests for old view rectangles, stale source intervals, ambiguous
TrainType strings/unknowns, untyped clocks, crossed session/server,
past-`arriveTs`, duplicate native cache UUIDs, UUID
reclassification/collision, missing current manager membership, and
unbacked/valid movement transitions. The exact installed
`WorldMarch.GetMarchCurPosIndex():int32` method token is **`0x06003208`**
(verified by read-only hash-gated `inspect_managed.py` and now the owned
`trace_installed_metadata.py`); only a source-observed transition attributed
to that method/token is eligible to explain a same-UUID coordinate change.
No production, core Lua, native host,
installed script, game session, continuation, commit or PR was touched.

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
* `marchRectEvents` must contain exactly one source-observed `WorldGetRectMarchInfosMessage.CSSetData` **request** (`0x060019a6`), one `WorldGetRectMarchInfosMessage.CSHandleResponse` **reply** (`0x060019a7`), one separately observed `WorldMarchDataManager.HandleWorldGetRectMarchInfos` **handler** (`0x0600327b`) and one `WorldMarchDataManager.TryExcuteMarchMessage` **process** (`0x06003276`) for a common independently witnessed correlation identifier, correctly pinned declaring classes, same server/world, identical applicable active **March manager `viewRect`** (not camera-only rect), ordered runtime/native timestamps bounded by native capture time, and processing after point readiness while both target March rectangles remain fixed. The actual request's effective rectangle must be source established. A `CSHandleResponse` entry can return before manager dispatch; an app-local point `requestId` is **not** a native network match.
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
