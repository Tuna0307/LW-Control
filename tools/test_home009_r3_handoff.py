"""R3: exact-session current-helper registration confirmation, no game/installer.

The producer may expose control only after a separate production host refresh.
This test injects only temporary files and bounded timer actors.
"""
from __future__ import annotations

import json
import sys
import tempfile
import threading
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_overview_bridge as helper


def report(p, session="r3-session", challenge="r3-challenge", pid=4141):
    helper.write_kv_atomic(p["runtime"] / "registration-confirmed.txt", {
        "schema": 1,
        "sessionId": session,
        "challenge": challenge,
        "instanceId": session,
        "gamePid": pid,
    })


def run():
    original_poll = helper.READY_POLL_SECONDS
    helper.READY_POLL_SECONDS = 0.01
    cases = []
    try:
        with tempfile.TemporaryDirectory(prefix="home009-r3-report-") as tmp:
            p = {"runtime": Path(tmp)}
            deadline = lambda: helper.wall_clock_milliseconds() + 350
            for label, kwargs, expected in (
                ("same-session-immediate", {}, True),
                ("wrong-session", {"session": "obsolete"}, False),
                ("wrong-challenge", {"challenge": "obsolete"}, False),
                ("wrong-pid", {"pid": 9999}, False),
            ):
                ack = p["runtime"] / "registration-confirmed.txt"
                ack.unlink(missing_ok=True)
                report(p, **kwargs)
                failed = False
                try:
                    helper.await_registration_confirmation(p, "r3-session", "r3-challenge", 4141, deadline())
                except helper.OverviewBridgeError as e:
                    failed = "not confirmed" in str(e)
                assert failed == (not expected), label
                cases.append(label)
            ack.unlink(missing_ok=True)
            producer = threading.Timer(0.06, lambda: report(p))
            producer.start()
            try:
                helper.await_registration_confirmation(p, "r3-session", "r3-challenge", 4141,
                                                       helper.wall_clock_milliseconds() + 2000)
            finally:
                producer.join()
            cases.append("delayed-confirmation")
            # stale handoff must not be accepted across sessions
            helper.clear_stale_runtime(p, "r3-session", "r3-challenge")
            assert not ack.exists()
            cases.append("exact-owner-teardown")
            report(p, "other-session", "other-challenge")
            helper.clear_stale_runtime(p, "r3-session", "r3-challenge")
            assert ack.exists()
            cases.append("foreign-owner-record-preserved")
            ack.unlink()
            helper.write_kv_atomic(p["runtime"] / "cancel-start.txt", {
                "schema": 1, "sessionId": "r3-session", "challenge": "r3-challenge",
            })
            failed = False
            try:
                helper.await_registration_confirmation(p, "r3-session", "r3-challenge", 4141, deadline())
            except helper.OverviewBridgeError as e:
                failed = "cancelled" in str(e)
            assert failed
            cases.append("owner-cancellation-wins")
    finally:
        helper.READY_POLL_SECONDS = original_poll
    print(json.dumps({"ok": True, "cases": cases, "count": len(cases), "gameLaunches": 0}))


if __name__ == "__main__":
    run()
