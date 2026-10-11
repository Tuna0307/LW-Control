"""Read-only genuine native region-versus-published-record sample witness.

This is a *sample* comparison of independent game-owned region point fields to
the SQLite publisher, never an assertion of perfect whole-world recall.
Run after a completed scan from a copied native probe snapshot, not from a
live file which changes every AOI response.
"""

import argparse
import collections
import datetime as dt
import json
import pathlib
import sqlite3


def truthy(document, name):
    if document.get(name) is not True:
        raise ValueError(f"Native witness missing required true flag: {name}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--native", type=pathlib.Path, required=True)
    parser.add_argument("--db", type=pathlib.Path, required=True)
    parser.add_argument("--server", type=int, required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--out", type=pathlib.Path, required=True)
    args = parser.parse_args()
    native = json.loads(args.native.read_text(encoding="utf-8"))
    truthy(native, "cameraTileStable")
    truthy(native, "responseFlagsTransitioned")
    truthy(native, "hasReceiveViewPointsReply")
    if native.get("requestMode") != "coverage":
        raise ValueError("Native witness does not use fresh coverage mode")
    if not native.get("requestId") or not native.get("launchSessionId"):
        raise ValueError("Missing native response correlation identity")
    captured = dt.datetime.fromisoformat(native["capturedAt"].replace("Z", "+00:00"))
    if native.get("state") != "proven":
        raise ValueError(f"Native result not proven: {native.get('state')}")
    conn = sqlite3.connect(f"file:{args.db.resolve().as_posix()}?mode=ro", uri=True)
    conn.row_factory = sqlite3.Row
    scan = conn.execute(
        "SELECT id,server_id,selected_types,status,created_at,updated_at,"
        "completed_blocks,total_blocks,failed_blocks FROM scan_runs WHERE id=?",
        (args.run_id,),
    ).fetchone()
    if scan is None or scan["server_id"] != args.server or scan["status"] != "completed":
        raise ValueError("Target run was not completed on requested server")
    if scan["completed_blocks"] != scan["total_blocks"] or scan["failed_blocks"]:
        raise ValueError("Published run had incomplete logical coverage")
    if not (
        scan["created_at"] - 120000
        <= int(captured.timestamp() * 1000)
        <= scan["updated_at"] + 120000
    ):
        raise ValueError("Native capture timestamp not bracketed by completed run")
    selected = set(json.loads(scan["selected_types"]))
    records = [
        dict(record) for record in conn.execute(
            "SELECT kind,server_id,record_key,point_index,uuid,name,level,"
            "updated_at,data_json FROM map_records WHERE server_id=?",
            (args.server,),
        )
    ]
    conn.close()
    by_uuid = collections.defaultdict(list)
    by_point = collections.defaultdict(list)
    for record in records:
        by_uuid[(record["kind"], str(record["uuid"]))].append(record)
        if record["point_index"] is not None:
            by_point[(record["kind"], int(record["point_index"]))].append(record)
    row_checks = []
    selected_native = collections.Counter()
    for raw in native.get("point_records", []):
        kind = {"player_base": "city", "resource": "resource", "resource_point": "resource"}.get(raw.get("kind"))
        if kind not in selected:
            continue
        if raw.get("serverId") != args.server:
            # A foreign owner inside a correctly served region is valid for
            # some records; it cannot be silently relabelled as target-owned.
            continue
        selected_native[kind] += 1
        point_id = raw.get("pointId") or raw.get("id")
        matches = by_point.get((kind, point_id), [])
        if not matches and raw.get("uuid"):
            matches = by_uuid.get((kind, str(raw["uuid"])), [])
        outcome = {
            "kind": kind,
            "pointId": point_id,
            "nativeUuid": raw.get("uuid"),
            "nativeTile": [raw.get("x"), raw.get("y")],
            "nativeLevel": raw.get("level"),
            "matched": len(matches) == 1,
        }
        if len(matches) == 1:
            stored = matches[0]
            data = json.loads(stored["data_json"])
            outcome["storedUuid"] = stored["uuid"]
            outcome["storedTile"] = [data.get("x"), data.get("y")]
            outcome["storedLevel"] = stored["level"]
            outcome["identityMatches"] = (
                stored["uuid"] is None or str(stored["uuid"]) == str(raw.get("uuid"))
            )
            outcome["levelMatches"] = (
                raw.get("level") is None or stored["level"] == raw["level"]
            )
            outcome["positionMatches"] = (
                data.get("x") == raw.get("x") and data.get("y") == raw.get("y")
            )
        row_checks.append(outcome)
    findings = {
        "kind": "genuine_native_region_vs_saved_published_sample",
        "wholeWorldGroundTruth": False,
        "source": "current-client bulk-aoi-diagnostic-result.json / map-data.db",
        "runId": args.run_id,
        "serverId": args.server,
        "capturedAt": native["capturedAt"],
        "nativeRequestId": native["requestId"],
        "nativeLaunchSessionId": native["launchSessionId"],
        "nativeGamePid": native.get("gamePid"),
        "nativeAoiCount": len(native.get("requestedIndices") or []),
        "nativePointCount": len(native.get("point_records") or []),
        "nativeMonsterCount": len(native.get("monster_march_records") or []),
        "selectedNativeKinds": dict(selected_native),
        "recordMatches": dict(collections.Counter(
            f"{row['kind']}:{'hit' if row['matched'] else 'miss'}"
            for row in row_checks
        )),
        "sampledMismatchCount": sum(
            not row["matched"] or any(
                row.get(field) is False
                for field in ("identityMatches", "levelMatches", "positionMatches")
            )
            for row in row_checks
        ),
        "missingOrDifferingSample": [
            row for row in row_checks
            if not row["matched"] or any(
                row.get(field) is False
                for field in ("identityMatches", "levelMatches", "positionMatches")
            )
        ][:30],
        "limitations": [
            "Native region file records game-owned visible point snapshot, not independent full-world census",
            "Scan UI and saved records can change due to movement, expiry, spawning or other game events",
            "Last recorded probe is one point in a multi-minute scan and carries no direct raw packet count",
            "Monster march objects need separate source-specific identity comparisons",
        ],
    }
    args.out.write_text(json.dumps(findings, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({
        "runId": args.run_id,
        "compared": len(row_checks),
        "selectedNativeKinds": dict(selected_native),
        "sampledMismatchCount": findings["sampledMismatchCount"],
    }))
    if not row_checks:
        raise ValueError("Witness selected zero native City/Resource records")
    if findings["sampledMismatchCount"]:
        raise ValueError("Independent native sample mismatched; see exact receipt")


if __name__ == "__main__":
    main()
