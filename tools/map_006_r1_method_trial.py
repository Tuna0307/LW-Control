"""Bounded genuine current-client Map acquisition comparison, isolated trial only.

Usage: python tools/map_006_r1_method_trial.py r1_20261011
Run ONLY after an owned Home-connected manual scan is terminal, Auto disabled,
and task-only jobs empty. Read-only diagnostic requests; no game claims/jobs.
The collected receipt is aggregate-only and remains under ignored artifacts/.
"""
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]


def require(ok, reason):
    if not ok:
        raise RuntimeError(reason)


def run(trial, scenario="paired"):
    require(trial == "r1_20261011", "Use only authorized fresh R1 task root")
    base = ROOT / "artifacts" / "map-006" / ("trial-" + trial)
    isolated = base / "isolated"
    profile = "map-006-" + trial
    ready = json.loads((isolated / "overview-bridge" / "ready.json").read_text())
    auto = json.loads((isolated / "profiles" / profile / "runtime" / "map-auto-scan.json").read_text())
    require(ready.get("ready") is True and ready.get("profileId") == profile, "Current owned session is not ready")
    require(auto["config"]["enabled"] is False and not auto["restart"]["active"], "Auto must be idle/off")
    db = isolated / "profiles" / profile / "map-data" / "map-data.db"
    with sqlite3.connect(f"file:{db.resolve().as_posix()}?mode=ro", uri=True) as connection:
        latest = connection.execute("SELECT server_id,status,selected_types FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
        jobs = [connection.execute(f"SELECT COUNT(*) FROM {t}").fetchone()[0]
                for t in ("dispatch_plunder_jobs", "truck_plunder_jobs")]
    require(latest and latest[0] == 2212 and latest[1] == "completed" and
            json.loads(latest[2]) == ["truck"], "The instrumented Truck-only baseline must have completed")
    require(jobs == [0, 0], "No plunder jobs allowed")
    require(scenario in ("paired", "official"), "Unsupported R1 bounded probe scenario")
    output = base / ("r1-method-paired-receipt.json" if scenario == "paired" else
                     "r1-official-train-list-receipt.json")
    require(not output.exists(), "Never overwrite a previous method receipt")
    status = json.loads((isolated / "overview-bridge" / "world-state-result.json").read_text())
    require(status.get("serverId") == 2212 and status.get("state") == "proven",
            "Actual authoritative current game server must be 2212")
    live = isolated / "live-resource"
    require(live.is_dir(), "Current-client native producer is absent")
    identity = {"schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
                "profileId": profile, "launchSessionId": ready["sessionId"],
                "challenge": ready["challenge"], "gamePid": ready["gamePid"],
                "serverId": 2212, "scanRunId": "r1benchmark" + uuid.uuid4().hex}
    results = []

    def sample(kind, xy):
        rid = "r1sample" + uuid.uuid4().hex
        fields = dict(identity, requestId=rid)
        if kind == "official-train-list":
            fields["liveServerId"] = 2212
            request = live / "train-list-diagnostic.txt"
            result = live / "train-list-diagnostic-result.json"
        else:
            fields.update(viewLevel=-1, requestMode=kind,
                          targetTileX=xy[0], targetTileY=xy[1], requestedCount=8,
                          holdMilliseconds=0, homeTileX=-1, homeTileY=-1,
                          includeCity="false", includeResource="false", includeMonster="false",
                          includeMonsterProtection="false", includeTrain="true",
                          includeDispatch="false", includeGhost="false", includeTreasure="false",
                          includeResourceDetails="false", deferRestoreUntilResponse="false")
            request = live / "bulk-aoi-diagnostic.txt"
            result = live / "bulk-aoi-diagnostic-result.json"
        require(not request.exists(), "Another producer request is already queued")
        temporary = request.with_name(request.name + ".r1-temp")
        temporary.write_text("\n".join(f"{k}={v}" for k, v in fields.items()) + "\n", encoding="utf-8")
        start = time.perf_counter()
        os.replace(temporary, request)
        record = None
        while time.perf_counter() - start < 15:
            try:
                value = json.loads(result.read_text(encoding="utf-8"))
                if value.get("requestId") == rid:
                    record = value
                    break
            except (FileNotFoundError, json.JSONDecodeError, PermissionError):
                pass
            time.sleep(0.05)
        elapsed = round((time.perf_counter() - start)*1000, 1)
        require(record is not None, f"{kind} diagnostic did not respond within 15s")
        require(record.get("profileId") == profile and record.get("launchSessionId") == ready["sessionId"]
                and record.get("gamePid") == ready["gamePid"] and
                (kind != "official-train-list" or record.get("serverId") == 2212),
                "Diagnostic response ownership mismatch")
        rows = record.get("rows", []) if kind == "official-train-list" else (record.get("train_march_records") or [])
        # Stable UUID is only retained as a salted opaque digest, never raw.
        stable = {}
        for row in rows:
            if not isinstance(row, dict):
                continue
            key = row.get("trainUuid") or row.get("marchUuid") or row.get("uuid")
            if key is None:
                continue
            digest = hashlib.sha256((identity["scanRunId"]+str(key)).encode()).hexdigest()[:20]
            stable[digest] = sorted(str(k) for k,v in row.items() if v is not None)
        indices = record.get("requestedIndices") or []
        item = {"kind":kind, "target":list(xy) if xy else None,
                "state": record.get("state"), "error":record.get("error"),
                "elapsedMs":elapsed, "producerElapsedSeconds":record.get("elapsedSeconds"),
                "requestMethod":record.get("requestMethod") or record.get("method"),
                "logicalProbeCommands":1, "attributableNetworkRequests":None,
                "networkTrafficDeltaNotAttributable": (
                    record.get("netSendCount")-record.get("baselineNetSendCount")
                    if isinstance(record.get("netSendCount"),int) and isinstance(record.get("baselineNetSendCount"),int) else None),
                "coveredAoiCount":len(set(indices)), "coveredAoiDigest":hashlib.sha256(
                    json.dumps(sorted(set(indices))).encode()).hexdigest(),
                "capturedAt":record.get("capturedAt"), "refreshObserved":record.get("refreshObserved"),
                "liveServerId":record.get("liveServerId"),
                "matchedServerIds":record.get("matchServerIds"),
                "uniqueOpaqueTrainIds":sorted(stable), "nonNullFieldsByTrain":stable,
                "sourceRows":len(rows), "uniqueIds":len(stable),
                "cameraTileStable":record.get("cameraTileStable"),
                "responseFlagsTransitioned":record.get("responseFlagsTransitioned"),
                "positionRestoredBeforeResponse":record.get("positionRestoredBeforeResponse"),
                "positionRestoredAfterResponse":record.get("positionRestoredAfterResponse"),
                "preTile":(record.get("preTileX"),record.get("preTileY")),
                "postTile":(record.get("postTileX"),record.get("postTileY")),
                "preServerLod":record.get("serverLod"), "postServerLod":record.get("postServerLod"),
                "preBlockSize":record.get("blockSize"), "postBlockSize":record.get("postBlockSize"),
                "preBlockCount":record.get("blockCount"), "postBlockCount":record.get("postBlockCount"),
                "splitPendingAfterInvoke":record.get("postInvokeSplitPending")}
        results.append(item)
        # Preserve an interrupted/failed measurement instead of driving
        # further requests against an uncertain native manager state.
        output.write_text(json.dumps({"trial":trial,"pairedSamples":results,
            "complete":False,"rawRecordsIncluded":False},indent=2))
        require(item["state"] == "proven" and (kind == "official-train-list" or
                (item["cameraTileStable"] is True and item["responseFlagsTransitioned"] is True and
                 item["preServerLod"] == item["postServerLod"] and
                 item["preBlockSize"] == item["postBlockSize"])),
                f"{kind} failed native/manager state restoration; no further probes")
        return item

    # Same client, server and selected types. AB then BA controls both ordering
    # and transient natural population; one region may have no visible trucks.
    sample("coverage", (505,505))
    if scenario == "paired": sample("direct", (505,505))
    sample("official-train-list", None)
    sample("official-train-list", None)
    if scenario == "paired": sample("direct", (505,505))
    sample("coverage", (505,505))
    output.write_text(json.dumps({"trial":trial,"pairedSamples":results,
        "complete":True,"rawRecordsIncluded":False},indent=2))
    print(json.dumps({"receipt":str(output),"samples":[{k:s[k] for k in
        ("kind","state","elapsedMs","coveredAoiCount","sourceRows","uniqueIds","cameraTileStable","responseFlagsTransitioned")}
        for s in results]},indent=2))


if __name__ == "__main__":
    if len(sys.argv) not in (2,3): raise SystemExit(__doc__)
    run(sys.argv[1],sys.argv[2] if len(sys.argv)==3 else "paired")
