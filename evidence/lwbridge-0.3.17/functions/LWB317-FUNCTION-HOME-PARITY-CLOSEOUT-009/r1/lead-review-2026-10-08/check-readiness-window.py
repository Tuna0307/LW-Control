"""Actual current await_ready versus recovered original deadline arithmetic.

Both models receive a valid connection/ready event at a controlled time. This is
bounded timer comparison, not proof of original execution or live event mapping.
"""
import json
import sys
from pathlib import Path
from unittest.mock import patch

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "AGENTS.md").is_file())
sys.path.insert(0, str(ROOT / "tools"))
import run_overview_bridge as ov

HERE = Path(__file__).resolve().parent
rows = []
for report_at, ready_at in [(5.0, 100.0), (70.0, 150.0)]:
    clock = {"now": report_at}
    paths = {"runtime": HERE / "inert-not-created"}
    epoch = 1700000000

    def read_json(path):
        if path.name != "ready.json" or clock["now"] < ready_at:
            return None
        return {"schemaVersion": 1, "bridgeVersion": ov.BRIDGE_VERSION,
                "profileId": "lead-inert", "sessionId": "lead-session", "challenge": "lead-challenge",
                "gamePid": 12345, "ready": True, "messageVisible": True,
                "messageText": ov.MESSAGE, "readyAt": epoch + ready_at}

    def sleep(seconds):
        clock["now"] += seconds

    with patch.object(ov.time, "monotonic", side_effect=lambda: clock["now"]), \
            patch.object(ov.time, "time", side_effect=lambda: epoch + clock["now"]), \
            patch.object(ov.time, "sleep", side_effect=sleep), \
            patch.object(ov, "read_json", side_effect=read_json), \
            patch.object(ov, "throw_if_start_cancelled", return_value=None), \
            patch.object(ov, "read_since_cursor", side_effect=lambda p, c: (c, "")), \
            patch.object(ov, "write_lease", return_value=None):
        try:
            ov.await_ready(paths, "lead-inert", "lead-session", "lead-challenge", 12345,
                              120.0, (0, 0, b""))
            current = "success"
        except ov.OverviewBridgeError:
            current = "timeout"
    original_deadline = report_at + 90.0
    expected = "success" if ready_at <= original_deadline else "timeout"
    rows.append({"gamePidReportSeconds": report_at, "validConnectionEventSeconds": ready_at,
                 "originalDeadlineSeconds": original_deadline, "currentDeadlineSeconds": 120.0,
                 "reconstructedOriginalOutcome": expected, "actualCurrentWaitOutcome": current,
                 "mismatch": current != expected})
report = {"scope": "actual current wait with virtual clock versus original recovered 90-second post-launch-report deadline; not original/live execution",
          "source": "home009_launch_contract.py and r1/launch-readiness-recovery.md, original 0x1DD114/0x1DD119",
          "cases": rows}
(HERE / "readiness-window-negative.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
raise SystemExit(0 if all(r["mismatch"] for r in rows) else 1)
