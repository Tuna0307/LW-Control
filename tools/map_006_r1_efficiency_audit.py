"""Summarize private R1 client results into an aggregate comparison (no rows/IDs)."""
from collections import Counter
import json
from pathlib import Path
import shutil
import sqlite3
import time

ROOT = Path(__file__).resolve().parents[1] / "artifacts" / "map-006" / "trial-r1_20261011"
isolated = ROOT / "isolated"
metrics = json.loads((isolated / "live-resource" / "map-fullworld-probe-metrics.json").read_text())
paired = json.loads((ROOT / "r1-method-paired-receipt.json").read_text())
official = json.loads((ROOT / "r1-official-train-list-receipt.json").read_text())
db = isolated / "profiles" / "map-006-r1_20261011" / "map-data" / "map-data.db"
with sqlite3.connect(f"file:{db.resolve().as_posix()}?mode=ro", uri=True) as d:
    run = d.execute("SELECT id,server_id,status,selected_types,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs ORDER BY created_at DESC LIMIT 1").fetchone()
    data = [json.loads(v[0]) for v in d.execute("SELECT data_json FROM map_records WHERE kind='truck' AND server_id=2212")]
    unique = d.execute("SELECT COUNT(DISTINCT record_key) FROM map_records WHERE kind='truck' AND server_id=2212").fetchone()[0]
    staged = d.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]

required = ("trainUuid", "marchUuid", "ownerServer", "trainType", "quality", "power", "arriveTs", "x", "y", "currentGoods", "maxLootCount")
field_present = {name:sum(record.get(name) is not None for record in data) for name in required}
now = int(time.time() * 1000)
visible = sum(record.get("arriveTs") is None or int(record["arriveTs"]) > now for record in data)
samples = paired["pairedSamples"] + official["pairedSamples"]
assert run[1:7] == (2212, "completed", '["truck"]', 2500, 2500, 0)
assert run[9] is None and unique == len(data) and staged == 0
assert metrics["identity"]["RunId"] == run[0] and metrics["uniqueCoveredAois"] == 10000
assert metrics["attributableLuaProbeAttempts"] == metrics["validatedProbeResponses"]
assert not paired["complete"] and official["complete"]
assert samples[0]["kind"] == "coverage" and samples[1]["kind"] == "direct"
assert samples[1]["state"] == "failed" and samples[1]["error"] == "bulk_aoi_response_timeout"
assert all(s["state"] == "proven" for s in official["pairedSamples"])

comparison = {
    "task": "LWB317-MAP-006-STOP-EFFICIENCY-R1",
    "evidenceType": "official-client-v24-genuine-measured-passive",
    "serverId": 2212,
    "scanTypes": ["truck"],
    "scanMode": "fast",
    "baseline": {
        "runId": run[0], "state": run[2],
        "elapsedMs": run[8]-run[7],
        "logicalBlocks":run[4], "failedBlocks":run[6],
        "uniqueAoiCoverage":metrics["uniqueCoveredAois"],
        "requiredAois":metrics["requiredAois"],
        "attributableLuaProbeAttempts":metrics["attributableLuaProbeAttempts"],
        "attributableProbeRetries":metrics["attributableRetries"],
        "validatedProbeResponses":metrics["validatedProbeResponses"],
        "probeWaitMs":round(metrics["summedProbeWaitMilliseconds"],1),
        "rawGameNetworkRequests":None,
        "storedRows":len(data), "uniqueRecordKeys":unique,
        "currentlyUnexpiredRecordsAtAudit":visible,
        "stagedRecords":staged, "nonNullFieldCounts":field_present,
        "recordFreshness": "validated post-response native footprint; separate exact updated_at persisted per unique record",
        "cameraRestoration": "each region validated by native `cameraTileStable`, server LOD and geometry; representative paired captures below",
    },
    "directPaired": [
        {k:v for k,v in s.items() if k in ("kind", "state", "error", "elapsedMs", "coveredAoiCount", "sourceRows",
                                            "uniqueIds", "cameraTileStable", "responseFlagsTransitioned", "preTile", "postTile", "preServerLod", "postServerLod")}
        for s in paired["pairedSamples"]],
    "officialPaired": [
        {k:v for k,v in s.items() if k in ("kind", "state", "error", "elapsedMs", "coveredAoiCount", "sourceRows", "uniqueIds",
                                            "refreshObserved", "matchedServerIds", "cameraTileStable", "responseFlagsTransitioned",
                                            "preTile", "postTile", "preServerLod", "postServerLod")}
        for s in official["pairedSamples"]],
    "officialLastRawSourceCounts": {},
    "networkAttribution": "340 owned application->Lua coverage probes. Raw network send/receive counters include concurrent game traffic; no whole-world game request total is attributable.",
    "candidateConclusion": "No replacement: direct diagnostic timed out without fresh response; official Train-list refresh has no demonstrated 2212 Truck population, despite complete AOI baseline. A second 30-AOI coverage after direct failure proved camera/manager restored.",
    "originalEquivalentOrGlobalFastestClaim":False,
}
last_official = json.loads((isolated / "live-resource" / "train-list-diagnostic-result.json").read_text(encoding="utf-8"))
comparison["officialLastRawSourceCounts"] = {
    "truckSourceCount":last_official.get("truckSourceCount"),
    "railwaySourceCount":last_official.get("railwaySourceCount"),
    "truckServerIds":last_official.get("truckServerIds"),
    "railwayServerIds":last_official.get("railwayServerIds"),
    "refreshObserved":last_official.get("refreshObserved"),
}
frozen = ROOT / "r1-completed-baseline-probe-metrics.json"
if not frozen.exists(): shutil.copy2(isolated / "live-resource" / "map-fullworld-probe-metrics.json", frozen)
out = ROOT / "r1-efficiency-aggregate.json"
if out.exists(): raise RuntimeError("Preserve existing R1 efficiency result")
out.write_text(json.dumps(comparison,indent=2),encoding="utf-8")
print(json.dumps({"receipt":str(out), "baselineMs":comparison["baseline"]["elapsedMs"],
                  "probeAttempts":comparison["baseline"]["attributableLuaProbeAttempts"],
                  "retries":comparison["baseline"]["attributableProbeRetries"],
                  "uniqueTrucks":unique, "directState":samples[1]["state"],
                  "officialRefreshes":2, "retainBaseline":True},indent=2))
