"""Inert lead inverses for Stop ownership/restoration, not original execution.

Runs actual production run_stop with temporary journals and controlled process APIs.
No real process, installation, desktop or owner runtime is touched.
"""
import json
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "AGENTS.md").is_file())
sys.path[:0] = [str(ROOT / "tools"), str(ROOT / "tools/lwbridge317")]
import run_overview_bridge as ov
import test_overview_bridge_lifecycle as base
import home009_oracle as oracle


def run_case(replacement=False):
    with tempfile.TemporaryDirectory(prefix="home009-lead-stop-") as td:
        _, runtime, backup, game, paths = base.fixture_root(td)
        base.write_state(runtime, game, backup)
        model = oracle.VirtualProcessModel(
            pid=base.PID, image=str(game), dies_on_terminate_after=None,
            query_denied_during_poll_at=None if replacement else 300)
        state = {"restoreCalls": 0, "replacementTerminated": False}

        def selected(_):
            return ([{"pid": base.PID, "path": str(game), "startedAtUtc": base.OLD_STARTED}]
                    if model.snapshot_pid_exists(base.PID) else [])

        if replacement:
            # Replacement occurs AFTER both selected-inventory identity checks, before open.
            def open_replacement(pid):
                state["replacementStartedAtUtc"] = base.NEW_STARTED
                return "replacement-process-handle"

            def terminate_replacement(handle, code):
                assert handle == "replacement-process-handle" and code == 1
                state["replacementTerminated"] = True
                model.gone_at = model.now
                model.terminate_calls += 1
                return True

            model.open_terminate = open_replacement
            model.terminate = terminate_replacement

        def restore(*_):
            state["restoreCalls"] += 1
            state["pidExistsAtRestore"] = model.snapshot_pid_exists(base.PID)
            return {"restored": True, "packageSha256": "inert-original"}

        def clear(_, __):
            (runtime / "recovery.json").unlink(missing_ok=True)

        with patch.object(ov, "overview_paths", return_value=paths), \
                patch.object(ov.lr, "selected_game_processes", side_effect=selected), \
                patch.object(ov.lr, "process_api", return_value=model), \
                patch.object(ov.lr, "stop_sleep_milliseconds", side_effect=model.sleep_ms), \
                patch.object(ov.lr, "restore_backup", side_effect=restore), \
                patch.object(ov.lr, "clear_recovery", side_effect=clear):
            result = ov.run_stop(base.PROFILE, base.SESSION, base.PID, str(game), None, base.OLD_STARTED)
        failed = state["replacementTerminated"] if replacement else state.get("pidExistsAtRestore", False)
        return {
            "finding": "LEAD009-02" if replacement else "LEAD009-01",
            "case": "same-path PID incarnation replacement before open" if replacement else "post-terminate image query denied while PID survives",
            "expected": "refuse replacement termination" if replacement else "do not restore or clear journal until owned exit is established",
            "actual": state, "productOk": result["ok"], "gameRunning": result["gameRunning"],
            "processExitedClaim": result["close"]["processExited"],
            "journalCleared": not (runtime / "recovery.json").exists(),
            "regressionReproduced": failed,
        }


if __name__ == "__main__":
    report = {"scope": "actual production handlers with inert seams; no original/live execution",
              "cases": [run_case(), run_case(True)]}
    report["reproduced"] = sum(c["regressionReproduced"] for c in report["cases"])
    target = Path(__file__).with_name("stop-ownership-negative.json")
    if target.exists():
        raise SystemExit("Refusing to overwrite immutable lead negative evidence")
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if report["reproduced"] == 2 else 1)
