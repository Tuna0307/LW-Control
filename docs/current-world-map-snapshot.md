# Current world-map read-only snapshot contract

This checkpoint identifies the current game's structured bulk world-point source
and defines a read-only JSON boundary for the reconstruction. All evidence in
this document was collected by static inspection of the installed 2026-09-06
build. The game was not launched, no network message was sent, and no installed
game file was modified.

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

## UNKNOWN / not claimed at this checkpoint

- A live snapshot from the current running game has not yet been captured.
- The exact current user-string text returned by `WorldGetBlockMessage.GetMsgId`
  was not re-decoded in this checkpoint, although the message class and complete
  C# request/response path are current-build proven.
- The exact mapping from point `id` to user-facing map X/Y coordinates is not yet
  part of schema version 1. AOI/block conversion methods exist, but the mapping
  will be documented only after it is recovered and independently checked.
- No policy for sweeping beyond the game's currently loaded AOI has been chosen.
- The additional season-specific `WorldPointInfo` payload bodies are not yet
  normalized into version 1.

The next implementation milestone is a bounded read-only capture adapter that
exports the already-loaded `WorldPointManager` state into this contract. A wider
map sweep should only be designed after that snapshot is proven against live
points.
