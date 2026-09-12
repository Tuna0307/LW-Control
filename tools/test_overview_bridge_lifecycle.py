"""Isolated regressions for Overview deferred restoration; never launches LastWar."""
from __future__ import annotations
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch

TOOLS = Path(__file__).resolve().parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))
import run_overview_bridge as ov

PROFILE = "overview_test_profile"
SESSION = "session_123"
PID = 42420
OLD_STARTED = "2026-09-12T01:00:00.0000000Z"
NEW_STARTED = "2026-09-12T02:00:00.0000000Z"


def write_state(runtime: Path, game: Path, backup: Path, *, started=OLD_STARTED,
                stage: str = "active_ready_deferred_restore") -> None:
    runtime.mkdir(parents=True, exist_ok=True)
    state = {
        "schemaVersion": 1, "requestId": SESSION, "sessionId": SESSION,
        "profileId": PROFILE, "gamePid": PID, "gamePath": str(game),
        "backupPath": str(backup), "stage": stage, "originalFiles": {},
    }
    if started is not None:
        state["gameStartedAtUtc"] = started
    (runtime / "recovery.json").write_text(json.dumps(state), encoding="utf-8")


def fixture_root(td: str):
    root = Path(td)
    runtime = root / "runtime"
    backup = root / "backup"
    game = root / "Game" / "LastWar.exe"
    game.parent.mkdir(parents=True)
    game.write_bytes(b"fake")
    backup.mkdir()
    paths = {"runtime": runtime, "game": game, "evidence_root": root / "evidence"}
    return root, runtime, backup, game, paths


def fake_close_process(*args, **kwargs):
    return subprocess.CompletedProcess(args=args, returncode=0, stdout="", stderr="")


def run_exact_live_case() -> dict[str, object]:
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-exact-") as td:
        _, runtime, backup, game, paths = fixture_root(td)
        write_state(runtime, game, backup)
        calls = 0
        def selected(_):
            nonlocal calls
            calls += 1
            if calls <= 2:
                return [{"pid": PID, "path": str(game), "startedAtUtc": OLD_STARTED}]
            return []
        def clear_recovery(p, state):
            (runtime / "recovery.json").unlink(missing_ok=True)
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", side_effect=selected), \
             patch.object(ov.lr, "restore_backup", return_value={"restored": True, "packageSha256": "original"}) as restore, \
             patch.object(ov.lr, "clear_recovery", side_effect=clear_recovery), \
             patch.object(ov.lr, "update_recovery_stage", side_effect=lambda p, state, value: state.__setitem__("stage", value)), \
             patch.object(ov.lr.subprocess, "run", side_effect=fake_close_process) as close_process:
            result = ov.run_stop(PROFILE, SESSION, PID, str(game), None, OLD_STARTED)
            assert result["ok"] is True
            assert result["gameStartedAtUtc"] == OLD_STARTED
            assert result["close"]["startedAtUtc"] == OLD_STARTED
            assert restore.call_count == 1 and close_process.call_count == 1
            return result


def run_already_exited_case(stage="active_ready_deferred_restore") -> dict[str, object]:
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-exited-") as td:
        _, runtime, backup, game, paths = fixture_root(td)
        write_state(runtime, game, backup, stage=stage)
        def clear_recovery(p, state):
            (runtime / "recovery.json").unlink(missing_ok=True)
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", return_value=[]), \
             patch.object(ov.lr, "restore_backup", return_value={"restored": True, "packageSha256": "original"}) as restore, \
             patch.object(ov.lr, "clear_recovery", side_effect=clear_recovery), \
             patch.object(ov.lr, "update_recovery_stage", side_effect=lambda p, state, value: state.__setitem__("stage", value)), \
             patch.object(ov.lr.subprocess, "run", side_effect=AssertionError("must not touch a process")) as close_process:
            result = ov.run_stop(PROFILE, SESSION, PID, str(game), None, OLD_STARTED)
            assert result["alreadyExited"] is True
            assert result["close"]["alreadyExited"] is True
            assert restore.call_count == 1 and close_process.call_count == 0
            return result


def expect_rejected(selected_values, *, journal_started=OLD_STARTED,
                    requested_started=OLD_STARTED, expected_text: str):
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-reject-") as td:
        _, runtime, backup, game, paths = fixture_root(td)
        write_state(runtime, game, backup, started=journal_started)
        values = iter(selected_values)
        def selected(_):
            started = next(values)
            return [{"pid": PID, "path": str(game), "startedAtUtc": started}]
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", side_effect=selected), \
             patch.object(ov.lr, "restore_backup") as restore, \
             patch.object(ov.lr, "clear_recovery") as clear_recovery, \
             patch.object(ov.lr, "update_recovery_stage", side_effect=lambda p, state, value: state.__setitem__("stage", value)), \
             patch.object(ov.lr.subprocess, "run", side_effect=AssertionError("destructive close must not run")) as close_process:
            try:
                ov.run_stop(PROFILE, SESSION, PID, str(game), None, requested_started)
            except (ov.OverviewBridgeError, ov.lr.LiveResourceError) as exc:
                assert expected_text in str(exc), str(exc)
            else:
                raise AssertionError("unsafe process identity unexpectedly accepted")
            assert restore.call_count == 0 and clear_recovery.call_count == 0
            assert close_process.call_count == 0

def reject_foreign_session() -> None:
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-foreign-") as td:
        _, runtime, backup, game, paths = fixture_root(td)
        write_state(runtime, game, backup)
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", return_value=[]), \
             patch.object(ov.lr, "restore_backup") as restore:
            try:
                ov.run_stop(PROFILE, "foreign_session", PID, str(game), None, OLD_STARTED)
            except (ov.OverviewBridgeError, ov.lr.LiveResourceError) as exc:
                assert "session/profile" in str(exc)
            else:
                raise AssertionError("foreign session unexpectedly accepted")
            assert restore.call_count == 0


def main() -> int:
    source = ov.bridge_source()
    assert not source.startswith(b"\xef\xbb\xbf")
    exact = run_exact_live_case()
    exited = run_already_exited_case()
    retry = run_already_exited_case("restoring_after_owned_game_exit")
    expect_rejected([NEW_STARTED], expected_text="creation identity changed before Overview Close")
    expect_rejected([OLD_STARTED], journal_started=None,
                    expected_text="journal gameStartedAtUtc is missing or invalid")
    expect_rejected([None], expected_text="current game startedAtUtc is missing or invalid")
    expect_rejected([OLD_STARTED, NEW_STARTED],
                    expected_text="creation identity changed before normal close")
    reject_foreign_session()
    print(json.dumps({
        "ok": True,
        "scope": "isolated Overview stop/deferred restoration; no app/game/process operation",
        "exactCreationIdentityRepairable": exact["close"]["method"],
        "samePidPathDifferentCreationRejected": True,
        "missingOrUnreadableCreationRejected": True,
        "creationChangedAtFinalCloseRejected": True,
        "alreadyExitedRestoresWithoutProcessTouch": exited["alreadyExited"],
        "retryStageAccepted": retry["alreadyExited"],
        "foreignSessionRejected": True,
        "bridgeSourceBomFree": True,
    }, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
