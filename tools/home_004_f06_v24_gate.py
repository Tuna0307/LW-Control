"""F-06 genuine replacement-bridge journal repair gate.

prepare/enable/finish delegate exact v24 package snapshots to the F-04 gate;
archive-adoption is an opt-in loss of ONLY the task's bridge authentication
record after that task's game and host have exact identities. No Lua edits,
official updater changes or game termination are performed here.
"""
from __future__ import annotations

import json
import os
import shutil
import sys

import home_004_f04_v24_gate as gate

gate.ROOT = gate.ROOT.parent / "f06-v24-genuine-repair"
gate.ISOLATED = gate.ROOT / "isolated"
gate.PREFLIGHT = gate.ROOT / "preflight.json"
gate.PROFILE = "home-004-f06-v24-repair"
gate.TASK_PROOF = "HOME004_F06_V24_GENUINE_REPAIR"


def archive_adoption():
    from pathlib import Path
    root = gate.ISOLATED / "overview-bridge"
    journal = root / "recovery.json"
    adoption = root / "adoption.json"
    dest = gate.ROOT / "task-owned-adoption-preserved.json"
    if dest.exists() or not journal.is_file() or not adoption.is_file():
        raise RuntimeError("A unique real task-owned adoption and journal are required")
    state = json.loads(journal.read_text(encoding="utf-8"))
    record = json.loads(adoption.read_text(encoding="utf-8"))
    if state.get("stage") != "active_ready_deferred_restore":
        raise RuntimeError("Journal is not an actual active ready session")
    if state.get("profileId") != gate.PROFILE or record.get("profileId") != gate.PROFILE:
        raise RuntimeError("Profile identity mismatch")
    if state.get("sessionId") != record.get("instanceId") or state.get("gamePid") != record.get("pid"):
        raise RuntimeError("Task journal and adoption record disagree")
    games = [p for p in gate.actors() if p.get("name", "").lower() == "lastwar.exe"]
    hosts = [p for p in gate.actors() if p.get("name", "").lower() == "lwbridge.desktop.exe"]
    if hosts or len(games) != 1 or games[0]["pid"] != record["pid"]:
        raise RuntimeError("Exact task game must survive while its task-owned host is absent")
    import subprocess
    check = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command",
        "$p=Get-Process -Id " + str(record["pid"]) + " -ErrorAction Stop; "
        + "$p.Path + '|' + $p.StartTime.ToUniversalTime().ToString('o')"],
        capture_output=True, text=True, check=True)
    path, creation = check.stdout.strip().split("|", 1)
    if os.path.normcase(os.path.abspath(path)) != os.path.normcase(os.path.abspath(state["gamePath"])):
        raise RuntimeError("Running game executable does not match captured task journal")
    # The process creation timestamp is also bound to the task journal;
    # preserve it in the proof for human review before repair.
    if state.get("gameStartedAtUtc") != creation:
        raise RuntimeError("Exact task process creation identity mismatch")
    shutil.move(str(adoption), str(dest))
    receipt = {"proof": "HOME004_F06_TASK_ADOPTION_LOST", "pid": record["pid"],
               "instanceId": record["instanceId"], "gameCreationFromWindows": creation,
               "gameCreationInJournal": state["gameStartedAtUtc"],
               "originalJournalStage": state["stage"], "adoptionPreserved": str(dest),
               "bridgeFilesChanged": [str(adoption)]}
    (gate.ROOT / "archive-adoption.json").write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "archive-adoption", "finish"}:
        raise SystemExit(__doc__)
    {"prepare": gate.prepare, "enable": gate.enable,
     "archive-adoption": archive_adoption, "finish": gate.finish}[sys.argv[1]]()
