"""Compare exact-owned R2 Fast Truck trial UUIDs with same-game camera rounds.

Usage: python tools/live-harness/compare_map015_truck.py artifacts/map014-lead-live/native-direct-full-...
Only aggregate counts are printed; native UUIDs remain in ignored receipts.
"""

from __future__ import annotations

import json
from pathlib import Path
import re
import sqlite3
import sys


def camera_uuids(path: Path) -> set[str]:
    if not path.is_file():
        raise FileNotFoundError(path)
    with sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True) as db:
        run = db.execute(
            "SELECT selected_types,status,completed_blocks,failed_blocks FROM scan_runs ORDER BY created_at DESC LIMIT 1"
        ).fetchone()
        if run != ('["truck"]', "completed", 2500, 0):
            raise ValueError(f"Camera Truck run was not fully published: {path.name} {run}")
        uuids = db.execute(
            "SELECT uuid FROM map_records WHERE kind='truck' AND uuid IS NOT NULL AND uuid<>''"
        ).fetchall()
    return {item[0] for item in uuids}


def compare(root: Path) -> dict:
    trials = json.loads((root / "direct-full.json").read_text(encoding="utf-8"))
    cameras_path = root / "direct-full-cameras.json"
    camera_rounds = json.loads(cameras_path.read_text(encoding="utf-8")) if cameras_path.exists() else []
    if not isinstance(trials, list) or not isinstance(camera_rounds, list):
        raise ValueError("Native trial receipts have incorrect shapes")
    results = []
    camera_sets = []
    for i, cam in enumerate(camera_rounds):
        if cam.get("round") != i or cam.get("terminal", {}).get("phase") != "completed":
            raise ValueError(f"Camera round {i} does not have exact completed proof")
        camera_sets.append(camera_uuids(root / f"truck-paired-{i}.db"))
    for i, trial in enumerate(trials):
        native = trial.get("result")
        if not isinstance(native, dict) or native.get("chunks") != 625 or not native.get("doneAt") or native.get("error"):
            raise ValueError(f"Fast round {i} lacks completed native sweep and cleanup")
        raw = native.get("trains", [])
        all_trains = {str(item["u"]) for item in raw}
        if len(all_trains) != native.get("trainSeen") or len(all_trains) > 400:
            raise ValueError(f"Fast round {i} had missing/duplicate/capped train UUID evidence")
        if all(item.get("k") is not None for item in raw):
            unknown_types = [str(item.get("k")) for item in raw
                             if re.search(r"(?<!\d)[12]$", str(item["k"])) is None]
            fast = {str(item["u"]) for item in raw
                    if re.search(r"(?<!\d)1$", str(item["k"])) is not None}
            typed = not unknown_types
        else:
            fast = all_trains
            typed = False
        row = {
            "round": i,
            "ratePerSecond": round(1 / float(trial["interval"]), 2),
            "nativeSeconds": round(float(trial["wallSeconds"]), 3),
            "marchPushes": native["marchPushes"],
            "pushComplete": trial.get("completePushObservation", native["marchPushes"] >= 625),
            "allNativeTrainUuids": len(all_trains),
            "typedTruckUuids": len(fast) if typed else None,
            "truckTypeVerified": typed,
            "sampledFrames": native.get("f", native.get("marchFrames")),
            "logMarchCount": native.get("lgCount"),
        }
        if i < len(camera_sets):
            camera = camera_sets[i]
            row.update(cameraTruckUuids=len(camera),
                       fastNotInCamera=len(fast - camera),
                       cameraNotInFast=len(camera - fast),
                       overlap=len(camera & fast),
                       cameraSeconds=round(float(camera_rounds[i]["seconds"]), 3))
        results.append(row)
    camera_drifts = []
    for i in range(len(camera_sets)-1):
        a, b = camera_sets[i], camera_sets[i+1]
        camera_drifts.append(dict(cameraRounds=f"{i}-{i+1}",
                                  olderNotInNewer=len(a-b), newerNotInOlder=len(b-a),
                                  retained=len(a&b), olderCount=len(a), newerCount=len(b)))
    return {"source": str(root), "fastRounds": results, "cameraVsCamera": camera_drifts,
            "classificationNote": "Native fixture trains include any WorldMarch.train; camera database is Truck-only."}


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    result = compare(Path(sys.argv[1]).resolve(strict=True))
    print(json.dumps(result, indent=2))
