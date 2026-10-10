"""Capture sanitized task-only ownership and restoration evidence.

Never persist ready/adoption/transport records or credentials, even hashed
tokens. Only compare owner/session/challenge/PID equality and hash the session
identity for longitudinal correlation without disclosing the original token.
"""
import hashlib
import json
import sys
import time
from pathlib import Path
import home_launch_002_gate as prior

repo = Path(__file__).resolve().parents[1]
task = repo / "artifacts" / "home-004" / "r1-live"
runtime = task / "isolated" / "overview-bridge"
def load(name):
    try:
        return json.loads((runtime / name).read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None
def observe(phase):
    adoption = load("adoption.json") or {}
    ready = load("ready.json") or {}
    heartbeat = load("heartbeat.json") or {}
    recovery = load("recovery.json") or {}
    session = adoption.get("instanceId")
    pid = adoption.get("pid")
    checks = {
        "adoptionExists": (runtime / "adoption.json").is_file(),
        "recoveryJournalExists": (runtime / "recovery.json").is_file(),
        "readyExists": (runtime / "ready.json").is_file(),
        "heartbeatExists": (runtime / "heartbeat.json").is_file(),
        "exactProfile": bool(adoption) and adoption.get("profileId") == "home-004-r1-live",
        "readySessionMatchesAdoption": bool(session) and ready.get("sessionId") == session,
        "heartbeatSessionMatchesAdoption": bool(session) and heartbeat.get("sessionId") == session,
        "recoverySessionMatchesAdoption": bool(session) and recovery.get("sessionId") == session,
        "readyPidMatchesAdoption": isinstance(pid, int) and ready.get("gamePid") == pid,
        "heartbeatPidMatchesAdoption": isinstance(pid, int) and heartbeat.get("gamePid") == pid,
        "recoveryPidMatchesAdoption": isinstance(pid, int) and recovery.get("gamePid") == pid,
        "readyChallengeMatchesHeartbeat": bool(ready.get("challenge")) and ready.get("challenge") == heartbeat.get("challenge"),
        "adoptionChallengeMatchesReady": bool(ready.get("challenge")) and adoption.get("challenge") == ready.get("challenge"),
        "recoveryHasOriginalFiles": bool(recovery.get("originalFiles")),
    }
    owners = prior.owners()
    report = {
        "phase": phase,
        "timeUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        "sessionDigest": hashlib.sha256(str(session).encode()).hexdigest()[:20] if session else None,
        "gamePid": pid,
        "processObserved": any(row["name"].lower() == "lastwar.exe" and row["pid"] == pid for row in owners),
        "cloneProcessCount": sum(row["name"].lower() == "lwbridge.desktop.exe" for row in owners),
        "launcherProcessCount": sum(row["name"].lower() == "lastwarlauncher.exe" for row in owners),
        "checks": checks,
        "installedScriptHashes": {n: prior.sha(prior.SCRIPT / n) for n in prior.FILES},
    }
    with (task / "snapshots.jsonl").open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(report, sort_keys=True) + "\n")
    print(json.dumps(report))
if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("specify one sanitized phase label")
    observe(sys.argv[1])
