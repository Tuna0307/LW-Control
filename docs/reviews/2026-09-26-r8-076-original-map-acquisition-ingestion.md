# R8-076 — recover original Map acquisition ingestion architecture

**Date:** 2026-09-26
**Reference:** verified LWBridge 0.3.1
**Scope:** close the original host-side ingestion path from protected game-side Map Scan events into normalized staging/published records. No production scanner change is made.

## Result

The original Map acquisition lane is no longer accurately classified as wholly UNKNOWN. Earlier R6 work already recovered the public control plane, 20-tile block-grid cardinality, progress/completion rules, normalized record builder, record identity, scalar normalization and SQLite publication. R8-076 closes the missing production ingestion linkage between those pieces.

The remaining protected gap is now below that boundary: exact game-side block traversal/order/coordinates, XluaBridgeMapScanTick scheduling/pacing, conversion/drain/ack sequencing inside the secure/plain proxy, and some per-kind serializer optionality/type details.

The current v21 host-driven movement/AOI scanner remains a live-working equivalent reimplementation. Its 2,500-block shape for a 1000×1000 world agrees with the native ceil(width/20) × ceil(height/20) contract, but that agreement does not make its traversal algorithm original parity.
## Recovered event topology

The common original Map event handler is native function 0x14033FFA1-0x140341913. It parses the incoming event object's type and payload and contains exact branches for:

- map.records;
- map.scan.diagnostic;
- map.native.capture;
- map.scan.progress;
- map.scan.complete;
- map.scan.error.

The sole direct caller found for this common handler is at 0x1402ADF8C in 0x1402ADE92-0x1402AE2DF.

This gives the original host a concrete separation between scan record batches, scan progress/terminal state and the independent native world-capture stream.

## Protected Start delegates acquisition to the game-side scanner

The original `map_scan_start` worker is `0x1400F9333-0x1400FB8D8`. Before protected acquisition it computes the exact logical cardinality:

`totalBlocks = ceil(tileWidth / 20) * ceil(tileHeight / 20)`.

It then sends one protected `startMapScan` request with exact fields `scanRunId`, `serverId`, `worldId`, `scanMode`, `concurrency`, `selectedTypes`, `tileWidth`, and `tileHeight`. The result deadline is exactly 5,000 ms.

The host requires response field `accepted`; false/missing acceptance rejects with exact message `map scan was not accepted`. The response also carries `totalBlocks`, which is checked against the host-computed cardinality before native capture readiness is published. Only after protected acceptance does the shared state transition `nativeCaptureReady=false -> true`.

This closes an important architecture boundary: the original host does not implement the current rebuild's camera/AOI block traversal itself. It delegates the scan geometry/mode/type selection to protected game-side acquisition and then consumes asynchronous scan/capture events.

## Direct-scan map.records staging

Exact map.records dispatch calls dedicated handler 0x140341FCC-0x140342926.

That handler resolves the current scan state; validates the event against the active scan context; reads event scanRunId; reads current scan-state selectedTypes, serverId, tileWidth and tileHeight; reads event records; iterates the records array; requires each accepted entry to carry type and payload; matches type against the original eight Map kinds and the current selected-type set; augments the record payload with current scan-state server/dimension context; builds a record-batch vector; and sends it to the scan-record transaction helper.
The transaction helper is 0x14025F01D-0x14025F5C8. It has exact diagnostics "begin scan record batch" and "commit scan record batch". For every batch item it calls the already-recovered normalized-record builder 0x1402785BA-0x1402793BE, then calls the shared upsert helper 0x14027A1E3-0x14027AA73 using exact table selector scan_records.

The shared upsert's staging SQL is:

    INSERT INTO scan_records(run_id,kind,server_id,record_key,point_index,uuid,name,alliance_name,level,quality,power,distance,shield_end_time,updated_at,data_json)

The run identity supplied to that branch comes from the map.records event's scanRunId. Thus run-scoped staging is no longer an inferred persistence concept: the production event path from event scanRunId plus record batch to scan_records is source-linked.

After successful staging, the handler increments a batch counter, adds the batch record count and updates a maximum-batch-size counter. These align with the adjacent terminal diagnostic fields record_batches, records and max_record_batch.

## Shared normalized record builder

R8-076 also closes the builder call signature enough to connect ingestion safely.

Function 0x1402785BA-0x1402793BE receives the Map kind as string pointer/length and the raw record as a JSON object. Its existing R6 evidence remains authoritative for record-key derivation and normalized scalar fields.

The staging path and native-capture path therefore do not use separate normalization rules: both converge on the same normalized-record builder and then the same shared SQLite upsert boundary.
## Passive map.native.capture is a different path

The map.native.capture event branch calls 0x1402574CD-0x1402577E2.

That helper also calls normalized builder 0x1402785BA, but its shared-upsert call at 0x14025767E selects exact table map_records, not scan_records.

Therefore map.native.capture is a live native-hook update path into the published index, while map.records is the run-scoped direct-scan staging path. They must not be collapsed into one acquisition mechanism.

## Protected game-side native capture

Both verified embedded secure and plain proxies contain the same native world-capture vocabulary, including:

__XLUA_BRIDGE_NATIVE_WORLD_CAPTURE_RUN, __XluaBridgeNativeWorldCapture, XluaBridgeMapScanTick, XluaBridgeNativeUpdate, and XluaBridgePoll.

The native side identifies/uses WorldPointManager, WorldTileInfo, PointInfo, ResPointInfo, WorldMarchDataManager, WorldMarch, WorldTroopManager, SFSObject and SFSArray, and carries explicit hooks for point/march add/update/remove/fold-up paths.

Its capture envelope vocabulary remains:

scanRunId, ready, points, marches, pointRemovals, marchRemovals, acks, error, dropped, pendingAcks, pendingMarchRemovals, pendingPointRemovals, pendingMarches, pendingPoints.

The proxy also contains explicit states for capture initialization, hooks-ready, optional-hooks-pending, hooks-unavailable and queue overflow.
## Remaining strict-parity gap

Static scans of both embedded proxies find XluaBridgeMapScanTick and the native world-capture globals, but no direct code xref to the exact string starts or their immediate surrounding range. The evidence therefore does not yet authorize a claim about:

1. block traversal order or each block's coordinate envelope;
2. Normal-versus-Fast pacing beyond recovered concurrency 8/20;
3. exact XluaBridgeMapScanTick registration/scheduling loop;
4. point/march/removal/ack drain ordering and retry behavior;
5. the exact conversion from native capture queues into every map.records batch;
6. all per-kind serializer optionality and scalar/string typing.

These remain the protected original acquisition work. The broad project label should change from UNKNOWN to PARTIAL EXACT_CONTRACT, not to complete parity.

## Verification

`tools/inspect_lwbridge_map_acquisition_ingestion.py` is a hash-locked static verifier for the verified 0.3.1 EXE. It independently checks the recovered host call graph, scan-record field xrefs, `scan_records` / `map_records` table selectors, the packed `map.records` dispatcher compare, and contiguous native-capture/completion markers. The verifier passes against SHA-256 `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`.

## Implementation decision

No scanner implementation is changed in R8-076. The current v21 scanner is kept because it is live-working and correctness-proven; replacing it before the remaining game-side traversal/tick contract is recovered would trade a proven equivalent path for speculation.

## Validation

The new hash-locked verifier is tools/inspect_lwbridge_map_acquisition_ingestion.py. It byte-verifies the packed map.records dispatch, direct call edges, scanRunId/selectedTypes/serverId/tileWidth/tileHeight/records/type/payload xrefs, and the scan_records versus map_records table selectors. Its durable output is evidence/lwbridge-implementation/2026-09-26-r8-076-map-acquisition-ingestion-verifier.json.

Python compilation passes. Both R8-076 JSON files parse successfully. git diff --check reports only existing line-ending notices. The Release desktop checks solution builds successfully with zero warnings and zero errors.
