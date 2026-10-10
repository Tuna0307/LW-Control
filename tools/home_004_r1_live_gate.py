"""Owner-authorized bounded Home R1 preflight, admission, restoration receipts.

Only modifies its fresh ignored task profile. Installed script files are read
and backed up; game mutations belong solely to the packaged native helper.
"""
import csv
import hashlib
import json
import os
import shutil
import sqlite3
import subprocess
import sys
import time
from pathlib import Path
import home_launch_002_gate as prior

repo = Path(__file__).resolve().parents[1]
task = repo / "artifacts" / "home-004" / "r1-live"
profile_root = task / "isolated"
receipt_file = task / "preflight.json"
profile = "home-004-r1-live"
def read():
    return json.loads(receipt_file.read_text(encoding="utf-8"))
def hashes():
    return {name: prior.sha(prior.SCRIPT / name) for name in prior.FILES}
def owners():
    return prior.owners()
def prepare():
    if task.exists():
        raise RuntimeError("Existing R1 task data: preserve it; do not overwrite or auto-delete.")
    if owners():
        raise RuntimeError("Active game/launcher/clone identity; stop before backup: " + str(owners()))
    if prior.sha(prior.TARGET) != prior.REFERENCE:
        raise RuntimeError("Original reference 0.3.17 executable hash changed")
    observed = hashes()
    if observed != prior.EXPECTED:
        raise RuntimeError("Installed original script hashes differ; do not launch: " + str(observed))
    for name in ("Game/LastWar.exe", "LastWarLauncher.exe", "Game/LastWar_Data/Plugins/x86_64/xlua.dll"):
        if not (prior.APP / name).is_file():
            raise RuntimeError("Verified current game path missing: " + name)
    task.mkdir(parents=True)
    (task / "backup-original").mkdir()
    profile_root.mkdir()
    for name in prior.FILES:
        shutil.copy2(prior.SCRIPT / name, task / "backup-original" / name)
        if prior.sha(task / "backup-original" / name) != observed[name]:
            raise RuntimeError("Exact backup copy validation failed: " + name)
    config = dict(schemaVersion=1, owner="LWBridgeRebuild", profileId=profile,
                  gameRoot=str(prior.APP), autoLaunchGame=False, autoReconnect=False,
                  gameDesiredRunning=False, serverJumpHistory=[])
    (profile_root / "config.json").write_text(json.dumps(config, indent=2), encoding="utf-8")
    with sqlite3.connect(profile_root / "controller.db") as db:
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
        db.execute("INSERT INTO profiles(id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at) VALUES(?,?,0,0,'TEST_UI_SETUP',1,?,?)",
                   (profile, "Home 004 R1 isolated", stamp, stamp))
        db.execute("INSERT INTO controller_state VALUES('selected_profile_id',?)", (profile,))
    data = dict(timeUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                profileId=profile, isolatedRoot=str(profile_root), gameRoot=str(prior.APP),
                scriptFolder=str(prior.SCRIPT), referenceSha256=prior.REFERENCE,
                originalScriptHashes=observed,
                backupHashes={name: prior.sha(task / "backup-original" / name) for name in prior.FILES},
                expectedBeforeLaunchState="disabled-profile; real WebView Auto Launch default ON must be switched OFF in UI",
                originalClientCheck="run_overview_bridge_current.py check-only ok=true; version=23",
                appProcessesBefore=owners())
    receipt_file.write_text(json.dumps(data, indent=2), encoding="utf-8")
    print(json.dumps(dict(action="prepare", isolatedRoot=str(profile_root), profileId=profile,
                          backupVerified=True, gameProcesses=owners())))
def enable():
    receipt = read()
    if owners():
        raise RuntimeError("Host or game is running: refusing profile enable")
    cfg = json.loads((profile_root / "config.json").read_text())
    if cfg["autoLaunchGame"] or cfg["gameDesiredRunning"]:
        raise RuntimeError("Native mirror still auto-launches; turn OFF actual WebView control first")
    with sqlite3.connect(profile_root / "controller.db") as db:
        row = db.execute("SELECT enabled,locked_reason FROM profiles WHERE id=?", (profile,)).fetchone()
        if row != (0, "TEST_UI_SETUP"):
            raise RuntimeError("Selected registry state changed: " + repr(row))
        db.execute("UPDATE profiles SET enabled=1,locked_reason=NULL WHERE id=?", (profile,))
    print(json.dumps(dict(action="enable", profile=receipt["profileId"], enabled=True)))
def finish():
    receipt = read()
    current = hashes()
    active = owners()
    recovery = profile_root / "overview-bridge" / "recovery.json"
    actual = dict(action="finish", timeUtc=time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                  processesRemaining=active, installedScriptHashes=current,
                  expected=receipt["originalScriptHashes"],
                  exactScriptsRestored=current == receipt["originalScriptHashes"],
                  recoveryJournalExists=recovery.exists(), recoveryJournal=str(recovery),
                  taskDataPreserved=str(task))
    (task / "finish.json").write_text(json.dumps(actual, indent=2), encoding="utf-8")
    print(json.dumps(actual))
    if active or current != receipt["originalScriptHashes"] or recovery.exists():
        raise RuntimeError("RESTORATION_OR_OWNERSHIP_FAILED; preserve task backup and journal")
if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit(__doc__)
    {"prepare": prepare, "enable": enable, "finish": finish}[sys.argv[1]]()
