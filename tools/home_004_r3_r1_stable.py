"""15-second exact-session, live native-heartbeat stabilization receipt."""
from __future__ import annotations
import datetime
import hashlib
import json
import pathlib
import sys
import time
import home_004_r3_r1_bounded_hang as witness

ROOT = witness.hang.TASK
RUNTIME = witness.hang.identity.RUNTIME
RECEIPT = ROOT / "stable-15-seconds.json"


def current():
    pid, _path, created, session = witness.hang.identity.read_owned()
    if pid == 28848:
        raise RuntimeError("Original faulted PID remained; no successor")
    heartbeat = json.loads((RUNTIME / "heartbeat.json").read_text(encoding="utf-8"))
    ready = json.loads((RUNTIME / "ready.json").read_text(encoding="utf-8"))
    transport = json.loads((RUNTIME / "pipe-transport.json").read_text(encoding="utf-8"))
    if (not heartbeat.get("ready") or not heartbeat.get("connected") or
        not heartbeat.get("loggedIn") or not heartbeat.get("gameReady") or
        heartbeat.get("gamePid") != pid or heartbeat.get("sessionId") != session or
        ready.get("gamePid") != pid or ready.get("sessionId") != session or
        not ready.get("ready") or not transport.get("clientConnected")):
        raise RuntimeError("Current-session heartbeat/ready/transport disconnected")
    age = time.time() - heartbeat.get("updatedAt", 0)
    if not (-3 < age < 8):
        raise RuntimeError(f"Live heartbeat stale or from future: {age:.2f}s")
    return pid, created, hashlib.sha256(session.encode()).hexdigest()[:20], round(age, 3)


def run():
    event_file = RUNTIME / "home004-recovery-events.jsonl"
    events = [json.loads(line) for line in event_file.read_text(encoding="utf-8").splitlines()]
    success = [v for v in events if v.get("reason") == "hang" and
               v.get("state") == "succeeded" and v.get("restarted") and
               v.get("authenticatedRoutes") == 1 and v.get("listenerStarted")]
    if not success:
        raise RuntimeError("Real native hang succeeded event with authenticated listener missing")
    start = time.monotonic()
    first = current()
    observations = [dict(offsetSeconds=0.0, heartbeatAgeSeconds=first[3])]
    while time.monotonic() - start < 15:
        time.sleep(min(1, max(0, start + 15 - time.monotonic())))
        sample = current()
        if sample[:3] != first[:3]:
            raise RuntimeError("Owned successor changed during stability verification")
        observations.append(dict(offsetSeconds=round(time.monotonic()-start, 3),
                                 heartbeatAgeSeconds=sample[3]))
    receipt = {
        "proof": "HOME004_R3_R1_STABLE_15_SECONDS_OK",
        "timeUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "nativeRecoveryReason": "hang",
        "nativeRecoveryState": "succeeded",
        "authenticatedRoutesAtSuccess": 1,
        "listenerStillStartedAtSuccess": True,
        "previousFaultedPid": 28848,
        "successorPid": first[0],
        "successorCreatedAtUtc": first[1],
        "sessionDigest": first[2],
        "durationSeconds": round(time.monotonic()-start, 3),
        "observationCount": len(observations),
        "maxHeartbeatAgeSeconds": max(v["heartbeatAgeSeconds"] for v in observations),
        "minHeartbeatAgeSeconds": min(v["heartbeatAgeSeconds"] for v in observations),
        "continuousNativeReadiness": True,
        "samples": observations,
    }
    RECEIPT.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print(json.dumps({k:v for k,v in receipt.items() if k != "samples"}, indent=2))

if __name__ == "__main__":
    if len(sys.argv) != 1:
        raise SystemExit("No arguments; verifies the exact owned R3-R1 successor only")
    run()
