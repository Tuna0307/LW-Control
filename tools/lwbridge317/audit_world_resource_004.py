"""Read-only 004 isolated run evidence. Does not write game, runtime, or SQLite state."""
from __future__ import annotations

import argparse
import hashlib
import json
import sqlite3
from pathlib import Path


ORIGINALS = {
    "LWScripts.data": "248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22",
    "LWScripts.txt": "d2f9bcc165f41427d4b3b7b9243e4a483bf316519a16b4ce9be658ba9afa783a",
    "version.txt": "785f3ec7eb32f30b90cd0fcf3657d388b5ff4297f2f9716ff66e9b69c05ddd09",
}

def hash_file(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            sha.update(chunk)
    return sha.hexdigest()

def audit(report_file: Path) -> dict:
    report = json.loads(report_file.read_text(encoding="utf-8-sig"))
    root = Path(report["isolatedRoot"])
    if not root.name.startswith("LWB317-BACKGROUND-WITNESS-002-") or not root.is_absolute():
        raise ValueError("not an isolated pilot root")
    profile = report["profileId"]
    backup = root / "overview-bridge-backups" / ("preflight-originals-" + report["attemptId"])
    backups_match = all(hash_file(backup / name) == original for name, original in ORIGINALS.items())
    installed_match = all(report.get("installedAfter", {}).get(name) == original
                          for name, original in ORIGINALS.items())
    manifest_states = []
    for manifest_file in sorted((root / "overview-bridge-backups").glob("*/manifest.json")):
        data = json.loads(manifest_file.read_text(encoding="utf-8-sig"))
        manifest_states.append(data.get("stage") or data.get("state"))
    ready = root / "overview-bridge" / "world-ready-result.json"
    world = None
    if ready.exists():
        obj = json.loads(ready.read_text(encoding="utf-8-sig"))
        # NEVER expose challenge, token or raw command body.
        live_instance = [e["details"] for e in report["events"]
                         if e["kind"] == "real-home-start-returned"]
        owned = live_instance[-1] if live_instance else {}
        world = {
            "requestId": obj.get("requestId"),
            "state": obj.get("state"),
            "method": obj.get("method"),
            "error": obj.get("error"),
            "profileMatch": obj.get("profileId") == profile,
            "sessionMatch": obj.get("sessionId") == owned.get("instance"),
            "pidMatch": obj.get("gamePid") == owned.get("ownedPid"),
            "serverId": obj.get("serverId"),
            "worldId": obj.get("worldId"),
            "tileWidth": obj.get("tileWidth"),
            "tileHeight": obj.get("tileHeight"),
        }
    db = root / "profiles" / profile / "map-data" / "map-data.db"
    sqlite_result = {}
    if db.exists():
        with sqlite3.connect(db.as_uri() + "?mode=ro", uri=True, timeout=3) as conn:
            conn.execute("PRAGMA query_only=ON")
            for table in ("scan_runs", "scan_blocks", "scan_records", "map_records"):
                exists = conn.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", (table,)).fetchone()
                if not exists:
                    sqlite_result[table] = {"exists": False}
                    continue
                columns = [row[1] for row in conn.execute(f"PRAGMA table_info({table})")]
                sqlite_result[table] = {
                    "count": conn.execute(f"SELECT count(*) FROM {table}").fetchone()[0],
                    "columns": columns,
                }
                if table == "scan_runs":
                    sqlite_result[table]["rows"] = [
                        dict(zip(columns, row)) for row in conn.execute(
                            "SELECT * FROM scan_runs ORDER BY rowid DESC LIMIT 3"
                        )
                    ]
                if table in ("scan_records", "map_records") and "kind" in columns:
                    sqlite_result[table]["resourceCount"] = conn.execute(
                        f"SELECT count(*) FROM {table} WHERE kind='resource'"
                    ).fetchone()[0]
    return {
        "report": str(report_file), "attemptId": report["attemptId"],
        "terminal": report.get("terminal"), "events": [e["kind"] for e in report["events"]],
        "worldReady": world, "sqlite": sqlite_result,
        "preflightOriginalBackupsMatch": backups_match,
        "installedAfterHashesMatch": installed_match if "installedAfter" in report else None,
        "manifestStages": manifest_states,
        "journalExists": (root / "overview-bridge" / "recovery.json").exists(),
        "ownedStopSucceeded": report.get("ownedGameStopSucceeded"),
    }

def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("reports", nargs="+", type=Path)
    p.add_argument("--output", type=Path)
    args = p.parse_args()
    out = [audit(path.resolve()) for path in args.reports]
    if len(out) == 2:
        completed, cancelled = out
        c = completed["sqlite"]
        x = cancelled["sqlite"]
        completed_run = c["scan_runs"]["rows"][0]
        cancelled_run = x["scan_runs"]["rows"][0]
        assert completed_run["status"] == "completed"
        assert completed_run["completed_blocks"] == completed_run["total_blocks"] == 2500
        assert completed_run["failed_blocks"] == 0
        assert completed["worldReady"] is not None
        assert completed["worldReady"]["state"] == "proven"
        assert completed["worldReady"]["profileMatch"]
        assert completed["worldReady"]["sessionMatch"]
        assert completed["worldReady"]["pidMatch"]
        assert completed["worldReady"]["serverId"] == 2212
        assert completed["worldReady"]["tileWidth"] == 1000
        assert completed["worldReady"]["tileHeight"] == 1000
        assert c["map_records"]["resourceCount"] == 8008
        assert c["scan_records"]["resourceCount"] == 0
        assert "resource-natural-terminal" in completed["events"]
        assert cancelled_run["status"] == "cancelled"
        assert cancelled_run["total_blocks"] == 2500
        assert x["map_records"]["resourceCount"] == 0
        assert x["scan_records"]["resourceCount"] == 0
        assert "resource-owned-active-stop-admission" in cancelled["events"]
        assert "production-map-stop" in cancelled["events"]
        assert cancelled["worldReady"] is not None
        assert cancelled["worldReady"]["state"] == "proven"
        for evidence in out:
            assert evidence["preflightOriginalBackupsMatch"]
            assert evidence["installedAfterHashesMatch"]
            assert evidence["manifestStages"] == ["restored"]
            assert evidence["journalExists"] is False
            assert evidence["ownedStopSucceeded"] is True
        print("WORLD004_INDEPENDENT_DUAL_AUDIT_PASS: completed=2500/2500, resource=8008, active_cancelled=0 published, both restored")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    for x in out:
        print(json.dumps({k:v for k,v in x.items() if k != "sqlite"}, indent=2))
        print("sqlite", json.dumps(x["sqlite"], indent=2)[:5000])

if __name__ == "__main__":
    main()
