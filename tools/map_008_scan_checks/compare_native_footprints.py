"""Bounded passive A/B native AOI camera-aspect and restoration trials.

Only known current-client WorldPointManager.UpdateViewRequest(coverage) is used;
the live Lua script validates profile/challenge/PID/server and restores the
original camera/FOV/aspect. Do not run while an ordinary scan/Auto is active.
This is region-method research, never a substitute for full-world accuracy.
"""
import argparse
import datetime as dt
import json
import pathlib
import sqlite3
import time
import uuid


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--root", required=True, type=pathlib.Path)
    p.add_argument("--profile", required=True)
    p.add_argument("--out", required=True, type=pathlib.Path)
    p.add_argument("--experiment", choices=("aspect", "resource_restore", "restore_recovery", "railway_witness", "nonresource_geometry", "city_recovery"), default="aspect")
    p.add_argument("--save-native", type=pathlib.Path, help="Task-private single native response for independent field audit")
    p.add_argument("--target-x", type=int, help="Known native Railway x for a single bounded read-only witness")
    p.add_argument("--target-y", type=int, help="Known native Railway y for a single bounded read-only witness")
    p.add_argument("--target-server",type=int,help="Restored target server, accepted only with an exact proven native return receipt")
    a = p.parse_args()
    db = a.root / "profiles" / a.profile / "map-data" / "map-data.db"
    conn = sqlite3.connect(f"file:{db.resolve().as_posix()}?mode=ro", uri=True)
    states = conn.execute("SELECT status, server_id FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
    if states and states[0] == "running":
        raise RuntimeError("Live scanner still owns AOI requests; refuse experiment")
    auto = a.root / "profiles" / a.profile / "runtime" / "map-auto-scan.json"
    if auto.exists() and json.loads(auto.read_text(encoding="utf-8")).get("enabled"):
        raise RuntimeError("Auto must be disabled for isolated method trial")

    resource = a.root / "live-resource"
    result = resource / "bulk-aoi-diagnostic-result.json"
    command = resource / "bulk-aoi-diagnostic.txt"
    identity = json.loads(result.read_text(encoding="utf-8"))
    if identity.get("state") != "proven" or identity.get("profileId") != a.profile:
        raise ValueError("No recent proven native identity in this trial profile")
    if not identity.get("challenge") or not identity.get("launchSessionId") or not identity.get("gamePid"):
        raise ValueError("Native identity incomplete")
    target_server = states[1]
    if a.target_server is not None and a.target_server != target_server:
        if a.experiment != "railway_witness":
            raise ValueError("Cross-server target override is only supported for bounded Railway witness")
        jump = json.loads((a.root / "overview-bridge" / "server-jump-result.json").read_text(encoding="utf-8"))
        if not (jump.get("state") == "proven" and jump.get("serverId") == a.target_server and
                jump.get("currentServerId") == a.target_server and jump.get("profileId") == a.profile and
                jump.get("gamePid") == identity["gamePid"] and
                jump.get("sessionId") == identity["launchSessionId"] and
                jump.get("challenge") == identity["challenge"]):
            raise ValueError("Cannot prove exact restored client/server before Railway witness")
        target_server = a.target_server
    if identity.get("coverageAspect") != 4:
        raise ValueError("The source baseline aspect is not confirmed at 4")
    receipt = []
    # Distinct nearby slices avoid the native producer's guard against reused
    # currently loaded target points. This trial only assesses native footprint
    # geometry, not population parity between different slices.
    if a.experiment == "aspect":
        attempts = [(4,True,155,255), (6,True,305,255), (6,True,505,455), (4,True,705,455),
                    (4,True,155,655), (6,True,305,655), (6,True,505,855), (4,True,705,855)]
    elif a.experiment == "resource_restore":
        attempts = [(4,True,105,205), (4,False,305,205), (4,False,505,405), (4,True,705,405),
                    (4,False,105,605), (4,True,305,605), (4,True,505,805), (4,False,705,805)]
    elif a.experiment == "restore_recovery":
        # One new post-adversity positive witness; prior faster-but-empty source
        # failure remains immutable and is not repeated.
        attempts = [(4,True,495,705)]
    elif a.experiment == "railway_witness":
        # The completed scanner found one exact target-server Railway at
        # an admitted known tile. Check its fresh native point now, with camera held
        # through the response. A moving target can legitimately be absent.
        if (a.target_x is None) != (a.target_y is None):
            raise ValueError("Railway target coordinates must be provided together")
        target_x = 483 if a.target_x is None else a.target_x
        target_y = 568 if a.target_y is None else a.target_y
        if not 0 <= target_x < 1000 or not 0 <= target_y < 1000:
            raise ValueError("Target outside admitted standard world")
        attempts = [(4,True,target_x,target_y)]
    elif a.experiment == "city_recovery":
        # Same AOI as an immediately-restored empty source, now requiring the
        # callback to settle. Independent previously published City witnesses
        # make this a distinguishing omission check.
        attempts = [(4,True,135,175)]
    else:
        # Independent, deliberately distributed non-Resource camera placement
        # measurements. The production scanner must still prove 10,000 cells.
        attempts = [(4,False,x,y) for y in (175,375,575,775) for x in (135,335,535,735)]
    for aspect, defer_restore, tile_x, tile_y in attempts:
        request_id = "qual" + a.experiment[:5] + uuid.uuid4().hex
        pairs = {
            "schema":"1", "probeVersion":identity["probeVersion"],
            "requestId":request_id, "profileId":identity["profileId"],
            "launchSessionId":identity["launchSessionId"],
            "challenge":identity["challenge"], "gamePid":identity["gamePid"],
            "serverId":target_server, "scanRunId": a.experiment + request_id,
            "viewLevel":-1, "requestMode":"coverage", "coverageAspect":aspect,
            "targetTileX":tile_x, "targetTileY":tile_y, "requestedCount":8,
            "holdMilliseconds":0, "homeTileX":identity.get("homeTileX",-1),
            "homeTileY":identity.get("homeTileY",-1),
            "includeCity":"true", "includeResource":"true", "includeMonster":"true",
            "includeMonsterProtection":"false", "includeTrain":"true",
            "includeDispatch":"true", "includeGhost":"false", "includeTreasure":"false",
            "includeResourceDetails":"true", "deferRestoreUntilResponse":str(defer_restore).lower(),
        }
        if a.experiment == "railway_witness":
            pairs.update({"includeCity":"false","includeResource":"false",
                          "includeMonster":"false","includeTrain":"true",
                          "includeDispatch":"false","includeResourceDetails":"false"})
        elif a.experiment == "nonresource_geometry":
            pairs.update({"includeResource":"false","includeResourceDetails":"false"})
        # This is a synchronous, sequential command to the already owned game;
        # the result identity is checked before another request can be sent.
        command.write_text("\n".join(f"{key}={value}" for key,value in pairs.items()) + "\n", encoding="utf-8")
        start = time.monotonic()
        found = None
        while time.monotonic() - start < 17:
            try:
                candidate = json.loads(result.read_text(encoding="utf-8"))
                if candidate.get("requestId") == request_id:
                    found = candidate
                    break
            except (OSError, json.JSONDecodeError):
                pass
            time.sleep(.07)
        if found is None:
            raise RuntimeError(f"No terminal correlated response at aspect {aspect}")
        if found.get("gamePid") != identity["gamePid"] or found.get("launchSessionId") != identity["launchSessionId"]:
            raise RuntimeError("Game session changed during geometry trial")
        indices = found.get("requestedIndices") or []
        columns = sorted({i % 100 for i in indices})
        rows = sorted({i // 100 for i in indices})
        entry = {"aspect":aspect, "deferRestoreUntilResponse":defer_restore,
                 "target":[tile_x,tile_y], "elapsedMs":round((time.monotonic()-start)*1000,1),
                 "state":found.get("state"), "error":found.get("error"),
                 "requestMethod":found.get("requestMethod"),
                 "responseFlagsTransitioned":found.get("responseFlagsTransitioned"),
                 "cameraTileStable":found.get("cameraTileStable"),
                 "positionRestoredAfterResponse":found.get("positionRestoredAfterResponse"),
                 "positionRestoredBeforeResponse":found.get("positionRestoredBeforeResponse"),
                 "columnCount":len(columns), "rowCount":len(rows), "aoiCount":len(indices),
                 "minColumn":min(columns) if columns else None,
                 "maxColumn":max(columns) if columns else None,
                 "minRow":min(rows) if rows else None,
                 "maxRow":max(rows) if rows else None,
                 "matchedResourceCount":found.get("matchedResourceCount"),
                 "matchedCityCount":found.get("matchedCityCount"),
                 "resourceDetailTargetCount":found.get("resourceCompleteness",{}).get("acceptedResourceCount") if isinstance(found.get("resourceCompleteness"),dict) else None,
                 "nativeRecordCount":len(found.get("point_records") or []) + len(found.get("monster_march_records") or []) + len(found.get("train_march_records") or [])}
        entry["nativeTrainMarchCount"] = len(found.get("train_march_records") or [])
        entry["nativeRailwayMarchCount"] = sum(1 for row in found.get("train_march_records") or [] if row.get("trainType") == 2)
        if a.save_native:
            a.save_native.parent.mkdir(parents=True, exist_ok=True)
            a.save_native.write_text(json.dumps(found, ensure_ascii=False), encoding="utf-8")
        receipt.append(entry)
        a.out.parent.mkdir(parents=True, exist_ok=True)
        a.out.write_text(json.dumps({"status":"controlled_bounded_native_region_comparison",
            "experiment":a.experiment,
            "source": "current-client v24 owned WorldPointManager coverage producer",
            "sameIdentity": {"profile":identity["profileId"],"server":target_server},
            "comparisons":receipt,"wholeWorldRecall":False}, indent=2) + "\n", encoding="utf-8")
        restored = entry["positionRestoredAfterResponse"] if defer_restore else entry["positionRestoredBeforeResponse"]
        if entry["state"] == "proven" and (not entry["cameraTileStable"] or not entry["responseFlagsTransitioned"] or not restored):
            raise RuntimeError("Native region response did not restore camera/flags")
    print(json.dumps({"tested":len(receipt),"results":receipt}, indent=2))


if __name__ == "__main__":
    main()
