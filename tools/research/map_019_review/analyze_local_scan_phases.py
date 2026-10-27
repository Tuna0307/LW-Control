"""Summarize existing sanitized Map ABBA performance/coverage evidence.

The producer's native AOI timer deliberately excludes the earlier mobile
rectangle sweep. This diagnostic reconciles those *different* phase clocks
without treating local persistence gains as acquisition or whole-scan gains.
It never reads account rows, native game dumps, tokens, or SQLite databases.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from statistics import mean
from typing import Any


def _seconds(value: Any, label: str) -> float:
    if not isinstance(value, (int, float)) or isinstance(value, bool) or value < 0:
        raise ValueError(f"invalid {label}")
    return round(float(value), 3)


def summarize(audit: dict[str, Any]) -> dict[str, Any]:
    rounds = audit.get("rounds")
    if not isinstance(rounds, list) or not rounds:
        raise ValueError("missing benchmark rounds")
    details: list[dict[str, Any]] = []
    for ordinal, item in enumerate(rounds):
        if not isinstance(item, dict):
            raise ValueError(f"round {ordinal} is not an object")
        method = item.get("effectiveMethod")
        if method not in ("direct", "camera"):
            raise ValueError(f"round {ordinal} has unknown effective method")
        if item.get("completed") is not True or item.get("terminalPhase") != "completed":
            raise ValueError(f"round {ordinal} did not complete")
        if item.get("validationErrors") not in (None, []):
            raise ValueError(f"round {ordinal} has unresolved validation errors")
        if "selectedTypes" in item and len(item["selectedTypes"]) != 10:
            raise ValueError(f"round {ordinal} is not all-ten")
        wall = _seconds(item.get("seconds"), f"round {ordinal} wall time")
        stage = item.get("stageMetrics") or {}
        process = item.get("process") or {}
        if not item.get("stageMetricsCorrelated") or not isinstance(stage, dict):
            raise ValueError(f"round {ordinal} has no correlated stage evidence")
        capture = _seconds(stage.get("sourceCaptureMilliseconds"), "source capture ms") / 1000
        checkpoint = _seconds(stage.get("checkpointMilliseconds"), "checkpoint ms") / 1000
        publication = _seconds(stage.get("publicationMilliseconds"), "publication ms") / 1000
        if capture > wall + 2 or checkpoint > wall or publication > wall:
            raise ValueError(f"round {ordinal} has impossible stage durations")
        summary: dict[str, Any] = {
            "round": item.get("round", ordinal),
            "method": method,
            "wallSeconds": wall,
            "sourceCaptureSeconds": round(capture, 3),
            "checkpointSeconds": round(checkpoint, 3),
            "publicationSeconds": round(publication, 3),
            "processCpuSeconds": _seconds(process.get("processCpuSeconds"), "process CPU seconds")
                if process.get("processCpuSeconds") is not None else None,
            "sampledPeakPrivateMiB": round(_seconds(process["sampledPeakPrivateBytes"], "private bytes") / 1048576, 1)
                if process.get("sampledPeakPrivateBytes") is not None else None,
            "sampledPeakWorkingSetMiB": round(_seconds(process["sampledPeakWorkingSetBytes"], "working set bytes") / 1048576, 1)
                if process.get("sampledPeakWorkingSetBytes") is not None else None,
            "rowCounts": {kind: int(number) for kind, number in (item.get("counts") or {}).items()
                          if kind in ("city", "resource", "monster", "zombie_boss", "truck", "railway", "dispatch", "ghost", "treasure", "other")
                          and isinstance(number, int) and number >= 0},
        }
        if method == "direct":
            metrics = item.get("metrics") or {}
            mobile = item.get("mobile-evidence") or {}
            direct = item.get("direct-evidence") or {}
            if not item.get("metricsCorrelated") or not isinstance(metrics, dict) or \
                    mobile.get("sameRunCorrelated") is not True or direct.get("sameRunCorrelated") is not True:
                raise ValueError(f"direct round {ordinal} has uncorrelated native evidence")
            if metrics.get("uniqueCoveredAois") != metrics.get("requiredAois") or \
                    metrics.get("requiredAois") != 10000 or mobile.get("rectanglesCompleted") != 625 or \
                    mobile.get("state") != "proven" or direct.get("state") != "proven":
                raise ValueError(f"direct round {ordinal} lacks complete native coverage")
            mobile_seconds = _seconds(mobile.get("elapsedSeconds"), "mobile seconds")
            aoi_seconds = _seconds(metrics.get("acquisitionMilliseconds"), "AOI ms") / 1000
            hydrate_seconds = sum(_seconds(metrics.get(key, 0), key) for key in (
                "truckHydrationMilliseconds", "resourceHydrationMilliseconds", "monsterHydrationMilliseconds"
            )) / 1000
            projection_seconds = _seconds(metrics.get("projectionMilliseconds", 0), "projection ms") / 1000
            # All values are durations from existing serial producers, but they
            # have distinct start/end points, so call the difference a residual.
            residual = capture - mobile_seconds - aoi_seconds - hydrate_seconds - projection_seconds
            if residual < -2:
                raise ValueError(f"direct round {ordinal} has irreconcilable phase clocks")
            summary.update({
                "mobileSweepSeconds": mobile_seconds,
                "nativeAoiSeconds": round(aoi_seconds, 3),
                "detailHydrationSeconds": round(hydrate_seconds, 3),
                "projectionSeconds": round(projection_seconds, 3),
                "remainingCaptureSecondsApprox": round(residual, 3),
                "mobileRequestsSent": mobile.get("requestsSent"),
                "mobileInitialRequests": mobile.get("initialRequestsSent"),
                "mobileRepairRequests": mobile.get("repairRequestsSent"),
                "mobileRectanglesVerified": mobile.get("rectanglesCompleted"),
                "nativeAoiRequests": metrics.get("attributableLuaProbeAttempts"),
                "nativeAoisVerified": metrics.get("uniqueCoveredAois"),
                "detailNativeRequestsReported": metrics.get("resourceDetailNativeRequests"),
            })
        details.append(summary)

    by_method: dict[str, dict[str, Any]] = {}
    for method in ("direct", "camera"):
        subset = [row for row in details if row["method"] == method]
        if subset:
            by_method[method] = {
                "rounds": len(subset),
                "meanWallSeconds": round(mean(row["wallSeconds"] for row in subset), 3),
                "meanSourceCaptureSeconds": round(mean(row["sourceCaptureSeconds"] for row in subset), 3),
            }
    if "direct" in by_method and "camera" in by_method:
        by_method["direct"]["wallReductionVsCameraPercent"] = round(
            100 * (1 - by_method["direct"]["meanWallSeconds"] / by_method["camera"]["meanWallSeconds"]), 2)
    result = {
        "schemaVersion": 1,
        "source": "sanitized existing same-session MAP-019 round records",
        "conclusions": {
            "mobileAndAoiHaveSeparateClocks": True,
            "phaseResidualIsApproximate": True,
            "cpuIsProcessTimeNotPercent": True,
            "memoryIsSampledPeakNotLeakProof": True,
            "repeatedLiveBeforeAfterOfNewOptimization": False,
            "thisDiagnosticExecutesGame": False,
        },
        "byMethod": by_method,
        "rounds": details,
    }
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    payload = summarize(json.loads(args.audit.read_text(encoding="utf-8")))
    content = json.dumps(payload, indent=2, ensure_ascii=False) + "\n"
    if args.output is None:
        print(content, end="")
    else:
        args.output.write_text(content, encoding="utf-8")


if __name__ == "__main__":
    main()
