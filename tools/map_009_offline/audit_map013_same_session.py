"""Audit retained native replies; never send a game request or qualify a scanner.

The profiler's packet hash identifies a retained object within this observation,
not a protocol request ID. Reply context is independent of requested geometry.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def same_footprint(wide: dict, narrow: dict, *, expected_server: int | None = None) -> dict:
    size, count = narrow["blockSize"], narrow["blockCount"]
    if type(size) is not int or type(count) is not int or size <= 0 or count <= 0:
        raise ValueError("invalid native geometry")
    indices = narrow["requestedIndices"]
    if not indices or len(set(indices)) != len(indices) or any(
            type(i) is not int or i < 0 or i >= count * count for i in indices):
        raise ValueError("invalid native AOI set")
    wanted = set(indices)

    def records(value: dict, select: bool) -> dict:
        result = {}
        for row in value["point_records"]:
            server = expected_server if expected_server is not None else wide["point_records"][0]["serverId"]
            if row.get("serverId") != server or row.get("worldId") != 0:
                raise ValueError("foreign native row context")
            x, y = row["x"], row["y"]
            if type(x) is not int or type(y) is not int or not (0 <= x < size * count and 0 <= y < size * count):
                raise ValueError("invalid point coordinates")
            if select and (y // size * count + x // size) not in wanted:
                continue
            key = (str(row["id"]), x, y, row["kind"])
            if key in result:
                raise ValueError("duplicate native identity")
            result[key] = row
        return result

    if not wide["point_records"]:
        raise ValueError("no independently populated control")
    expected, observed = records(wide, True), records(narrow, False)
    differences = sum(expected[k] != observed[k] for k in expected.keys() & observed.keys())
    return {"wideRows": len(wide["point_records"]), "wideAOIs": len(wide["requestedIndices"]),
            "narrowAOIs": len(indices), "wideRowsInsideNarrow": len(expected),
            "narrowRows": len(observed), "missing": len(expected.keys() - observed.keys()),
            "extra": len(observed.keys() - expected.keys()), "differingRows": differences}


def fresh_reply_context(trace: dict, *, server: int, world: int, x: int, y: int) -> list[dict]:
    seen = set()
    matches = []
    for snapshot_index, snapshot in enumerate(trace["managedPacketSnapshots"]):
        data = json.loads(snapshot["json"])
        if data.get("errorType") or data.get("receiveEnumerationTruncated") or data.get("packetRowsTruncated"):
            raise ValueError("incomplete native packet observation")
        for packet in data["packets"]:
            if packet["direction"] != "receive" or packet["command"] != "world.get.block":
                continue
            identity = packet["packetHash"]
            if identity in seen:
                continue
            seen.add(identity)
            # A packet already retained at observation start is not fresh proof.
            if snapshot_index == 0 or snapshot["elapsed"] <= 0:
                continue
            segments = packet.get("segments") or {}
            if segments.get("truncated"):
                raise ValueError("incomplete native segments")
            for entry in segments.get("entries", []):
                numbers = entry["numbers"]
                if numbers == {**numbers, "serverId": server, "worldId": world,
                               "x": x, "y": y, "viewLvl": 0}:
                    matches.append({"firstSeenSeconds": snapshot["elapsed"],
                                    "nativeContext": numbers, "pointCount": entry["arrayCounts"].get("points")})
    return matches


def audit(attempt: Path) -> dict:
    names = ["attempt.json", "world-status.json", "native-first-region.json", "trace-status.json",
             "second-region-coverage.json", "second-trace-coverage.json",
             "second-region-native.json", "second-trace-native.json", "final.json"]
    saved = {name: json.loads((attempt / name).read_text(encoding="utf-8")) for name in names}
    owner, world = saved["attempt.json"], saved["world-status.json"]
    for name, value in saved.items():
        if name in ("attempt.json", "final.json"):
            continue
        session = value.get("launchSessionId", value.get("sessionId"))
        if (session != owner["sessionId"] or value.get("profileId") != owner["profileId"] or
                value.get("gamePid") != saved["final.json"]["ownedGamePid"] or
                hashlib.sha256(value["challenge"].encode()).hexdigest() != owner["challengeSha256"]):
            raise ValueError("retained replies have different native owners")
    if world["state"] != "proven" or world["serverId"] <= 0 or world["worldId"] != 0:
        raise ValueError("no admitted world")
    controls = []
    for region_name, trace_name in [("native-first-region.json", "trace-status.json"),
                                   ("second-region-coverage.json", "second-trace-coverage.json"),
                                   ("second-region-native.json", "second-trace-native.json")]:
        region = saved[region_name]
        controls.append({"file": region_name, "state": region["state"], "error": region.get("error"),
                         "nativeReplies": fresh_reply_context(saved[trace_name], server=world["serverId"],
                            world=world["worldId"], x=region["targetTileX"], y=region["targetTileY"]),
                         "storedPointRows": len(region["point_records"]),
                         "centreCountBefore": region["baselineTargetPointCount"],
                         "centreCountAfter": region["targetPointCount"],
                         "responseFlagsTransitioned": region["responseFlagsTransitioned"],
                         "cameraStable": region["cameraTileStable"]})
    final = saved["final.json"]
    if not final["exactStopSucceeded"] or not final["originalScriptsRestored"]:
        raise ValueError("exact cleanup unproved")
    return {"schemaVersion": 1, "status": "PARTIAL", "attempt": attempt.name,
            "sourceCheckpoint": "0022c4ce64c415b31cbe1a0d93c220dc792ad8e1",
            "inputSha256": {name: hashlib.sha256((attempt / name).read_bytes()).hexdigest() for name in names},
            "regions": controls, "sameFootprint": same_footprint(saved["second-region-coverage.json"],
                                      saved["second-region-native.json"], expected_server=world["serverId"]),
            "cleanup": {"exactStopSucceeded": True, "originalScriptsRestored": True},
            "limits": ["Packet object identity is not a network request ID.",
                       "Outgoing envelope and cache-insertion invocation are not observed.",
                       "native is an unexpanded camera-assisted experiment, not camera-free discovery.",
                       "No full-world recall or performance improvement is qualified."]}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("attempt", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.write_text(json.dumps(audit(args.attempt), indent=2) + "\n", encoding="utf-8")
    print("MAP013_SAME_SESSION_REPLY_AUDIT_OK")
