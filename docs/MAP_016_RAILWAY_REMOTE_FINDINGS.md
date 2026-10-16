# MAP-016 Railway acquisition and remote-server boundary

Static findings and scoped collector change, October 16, 2026. Packaged acceptance and final timings belong to
`MAP_016_WORKER_REPORT.md`; the controlled checks below are not live-positive evidence.

## Owner reproduction

The exact-owned real MAP-015 package ran a Railway Auto cycle targeting 2211 from home 2212. Retained evidence:
`artifacts/map016/before/live-resource/`.

- `camera-free-scan-result.json`: target 2211, 63 replies, 10,000 AOIs, 17.644 seconds, original camera restored,
  no native Train march rows.
- `map015-direct-fallback.json`: the direct stage rejected the empty result with
  `Camera-free scan returned zero rows after final record hydration.` This forced camera scanning.
- `map-fullworld-probe-metrics.json`: the camera fallback took 117.322 seconds and 210 probes. The owner cycle,
  including server navigation and return, took 141.286 seconds.
- The final `train-list-diagnostic-result.json` refreshed nearly two minutes after the direct failure. It contains
  three typed Trucks and one **type 2 Railway**, configuration 152, six carriages, quality 5, server 2211. The list
  changed across time; this later observation cannot prove that exact identity existed during the first stage.

## Source cause and correction

**RECOVERED current v26:** read-only inspection of installed `LWScripts.data` found SHA-256
`748e4283ea36782d300c50b7a35ea36c83cc0377fbadb5a2fc26c7a37415d7be`, content version 26, 18,745 entries.
`GetTrainListMessage.OnCreate` (source lines 11–14) sends only `isRefresh`, with no requested server argument.
`LWTrainDataManager.OnTrainListGet` (224–247) replaces `enemyTrains` from `allianceTrainList`;
`GetEnemyTrain` (257–259) returns `enemyTrains[1]`. This is an enemy recommendation, not a demonstrated world census.
The manager separately maintains `allTrains` and `allTrainsByMarchUuid`.

**PROVEN repository behavior:** the 63-chunk point sweep samples native Train marches at wide rectangles, then
refreshes the recommendation. The direct completion path permitted Railway-empty acquisition but unconditionally
rejected empty final blocks. That caused the reproduced camera fallback. MAP-015 already retained a one-identity
Railway gap between time-separated direct and camera controls.

**OWN_DESIGN correction:** reuse the accepted 625 native 40×40 rectangle march sweep for Railway, collecting
authoritative `WorldMarch.train.type=2` through the established native train builder. Truck and Railway share one
sweep when both are selected. Railway-only scans skip the point sweep entirely. A full verified Railway sweep may
publish an empty result; incomplete rectangle attribution, duplicate identities, foreign map servers, missing
typed/source/time provenance, or failed restoration remain failures. The recommendation cannot replace or invalidate
this qualified rectangle source. Camera fallback remains available after exact-owned native cleanup.

Railway records retain their game-calculated positions and actual observation times. Truck segment expiry filtering
does not apply to a Railway dwelling at an intermediate station. Truck type, metadata, arrival and publication gates
remain intact. Native and managed receipts expose Railway separately from Truck; saved Railway rows carry
`captureProtocol=current_fast_railway_625_v1`.

Controlled checks: six Python tests execute the actual production Lua builder/sweep against inert game-shaped
objects, including typed Railway capture, unknown-type rejection, dwelling, shared Truck expiry, complete empty
coverage and missing-rectangle rejection. `Map016RailwayChecks` exercises actual managed capture/projection and
seven negative receipt mutations. These are regressions, not evidence of native availability or live recall.

## Remote raw replies

**RECOVERED managed IL:** `artifacts/map014-il-decode/il_WorldPointManager_reply.txt`,
`WorldPointManager.ParseWorldGetBlock` checks the reply server against the current server in single-server mode
at IL_0021–005e. The method clears its temporary point collections before this check. Points are protobuf byte-array
entries, parsed with `Protobuf.WorldPointInfo.Parser.ParseFrom` before conversion to `PointInfo` at IL_007f–00a6.
Calling this manager method to ingest a remote reply would alter live client state and still encounter its server gate.

`tools/map_009_offline/managed_sampler/Sampler.cs` already reaches incoming packet `info` through
`NetRawProxy.profiler._receiveData`. Its retained reader deliberately emits only map-command keys, numeric region
context, array counts and up to three point shapes. It does not provide complete production records.
`ReplyWindow.cs` additionally requires matching cache population, which cannot admit the dropped remote data.

### Independent offline feasibility audit (2026-10-16, worker-2)

The new **offline-only** `tools/map_016_remote_feasibility.py` inspects the preserved managed IL and all matching
`artifacts/map-013-live/native-causal-*/live-resource/map013-native-trace-result.json` records. It does not connect
to the game, install scripts, send packets or touch the profiler. Its narrow assessment is reproducible with:

```powershell
python tools/map_016_remote_feasibility.py
python tools/map_016_remote_feasibility_test.py -v
```

The audited source contract is stronger than a generic "protobuf payload missing" statement:

1. The recovered `WorldPointManager.ParseWorldGetBlock` at method token `0x06003553` clears `_pointInfos`,
   `_landPointInfos` and `_desertInfos`, reads each segment's actual `serverId` at `IL_0022`, consults
   `WorldScene.IsSingleServerMode()` and `DCPlayer.GetCurServerId()`, and returns at `IL_005e` when they differ.
   It reads `worldId` only after that gate at `IL_0060`. The generic incoming handler
   `HandleViewPointsReply` extracts `serverPointArr` and iterates over segments via `ParseWorldGetBlock`.
   The early return proves current-world cache ingestion is not a remote source in single-server mode; it does
   **not** prove the transport never receives foreign packets.
2. At `IL_006c` the point collection is an SFS `points` array; at `IL_0087–0091` each element is read through
   `ISFSArray.GetByteArray(index).Bytes` and decoded with `Protobuf.WorldPointInfo.Parser.ParseFrom(bytes)`.
   Other typed segment members include byte arrays for `lands`, `deserts` and `alInfos` plus auxiliary
   `cityGreen`, `v2WolfPoints` and `lightRangeData`. A typed decoder needs the corresponding admitted protobuf
   schema and preserved *original per-element bytes*, not a JSON rendering of the SFS object or merely the
   point count.
3. In contrast, the diagnostic `Sampler.cs` tries `GetSFSObject(index)` for its first three `points` entries,
   serializes only `keys` and a few attempted numeric fields, and never calls `GetByteArray` or preserves bytes.
   Its packet observation uses an ephemeral `packet.GetHashCode()` identity, with at most 512 enumerated entries,
   120 returned map packets and 40 segments per reply. The actual retained samples have no nonempty point shapes;
   a synthetic SFS-object shape test is not a valid protobuf decoder. `ReplyWindow.cs` binds a 12-second
   observation to an exact scene and checks *local cache population*; even a fresh foreign segment with
   matching numeric envelope would not establish independent remote data ingestion through that route.

The offline archive audit produced the following **metadata observations**. Packet rows and point counts repeat
across snapshots; they are not unique requests, objects or a world population census.

| Retained evidence | Offline result |
| --- | ---: |
| MAP-013 native trace files / traces with independently known current server | 41 / 38 |
| Managed packet snapshots | 570 |
| Receive-side `world.get.block` rows, including repeats | 1,772 |
| Extracted segment appearances with native `serverId` | 1,745 |
| Attributed same-server segments | 1,745 |
| Attributed off-server segments | **0** |
| Reported point counts, including repeated snapshots | 123,871 |
| Sampled pseudo-point shapes / shapes with any real key or numeric field | 4,338 / **0** |
| Serialized protobuf `points` byte-array payloads | **0** |
| Incomplete/truncated snapshot indicators in those retained records | 0 |

The 41 attempts were historical *same-server* observations. The absence of foreign segments therefore cannot
answer whether the server would return valid off-server data to a newly sent remote request. Likewise, the
client's outgoing `WorldGetBlockMessage.Request` accepting `serverId` is merely a request contract: the observed
profiler had missing outgoing `info`, no echoed request ID, no fresh foreign payload, and no independent off-server
`leftBottom`/`rightTop` footprint matched to the requested AOIs. A response's echoed x/y without bounds is
insufficient even for same-server acquisition (see MAP-013's stale-anchor negatives).

The three new inert tests check (a) that decoded IL still places the single-server guard before byte parsing,
(b) that a forged *outgoing* foreign server envelope and SFS pseudo-points cannot count as a received typed source,
and (c) that the actual archived observations contain neither attributed foreign segments nor original point
byte arrays. **All three passed.** They demonstrate a technical dependency and guard against false positives;
they do not provide an off-server decoder, packet delivery proof or real-world scan result.

### Exact unqualified edge and fallback efficiency

The smallest credible next *research* witness would be an actual fresh incoming `world.get.block` packet with
serverId different from the unchanged authenticated current server, correlated to a permitted read-only request
and containing all original segment fields, byte-array payloads, server/world/LOD and converted native
`leftBottom`/`rightTop` bounds. The sender would need to exclude stale profiler packets and correlate all target
AOIs, including empty ones, through an immutable request/response identity or an independently validated causal
window. Typed decoding must use a verified `WorldPointInfo` protobuf schema, preserve City identities and object
fields, then separately verify remote Resource/Monster detail data and the transient
`push.world.march.world.get.new` typed Truck/Railway population (the point reply alone cannot supply those).
Actual cross-server packet delivery, provenance, coverage, freshness and per-kind parity remain **unknown**.
No MAP-016 runtime remote parser is justified by the present files, and bypassing the current-server guard would
weaken unrelated native cache ownership.

Independent efficiency opportunities for the **existing permitted switch–fast scan–return** route are bounded
by actual packaged metrics, without needing an off-server protocol change:

- On two genuine home 2212 → Railway target 2211 → home 2212 cycles (`native-ui-20261016-122209` and
  `native-ui-20261016-123036`), outbound navigation was **3.689 / 3.269 seconds**, and observed home return
  **1.890 / 1.810 seconds**. Navigation plus return therefore cost about **5.58 / 5.08 seconds**; native sweeps
  cost **47.965 / 83.200 seconds** respectively. Improving verified acquisition/reply-repair latency or avoiding
  redundant full scans has much more potential than shaving a small amount from server navigation.
- Already implemented: the native 625-rectangle sweep is shared by Truck and Railway when selected together;
  a fully verified Railway-empty result is allowed and avoids the earlier **117.322-second** camera retry;
  the non-Truck direct point sweep uses 63 source-correlated responses spanning 10,000 AOIs on the *current*
  target server, rather than camera traversal. Keep bounded lost-rectangle repairs and native camera restoration.
- Worth investigating *offline first*: avoid repeated visits to the same target within a scheduled multi-kind
  scan cycle, reuse qualified within-run observations/receipts without treating expired moving records as current,
  record per-server switch/scan/return timing and repair counts, and profile legitimate detail hydration waits.
  Overlap only operations shown to be accurately attributable: past AOI pipeline attempts lost results and
  transient march data can disappear when later push sets replace earlier ones. Do not weaken those gates to
  report a faster number. A separately measured 2213 Resource scan (28.015-second engine time) and 2211 all-nine
  scan (103.985-second engine time) involve different populations and cannot establish same-server speed parity.

**UNKNOWN/unqualified:** a production raw-reply route would need fresh packet exclusion, exact session and requested
server/world/LOD/bounds attribution, all protobuf records, City canonical identity, per-kind source/lifecycle fields,
Resource and Monster detail qualification, and separate transient march-push acquisition. No such route is enabled
by MAP-016. Removing the current-server guard would incorrectly read the existing current-server caches.

The retained production fallback is automatic switch–scan–return. Final report measurements must distinguish total
cycle time, native acquisition and navigation/return overhead. A Railway rectangle sweep can exceed MAP-015's sparse
18.338-second historical direct Railway sample even while improving the reproduced 141.286-second failed-fast cycle;
the report must retain that speed limitation if the packaged measurements confirm it.
