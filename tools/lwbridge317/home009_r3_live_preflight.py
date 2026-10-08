"""Read-only current-client/ownership gates followed by a reversible original file backup.

Run before any R3 live launch. Does not launch, stop, or modify a game file.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import run_overview_bridge as ov
import current_client_compat as compat


def checksum(path):
    return ov.lr.sha256_file(path)


def main():
    if len(sys.argv) != 3:
        raise SystemExit("usage: home009_r3_live_preflight.py <isolated-data-root> <json-evidence>")
    isolated = Path(sys.argv[1]).resolve()
    report = Path(sys.argv[2]).resolve()
    p = ov.lr.paths()
    verdict = compat.inspect_current(ov.lr, p)
    query = ("Get-CimInstance Win32_Process | Where-Object {"
             "$_.Name -match 'LastWar|LWBridge' -or "
             "$_.ExecutablePath -like '*Last War-Survival Game*'"
             "} | Select-Object Name,ProcessId,ParentProcessId,CreationDate,ExecutablePath "
             "| ConvertTo-Json -Compress")
    result = subprocess.run(["powershell.exe", "-NoProfile", "-Command", query],
                            text=True, capture_output=True, check=True)
    processes = json.loads(result.stdout or "null")
    processes = [] if processes is None else processes if isinstance(processes, list) else [processes]
    if isolated.exists():
        existing = [str(p.relative_to(isolated)) for p in isolated.rglob("*") if p.is_file()]
    else:
        existing = []
    restore_inputs = {}
    for key in ("data", "metadata", "version"):
        item = p[key]
        restore_inputs[key] = {"path": str(item), "sha256": checksum(item) if item.is_file() else None,
                               "bytes": item.stat().st_size if item.is_file() else None}
    safe = bool(verdict["ok"]) and not processes and not existing and all(
        i["sha256"] for i in restore_inputs.values())
    backups = {}
    if safe:
        directory = isolated / "before-original-game-files"
        directory.mkdir(parents=True, exist_ok=False)
        for key in ("data", "metadata", "version"):
            target = directory / p[key].name
            shutil.copy2(p[key], target)
            if checksum(target) != restore_inputs[key]["sha256"]:
                raise RuntimeError("reversible backup verification failed: " + key)
            backups[key] = {"path": str(target), "sha256": checksum(target)}
    summary = {
        "compatibility": verdict, "processesAtGate": processes,
        "isolatedRoot": str(isolated), "existingIsolatedFilesBeforeGate": existing,
        "triplet": restore_inputs, "verifiedReversibleBackups": backups,
        "allowLiveHomeLaunch": safe,
        "note": "No installation mutation, process start or stop occurred in preflight."
    }
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"allowLiveHomeLaunch": safe,
                      "problems": verdict.get("problems"), "processCount": len(processes),
                      "isolated": str(isolated), "backupCount": len(backups)}))
    return 0 if safe else 2


if __name__ == "__main__":
    raise SystemExit(main())
