"""Bounded MAP-005 official-current-client pilot.

Actions: prepare | enable | snapshot | check-stop | check-clear | reentry |
record-city | record-resource | finish.

This script never installs or launches a game. It validates exact original
identity, backs up the mutable Lua triplet and owns only a fresh, isolated
LWBridge profile. The normal packaged Home and Map controls own game lifecycle.
"""
import json
import hashlib
import shutil
import sqlite3
import sys
import time
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

import home_004_f04_v24_gate as known

ROOT = Path(__file__).resolve().parents[1] / "artifacts" / "map-005" / "genuine-pilot"
ISOLATED = ROOT / "isolated"
PREFLIGHT = ROOT / "preflight.json"
PROFILE = "map-005-genuine-pilot"
MAPDB = ISOLATED / "profiles" / PROFILE / "map-data" / "map-data.db"


def prepare():
    if ROOT.exists():
        raise RuntimeError("MAP-005 task root already exists; keep the prior receipt")
    if known.actors():
        raise RuntimeError("A game, launcher or desktop host is active: " + repr(known.actors()))
    if known.prior.prior.sha(known.prior.prior.TARGET) != known.prior.prior.REFERENCE:
        raise RuntimeError("Original 0.3.17 reference identity changed")
    if known.scripts_hashes() != known.EXPECTED:
        raise RuntimeError("Original installed Lua triplet changed")
    compatibility = known.check_only()
    ISOLATED.mkdir(parents=True)
    backup = ROOT / "backup-original"
    backup.mkdir()
    for name, sha in known.EXPECTED.items():
        shutil.copy2(known.prior.prior.SCRIPT / name, backup / name)
        if known.prior.prior.sha(backup / name) != sha:
            raise RuntimeError("Task backup checksum failed: " + name)
    (ISOLATED / "config.json").write_text(json.dumps(dict(
        schemaVersion=1, owner="LWBridgeRebuild", profileId=PROFILE,
        gameRoot=str(known.prior.prior.APP), autoLaunchGame=False, autoReconnect=False,
        gameDesiredRunning=False, serverJumpHistory=[]), indent=2), encoding="utf-8")
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
        stamp = int(time.time() * 1000)
        db.execute("""INSERT INTO profiles
            (id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at)
            VALUES (?,?,0,0,'TEST_UI_SETUP',1,?,?)""",
            (PROFILE, "Map 005 genuine pilot", stamp, stamp))
        db.execute("INSERT INTO controller_state VALUES('selected_profile_id',?)", (PROFILE,))
    data = {
        "task": "LWB317-MAP-MANUAL-SCAN-DELIVERY-005",
        "timeUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "isolatedRoot": str(ISOLATED), "profileId": PROFILE,
        "gameRoot": str(known.prior.prior.APP), "gameVersion": compatibility["currentClient"]["contentVersion"],
        "originalHashes": known.EXPECTED,
        "backupHashes": {name: known.prior.prior.sha(backup / name) for name in known.EXPECTED},
        "compatibility": compatibility["ok"],
        "ownerProcessesBefore": known.actors(),
        "initialProfileDisabled": True,
        "initialAutoLaunch": False,
    }
    PREFLIGHT.write_text(json.dumps(data, indent=2), encoding="utf-8")
    print(json.dumps(data, indent=2))


def enable():
    if known.actors():
        raise RuntimeError("Game, launcher or desktop still running; close preliminary UI first")
    saved = json.loads((ISOLATED / "config.json").read_text(encoding="utf-8"))
    if saved.get("autoLaunchGame") or saved.get("gameDesiredRunning"):
        raise RuntimeError("Auto Launch and desired running must be OFF in the actual Home UI")
    with sqlite3.connect(ISOLATED / "controller.db") as db:
        state = db.execute("SELECT enabled,locked_reason FROM profiles WHERE id=?", (PROFILE,)).fetchone()
        if state != (0, "TEST_UI_SETUP"):
            raise RuntimeError("Preflight disabled owner state changed: " + repr(state))
        db.execute("UPDATE profiles SET enabled=1,locked_reason=NULL WHERE id=?", (PROFILE,))
    print(json.dumps({"profileId": PROFILE, "enabled": True}))


def finish():
    preflight = json.loads(PREFLIGHT.read_text(encoding="utf-8"))
    hashes = known.scripts_hashes()
    journal = ISOLATED / "overview-bridge" / "recovery.json"
    receipt = {
        "task": preflight["task"],
        "timeUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "ownedProcessesRemaining": known.actors(),
        "restored": hashes == preflight["originalHashes"],
        "originalHashes": hashes,
        "journalExists": journal.exists(),
        "isolatedRootRetained": str(ISOLATED),
    }
    (ROOT / "finish.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if receipt["ownedProcessesRemaining"] or not receipt["restored"] or receipt["journalExists"]:
        raise RuntimeError("MAP-005: game ownership, journal or original restoration unresolved")


def counts(db):
    with sqlite3.connect(db) as connection:
        return dict(connection.execute("SELECT kind,COUNT(*) FROM map_records GROUP BY kind"))


def snapshot():
    backup = ROOT / "published-snapshot.db"
    receipt_path = ROOT / "published-snapshot.json"
    if backup.exists() or receipt_path.exists():
        raise RuntimeError("Previously captured published dataset must be preserved")
    if not MAPDB.is_file():
        raise RuntimeError("Isolated live Map database missing")
    original_counts = counts(MAPDB)
    with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as original:
        servers = original.execute("SELECT DISTINCT server_id FROM map_records").fetchall()
    if len(servers) != 1:
        raise RuntimeError("Fresh scan has missing or mixed published server identity")
    server_id = int(servers[0][0])
    if original_counts.get("city", 0) <= 0 or original_counts.get("resource", 0) <= 0:
        raise RuntimeError("Cannot snapshot without positive genuine City and Resource results")
    with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as original:
        with sqlite3.connect(backup) as destination:
            original.backup(destination)
    if counts(backup) != original_counts:
        raise RuntimeError("Published SQLite backup counts differ")
    receipt = {
        "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "server": server_id,
        "publishedCounts": original_counts,
        "path": str(backup),
        "sha256": hashlib.sha256(backup.read_bytes()).hexdigest(),
    }
    receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))


def check_stop():
    pre = json.loads((ROOT / "published-snapshot.json").read_text(encoding="utf-8"))
    current = counts(MAPDB)
    with sqlite3.connect(MAPDB) as db:
        last = db.execute("SELECT status,total_blocks,completed_blocks FROM scan_runs "
                          "ORDER BY created_at DESC LIMIT 1").fetchone()
        staged = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
    receipt = {
        "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "lastRun": last, "stagedRecords": staged,
        "publishedAfterStop": current, "expectedBeforeStop": pre["publishedCounts"],
        "unchanged": current == pre["publishedCounts"],
    }
    (ROOT / "active-stop.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if not last or last[0] != "cancelled" or staged or not receipt["unchanged"]:
        raise RuntimeError("Stop failed to discard staged results without changing published records")


def check_clear():
    with sqlite3.connect(MAPDB) as db:
        deleted = {name: db.execute(f"SELECT COUNT(*) FROM {name}").fetchone()[0]
                   for name in ("map_records", "scan_records", "scan_runs")}
    receipt = {
        "createdUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "remaining": deleted, "preservedEvidence": str(ROOT / "published-snapshot.db")
    }
    (ROOT / "task-clear.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if any(deleted.values()):
        raise RuntimeError("Actual task Clear left Map published or staged scan records")


def reentry():
    preflight = json.loads(PREFLIGHT.read_text(encoding="utf-8"))
    if known.actors():
        raise RuntimeError("Task already has active game or host")
    if known.scripts_hashes() != preflight["originalHashes"]:
        raise RuntimeError("Original Lua identity changed before second genuine scan")
    if not known.check_only()["ok"]:
        raise RuntimeError("Current official game compatibility failed")
    if (ISOLATED / "overview-bridge" / "recovery.json").exists():
        raise RuntimeError("Pending recovery journal must be resolved before reentry")
    cfg = json.loads((ISOLATED / "config.json").read_text(encoding="utf-8"))
    if cfg["profileId"] != PROFILE or cfg["autoLaunchGame"] or cfg["autoReconnect"]:
        raise RuntimeError("Task profile config or launch isolation changed")
    with sqlite3.connect(ISOLATED / "controller.db") as db:
        state = db.execute("SELECT enabled FROM profiles WHERE id=?", (PROFILE,)).fetchone()
        if state != (1,):
            raise RuntimeError("Task profile not safely enabled")
    with sqlite3.connect(MAPDB) as db:
        jobs = [db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
                for table in ("dispatch_plunder_jobs", "truck_plunder_jobs")]
    if any(jobs):
        raise RuntimeError("Task database contains actionable scheduled jobs")
    print(json.dumps({"task": preflight["task"], "originalHashes": "verified",
        "compatibility": True, "ownedProcesses": [], "profile": PROFILE,
        "autoLaunch": False, "autoReconnect": False, "scheduledJobs": 0}))


def record_kind(kind):
    receipt_path = ROOT / f"separate-{kind}-completed.json"
    if receipt_path.exists():
        raise RuntimeError("Separate genuine run receipt already exists")
    with sqlite3.connect(MAPDB) as db:
        run = db.execute("""
            SELECT id,server_id,status,selected_types,total_blocks,
                   completed_blocks,failed_blocks,created_at,updated_at
            FROM scan_runs ORDER BY created_at DESC LIMIT 1
            """).fetchone()
        if not run:
            raise RuntimeError("No individual genuine scan found")
        identifiers = db.execute("SELECT COUNT(*),COUNT(DISTINCT record_key) FROM map_records "
                                 "WHERE kind=? AND server_id=?", (kind, run[1])).fetchone()
        staging = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
        matching = db.execute("SELECT COUNT(*) FROM scan_runs WHERE id=? AND status='completed'",
                              (run[0],)).fetchone()[0]
    if run[2] != "completed" or json.loads(run[3]) != [kind] or run[4] <= 0 or run[4] != run[5] or run[6] or staging or matching != 1 or identifiers[0] <= 0 or identifiers[0] != identifiers[1]:
        raise RuntimeError("Latest actual run is incomplete or not a positive standalone " + kind)
    receipt = {
        "evidenceType": "genuine-official-current-server-packaged-standalone",
        "kind": kind, "runId": run[0], "serverId": run[1],
        "status": run[2], "selectedTypesJson": run[3], "totalBlocks": run[4],
        "completedBlocks": run[5], "failedBlocks": run[6],
        "createdAtMilliseconds": run[7], "updatedAtMilliseconds": run[8],
        "uiObservedMode": "normal" if kind == "city" else "fast",
        "worldId": "not persisted in Map317 scan_runs schema",
        "freshPublishedCount": identifiers[0],
        "uniquePublishedKeys": identifiers[1], "stagedRecords": staging,
        "publishedCountsForServer": counts(MAPDB),
    }
    receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))


def verify_workbooks():
    receipt_path = ROOT / "reopened-city-workbooks.json"
    evidence = {}
    expected = {
        "actual-packaged-ui-city-save-as": (ROOT / "ui-city-en-light.xlsx", 7052),
        "separate-current-server-native-export": (
            ROOT / "native-separate-audit" / "city-native-audit.xlsx", 7058),
    }
    for key, (workbook, expected_count) in expected.items():
        with zipfile.ZipFile(workbook) as archive:
            sheet = ET.fromstring(archive.read("xl/worksheets/sheet1.xml"))
        ns = {"x": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
        rows = sheet.findall(".//x:sheetData/x:row", ns)
        if not rows:
            raise RuntimeError(f"No worksheet header in {key}")
        data_count = len(rows) - 1
        header_columns = len(rows[0].findall("x:c", ns))
        if data_count != expected_count or header_columns != 12:
            raise RuntimeError(f"Reopened City workbook shape mismatched: {key}")
        evidence[key] = dict(
            sha256=hashlib.sha256(workbook.read_bytes()).hexdigest(),
            dataRows=data_count, headerColumns=header_columns,
            reopenedWithStandardZipXml=True)
    receipt_path.write_text(json.dumps(evidence, indent=2), encoding="utf-8")
    print(json.dumps(evidence, indent=2))


if __name__ == "__main__":
    actions = {"prepare": prepare, "enable": enable, "snapshot": snapshot,
               "check-stop": check_stop, "check-clear": check_clear,
               "reentry": reentry, "check-xlsx": verify_workbooks,
               "record-city": lambda: record_kind("city"),
               "record-resource": lambda: record_kind("resource"), "finish": finish}
    if len(sys.argv) != 2 or sys.argv[1] not in actions:
        raise SystemExit(__doc__)
    actions[sys.argv[1]]()
