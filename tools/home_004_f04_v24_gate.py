"""One-shot, isolated HOME004 F-04 v24 game task: prepare | enable | finish.

Read-only installation preflight, exact backup, opt-in isolated config. Only the
packaged LWBridge native lifecycle may patch/restore a verified original Lua
triplet; this tool never installs, launches, terminates or mutates the game.
"""
from __future__ import annotations

import json
import os
import sqlite3
import subprocess
import sys
import time
from pathlib import Path

import home_004_r1_live_gate as prior

ROOT = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "f04-v24-genuine"
ISOLATED = ROOT / "isolated"
PREFLIGHT = ROOT / "preflight.json"
PROFILE = "home-004-f04-v24-genuine"
TASK_PROOF = "HOME004_F04_V24_GENUINE"
EXPECTED = {
    "LWScripts.data": "d520dcd3b6f2b2c02ec3c4e42c2dc34fada513a4af955337401a2868d90ab495",
    "LWScripts.txt": "b84cfa8762d9f8b93fcbe9e64b8715e84d1549248c2e98dc7dc272321476933b",
    "version.txt": "c2356069e9d1e79ca924378153cfbbfb4d4416b1f99d41a2940bfdb66c5319db",
}


def scripts_hashes():
    return {name: prior.prior.sha(prior.prior.SCRIPT / name) for name in prior.prior.FILES}


def actors():
    return prior.owners()


def check_only():
    result = subprocess.run(
        [sys.executable, str(prior.repo / "tools" / "run_overview_bridge_current.py"),
         "check-only", "--game-root", str(prior.prior.APP)],
        check=True, capture_output=True, text=True,
    )
    data = json.loads(result.stdout)
    assert data.get("ok") is True and data.get("installedFilesChanged") is False
    assert data["currentClient"]["contentVersion"] == 24
    assert data["currentClient"]["packageSha256"] == EXPECTED["LWScripts.data"]
    return data


def prepare():
    if ROOT.exists():
        raise RuntimeError("F-04 task root already exists; preserve the previous attempt")
    if actors():
        raise RuntimeError("Game or launcher already running; refusing preflight: " + repr(actors()))
    if prior.prior.sha(prior.prior.TARGET) != prior.prior.REFERENCE:
        raise RuntimeError("Protected original application SHA changed")
    if scripts_hashes() != EXPECTED:
        raise RuntimeError("v24 original baseline mismatch; inspect installed files without mutation")
    compatible = check_only()
    ROOT.mkdir(parents=True)
    ISOLATED.mkdir()
    backup = ROOT / "backup-original"
    backup.mkdir()
    import shutil
    for name in prior.prior.FILES:
        shutil.copy2(prior.prior.SCRIPT / name, backup / name)
        if prior.prior.sha(backup / name) != EXPECTED[name]:
            raise RuntimeError("Could not verify task backup: " + name)

    (ISOLATED / "config.json").write_text(json.dumps({
        "schemaVersion": 1, "owner": "LWBridgeRebuild", "profileId": PROFILE,
        "gameRoot": str(prior.prior.APP), "autoLaunchGame": False,
        "autoReconnect": False, "gameDesiredRunning": False,
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
        db.execute("INSERT INTO profiles(id,display_name,display_order,enabled,locked_reason,is_primary,created_at,updated_at) VALUES (?,?,0,0,'TEST_UI_SETUP',1,?,?)",
                   (PROFILE, PROFILE, now, now))
        db.execute("INSERT INTO controller_state(key,value) VALUES('selected_profile_id',?)", (PROFILE,))
    receipt = {
        "task": TASK_PROOF, "timeUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "root": str(ROOT), "isolatedRoot": str(ISOLATED), "profileId": PROFILE,
        "gameRoot": str(prior.prior.APP), "installedOriginalScriptHashes": EXPECTED,
        "backupHashes": {n: prior.prior.sha(backup / n) for n in prior.prior.FILES},
        "compatibility": {"ok": compatible["ok"], "contentVersion": 24,
                          "installedFilesChanged": compatible["installedFilesChanged"],
                          "packageSha256": compatible["currentClient"]["packageSha256"]},
        "ownerProcessesBefore": actors(), "admission": "profile disabled; auto-launch native OFF",
        "fault": "requires LWBRIDGE_HOME004_ISOLATED_ROUTE_FAULT=1 on only the isolated candidate",
    }
    PREFLIGHT.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))


def enable():
    if actors():
        raise RuntimeError("Refusing to enable while any game or launcher exists")
    current = json.loads((ISOLATED / "config.json").read_text(encoding="utf-8"))
    if current["autoLaunchGame"] or current["gameDesiredRunning"]:
        raise RuntimeError("AutoLaunch must be disabled through the actual Home UI before enabling")
    with sqlite3.connect(ISOLATED / "controller.db") as db:
        row = db.execute("SELECT enabled,locked_reason FROM profiles WHERE id=?", (PROFILE,)).fetchone()
        if row != (0, "TEST_UI_SETUP"):
            raise RuntimeError("Isolated registry no longer disabled: " + repr(row))
        db.execute("UPDATE profiles SET enabled=1,locked_reason=NULL WHERE id=?", (PROFILE,))
    print(json.dumps({"task": str(ROOT), "profile": PROFILE, "enabled": True}))


def finish():
    preflight = json.loads(PREFLIGHT.read_text(encoding="utf-8"))
    current = scripts_hashes()
    result = {
        "timeUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "processesRemaining": actors(), "originalHashesRestored": current == EXPECTED,
        "currentHashes": current, "expected": preflight["installedOriginalScriptHashes"],
        "recoveryJournalExists": (ISOLATED / "overview-bridge" / "recovery.json").exists(),
        "isolatedTaskPreserved": str(ROOT),
    }
    (ROOT / "finish.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    if result["processesRemaining"] or not result["originalHashesRestored"] or result["recoveryJournalExists"]:
        raise RuntimeError("OWNERSHIP_OR_RESTORATION_FAILED; preserve exact journal and backups")


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit(__doc__)
    {"prepare": prepare, "enable": enable, "finish": finish}[sys.argv[1]]()
