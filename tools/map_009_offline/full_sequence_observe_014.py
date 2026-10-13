"""Private full-world qualification; never publishes product Map data.

Each covered cell requires an owner-correlated fresh native response. The
union must contain exactly all 10,000 LOD0 cells, with actual native rows.
Final restoration is a separate fresh home-region response, not a timer.
"""
from __future__ import annotations
import json
import time
import secrets


def run_full_sequence(observer, *, attempt, live_root, command, world,
                      profile, session, pid, challenge, previous_id):
    covered = set()
    rows = {}
    metrics = []
    started = time.monotonic()
    for number in range(1, 401):
        gap = next((index for index in range(10000) if index not in covered), None)
        final = gap is None
        x = int(world["playerTileX"]) if final else min(995, (gap % 100 + 7) * 10 + 5)
        y = int(world["playerTileY"]) if final else min(995, (gap // 100 + 2) * 10 + 5)
        observer.retire_owned_reply(live_root / "bulk-aoi-diagnostic-result.json",
                                   previous_id, profile, session, pid, challenge)
        request_id = "map014world" + secrets.token_hex(8)
        values = {**command, "requestId": request_id, "scanRunId": request_id,
                  "targetTileX": str(x), "targetTileY": str(y),
                  "map013Sequence": "final" if final else "keep"}
        sent = observer.publish_command(live_root / "bulk-aoi-diagnostic.txt", values)
        result = observer.wait_for(live_root / "bulk-aoi-diagnostic-result.json",
            observer.ExpectedReply("region", request_id, profile, session, pid, challenge, sent,
                                   server_id=world["serverId"], world_id=world["worldId"],
                                   request_mode="coverage", target_tile_x=x, target_tile_y=y), 28)
        indices = result.get("requestedIndices") or []
        target = y // 10 * 100 + x // 10
        if (result.get("state") != "proven" or result.get("map013NativeReplyState") != "matched"
                or (result.get("serverLod"), result.get("blockSize"), result.get("blockCount")) != (0, 10, 100)
                or not indices or len(set(indices)) != len(indices) or target not in indices
                or any(type(i) is not int or not 0 <= i < 10000 for i in indices)
                or result.get("nativeCurrentSetCount") != len(indices)):
            (attempt / "full-sequence-negative.json").write_text(json.dumps(result, indent=2), encoding="utf8")
            raise RuntimeError("full sequence lacks fresh native target/geometry/identity")
        columns = {i % 100 for i in indices}; bands = {i // 100 for i in indices}
        if {r * 100 + c for r in bands for c in columns} != set(indices):
            raise RuntimeError("full sequence footprint is not a complete native rectangle")
        before = len(covered)
        if final:
            if result.get("cameraGeometryConfirmed") is not True or result.get("cameraTileStable") is not True:
                raise RuntimeError("full sequence final camera did not restore exactly")
        else:
            if result.get("map013PositionRetained") is not True:
                raise RuntimeError("full sequence unexpectedly returned home between regions")
            covered.update(indices)
            if len(covered) == before:
                raise RuntimeError("full sequence made no new native AOI progress")
            for row in result.get("point_records") or []:
                if row.get("serverId") != world["serverId"] or row.get("worldId") != world["worldId"]:
                    raise RuntimeError("full sequence row has foreign native context")
                rx, ry = row.get("x"), row.get("y")
                if not isinstance(rx, (int, float)) or not isinstance(ry, (int, float)) or int(ry)//10*100+int(rx)//10 not in indices:
                    raise RuntimeError("full sequence row is outside fresh native footprint")
                identity = str(row.get("pointId") or row.get("id") or row.get("uuid") or "")
                if not identity:
                    raise RuntimeError("full sequence native row lacks identity")
                rows[(row.get("kind"), identity)] = row
        metrics.append({"number": number, "target": [x, y], "nativeAoIs": len(indices),
                        "coverage": len(covered), "rows": len(result.get("point_records") or []),
                        "elapsed": time.monotonic()-started, "final": final})
        (attempt / "full-sequence-progress.json").write_text(json.dumps(metrics, indent=2), encoding="utf8")
        if final:
            receipt = {"nativeCoverage": len(covered), "nativeTotal": 10000,
                       "probes": number-1, "elapsedSeconds": time.monotonic()-started,
                       "nativeSourceRows": list(rows.values()), "exactCameraRestored": True,
                       "publishedProductRows": 0, "productionEnabled": False,
                       "externalPopulationCensus": False}
            (attempt / "full-sequence-result.json").write_text(json.dumps(receipt, indent=2), encoding="utf8")
            print("FULL_SEQUENCE_OBSERVED", len(covered), "rows", len(rows),
                  "probes", number-1, "seconds", round(receipt["elapsedSeconds"], 3), flush=True)
            return request_id
        if number % 10 == 0:
            print("FULL_SEQUENCE_PROGRESS", number, len(covered), len(rows), flush=True)
        previous_id = request_id
    raise RuntimeError("full sequence exceeded bounded 400-probe coverage plan")
