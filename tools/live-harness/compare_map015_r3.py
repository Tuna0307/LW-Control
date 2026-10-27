"""Read-only, exact-owned R3 fast/camera/fast Truck field and drift audit.

Usage: python tools/live-harness/compare_map015_r3.py artifacts/map014-lead-live/native-benchmark-truck-...
Private UUIDs and raw rewards stay inside ignored game artifacts.
"""

from __future__ import annotations

import json
from pathlib import Path
import sqlite3
import sys

REQUIRED = frozenset((
    "uuid", "marchUuid", "ownerName", "quality", "power", "x", "y",
    "trainType", "trainCfgId", "carriageNum", "updatedAt", "source",
    "arriveTs", "currentGoods",
))


def read_round(root: Path, round_index: int, proof: dict) -> dict:
    file = root / f"benchmark-{round_index}.db"
    with sqlite3.connect(file.resolve().as_uri() + "?mode=ro", uri=True) as database:
        run = database.execute(
            "SELECT id,status,selected_types,total_blocks,completed_blocks,failed_blocks FROM scan_runs "
            "ORDER BY created_at DESC LIMIT 1").fetchone()
        if (run is None or run[0] != proof["terminal"]["scanRunId"] or
                run[1:] != ("completed", '["truck"]', 2500, 2500, 0)):
            raise ValueError(f"Round {round_index} has no complete same-run genuine Truck publication")
        rows = database.execute(
            "SELECT uuid,data_json FROM map_records WHERE kind='truck' ORDER BY uuid"
        ).fetchall()
    recorded: dict[str, dict] = {}
    missing_keys = 0
    malformed = 0
    schemas: dict[str, int] = {}
    for uuid, raw in rows:
        if not uuid or uuid in recorded:
            raise ValueError(f"Round {round_index} contains missing/duplicate Truck UUIDs")
        data = json.loads(raw)
        missing_keys += int(not REQUIRED.issubset(data))
        malformed += int(data.get("trainType") != 1 or
                         not isinstance(data.get("ownerName"), str) or
                         not data["ownerName"].strip() or
                         not isinstance(data.get("power"), int) or data["power"] < 0 or
                         not isinstance(data.get("quality"), int) or data["quality"] < 1 or
                         not isinstance(data.get("arriveTs"), int) or data["arriveTs"] <= 0 or
                         not isinstance(data.get("currentGoods"), list))
        signature = ",".join(sorted(data.keys()))
        schemas[signature] = schemas.get(signature, 0) + 1
        recorded[uuid] = data
    return {"count": len(recorded), "uuid_set": set(recorded),
            "missingRequiredFieldRows": missing_keys, "malformedTruckRows": malformed,
            "fieldSchemas": schemas}


def compare(path: Path) -> dict:
    root = path.resolve(strict=True)
    benchmark = json.loads((root / "benchmark.json").read_text(encoding="utf8"))
    if len(benchmark) != 3 or [p.get("method") for p in benchmark] != ["direct", "camera", "direct"]:
        raise ValueError("Requires one exact-owned fast-camera-fast Truck sequence")
    if any(p["terminal"]["phase"] != "completed" for p in benchmark):
        raise ValueError("All 3 rounds must publish complete genuine Truck records")
    if any(p["cameraBefore"]["cameraSnapshot"] != p["cameraAfter"]["cameraSnapshot"]
           for p in benchmark):
        raise ValueError("Independent game camera restoration differed on a round")
    if benchmark[0].get("effectiveMethod") != "direct" or benchmark[2].get("effectiveMethod") != "direct":
        raise ValueError("Fast rounds used camera fallback")
    sessions = {p["terminal"].get("serverId") for p in benchmark}
    if len(sessions) != 1:
        raise ValueError("Qualification rounds used different servers")
    rows = [read_round(root, i, proof) for i, proof in enumerate(benchmark)]
    f1, camera, f2 = (r["uuid_set"] for r in rows)
    drift = len(f1 ^ f2)
    observed = [len(f1 ^ camera), len(f2 ^ camera)]
    # Expose both directions. A native sweep may legitimately see moving
    # Trucks that the slower camera missed; the reviewer decides whether
    # symmetric or camera-only coverage parity is the appropriate criterion.
    camera_only = [len(camera - f1), len(camera - f2)]
    fast_only = [len(f1 - camera), len(f2 - camera)]
    field_schema_reference = set(rows[1]["fieldSchemas"])
    fields_match = all(r["missingRequiredFieldRows"] == 0 and r["malformedTruckRows"] == 0
                       for r in rows) and all(r["fieldSchemas"] for r in rows) and all(
        set(r["fieldSchemas"]).issubset(field_schema_reference) for r in (rows[0], rows[2]))
    return {"methodOrder": ["fast", "camera", "fast"],
            "counts": [r["count"] for r in rows],
            "fastVsFastSymmetricDrift": drift,
            "fastVsCameraSymmetricDifferences": observed,
            "cameraOnlyMissesInFast": camera_only,
            "fastOnlyMissesInCamera": fast_only,
            "fieldSchemas": [len(r["fieldSchemas"]) for r in rows],
            "missingRequiredFieldRows": [r["missingRequiredFieldRows"] for r in rows],
            "malformedTruckRows": [r["malformedTruckRows"] for r in rows],
            "fieldsMatchCameraSchema": fields_match,
            "strictSymmetricDriftPass": fields_match and all(diff <= drift for diff in observed),
            "cameraOnlyDriftPass": fields_match and all(diff <= drift for diff in camera_only)}


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    result = compare(Path(sys.argv[1]))
    print(json.dumps(result, indent=2))
