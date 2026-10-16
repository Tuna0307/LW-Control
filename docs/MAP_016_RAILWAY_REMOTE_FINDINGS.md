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

**UNKNOWN/unqualified:** a production raw-reply route would need fresh packet exclusion, exact session and requested
server/world/LOD/bounds attribution, all protobuf records, City canonical identity, per-kind source/lifecycle fields,
Resource and Monster detail qualification, and separate transient march-push acquisition. No such route is enabled
by MAP-016. Removing the current-server guard would incorrectly read the existing current-server caches.

The retained production fallback is automatic switch–scan–return. Final report measurements must distinguish total
cycle time, native acquisition and navigation/return overhead. A Railway rectangle sweep can exceed MAP-015's sparse
18.338-second historical direct Railway sample even while improving the reproduced 141.286-second failed-fast cycle;
the report must retain that speed limitation if the packaged measurements confirm it.
