"""Offline-only UUID/timestamp comparison of saved MAP-014 mixed benchmarks.

Uses read-only SQLite snapshots and recorded benchmark metadata. It never
connects to the game, requests marches, or makes a live-availability claim.
"""

import argparse
import json
import sqlite3
from pathlib import Path


KINDS = ("truck", "railway")


def _read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def _arrival_seconds(value):
    """Accept only plausible explicit Unix seconds or milliseconds."""
    if type(value) not in (int, float) or value <= 0:
        return None
    if 946684800 <= value <= 4102444800:  # 2000-2100, seconds
        return value
    if 946684800000 <= value <= 4102444800000:  # 2000-2100, ms
        return value / 1000
    return None


def _read_marches(root: Path, index: int, server_id: int):
    dbpath = (root / f"benchmark-{index}.db").resolve()
    connection = sqlite3.connect(dbpath.as_uri() + "?mode=ro", uri=True)
    rows = {kind: {} for kind in KINDS}
    try:
        for kind, server, key, updated_at, payload in connection.execute(
            "SELECT kind,server_id,record_key,updated_at,data_json "
            "FROM map_records WHERE kind IN ('truck','railway')"
        ):
            if server != server_id:
                raise ValueError(f"round {index}: unexpected server {server}")
            data = json.loads(payload)
            if not isinstance(data, dict) or not isinstance(key, str) or not key:
                raise ValueError(f"round {index}: malformed {kind} row")
            if str(data.get("uuid", key)) != key:
                raise ValueError(f"round {index}: mismatched native march UUID")
            if data.get("serverId", server) != server:
                raise ValueError(f"round {index}: conflicting stored server")
            if not isinstance(updated_at, int) or updated_at <= 0:
                raise ValueError(f"round {index}: missing observation time")
            if key in rows[kind]:
                raise ValueError(f"round {index}: duplicate {kind} UUID {key}")
            rows[kind][key] = {
                "arrival": _arrival_seconds(data.get("arriveTs")),
                "endTime": data.get("endTime"),
                "updatedAtMs": updated_at,
            }
    finally:
        connection.close()
    return rows


def audit(root: Path):
    measurements = _read_json(root / "benchmark.json")
    if len(measurements) != 6:
        raise ValueError("three complete alternating baseline/FOV20 pairs required")
    populations = []
    for i, run in enumerate(measurements):
        if run.get("round") != i or run.get("boundedLens") is not (i % 2 == 1):
            raise ValueError(f"round {i}: invalid alternating order")
        state = run["terminal"]
        if (state.get("phase") != "completed" or state.get("failedBlocks") != 0
                or state.get("completedBlocks") != 2500
                or not set(KINDS).issubset(set(state.get("selectedTypes", [])))):
            raise ValueError(f"round {i}: incomplete or mismatched mixed selection")
        metrics = _read_json(root / f"benchmark-{i}-probe-metrics.json")
        if metrics.get("uniqueCoveredAois") != 10000:
            raise ValueError(f"round {i}: incomplete POINT AOI coverage")
        populations.append(_read_marches(root, i, state["serverId"]))

    comparisons = []
    for base_idx in (0, 2, 4):
        candidate_idx = base_idx + 1
        candidate_run = measurements[candidate_idx]
        # The metadata's second-resolution start and measured duration bound the
        # candidate finish; they are not a native server-time measurement.
        candidate_finish = candidate_run["terminal"]["startedAt"] + candidate_run["seconds"]
        kinds = {}
        for kind in KINDS:
            baseline = populations[base_idx][kind]
            candidate = populations[candidate_idx][kind]
            missing = set(baseline) - set(candidate)
            future = {key for key in missing
                      if baseline[key]["arrival"] is not None
                      and baseline[key]["arrival"] > candidate_finish}
            other = missing - future
            expired_deadline = {key for key in other
                                if baseline[key]["arrival"] is not None
                                and baseline[key]["arrival"] <= candidate_finish}
            kinds[kind] = {
                "baselineCount": len(baseline),
                "candidateCount": len(candidate),
                "sharedUUIDs": len(set(baseline) & set(candidate)),
                "missingBaselineUUIDs": len(missing),
                "extraCandidateUUIDs": len(set(candidate) - set(baseline)),
                "missingWithFutureSourceArrival": len(future),
                "missingWithPastSourceArrival": len(expired_deadline),
                "missingWithoutUsableArrival": len(other - expired_deadline),
                "futureMissingExamples": sorted(future)[:5],
            }
        comparisons.append({
            "baselineRound": base_idx,
            "candidateRound": candidate_idx,
            "candidateApproxFinishUnixSeconds": candidate_finish,
            "kinds": kinds,
        })
    return {
        "source": str(root),
        "evidenceType": "archived consecutive scan snapshots",
        "pointAoiCoverageEachRun": 10000,
        "pairs": comparisons,
        "mobileAccuracyQualified": False,
        "limitation": (
            "Future arriveTs excludes known arrival by the candidate end, but does "
            "not prove native march subscription, request completion, or continued "
            "existence. The two scans are time-separated. March endTime is a movement "
            "leg timestamp and must not be substituted for TrainData.arriveTs."
        ),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("benchmark_directory", type=Path)
    args = parser.parse_args()
    print(json.dumps(audit(args.benchmark_directory), indent=2))
