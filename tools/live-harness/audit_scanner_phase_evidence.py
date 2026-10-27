"""Read-only native scanner timing and rejection audit over preserved receipts.

Only successful source-matched, populated and camera-restored controls enter
the phase statistics. Failed/empty/LOD2 candidates are reported separately.
These measurements are *not* new paired end-to-end benchmarks.
"""
from __future__ import annotations

import json
from pathlib import Path
import statistics

ROOT = Path(__file__).resolve().parents[2]
POSITIVE = (
    "artifacts/map-013-live/native-causal-20261013-194843/native-first-region.json",
    "artifacts/map-013-live/native-causal-20261013-194843/sequence-baseline-2.json",
    "artifacts/map-013-live/native-causal-20261013-194843/sequence-baseline-3.json",
    "artifacts/map-013-live/native-causal-20261013-210353/zoom-pair-01-coverage.json",
    "artifacts/map-013-live/native-causal-20261013-223148/native-first-region.json",
    "artifacts/map-013-live/native-causal-20261013-230739/native-first-region.json",
)
NEGATIVE = (
    "artifacts/map-013-live/native-causal-20261013-194843/sequence-region-2.json",
    "artifacts/map-013-live/native-causal-20261013-210353/zoom-pair-02-zoom.json",
)


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def readout(s):
    return {k: s.get(k) for k in (
        "targetResponseElapsedSeconds", "homeRestorationElapsedSeconds",
        "totalProbeElapsedSeconds", "map013NativeReplyState", "serverLod",
        "blockSize", "blockCount", "nativeCurrentSetCount",
        "zoomFinalServerLod", "zoomFinalBlockSize", "zoomFinalBlockCount",
        "state", "error", "cameraGeometryRestored", "cameraTileStable",
    )}


def qualify_region(doc, *, expected_server=2212, expected_world=0):
    """Report exactly why a native reply is not an accuracy-qualified control."""
    problems = []
    if doc.get("state") != "proven": problems.append("not_proven")
    if doc.get("map013NativeReplyState") != "matched": problems.append("reply_not_source_matched")
    if doc.get("cameraGeometryRestored") is not True: problems.append("camera_not_restored")
    if doc.get("cameraTileStable") is not True: problems.append("tile_not_restored")
    if (doc.get("serverLod"), doc.get("blockSize"), doc.get("blockCount")) != (0, 10, 100):
        problems.append("wrong_native_lod_or_geometry")
    indices = doc.get("requestedIndices")
    if not isinstance(indices, list) or not indices or any(
            not isinstance(i, int) or i < 0 or i >= 10000 for i in indices):
        problems.append("invalid_native_footprint")
        valid_indices = set()
    else:
        valid_indices = set(indices)
        if len(valid_indices) != len(indices): problems.append("duplicate_requested_aois")
    rows = doc.get("point_records")
    if not isinstance(rows, list) or not rows:
        problems.append("no_positive_rows")
        rows = []
    keys = set()
    for row in rows:
        if not isinstance(row, dict):
            problems.append("invalid_source_row")
            continue
        identity = row.get("pointId") or row.get("id") or row.get("uuid")
        key = (row.get("kind"), str(identity))
        if not identity or not row.get("kind"):
            problems.append("missing_source_identity")
        elif key in keys:
            problems.append("duplicate_source_ids")
        keys.add(key)
        if row.get("serverId") != expected_server or row.get("worldId") != expected_world:
            problems.append("wrong_server_or_world_row")
        x, y = row.get("x"), row.get("y")
        if (not isinstance(x, (int, float)) or not isinstance(y, (int, float)) or
                not (0 <= x < 1000 and 0 <= y < 1000)):
            problems.append("invalid_source_coordinates")
        elif int(y) // 10 * 100 + int(x) // 10 not in valid_indices:
            problems.append("row_outside_qualified_footprint")
    for key in ("targetResponseElapsedSeconds", "homeRestorationElapsedSeconds", "totalProbeElapsedSeconds"):
        if not isinstance(doc.get(key), (int, float)) or doc[key] <= 0:
            problems.append("missing_" + key)
    return sorted(set(problems))


def summarize(values):
    return {
        "n": len(values),
        "medianSeconds": round(statistics.median(values), 4) if values else None,
        "minimumSeconds": round(min(values), 4) if values else None,
        "maximumSeconds": round(max(values), 4) if values else None,
    }


def audit():
    accepted, rejected = [], []
    for path in POSITIVE + NEGATIVE:
        doc = load(path)
        rows = doc.get("point_records") or []
        problems = qualify_region(doc)
        row = {
            "receipt": path.rsplit("/", 1)[-1],
            "sourceMatched": doc.get("map013NativeReplyState"),
            "rows": len(rows),
            "selectedAois": len(doc.get("requestedIndices") or []),
            **readout(doc),
        }
        if problems:
            rejected.append({**row, "reasons": problems})
        else:
            accepted.append(row)
    target = [v["targetResponseElapsedSeconds"] for v in accepted]
    home = [v["homeRestorationElapsedSeconds"] for v in accepted]
    totals = [v["totalProbeElapsedSeconds"] for v in accepted]
    print(json.dumps({
        "classification": "bounded regional native phase samples; no accepted faster method",
        "positiveControls": len(accepted),
        "targetResponse": summarize(target),
        "homeRestoration": summarize(home),
        "totalProbe": summarize(totals),
        "restorationShareOfMeasuredProbeTime": round(sum(home) / sum(totals), 4) if totals else None,
        "controls": accepted,
        "negativeCandidates": rejected,
        "benchmarkLimit": "not three alternating source-comparable end-to-end scanner pairs",
    }, indent=2))


if __name__ == "__main__":
    audit()
