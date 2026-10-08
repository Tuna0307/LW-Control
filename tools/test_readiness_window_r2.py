"""HOME 009 R2 D: actual run_overview_bridge.await_ready against the recovered original bridge-connect window.

Original (RECONSTRUCTED, 0x1DD114/0x1DD119/0x1DD3F7/0x1DD3FE/0x1DD3B6/0x1DD45C): deadline = wall_clock_ms() + 90 000 taken when the
launcher reports the game PID; loop "now >= deadline ? exit : lookup, sleep 250 ms"; ONE final lookup after the loop.
The production function is driven with a scripted wall clock, an inert ready.json provider and no process, game or desktop.
The lookup is the current-client adaptation (same-session ready.json); the timer arithmetic is the original contract.
Writes a JSON record when given a path argument (refuses to overwrite).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import run_overview_bridge as ov  # noqa: E402

EPOCH_MS = 1_790_000_000_000
PID = 12345


def run(report_at_s: float, event_at_s: float | None, *, jumps=(), pid=PID, session="s", payload_pid=PID,
        cancel_at_s: float | None = None) -> dict:
    """Clock starts at ``report_at_s`` (seconds on the scripted wall clock). Returns outcome, lookups and deadline."""
    state = {"now": report_at_s, "real": report_at_s, "lookups": 0}
    deadline_ms = EPOCH_MS + int(report_at_s * 1000) + ov.READY_WINDOW_MILLISECONDS
    pending_jumps = sorted(jumps)

    def wall_ms():
        return EPOCH_MS + int(round(state["now"] * 1000))

    def sleep(seconds):
        state["now"] += seconds
        state["real"] += seconds            # events and jumps are scheduled in real elapsed time
        while pending_jumps and state["real"] >= pending_jumps[0][0]:
            state["now"] += pending_jumps.pop(0)[1]

    def read_json(path):
        if path.name != "ready.json":
            return None
        state["lookups"] += 1
        if event_at_s is None or state["real"] < event_at_s:
            return None
        return {"schemaVersion": 1, "bridgeVersion": ov.BRIDGE_VERSION, "profileId": "p", "sessionId": session,
                "challenge": "c", "gamePid": payload_pid, "ready": True, "messageVisible": True,
                "messageText": ov.MESSAGE, "readyAt": EPOCH_MS // 1000 + 1_000_000}

    def cancelled(*_a):
        if cancel_at_s is not None and state["real"] >= cancel_at_s:
            raise ov.OverviewBridgeError("Overview start cancelled by closing LWBridge")

    with patch.object(ov, "wall_clock_milliseconds", side_effect=wall_ms), \
            patch.object(ov.time, "sleep", side_effect=sleep), \
            patch.object(ov.time, "time", side_effect=lambda: EPOCH_MS / 1000), \
            patch.object(ov, "read_json", side_effect=read_json), \
            patch.object(ov, "throw_if_start_cancelled", side_effect=cancelled), \
            patch.object(ov, "read_since_cursor", side_effect=lambda p, c: (c, "")), \
            patch.object(ov, "write_lease", return_value=None), \
            patch.object(ov, "player_log_path", return_value=Path("inert-player.log")):
        try:
            ov.await_ready({"runtime": Path("inert-not-created")}, "p", "s", "c", pid, deadline_ms, (0, 0, b""))
            outcome = "success"
        except ov.ServerMaintenanceError:
            outcome = "maintenance"
        except ov.OverviewBridgeError as exc:
            outcome = "cancelled" if "cancelled" in str(exc) else "timeout"
    return {"outcome": outcome, "lookups": state["lookups"], "endedAtSeconds": round(state["now"], 3),
            "deadlineSeconds": report_at_s + 90.0}


def original(report_at_s: float, event_at_s: float | None) -> str:
    """Independent model: lookups at report + 0.25 k while now < deadline, then one final lookup at the first now >= deadline."""
    deadline = report_at_s + 90.0
    t = report_at_s
    while t < deadline:
        if event_at_s is not None and t >= event_at_s:
            return "success"
        t += 0.25
    return "success" if event_at_s is not None and t >= event_at_s else "timeout"


def main() -> int:
    rows, failures = [], []

    def record(name, actual, expected):
        ok = actual == expected
        rows.append({"case": name, "expected": expected, "actual": actual, "pass": ok})
        if not ok:
            failures.append(name)

    # lead bounded cases (immutable negative: readiness-window-negative.json)
    record("report 5 s, event 100 s (old 120 s window succeeded)", run(5.0, 100.0)["outcome"], "timeout")
    record("report 70 s, event 150 s (old 120 s window timed out)", run(70.0, 150.0)["outcome"], "success")
    # exact boundaries around the deadline (report at 10 s => deadline 100 s)
    for event, expected in [(99.75, "success"), (100.0, "success"), (100.001, "timeout"), (100.25, "timeout"), (10.0, "success")]:
        record(f"event at {event}s (deadline 100.0 s)", run(10.0, event)["outcome"], expected)
        record(f"independent model event {event}s", original(10.0, event), expected)
    record("no event", run(10.0, None)["outcome"], "timeout")
    # final lookup happens after the loop: an event landing between the last in-loop lookup and the deadline still succeeds
    final = run(0.0, 89.9)
    record("event inside the last poll interval is found by the final lookup", final["outcome"], "success")
    # wall-clock movement
    record("backward clock jump of 60 s extends the wait", run(0.0, 130.0, jumps=[(30.0, -60.0)])["outcome"], "success")
    record("forward clock jump of 10 min ends the wait immediately", run(0.0, 40.0, jumps=[(10.0, 600.0)])["outcome"], "timeout")
    # wrong PID / session payloads are never accepted
    record("wrong game PID", run(0.0, 5.0, payload_pid=99999)["outcome"], "timeout")
    record("wrong session", run(0.0, 5.0, session="other")["outcome"], "timeout")
    # cancellation wins before the final lookup and inside the loop
    record("cancellation inside the loop", run(0.0, None, cancel_at_s=20.0)["outcome"], "cancelled")
    record("cancellation exactly at the deadline", run(0.0, 89.9, cancel_at_s=90.0)["outcome"], "cancelled")
    report = {"workItem": "LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009-R2", "checkpoint": "D",
              "scope": "actual await_ready with scripted wall clock; recovered timer contract (reconstructed), adapter lookup", "rows": rows,
              "failures": failures}
    if len(sys.argv) > 1:
        target = Path(sys.argv[1])
        if target.exists():
            raise SystemExit(f"refusing to overwrite {target}")
        target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"ok": not failures, "cases": len(rows), "failures": failures}))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
