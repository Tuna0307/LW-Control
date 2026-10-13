#!/usr/bin/env python3
"""MAP-010 task-isolated actual native-name UI preflight (never launches game).

Copies a historical genuine Map317 scan via SQLite's consistent read-only
backup API to a disposable disabled profile. Only paths under ignored
artifacts/map-010/ are created. No owner settings or installed game files change.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import sqlite3
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "artifacts/map-010/native-names-pilot"
ISOLATED = DEST / "isolated"
PROFILE = "map-010-native-names"
SOURCE = (ROOT / "artifacts/map-006/trial-scan008r5_20261012/isolated/profiles/"
          "map-006-scan008r5_20261012/map-data/map-data.db")
GAME = Path.home() / "AppData/Local/FunFly/Last War-Survival Game"
SCRIPTS = Path.home() / "AppData/LocalLow/FunFly/Last War-Survival Game/lwScripts"
HASHES = {
    "LWScripts.data": "d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495",
    "LWScripts.txt": "b84cfa8762d9f8b93fcbe9e64b8715e84d1549248c2e98dc7dc272321476933b",
    "version.txt": "c2356069e9d1e79ca924378153cfbbfb4d4416b1f99d41a2940bfdb66c5319db",
}


def sha(file: Path) -> str:
    with file.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def check_state():
    sys.path.insert(0, str(ROOT / "tools"))
    import home_004_f04_v24_gate as previous
    actors = previous.actors()
    if actors:
        raise RuntimeError("Another game/launcher/desktop host is running; do not inspect or close it: " + repr(actors))
    actual = {name: sha(SCRIPTS / name) for name in HASHES}
    if actual != HASHES:
        raise RuntimeError("Installed original game scripts changed; no task files will be created")
    return previous.check_only()


def prepare():
    if DEST.exists():
        raise RuntimeError("Existing task root must be preserved; refusing to overwrite: " + str(DEST))
    if not SOURCE.is_file():
        raise FileNotFoundError("Genuine historical R5 Map data source is not available")
    compatibility = check_state()
    with sqlite3.connect(f"file:{SOURCE.as_posix()}?mode=ro", uri=True) as original:
        counts = dict(original.execute(
            "SELECT kind,COUNT(*) FROM map_records WHERE kind IN ('resource','monster') GROUP BY kind"
        ).fetchall())
        if counts.get("resource", 0) < 1 or counts.get("monster", 0) < 1:
            raise RuntimeError("Genuine historical R5 snapshot lacks positive Resource or Monster records")
    ISOLATED.mkdir(parents=True)
    backup = DEST / "original-scripts"
    backup.mkdir()
    for name in HASHES:
        shutil.copy2(SCRIPTS / name, backup / name)
        if sha(backup / name) != HASHES[name]:
            raise RuntimeError("Original script backup validation failed: " + name)

    (ISOLATED / "config.json").write_text(json.dumps({
        "schemaVersion": 1, "owner": "LWBridgeRebuild",
        "profileId": PROFILE, "gameRoot": str(GAME),
        "autoLaunchGame": False, "autoReconnect": False,
        "gameDesiredRunning": False, "serverJumpHistory": [],
    }, indent=2) + "\n", encoding="utf-8")
    with sqlite3.connect(ISOLATED / "controller.db") as db:
        db.executescript("""
          CREATE TABLE controller_state(key TEXT PRIMARY KEY,value TEXT NOT NULL);
          CREATE TABLE profiles (id TEXT PRIMARY KEY,display_name TEXT NOT NULL,
            role_name TEXT,server_id TEXT,game_uid TEXT UNIQUE,note TEXT NOT NULL DEFAULT '',
            display_order INTEGER NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,
            locked_reason TEXT,is_primary INTEGER NOT NULL DEFAULT 0,
            created_at INTEGER NOT NULL,updated_at INTEGER NOT NULL,last_launched_at INTEGER);
          CREATE UNIQUE INDEX idx_profiles_primary ON profiles(is_primary) WHERE is_primary=1;
        """)
        now = int(time.time() * 1000)
        db.execute("""INSERT INTO profiles
            (id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at)
            VALUES (?,?,0,0,'MAP010_ARCHIVED_NAME_PROOF',1,?,?)""",
                   (PROFILE, "MAP-010 archived native records", now, now))
        db.execute("INSERT INTO controller_state VALUES('selected_profile_id',?)", (PROFILE,))

    dbpath = ISOLATED / "profiles" / PROFILE / "map-data/map-data.db"
    dbpath.parent.mkdir(parents=True)
    with sqlite3.connect(f"file:{SOURCE.as_posix()}?mode=ro", uri=True) as source:
        with sqlite3.connect(dbpath) as destination:
            source.backup(destination)
    (DEST / "preflight.json").write_text(json.dumps({
        "task": "LWB317-MAP-BACKGROUND-ACQUISITION-010",
        "isolated": True, "profileInitiallyDisabled": True,
        "autoLaunch": False, "autoReconnect": False,
        "sourceKind": "historical genuine R5 private stored SQLite read-only backup",
        "sourceSha256": sha(SOURCE), "copiedSha256": sha(dbpath),
        "positiveKindCountsBefore": counts,
        "gameScriptHashBefore": HASHES,
        "clientContentVersion": compatibility["currentClient"]["contentVersion"],
        "processesBefore": [],
    }, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"prepared": True, "profile": PROFILE, "root": str(ISOLATED),
                      "genuineResourceMonsterRecords": counts, "noGameStarted": True}))


def finish():
    result = {
        "task": "LWB317-MAP-BACKGROUND-ACQUISITION-010",
        "processesRemaining": [],
        "scriptHashesRestored": {name: sha(SCRIPTS / name) for name in HASHES} == HASHES,
        "journalPresent": (ISOLATED / "overview-bridge/recovery.json").exists(),
        "auto": json.loads((ISOLATED / "config.json").read_text()).get("autoLaunchGame"),
        "preservedTaskRoot": str(DEST),
    }
    sys.path.insert(0, str(ROOT / "tools"))
    import home_004_f04_v24_gate as previous
    result["processesRemaining"] = previous.actors()
    (DEST / "finish.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))
    if result["processesRemaining"] or not result["scriptHashesRestored"] or result["journalPresent"] or result["auto"]:
        raise RuntimeError("Task owner/script/Auto cleanup not established")


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "finish"}:
        raise SystemExit(__doc__)
    {"prepare": prepare, "finish": finish}[sys.argv[1]]()
