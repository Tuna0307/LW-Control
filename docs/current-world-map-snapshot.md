# Current world-map read-only snapshot contract

This checkpoint identifies the current game's structured bulk world-point source
and defines a read-only JSON boundary for the reconstruction. The contract was
first recovered statically and was then proven against a bounded live current-
build capture on 2026-09-06. Later bounded live requests proved the recovered
5-by-4 minimum transport, three-logical-block coverage, and a serial 13-by-13
two-batch completion rule with 169/169 correlated logical blocks. Every live
candidate restored the exact pre-run installed hashes after capture.

The machine-readable contract is
[`current-world-map-snapshot.schema.json`](current-world-map-snapshot.schema.json),
with its C# validator in
[`CurrentWorldMapSnapshot.cs`](../src/LWControl.Core/CurrentWorldMapSnapshot.cs).

## PROVEN: bulk world-point source

Current `Assembly-CSharp.rdl` SHA-256:
`871efe06819fbac438413eb96b7df8193d0be56094f3a44d5ff141e6219adcbd`.

`WorldPointManager` is the current C# owner of the loaded world-point state. Its
metadata contains the following relevant fields:

- `_pointInfos`, `allViewPoints`, `uuidInfoMap`, `allObjs`;
- `outOfViewPoints`, `outOfViewPointsObj`, `timeOutPoints`;
- `_lwAoiBlockSize`, `_lwAoiBlockCount`, `_curViewIndex`, `_msgViewIndex`;
- `_leftBottomWorldPos`, `_rightTopWorldPos`, and the four last-view AOI blocks.

The current request/response path is also recovered:

1. `WorldPointManager.SendAoiRequest` is MethodDef `0x0600351F`.
2. It constructs the nested `WorldGetBlockMessage.Request` object.
3. `WorldGetBlockMessage.CSHandleResponse` obtains `SceneManager.World` and calls
   `SceneInterface.HandleViewPointsReply`.
4. The concrete `WorldPointManager.HandleViewPointsReply` calls
   `ParseWorldGetBlock` (`0x06003555`).
5. `ParseWorldGetBlock` reads the manager's `_pointInfos` collection and creates
   point records through `NewPointInfo`.
6. `AddPointInfo` (`0x06003570`) inserts/updates point data in the manager's
   `allViewPoints` and UUID-indexed state, with object replacement/removal paths
   for changed entries.

This is the structured bulk source the scanner needs. It is materially different
from the separately recovered `FindMonster`, `FindMonsterBoss`, and
`FindResourcePoint` targeted-search endpoints.

## PROVEN: world block request shape

The current `WorldGetBlockMessage.Request` type has exactly these 13 fields:

| Field | Type |
| --- | --- |
| `bigMap` | int32 |
| `x` | int32 |
| `y` | int32 |
| `serverId` | int32 |
| `worldId` | int32 |
| `type` | int32 |
| `lod` | int32 |
| `index` | int32 array |
| `blockSize` | int32 |
| `firstTime` | bool |
| `leftBottom` | int32 |
| `rightTop` | int32 |
| `battleFieldFirst` | bool |

`WorldPointManager` also contains `UpdateLWAoi_Normal`, `UpdateLWAoi_Big3000`,
`SendViewRequest`, `UpdateViewRequest`, AOI block/index conversion methods, and
`once_max_request_count`. These prove the game already performs bounded
area-of-interest/block loading. They do not yet define our replacement scanner's
sweep policy.

## PROVEN: point record fields

The current generated `Protobuf.WorldPointInfo` in `Assembly-CSharp.rdl` confirms
the stable identity/routing fields used by schema version 1:

- `id`;
- `pointType`;
- `uuid`;
- `serverId`;
- `srcServerId`;
- `worldId`.

The current Lua protobuf source also gives exact fields for the common payloads
preserved by version 1:

- `BuildInfo`: owner, UUID, build ID, level, states, alliance, HP/protection and
  queue/update timers, name/abbreviation, appearance, special type, position ID;
- `RoadInfo`: owner, UUID, road state, inside flag, HP, alliance;
- `CollectResourceInfo`: resource type, level, type, attach ID;
- `ResourceInfo`: resource ID, state, gather UUID;
- `ExplorePointInfo` and `SamplePointInfo`: owner, UUID, event ID;
- `GarbagePointInfo`: owner, UUID, event ID, end time.

The generated C# protobuf contains additional newer payloads such as hero
dispatch, treasure, alliance collection, ice supplies, ghost recon, city
attachment, zone mobilization, surprise/meteorite/activity/quarantine points,
status entries, and other season-specific records. Version 1 deliberately leaves
those payload bodies out until their individual field contracts are recovered.
Their point identity and `pointType` are still retained.

## Snapshot version 1

Required top-level fields are:

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Must be exactly `1`. |
| `mode` | Must be exactly `state`. |
| `source` | Must be exactly `WorldPointManager`. |
| `captureId` | Unique capture identity. |
| `capturedAt` | Timestamp for the capture. |
| `heartbeat` | Probe version and observed timestamp. |
| `points` | Structured world-point records from the recovered manager boundary. |

Example:

```json
{
  "schemaVersion": 1,
  "mode": "state",
  "source": "WorldPointManager",
  "captureId": "world-example-1",
  "capturedAt": "2026-09-06T03:45:00+08:00",
  "heartbeat": {
    "probeVersion": "lwcontrol-world-state-probe-1",
    "observedAt": "2026-09-06T03:45:00+08:00"
  },
  "points": [
    {
      "id": 123456,
      "pointType": 4,
      "uuid": 987654321,
      "serverId": 1,
      "srcServerId": 1,
      "worldId": 1,
      "collectResourceInfo": {
        "resourceType": 1,
        "level": 8,
        "type": 0,
        "attachId": 0
      }
    }
  ]
}
```

The numbers in this example are illustrative only; they are not observations of
a live point.

## Reconstruction-side fail-closed guards

These are local input-safety rules, not claims that the game enforces identical
limits:

- unknown JSON properties are rejected;
- snapshots must be fresh by the same 15-second default used by the existing
  bridge/state contracts, with five seconds of future-clock tolerance;
- at most 50,000 point records are accepted;
- identity/routing integers and UUIDs must be non-negative;
- `(worldId, serverId, id)` must be unique inside one capture;
- recovered string fields are capped at 512 characters;
- common payload identifiers/levels that represent IDs or counts are rejected
  when negative.

The shared JSON reader also imposes its existing 2 MiB input limit.

## PROVEN: one bounded wider AOI request

Candidate a20 selected the adjacent logical AOI block `(58,47)` while the live
visible range was X `53..57`, Y `46..49`. Following recovered original-LW-Control
behavior, the logical request was carried in a padded 5-by-4 transport envelope,
X `56..60`, Y `46..49`, containing 20 row-major block indexes. The request used
the current manager's block size `10` and block count `100`, and converted the
padded tile bounds to `leftBottom = 460561` and `rightTop = 500611`.

Exactly one request was sent through the recovered managed bridge. Exactly one
`world.get.block` target response was observed through `DispatchResponse2`. Its
authoritative nested `serverPointArr` envelope decoded to exactly the same block
coverage X `56..60`, Y `46..49`, including the requested logical block. This is
live proof that the recovered transport shape, managed argument bridge, nested
response extraction, and overlap/coverage correlation work on the current
build for one bounded adjacent request.

The manager state changed from 86 to 60 loaded points and contained 13 point IDs
not present before the request. Several returned IDs independently decoded to
tile coordinates inside the authoritative response bounds, proving the current
ID-to-tile decode for those observed records. See
[`current-world-map-live-capture.md`](current-world-map-live-capture.md) for the
complete evidence.

## PROVEN: multi-block and serial multi-batch logical coverage

Candidate a21 sent one padded request for three adjacent logical targets and
completed only after correlated authoritative `world.get.block` response
coverage accounted for all **3/3** targets. This proves that logical coverage
can be tracked independently from the larger padded transport envelope on the
current build.

Candidate a22 then exercised the recovered native batch boundary with a bounded
13-by-13 logical window (169 blocks). The recovered planner split it into **156
+ 13** logical blocks. Batch 1 completed 156/156 before batch 2 was sent; batch 2
then completed 13/13. Both responses came from the authoritative nested
`serverPointArr` envelope for the compatible live server/world identity. The
final result was 169/169, exactly two sends, zero retries, and zero camera moves.

This is live proof of the serial capability-probe ordering and bounded
multi-batch completion rule. It is not a claim that `_pointInfos` itself is
append-only or complete; response-envelope coverage remains the authoritative
completion evidence.

## PROVEN: bounded concurrent native batch wave

Candidate w23 extended the same recovered scheduler boundary with a 19-by-19
logical request (361 blocks). The recovered planner produced three batches of
152, 152, and 57 logical blocks. Batch 1 completed serially first. Batches 2 and
3 were then both issued before the first of their responses arrived, proving a
two-inflight concurrent wave rather than sequential request timing.

Each concurrent response was correlated to exactly one in-flight request by
compatible server/world identity plus exact `leftBottom`/`rightTop` bounds. The
concurrent batches covered 152/152 and 57/57 logical targets respectively, for a
final 361/361 completion result. There were exactly three sends, zero retries,
zero camera moves, and exact hook/flag/file restoration.

This proves bounded concurrent scheduling at in-flight width two on the current
build. The recovered original scanner's default width of eight and its adaptive
retry/concurrency-reduction/fallback paths remain separate claims.

## UNKNOWN / not claimed at this checkpoint

- The exact current user-string text returned by `WorldGetBlockMessage.GetMsgId`
  was not re-decoded in this checkpoint, although the message class and complete
  C# request/response path are current-build proven.
- The observed point-ID-to-tile decode is live-proven for records returned by
  candidate a20, but schema version 1 still does not promise a universal
  user-facing X/Y presentation contract for every point/map mode.
- Multi-block coverage, serial two-batch completion, and a two-inflight
  concurrent wave are proven. Full-world orchestration, the wider original
  concurrency policy, retries, and camera fallback remain separate unproven
  behaviors.
- The additional season-specific `WorldPointInfo` payload bodies are not yet
  normalized into version 1.

`tools/current_world_map_snapshot_probe.lua` implements the bounded adapter from
the recovered original reflection contract. It enumerates only the already-
loaded `WorldPointManager._pointInfos` collection, verifies its observed count
and duplicate identities, and emits schema-v1 identity/routing fields.

Candidate a13 proved this boundary live: `CS.SceneManager.CurrSceneID` matched
the current World scene ID, `CS.SceneManager.World.PointManager` was the manager
source, `_pointInfos` stabilized at 86 records, and a schema-v1 snapshot was
emitted. See [`current-world-map-live-capture.md`](current-world-map-live-capture.md)
and [`current-world-map-live-evidence.json`](current-world-map-live-evidence.json).

The bounded scanner policy is implemented in `RecoveredWorldMapScanPlanner`,
including the recovered 160-index limit, minimum 5-by-4 transport padding,
batching, serpentine order, and fail-closed response coverage accounting. Live
candidates a21 and a22 now prove multi-block acceptance and serial two-batch
completion on the current build. Full-world completeness remains a later,
separate claim.
