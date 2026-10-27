"""MAP-019 offline request-plan prototype. It cannot send a game request.

Run: python tools/research/map_019_review/plan_requests.py --out artifacts/map019/request-plan
Numbers describe source-derived plans, never native performance or completeness.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import asdict, dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


@dataclass(frozen=True)
class Request:
    lane: str
    ordinal: int
    indices: tuple[int, ...]
    x: int
    y: int
    left_bottom: int
    right_top: int


def linear_points(size: int = 160) -> list[Request]:
    if not 1 <= size <= 160:
        raise ValueError("Plan refuses the already-rejected >160-area request family")
    result = []
    for first in range(0, 10000, size):
        last = min(9999, first + size - 1)
        row_first, row_last = first // 100, last // 100
        result.append(Request("point", len(result), tuple(range(first, last + 1)), 500,
                              (row_first + row_last + 1) * 5,
                              row_first * 10000 + 1,
                              1000 + min(999, (row_last + 1) * 10) * 1000))
    return result


def rectangles(rows: int, cols: int) -> list[Request]:
    """Fixture-compatible LOD0 AOI rectangles, kept at the native area cap."""
    if rows < 1 or cols < 1 or rows * cols > 160:
        raise ValueError("Rectangle must have 1..160 AOIs")
    result = []
    for r0 in range(0, 100, rows):
        for c0 in range(0, 100, cols):
            r1, c1 = min(99, r0 + rows - 1), min(99, c0 + cols - 1)
            indices = tuple(r * 100 + c for r in range(r0, r1 + 1) for c in range(c0, c1 + 1))
            result.append(Request("point", len(result), indices,
                                  (c0 + c1 + 1) * 5, (r0 + r1 + 1) * 5,
                                  c0 * 10 + r0 * 10000 + 1,
                                  min(999, (c1 + 1) * 10) + min(999, (r1 + 1) * 10) * 1000 + 1))
    return result


def truck_rectangles(combine_points: bool = False) -> list[Request]:
    result = []
    for n in range(625):
        cx, cy = n % 25, n // 25
        x0, y0 = cx * 40, cy * 40
        # Production uses the central one AOI. The opt-in PLAN keeps exactly
        # those request rectangles, replacing its list by the 4x4 AOIs inside.
        indices = (tuple(r * 100 + c for r in range(cy * 4, cy * 4 + 4)
                         for c in range(cx * 4, cx * 4 + 4)) if combine_points else
                   ((cy * 4 + 2) * 100 + cx * 4 + 2,))
        result.append(Request("hybrid_plan" if combine_points else "truck", n, indices,
                              x0 + 20, y0 + 20, x0 + y0 * 1000 + 1,
                              min(999, x0 + 40) + min(999, y0 + 40) * 1000 + 1))
    return result


def stats(requests: list[Request]) -> dict:
    flattened = [i for request in requests for i in request.indices]
    covered = set(flattened)
    return {"requests": len(requests), "indexEntries": len(flattened),
            "uniqueAois": len(covered), "duplicateAoiEntries": len(flattened) - len(covered),
            "missingAois": 10000 - len(covered), "maxAreasPerRequest": max(map(lambda r: len(r.indices), requests)),
            "fullWorldIndexCoverage": covered == set(range(10000))}


def source_receipt(path: str, needles: tuple[str, ...]) -> dict:
    data = (ROOT / path).read_bytes()
    lines = data.decode("utf-8-sig").splitlines()
    anchors = []
    for needle in needles:
        found = [i + 1 for i, line in enumerate(lines) if needle in line]
        if not found:
            raise ValueError(f"Production source drift: {path}: missing {needle}")
        anchors.append({"text": needle, "lines": found})
    return {"path": path, "sha256": hashlib.sha256(data).hexdigest(), "anchors": anchors}


def build_report() -> tuple[dict, dict[str, list[Request]]]:
    plans = {"current_points_160": linear_points(), "fewer_areas_80": linear_points(80),
             "fixture_rectangle_10x16": rectangles(10, 16),
             "rectangle_8x20": rectangles(8, 20), "current_truck_625": truck_rectangles(),
             "opt_in_combined_4x4": truck_rectangles(True)}
    current = stats(plans["current_points_160"] + plans["current_truck_625"])
    hybrid = stats(plans["opt_in_combined_4x4"])
    report = {
        "schemaVersion": 1, "evidenceClass": "OFFLINE_SOURCE_DERIVED_PLAN_ONLY",
        "nativeTrial": False, "canSendRequests": False,
        "sources": [source_receipt("src/LWBridge.GameScripts/current_live_resource_probe.lua", (
            "math.min(9999, s.nextAoi + 159)", "local cx, cy = n % 25, math.floor(n / 25)",
            "CS.System.Array.CreateInstance(typeof(CS.System.Int32), 1)",
            "local requested_rate = tonumber(values.rate)",
            "local requested_fence = tonumber(values.fenceSize)",
            "The march push contains NO echoed rectangle coordinates/request ID.")),
            source_receipt("src/LWBridge.Desktop/CurrentClientMapBlockSource.FastCity.cs", (
                "CaptureFastTruckRowsAsync",)),
            source_receipt("tools/live-harness/map014_fullscan_fixture.lua", (
                "local rows, cols = tonumber(req.rows or 10), tonumber(req.cols or 16)",))],
        "plans": {name: stats(requests) for name, requests in plans.items()},
        "currentCombined": current,
        "requestCountScope": "Initial world.get.block acquisition requests for a successful mixed Truck+point sweep; excludes repairs, retry/fallback scans, startup and per-kind detail/list/enrichment requests.",
        "combinedHypothesis": {
            "requestReduction": current["requests"] - hybrid["requests"],
            "requestReductionPercent": 100 * (current["requests"] - hybrid["requests"]) / current["requests"],
            "indexEntryReduction": current["indexEntries"] - hybrid["indexEntries"],
            "indexEntryReductionPercent": 100 * (current["indexEntries"] - hybrid["indexEntries"]) / current["indexEntries"],
            "pointCaptureBatchesBefore": 63, "pointCaptureBatchesAfter": 625,
            "pointCaptureBatchMultiplier": 625 / 63,
            "minimumSendSpacingSecondsAtExisting20PerSecond": 624 / 20,
            "nativeSpeedGainSeconds": None, "nativeAccuracyQualified": False,
            "bytesOnWire": None, "fpsOrMemoryImpact": None,
            "changedFailureReason": "Capture each actual native point reply rather than polling one overwritten cache/ready flag; this hook and attribution do not yet exist in this prototype.",
            "constraints": ["Same 625 Truck rectangles and centers; same existing maximum 20 requests/s",
                            "16 indices per combined request; below 160 area cap",
                            "No server/LOD/world change; LOD0 current-server plan only",
                            "No live implementation; no default scanner integration"],
            "qualificationGates": [
                "Observe actual point reply bodies before client deletion; preserve source server/world/LOD/indices and per-reply time",
                "Prove every request's AOIs/fields captured once without guessing FIFO attribution; no flag-only acknowledgements",
                "Retain Truck's bounded fences and serialized repair; march push lacks a rectangle/request echo",
                "Same-session two rounds vs current plus camera with identity/field loss no worse than natural drift",
                "Measure capture CPU/frame pressure: 625 small point captures may outweigh 63 fewer requests",
                "Measure actual protocol bytes and server rate; AOI entry counts are not encoded byte counts"],
        },
        "interpretation": [
            "63 requests is the arithmetic minimum to enumerate 10000 AOIs with <=160 indices/request; this does not prove minimum time or complete native discovery.",
            "Smaller 80-area requests use 125 requests; 8x20 compact rectangles use 65 and 10x16 use 70. Geometry is not an accuracy measurement.",
            "The current Truck one-index plan intentionally covers only 625 AOI indices; it discovers Trucks by request rectangle, not full point coverage.",
            "These plans do not describe every kind-specific shortcut (for example the Zombie-Boss-only coarse route) or count enrichment requests.",
            "Combined 4x4 is a promising mixed-scan hypothesis only. It increases AOIs per Truck request from 1 to16, despite reducing total combined indices; report request-shape risk before a live trial.",
            "The 31.2s spacing floor excludes RTT, response fences, repairs, capture, hydration, serialization and publication; it is not a scan-time prediction.",
        ]}
    return report, plans


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    report, plans = build_report()
    args.out.mkdir(parents=True, exist_ok=True)
    (args.out / "summary.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    for name, requests in plans.items():
        (args.out / f"{name}.json").write_text(json.dumps([asdict(r) for r in requests], indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(args.out), "evidenceClass": report["evidenceClass"],
                      "plans": report["plans"], "currentCombined": report["currentCombined"]}, indent=2))


if __name__ == "__main__":
    main()
