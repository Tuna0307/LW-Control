#!/usr/bin/env python3
"""Classify immutable attempt-5 pilot witnesses without calling a live provider.

This is an archived evidence readiness check, not a live handshake or scan oracle.
"""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path

DEFAULT_EVIDENCE = (Path(__file__).resolve().parents[2] / "evidence" /
    "lwbridge-0.3.17" / "functions" /
    "LWB317-FUNCTION-HOME-MAP-LIVE-PILOT-001" / "attempt-5-fresh-ready")


def read_json(path: Path) -> dict | list:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def as_utc(epoch_ms: int) -> str:
    return datetime.fromtimestamp(epoch_ms / 1000, tz=timezone.utc).isoformat()


def evaluate(evidence: Path) -> dict:
    ready = read_json(evidence / "ready.json")
    heartbeat = read_json(evidence / "heartbeat.json")
    transport = read_json(evidence / "pipe-transport.json")
    pre_scan = read_json(evidence / "map-pre-scan.json")
    recovery = read_json(evidence / "recovery-active.json")
    processes = read_json(evidence / "processes.json")
    control = dict(line.split("=", 1) for line in
        (evidence / "control.txt").read_text(encoding="utf-8-sig").splitlines()
        if "=" in line)
    assert isinstance(processes, list)
    game_pid = int(control["gamePid"])
    matching_processes = [p for p in processes
        if p.get("Name", "").lower() == "lastwar.exe"
        and p.get("ProcessId") == game_pid]
    if len(matching_processes) != 1:
        raise ValueError("Exact attempt-5 game PID is absent or ambiguous")
    timestamp = re.fullmatch(r"/Date\((\d+)\)/", matching_processes[0]["CreationDate"])
    if timestamp is None:
        raise ValueError("Game creation timestamp is not parseable")
    launched_ms = int(timestamp.group(1))
    for label, document in (("ready", ready), ("heartbeat", heartbeat), ("recovery", recovery)):
        if (document.get("profileId") != control["profileId"] or
            document.get("sessionId") != control["sessionId"] or
            document.get("gamePid") != game_pid):
            raise ValueError(f"{label} does not match attempt-5 owned identity")
    if ready["challenge"] != control["challenge"] or heartbeat["challenge"] != control["challenge"]:
        raise ValueError("ready/heartbeat challenge does not match control identity")
    ready_ms = int(ready["readyAt"]) * 1000
    heartbeat_ms = int(heartbeat["updatedAt"]) * 1000
    if not launched_ms <= ready_ms <= heartbeat_ms:
        raise ValueError("Attempt-5 process/ready/heartbeat chronology invalid")
    rows = []
    for run in pre_scan["latestScanRuns"]:
        start_ms = int(run["created_at"])
        end_ms = int(run["updated_at"])
        if end_ms < start_ms:
            raise ValueError("Persisted scan run timestamps are inverted")
        rows.append({
            "scanRunId": run["id"],
            "selectedTypes": json.loads(run["selected_types"]),
            "status": run["status"],
            "completedBlocks": int(run["completed_blocks"]),
            "failedBlocks": int(run["failed_blocks"]),
            "createdAtUtc": as_utc(start_ms),
            "updatedAtUtc": as_utc(end_ms),
            "predatesAttempt5Game": end_ms < launched_ms,
            # A run created after launch is only temporally eligible, not
            # authenticated to this attempt's session by the database schema.
            "temporallyEligibleForAttempt5": launched_ms <= start_ms <= heartbeat_ms,
        })
    resource = [x for x in rows if x["selectedTypes"] == ["resource"]]
    if len(resource) != 1:
        raise ValueError("Expected exactly one archived Resource run")
    pipe_snapshot_connected = (
        transport.get("state") == "connected" and transport.get("clientConnected") is True)
    return {
        "evidenceClass": "archived-static-only",
        "attempt5GamePid": game_pid,
        "launchUtc": as_utc(launched_ms),
        "readyUtc": as_utc(ready_ms),
        "heartbeatUtc": as_utc(heartbeat_ms),
        "gameHeartbeatReady": heartbeat.get("ready") is True and heartbeat.get("connected") is True,
        "pipeSnapshotState": transport.get("state"),
        "pipeSnapshotClientConnected": transport.get("clientConnected"),
        "pipeSnapshotReportsConnected": pipe_snapshot_connected,
        "pipeHelloAckProvenForSession": False,
        "pipeLimitation": "No timestamped, session-correlated host hello.ack or authenticated RPC result in the seven saved captures",
        "resourceRowsBeforeScan": int(pre_scan["resource"]["count"]),
        "resourceRunCancelledBeforeAttempt5": (
            resource[0]["status"] == "cancelled" and resource[0]["predatesAttempt5Game"]),
        "resourceRunZeroCompletedBlocks": resource[0]["completedBlocks"] == 0,
        "runs": rows,
        "firstUnprovenBoundary": "same-session authenticated hello.ack -> connected RPC -> Resource provider capture/publish",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evidence-root", type=Path, default=DEFAULT_EVIDENCE)
    args = parser.parse_args()
    print(json.dumps(evaluate(args.evidence_root), indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
