"""Isolated MAP-006 live passive Map/Auto witness gates (no game launch here).

Usage: python tools/map_006_live_gate.py prepare|enable|reentry|status|audit <label>|audit-cycle <label>|audit-stop <label>|finish
       python tools/map_006_live_gate.py --trial <safe_label> prepare|enable|reentry|status|audit <label>|audit-cycle <label>|audit-stop <label>|finish

The normal source-identified LWBridge Desktop Home/Map UI owns every genuine
game action. This helper verifies exact compatibility/restoration and reads
task-only SQLite; it never starts scans, changes servers, claims or plunders.
No data, profile, backup or private rows are copied into Git or the ZIP.
"""

import hashlib
import json
import shutil
import sqlite3
import sys
import time
from pathlib import Path

import home_004_f04_v24_gate as known

ROOT = Path(__file__).resolve().parents[1] / "artifacts" / "map-006" / "genuine-pilot"
ISOLATED = ROOT / "isolated"
PREFLIGHT = ROOT / "preflight.json"
PROFILE = "map-006-genuine-pilot"
MAPDB = ISOLATED / "profiles" / PROFILE / "map-data" / "map-data.db"
AUTO = ISOLATED / "profiles" / PROFILE / "runtime" / "map-auto-scan.json"
TASK = "LWB317-MAP-COMPLETE-DELIVERY-006"
KINDS = {"city", "resource", "monster", "truck", "railway", "dispatch", "ghost", "treasure"}


def use_separate_trial(label):
    """Preserve the original MAP-006 pilot and use a brand-new isolated root."""
    global ROOT, ISOLATED, PREFLIGHT, PROFILE, MAPDB, AUTO
    if (not label.isidentifier() or label.startswith("_") or len(label) > 40 or
            not label.isascii()):
        raise RuntimeError("Trial label must be a short ASCII identifier")
    ROOT = Path(__file__).resolve().parents[1] / "artifacts" / "map-006" / ("trial-" + label)
    ISOLATED = ROOT / "isolated"
    PREFLIGHT = ROOT / "preflight.json"
    PROFILE = "map-006-" + label
    MAPDB = ISOLATED / "profiles" / PROFILE / "map-data" / "map-data.db"
    AUTO = ISOLATED / "profiles" / PROFILE / "runtime" / "map-auto-scan.json"


def utc_stamp():
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def validate_original():
    if known.actors():
        raise RuntimeError("Active game/launcher/host process: " + repr(known.actors()))
    if known.prior.prior.sha(known.prior.prior.TARGET) != known.prior.prior.REFERENCE:
        raise RuntimeError("Original 0.3.17 reference hash changed")
    if known.scripts_hashes() != known.EXPECTED:
        raise RuntimeError("Original installed Lua triplet not restored")
    compatible = known.check_only()
    if not compatible["ok"]:
        raise RuntimeError("Official current-client compatibility gate failed")
    return compatible


def prepare():
    if ROOT.exists():
        raise RuntimeError("MAP-006 isolated pilot already exists; preserve evidence")
    compatibility = validate_original()
    ISOLATED.mkdir(parents=True)
    backup = ROOT / "backup-original"
    backup.mkdir()
    for name, expected in known.EXPECTED.items():
        shutil.copy2(known.prior.prior.SCRIPT / name, backup / name)
        if known.prior.prior.sha(backup / name) != expected:
            raise RuntimeError("MAP-006 backup hash did not match reference: " + name)

    (ISOLATED / "config.json").write_text(json.dumps({
        "schemaVersion": 1,
        "owner": "LWBridgeRebuild",
        "profileId": PROFILE,
        "gameRoot": str(known.prior.prior.APP),
        "autoLaunchGame": False,
        "autoReconnect": False,
        "gameDesiredRunning": False,
        "serverJumpHistory": [],
    }, indent=2), encoding="utf-8")
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
            VALUES (?,?,0,0,'TEST_UI_SETUP',1,?,?)""",
            (PROFILE, "Map 006 genuine passive pilot", now, now))
        db.execute("INSERT INTO controller_state VALUES('selected_profile_id',?)", (PROFILE,))

    receipt = {
        "task": TASK, "timeUtc": utc_stamp(), "isolatedRoot": str(ISOLATED),
        "profileId": PROFILE, "clientContentVersion": compatibility["currentClient"]["contentVersion"],
        "gameSha256": compatibility["currentClient"]["gameSha256"],
        "originalHashes": known.EXPECTED,
        "backupHashes": {name: known.prior.prior.sha(backup / name) for name in known.EXPECTED},
        "compatibility": compatibility["ok"],
        "initialProfileEnabled": False, "autoLaunch": False, "autoReconnect": False,
        "ownedProcesses": [], "permittedActions": ["passive current-server Map", "bounded passive multi-server Auto and return"],
    }
    PREFLIGHT.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))


def enable():
    if known.actors():
        raise RuntimeError("Close preliminary UI before enabling pilot profile")
    config = json.loads((ISOLATED / "config.json").read_text(encoding="utf-8"))
    if config.get("autoLaunchGame") or config.get("autoReconnect") or config.get("gameDesiredRunning"):
        raise RuntimeError("Passive pilot requires all Home automatic behaviors OFF")
    with sqlite3.connect(ISOLATED / "controller.db") as db:
        row = db.execute("SELECT enabled,locked_reason FROM profiles WHERE id=?", (PROFILE,)).fetchone()
        if row != (0, "TEST_UI_SETUP"):
            raise RuntimeError("Unexpected task profile state " + repr(row))
        db.execute("UPDATE profiles SET enabled=1,locked_reason=NULL WHERE id=?", (PROFILE,))
    print(json.dumps({"task": TASK, "profileId": PROFILE, "enabled": True}))


def owned_job_counts():
    if not MAPDB.exists():
        return {}
    with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as db:
        return {table: db.execute("SELECT COUNT(*) FROM " + table).fetchone()[0]
                for table in ("dispatch_plunder_jobs", "truck_plunder_jobs")}


def auto_state():
    if not AUTO.exists():
        return {"enabled": False, "running": False}
    document = json.loads(AUTO.read_text(encoding="utf-8"))
    config = document.get("config") or {}
    restart = document.get("restart") or {}
    return {"enabled": config.get("enabled") is True,
            "running": restart.get("active") is True,
            "nextRunAt": config.get("nextRunAt", 0)}


def reentry():
    previous = json.loads(PREFLIGHT.read_text(encoding="utf-8"))
    compatibility = validate_original()
    if known.scripts_hashes() != previous["originalHashes"] or not compatibility["ok"]:
        raise RuntimeError("MAP-006 original identity/compatibility changed")
    if (ISOLATED / "overview-bridge" / "recovery.json").exists():
        raise RuntimeError("Pending owner restoration journal exists")
    config = json.loads((ISOLATED / "config.json").read_text(encoding="utf-8"))
    if config.get("profileId") != PROFILE or config.get("autoLaunchGame") or config.get("autoReconnect"):
        raise RuntimeError("Task profile ID or Home isolation changed")
    if auto_state()["enabled"] or auto_state()["running"]:
        raise RuntimeError("Recurring Auto Scan must be OFF at reentry")
    if any(owned_job_counts().values()):
        raise RuntimeError("Actionable/plunder job data present in task database")
    print(json.dumps({"task": TASK, "compatibility": True, "originalHashes": "verified",
                      "ownedProcesses": [], "autoEnabled": False, "actionJobs": 0}))


def status():
    """Read only summary of the task's live Map/Auto state (never a game command)."""
    result = {"task": TASK, "timeUtc": utc_stamp(), "profileId": PROFILE,
              "auto": auto_state(), "actionJobs": owned_job_counts()}
    if MAPDB.exists():
        with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as db:
            try:
                row = db.execute("""SELECT id,server_id,status,selected_types,total_blocks,
                    completed_blocks,failed_blocks,created_at,updated_at,error
                    FROM scan_runs ORDER BY created_at DESC LIMIT 1""").fetchone()
                result["latestScan"] = dict(zip(
                    ("runId", "serverId", "state", "selectedTypes", "totalBlocks",
                     "completedBlocks", "failedBlocks", "createdAt", "updatedAt", "error"), row)) if row else None
                result["publishedCounts"] = {f"{kind}:{server}": count for kind, server, count in
                    db.execute("SELECT kind,server_id,COUNT(*) FROM map_records GROUP BY kind,server_id")}
                result["stagedRows"] = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
            except sqlite3.OperationalError as error:
                result["readStatus"] = str(error)
    print(json.dumps(result, indent=2))


def audit(label):
    if not label.isidentifier() or label.startswith("_"):
        raise RuntimeError("Audit label must be a simple unique identifier")
    out = ROOT / ("scan-" + label + ".json")
    if out.exists():
        raise RuntimeError("Cannot overwrite an existing genuine scan receipt")
    if not MAPDB.is_file():
        raise RuntimeError("No genuine task Map SQLite yet")
    with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as db:
        run = db.execute("""SELECT id,server_id,status,selected_types,total_blocks,
            completed_blocks,failed_blocks,created_at,updated_at,error
            FROM scan_runs ORDER BY created_at DESC LIMIT 1""").fetchone()
        rows = db.execute("""SELECT kind,server_id,COUNT(*),COUNT(DISTINCT record_key)
            FROM map_records GROUP BY kind,server_id ORDER BY kind,server_id""").fetchall()
        staged = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
    if run is None:
        raise RuntimeError("No actual scan run to record")
    kinds = json.loads(run[3])
    if not isinstance(kinds, list) or not kinds or any(kind not in KINDS for kind in kinds):
        raise RuntimeError("Unexpected selected scan kinds")
    positive = [kind for kind, server, count, unique in rows
                if server == run[1] and kind in kinds and count > 0 and count == unique]
    success = run[2] == "completed" and run[4] > 0 and run[5] == run[4] and run[6] == 0 and staged == 0
    receipt = {
        "task": TASK, "evidenceType": "genuine-packaged-official-current-client",
        "timeUtc": utc_stamp(), "label": label,
        "runId": run[0], "serverId": run[1], "state": run[2],
        "selectedTypes": kinds, "totalBlocks": run[4], "completedBlocks": run[5],
        "failedBlocks": run[6], "createdAt": run[7], "updatedAt": run[8],
        "error": run[9], "stagedRows": staged,
        "publishedCounts": {f"{kind}:{server}": count for kind, server, count, unique in rows},
        "publishedKeyUniqueness": all(count == unique for kind, server, count, unique in rows),
        "positiveSelectedKinds": positive, "completePublication": success,
        "actionJobs": owned_job_counts(),
    }
    out.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if not success:
        raise RuntimeError("Recorded unsuccessful scan; preserve receipt and diagnose")


def audit_cycle(label):
    """Freeze an actual native multi-server Auto result with exact target run identities."""
    if not label.isidentifier() or label.startswith("_") or not label.isascii():
        raise RuntimeError("Auto cycle audit label must be a simple ASCII identifier")
    out = ROOT / ("auto-cycle-" + label + ".json")
    if out.exists():
        raise RuntimeError("Cannot overwrite existing Auto cycle receipt")
    document = json.loads(AUTO.read_text(encoding="utf-8"))
    config, cycle = document.get("config") or {}, document.get("lastCycle") or {}
    servers = cycle.get("targetServerIds") or []
    completed = cycle.get("completedServerIds") or []
    if not servers or not isinstance(servers, list) or not all(type(s) is int for s in servers):
        raise RuntimeError("Cannot audit Auto without an actual target server list")
    runs = []
    with sqlite3.connect(f"file:{MAPDB.as_posix()}?mode=ro", uri=True) as db:
        for run in db.execute("""SELECT id,server_id,status,selected_types,total_blocks,
            completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs
            WHERE created_at>=? AND created_at<=? ORDER BY created_at,id""",
            (cycle.get("startedAt", 0), cycle.get("completedAt", 0))):
            runs.append(dict(zip(("runId", "serverId", "state", "selectedTypes", "totalBlocks",
                                  "completedBlocks", "failedBlocks", "createdAt", "updatedAt", "error"), run)))
        counts = {f"{kind}:{server}": {"count": count, "uniqueKeys": distinct}
                  for kind, server, count, distinct in db.execute("""SELECT kind,server_id,
                       COUNT(*),COUNT(DISTINCT record_key) FROM map_records GROUP BY kind,server_id""")}
        staging = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
    for run in runs:
        run["selectedTypes"] = json.loads(run["selectedTypes"])
        run["elapsedMilliseconds"] = run["updatedAt"] - run["createdAt"]
    jump_path = ISOLATED / "overview-bridge" / "server-jump-result.json"
    jump = json.loads(jump_path.read_text(encoding="utf-8")) if jump_path.is_file() else {}
    return_evidence = {name: jump.get(name) for name in
                       ("state", "serverId", "previousServerId", "currentServerId", "changed", "method")}
    valid_runs = (len(runs) == len(servers) and [r["serverId"] for r in runs] == servers and
                  all(r["state"] == "completed" and r["totalBlocks"] == 2500 and
                      r["completedBlocks"] == r["totalBlocks"] and r["failedBlocks"] == 0 and
                      r["error"] is None and r["selectedTypes"] == config.get("selectedTypes")
                      for r in runs))
    complete = (cycle.get("outcome") == "completed" and cycle.get("lastError") is None and
                servers == completed and valid_runs and staging == 0 and
                not auto_state()["enabled"] and not auto_state()["running"] and
                not any(owned_job_counts().values()) and
                (not config.get("returnToOriginalServer") or
                 (jump.get("state") == "proven" and jump.get("serverId") == cycle.get("originalServerId")
                  and jump.get("currentServerId") == cycle.get("originalServerId"))))
    receipt = {"task": TASK, "evidenceType": "genuine-packaged-official-current-client",
               "timeUtc": utc_stamp(), "label": label, "trialProfileId": PROFILE,
               "configuredTypes": config.get("selectedTypes"), "cycle": cycle, "runs": runs,
               "publishedCounts": counts, "stagedRows": staging,
               "returnResult": return_evidence, "completeMultiServerPublication": complete,
               "auto": auto_state(), "actionJobs": owned_job_counts()}
    out.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if not complete:
        raise RuntimeError("Auto cycle evidence incomplete; preserve receipt")


def audit_stop(label):
    """Prove an exact cancelled Auto run preserved a separate completed baseline."""
    if not label.isidentifier() or label.startswith("_") or not label.isascii():
        raise RuntimeError("Auto Stop audit label must be a simple ASCII identifier")
    out = ROOT / ("auto-stop-" + label + ".json")
    if out.exists():
        raise RuntimeError("Cannot overwrite an existing Auto Stop witness")
    backup = ROOT / "published-before-auto-stop.db"
    if not backup.is_file():
        raise RuntimeError("No separately backed up genuine completed baseline")
    auto = json.loads(AUTO.read_text(encoding="utf-8"))
    cycle = auto.get("lastCycle") or {}
    def inspect(path):
        with sqlite3.connect(f"file:{path.as_posix()}?mode=ro", uri=True) as db:
            counts = {f"{kind}:{server}": (count, unique) for kind,server,count,unique in
                db.execute("SELECT kind,server_id,COUNT(*),COUNT(DISTINCT record_key) FROM map_records GROUP BY kind,server_id")}
            runs = [dict(zip(("runId", "serverId", "state", "selectedTypes", "totalBlocks", "completedBlocks", "failedBlocks", "createdAt", "updatedAt", "error"), row)) for row in
                    db.execute("SELECT id,server_id,status,selected_types,total_blocks,completed_blocks,failed_blocks,created_at,updated_at,error FROM scan_runs ORDER BY created_at")]
            staged = db.execute("SELECT COUNT(*) FROM scan_records").fetchone()[0]
            return counts, runs, staged
    before, completed, prior_staged = inspect(backup)
    after, runs, staged = inspect(MAPDB)
    for run in runs:
        run["selectedTypes"] = json.loads(run["selectedTypes"])
    new_runs = [r for r in runs if r["runId"] not in {r["runId"] for r in completed}]
    cycle_runs = [r for r in new_runs if cycle.get("startedAt", 0) <= r["createdAt"] <= cycle.get("completedAt", 0)]
    latest = cycle_runs[-1] if cycle_runs else None
    success = (bool(before) and all(v[0] == v[1] for v in before.values()) and
               before == after and staged == prior_staged == 0 and len(cycle_runs) == 1 and
               latest["state"] == "cancelled" and latest["selectedTypes"] == ["truck"] and
               latest["serverId"] == cycle.get("originalServerId") and
               latest["completedBlocks"] < latest["totalBlocks"] == 2500 and
               latest["error"] is None and cycle.get("outcome") == "canceled" and
               cycle.get("completedServerIds") == [] and cycle.get("lastError") is None and
               cycle.get("targetServerIds") == [2212,2213] and
               not auto_state()["enabled"] and not auto_state()["running"] and
               not any(owned_job_counts().values()))
    receipt = {"task": TASK, "evidenceType": "genuine-packaged-current-client-Auto-Stop",
               "timeUtc": utc_stamp(), "profileId": PROFILE, "label": label,
               "baselineSha256": hashlib.sha256(backup.read_bytes()).hexdigest(),
               "beforeStoredCounts": {k:v[0] for k,v in before.items()},
               "afterStoredCounts": {k:v[0] for k,v in after.items()},
               "cancelledNewRuns": cycle_runs, "priorInterruptedRuns": len(new_runs)-len(cycle_runs),
               "lastCycle": cycle, "stagedRows": staged,
               "auto": auto_state(), "actionJobs": owned_job_counts(),
               "exactOwnedStopPreservedData": success}
    out.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if not success:
        raise RuntimeError("Genuine Auto Stop failed its exact ownership/publication gate")


def finish():
    previous = json.loads(PREFLIGHT.read_text(encoding="utf-8"))
    actual_hashes = known.scripts_hashes()
    receipt = {
        "task": TASK, "timeUtc": utc_stamp(),
        "processes": known.actors(),
        "originalHashesRestored": actual_hashes == previous["originalHashes"],
        "originalHashes": actual_hashes,
        "journalExists": (ISOLATED / "overview-bridge" / "recovery.json").exists(),
        "auto": auto_state(), "actionJobs": owned_job_counts(),
    }
    (ROOT / "finish.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))
    if (receipt["processes"] or not receipt["originalHashesRestored"] or
            receipt["journalExists"] or receipt["auto"]["enabled"] or
            receipt["auto"]["running"] or any(receipt["actionJobs"].values())):
        raise RuntimeError("MAP-006 unfinished cleanup/restoration or active automation")


if __name__ == "__main__":
    actions = {"prepare": prepare, "enable": enable, "reentry": reentry,
               "status": status, "finish": finish}
    if len(sys.argv) >= 4 and sys.argv[1] == "--trial":
        use_separate_trial(sys.argv[2])
        sys.argv = [sys.argv[0], *sys.argv[3:]]
    if len(sys.argv) == 3 and sys.argv[1] == "audit":
        audit(sys.argv[2])
    elif len(sys.argv) == 3 and sys.argv[1] == "audit-cycle":
        audit_cycle(sys.argv[2])
    elif len(sys.argv) == 3 and sys.argv[1] == "audit-stop":
        audit_stop(sys.argv[2])
    elif len(sys.argv) == 2 and sys.argv[1] in actions:
        actions[sys.argv[1]]()
    else:
        raise SystemExit(__doc__)
