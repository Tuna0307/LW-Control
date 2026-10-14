"""Source-conservative, read-only six-round Resource population and field audit.

The benchmark SQLite snapshots contain *capture* timestamps, not server spawn,
expiry or occupation event timestamps. Disappearing occupied points are source-
supported occupation-associated departures, not proven causal despawns. Never
use this audit to enable a production scanner mode automatically.
"""

import argparse
from collections import Counter
from datetime import datetime
import json
from pathlib import Path
import sqlite3
import statistics

SOURCE = "CurrentClientMapBlockSource.ProbeFastCityBatchAsync"
IMMUTABLE_FIELDS = (
    "pointId", "pointType", "resourceTypeId", "resourceConfigId",
    "resourceMaxAmount", "resourceNameKey", "resourceSourceType",
    "runtimeClass", "sourceKind", "source", "level", "x", "y",
    "worldId", "serverId", "srcServerId",
)
CAMERA_IDENTITY_FIELDS = ("gamePid", "profileId", "launchSessionId", "challenge")


def _capture_milliseconds(value, label):
    require(isinstance(value, str) and value.endswith("Z"),
            f"{label}: missing independent UTC camera capture")
    try:
        capture = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as exc:
        raise ValueError(f"{label}: malformed independent UTC camera capture") from exc
    return round(capture.timestamp() * 1000)


def _stable_identity(a, b):
    """A spatial point ID alone does not distinguish a destroyed/reused site."""
    return all(a["data"].get(field) == b["data"].get(field)
               for field in IMMUTABLE_FIELDS)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def _run(root, index, receipt):
    require(receipt.get("round") == index and
            type(receipt.get("boundedLens")) is bool and
            receipt["boundedLens"] == (index % 2 == 1),
            f"round {index}: benchmark lens/order mismatch")
    require(type(receipt.get("seconds")) in (int, float) and
            receipt["seconds"] > 0, f"round {index}: invalid duration")
    status = receipt["terminal"]
    require(status.get("phase") == "completed" and
            status.get("isReading") is False and
            status.get("failedBlocks") == 0 and
            status.get("unreadBlocks") == 0 and
            status.get("completedBlocks") == 2500 and
            status.get("totalBlocks") == 2500 and
            status.get("nativeDroppedRecords", 0) == 0 and
            status.get("selectedTypes") == ["resource"] and
            type(status.get("serverId")) is int and status["serverId"] > 0,
            f"round {index}: incomplete/foreign Resource scan")
    require(receipt["cameraBefore"]["cameraSnapshot"] ==
            receipt["cameraAfter"]["cameraSnapshot"],
            f"round {index}: camera not restored")
    before_camera, after_camera = receipt["cameraBefore"], receipt["cameraAfter"]
    before_ms = _capture_milliseconds(before_camera.get("capturedAt"), f"round {index} before")
    after_ms = _capture_milliseconds(after_camera.get("capturedAt"), f"round {index} after")
    require(before_ms < after_ms and
            all(before_camera.get(field) is not None and
                before_camera.get(field) == after_camera.get(field)
                for field in CAMERA_IDENTITY_FIELDS) and
            before_camera["cameraSnapshot"].get("serverId") == status["serverId"] and
            after_camera["cameraSnapshot"].get("serverId") == status["serverId"] and
            type(status.get("startedAt")) is int and
            before_ms - 1000 <= status["startedAt"] * 1000 <= after_ms + 1000,
            f"round {index}: wrong owner/server or inconsistent camera wall-clock interval")

    metrics = json.loads((root / f"benchmark-{index}-probe-metrics.json").read_text(encoding="utf8"))
    identity = metrics.get("identity", {})
    require(metrics.get("phase") == "prepared" and
            metrics.get("source") == SOURCE and
            metrics.get("uniqueCoveredAois") == 10000 and
            metrics.get("requiredAois") == 10000 and
            identity.get("RunId") == status.get("scanRunId") and
            identity.get("ServerId") == status["serverId"] and
            identity.get("WorldId") == 0 and
            identity.get("ScanMode") == "normal" and
            identity.get("SelectedTypesKey") == "resource" and
            metrics.get("actualPeakConcurrentCameraProbes") == 1 and
            type(metrics.get("validatedProbeResponses")) is int and
            metrics["validatedProbeResponses"] > 0 and
            metrics.get("attributableLuaProbeAttempts", 0) >= metrics["validatedProbeResponses"],
            f"round {index}: missing/stale or incomplete native AOI evidence")

    path = (root / f"benchmark-{index}.db").resolve()
    # immutable=1 is mandatory: never create -shm or recover/change a live receipt.
    require(path.is_file() and
            not any(Path(str(path) + suffix).exists() and Path(str(path) + suffix).stat().st_size
                    for suffix in ("-wal",)),
            f"round {index}: missing SQLite snapshot or non-checkpointed WAL")
    conn = sqlite3.connect(path.as_uri() + "?mode=ro&immutable=1", uri=True)
    rows = {}
    source_counts = Counter()
    occupied = 0
    detail_known = 0
    bearing_aoi = set()
    try:
        for kind, server, key, point, seen_at, encoded in conn.execute(
                "SELECT kind,server_id,record_key,point_index,updated_at,data_json FROM map_records"):
            require(kind == "resource" and server == status["serverId"],
                    f"round {index}: foreign kind/server in Resource snapshot")
            data = json.loads(encoded)
            require(type(data) is dict and type(key) is str and key.isdecimal() and
                    type(point) is int and int(key) == point and
                    type(seen_at) is int and seen_at > 0 and
                    data.get("serverId") == server and data.get("srcServerId") == server and
                    data.get("worldId") == 0 and
                    data.get("kind") == "resource" and
                    data.get("sourceKind") == "resource_point" and
                    data.get("source") ==
                    "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange" and
                    data.get("resourceSourceType") == "ResPointInfo" and
                    data.get("runtimeClass") == "ResPointInfo" and
                    data.get("pointType") == 7 and
                    data.get("pointId") == point and
                    data.get("pointIndex") == point and
                    data.get("recordKey") == key and
                    data.get("updatedAt") == seen_at,
                    f"round {index}: Resource identity/source/timestamp contradiction at {key}")
            require(before_ms <= seen_at < after_ms + 1000,
                    f"round {index}: stale or foreign Resource capture outside independent camera window at {key}")
            x, y = data.get("x"), data.get("y")
            require(type(x) is int and type(y) is int and
                    0 <= x <= 999 and 0 <= y <= 999 and
                    point == y * 1000 + x + 1,
                    f"round {index}: invalid Resource geometry at {key}")
            for field in ("resourceTypeId", "resourceConfigId", "level",
                          "resourceMaxAmount"):
                require(type(data.get(field)) is int and data[field] > 0,
                        f"round {index}: invalid {field} at {key}")
            require(data.get("blackTileKnown") is True and
                    type(data.get("isBlackTile")) is bool and
                    data.get("gatherOccupancyKnown") is True and
                    data.get("rebuildGatherOccupancyKnown") is True and
                    type(data.get("gatherOccupied")) is bool and
                    data["gatherOccupied"] is data.get("rebuildGatherOccupied"),
                    f"round {index}: Resource tile/occupation provenance lost at {key}")
            if data["gatherOccupied"]:
                occupied += 1
                require(data.get("resourceDetailKnown") is not True,
                        f"round {index}: occupied Resource incorrectly asserted fresh detail at {key}")
            else:
                detail_known += 1
                full, remaining = data.get("resourceFullAmount"), data.get("resourceRemainingAmount")
                require(data.get("resourceDetailKnown") is True and
                        type(full) is int and type(remaining) is int and
                        full > 0 and 0 <= remaining <= full and
                        data.get("resourceCapacityMatchesConfig") is True and
                        data.get("resourceFull") is (remaining == full),
                        f"round {index}: unoccupied Resource missing real hydrated detail at {key}")
            require(key not in rows, f"round {index}: duplicated Resource point {key}")
            rows[key] = {"data": data, "captureMs": seen_at, "aoi": (y // 10, x // 10)}
            bearing_aoi.add((y // 10, x // 10))
            source_counts[data["resourceTypeId"]] += 1
    finally:
        conn.close()

    require(metrics.get("uniqueRecordKeys", {}).get("resource") == len(rows) and
            receipt.get("summary", {}).get("counts", {}).get("resource") == len(rows) and
            metrics.get("resourceDetailNativeReady") == detail_known and
            type(metrics.get("resourceDetailNativeRequests")) is int and
            0 <= metrics["resourceDetailNativeRequests"] <= detail_known,
            f"round {index}: DB/metrics/hydrated Resource count mismatch")
    return rows, {
        "round": index, "lens": "FOV20-opt-in" if index % 2 else "FOV120-baseline",
        "serverId": status["serverId"], "scanRunId": status["scanRunId"],
        "seconds": round(receipt["seconds"], 3), "resourceRows": len(rows),
        "sourceResourceTypes": dict(sorted(source_counts.items())),
        "occupied": occupied, "unoccupiedHydrated": detail_known,
        "resourceBearingAois": len(bearing_aoi),
        "nativeCoveredPointAois": metrics["uniqueCoveredAois"],
        "probeAttempts": metrics["attributableLuaProbeAttempts"],
        "validatedProbeResponses": metrics["validatedProbeResponses"],
        "retries": metrics["attributableRetries"],
        "resourceDetailNativeRequests": metrics["resourceDetailNativeRequests"],
        "resourceDetailNativeReady": metrics["resourceDetailNativeReady"],
        "acquisitionMilliseconds": metrics["acquisitionMilliseconds"],
        "resourceHydrationMilliseconds": metrics["resourceHydrationMilliseconds"],
        "captureMinMs": min(row["captureMs"] for row in rows.values()) if rows else None,
        "captureMaxMs": max(row["captureMs"] for row in rows.values()) if rows else None,
        "independentCameraWallClockWindowMs": [before_ms, after_ms],
        "cameraOwnerIdentity": {
            field: before_camera[field] for field in
            ("gamePid", "profileId", "launchSessionId")
        },
        "metricsAcquisitionPolicy": metrics.get("acquisitionPolicy"),
    }


def _aoi_cohorts(rows):
    by_aoi = {}
    for point_id, record in rows.items():
        by_aoi.setdefault(record["aoi"], {})[point_id] = record
    return by_aoi


def _difference(key, all_rows, all_cohorts, runs, src_idx, dest_idx):
    earlier, later = all_rows[src_idx], all_rows[dest_idx]
    record = earlier[key]
    data = record["data"]
    aoi = record["aoi"]
    opposite_members = all_cohorts[dest_idx].get(aoi, {})
    neighbors = len(opposite_members)
    # A point-index is a coordinate, not a native resource lifetime/creation ID.
    # Same AOI/source cohorts at different wall times prove samples were taken
    # there, not that an absent member was still alive at a later capture.
    src_members = all_cohorts[src_idx].get(aoi, {})
    stable_src_opposite = sorted(
        (member for member in src_members.keys() & opposite_members.keys()
         if member != key and _stable_identity(src_members[member], opposite_members[member])),
        key=int,
    )
    baseline_idx, candidate_idx = min(src_idx, dest_idx), max(src_idx, dest_idx)
    next_baseline = candidate_idx + 1
    if next_baseline >= len(all_rows):
        next_baseline = None
    following = all_cohorts[next_baseline].get(aoi, {}) if next_baseline is not None else {}
    stable_three_round = [
        member for member in stable_src_opposite
        if member in following and
        _stable_identity(src_members[member], following[member]) and
        # Independent capture clocks, rather than position-only intersections.
        all_rows[baseline_idx][member]["captureMs"] <
        all_rows[candidate_idx][member]["captureMs"] <
        following[member]["captureMs"]
    ]
    cohort_timeline = []
    for round_idx, cohorts in enumerate(all_cohorts):
        members = cohorts.get(aoi, {})
        captures = [member["captureMs"] for member in members.values()]
        cohort_timeline.append({
            "round": round_idx,
            "lens": runs[round_idx]["lens"],
            "pointObserved": key in all_rows[round_idx],
            "pointCaptureMs": all_rows[round_idx][key]["captureMs"]
            if key in all_rows[round_idx] else None,
            "sameAoiSourceCohortCount": len(members),
            "sameAoiSourceCohortCaptureBoundsMs": [min(captures), max(captures)]
            if captures else None,
            "allCohortRowsSameCaptureSecond": len(set(captures)) == 1
            if captures else None,
            "independentCameraWallClockWindowMs":
                runs[round_idx]["independentCameraWallClockWindowMs"],
        })
    appearances = [(round_idx, entries[key]) for round_idx, entries in enumerate(all_rows)
                   if key in entries]
    mismatched_incarnations = [
        round_idx for round_idx, entry in appearances
        if not _stable_identity(record, entry)
    ]
    return {
        "pointId": key,
        "xy": [data["x"], data["y"]],
        "aoi": list(record["aoi"]),
        "sourceRound": src_idx, "comparedRound": dest_idx,
        "observedCaptureMs": record["captureMs"],
        "gatherOccupiedAtObservation": data["gatherOccupied"],
        "resourceDetailKnown": data.get("resourceDetailKnown") is True,
        "fullAmount": data.get("resourceFullAmount"),
        "remainingAmount": data.get("resourceRemainingAmount"),
        "resourceTypeId": data["resourceTypeId"],
        "level": data["level"],
        "sameAoiOtherResourceRows": neighbors,
        "oppositeScanSameAoiSourceCaptureBoundsMs":
            cohort_timeline[dest_idx]["sameAoiSourceCohortCaptureBoundsMs"],
        "sourceCohortWallClockTimeline": cohort_timeline,
        "sameAoiStableAnchorPointIdsAcrossPair": stable_src_opposite,
        "sameAoiStableAnchorPointIdsAcrossBaselineCandidateNextBaseline":
            stable_three_round,
        "pointIndexReusedWithChangedSourceFieldsInRounds": mismatched_incarnations,
        "sameObservedSourceIncarnationInOtherRounds":
            [round_idx for round_idx, entry in appearances
             if round_idx not in (src_idx, dest_idx) and _stable_identity(record, entry)],
        "nativePointLifecycleCausality": "unknown",
        "simultaneousAuthoritativePresenceAndAbsenceProof": False,
        "observedInOtherRounds": [
            i for i, entries in enumerate(all_rows) if i not in (src_idx, dest_idx) and key in entries
        ],
        "lastObservedRound": max(i for i, entries in enumerate(all_rows) if key in entries),
        "firstObservedRound": min(i for i, entries in enumerate(all_rows) if key in entries),
    }


def audit(root):
    root = Path(root)
    sequence = json.loads((root / "benchmark.json").read_text(encoding="utf8"))
    require(type(sequence) is list and len(sequence) == 6,
            "six alternating native benchmark rounds required")
    snapshots, runs = [], []
    for index, receipt in enumerate(sequence):
        records, summary = _run(root, index, receipt)
        snapshots.append(records)
        runs.append(summary)
    require(len({r["serverId"] for r in runs}) == 1, "server changed across runs")
    require(len({tuple(run["cameraOwnerIdentity"].items()) for run in runs}) == 1,
            "camera owner/session identity changed across rounds")
    require(all(left["captureMaxMs"] < right["captureMinMs"]
                for left, right in zip(runs, runs[1:])),
            "Resource snapshots not ordered as strictly later source captures")
    require(all(left["independentCameraWallClockWindowMs"][1] <=
                right["independentCameraWallClockWindowMs"][0]
                for left, right in zip(runs, runs[1:])),
            "independent camera wall-clock windows overlap or run out of order")
    cohorts = [_aoi_cohorts(snapshot) for snapshot in snapshots]
    pairs = []
    for base_idx in (0, 2, 4):
        earlier, later = snapshots[base_idx], snapshots[base_idx + 1]
        common = earlier.keys() & later.keys()
        changed = {
            field: sum(earlier[key]["data"].get(field) != later[key]["data"].get(field)
                       for key in common) for field in IMMUTABLE_FIELDS
        }
        require(not any(changed.values()), f"pair {base_idx // 2 + 1}: immutable Resource fields changed")
        require(all(earlier[key]["captureMs"] < later[key]["captureMs"] for key in common),
                f"pair {base_idx // 2 + 1}: candidate has stale capture timestamps")
        missing = [_difference(key, snapshots, cohorts, runs, base_idx, base_idx + 1)
                   for key in sorted(earlier.keys() - later.keys(), key=int)]
        extra = [_difference(key, snapshots, cohorts, runs, base_idx + 1, base_idx)
                 for key in sorted(later.keys() - earlier.keys(), key=int)]
        flips = [
            {"pointId": key, "beforeOccupied": earlier[key]["data"]["gatherOccupied"],
             "afterOccupied": later[key]["data"]["gatherOccupied"]}
            for key in sorted(common, key=int)
            if earlier[key]["data"]["gatherOccupied"] != later[key]["data"]["gatherOccupied"]
        ]
        pairs.append({
            "pair": base_idx // 2 + 1, "common": len(common),
            "baseline": len(earlier), "candidate": len(later),
            "missingCount": len(missing), "candidateExtraCount": len(extra),
            "missingByPriorOccupation": dict(Counter(str(item["gatherOccupiedAtObservation"])
                                                      for item in missing)),
            "extraByCandidateOccupation": dict(Counter(str(item["gatherOccupiedAtObservation"])
                                                       for item in extra)),
            "missingWithContemporaneousSameAoiSourceCohort":
                sum(item["sameAoiOtherResourceRows"] > 0 for item in missing),
            "candidateExtraWithEarlierSameAoiSourceCohort":
                sum(item["sameAoiOtherResourceRows"] > 0 for item in extra),
            "missingWithStableSameAoiBaselineCandidateNextBaselineAnchors":
                sum(bool(item["sameAoiStableAnchorPointIdsAcrossBaselineCandidateNextBaseline"])
                    for item in missing),
            "candidateExtraWithStableSameAoiBaselineCandidateNextBaselineAnchors":
                sum(bool(item["sameAoiStableAnchorPointIdsAcrossBaselineCandidateNextBaseline"])
                    for item in extra),
            "genuineSpawnDestructionOrScannerMissAttribution": "unknown",
            "immutableFieldChanges": changed, "sharedOccupationChanges": flips,
            "missingIdentities": missing, "candidateExtraIdentities": extra,
        })
    baseline = statistics.median(run["seconds"] for run in runs[::2])
    candidate = statistics.median(run["seconds"] for run in runs[1::2])
    return {
        "source": str(root), "serverId": runs[0]["serverId"],
        "runs": runs, "pairs": pairs,
        "baselineMedianSeconds": round(baseline, 3),
        "candidateMedianSeconds": round(candidate, 3),
        "reductionPercent": round((1 - candidate / baseline) * 100, 2),
        "resourceFov20AccuracyQualified": False,
        "sameAoiCohortQualification": (
            "Identical AOI co-sampled Source Resource rows and stable spatial anchors "
            "supply bounded sequential native wall-clock observations, not a "
            "simultaneous authoritative positive state for any absent point. "
            "Map point ID is spatial and can be recycled by a new spawn."
        ),
        "causality": (
            "All timestamps are local point-capture times, not native spawn, "
            "occupancy-transition or destruction times. Paired differences and "
            "later appearances/disappearances prove temporal observed populations, "
            "not the reason a single Resource point was absent. Exact 10,000 "
            "AOI coverage is point-manager geometry, not source population parity; "
            "attributable Lua probes are not raw network request counts."
        ),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("root", type=Path, help="Existing preserved six-round receipt root (read-only)")
    args = parser.parse_args()
    print(json.dumps(audit(args.root), indent=2))
