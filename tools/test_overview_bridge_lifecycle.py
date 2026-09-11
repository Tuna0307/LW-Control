#!/usr/bin/env python3
"""Isolated regressions for Overview deferred restoration; never launches LastWar."""
from __future__ import annotations
import json
import os
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


def write_state(runtime: Path, game: Path, backup: Path, stage: str = "active_ready_deferred_restore") -> None:
    runtime.mkdir(parents=True, exist_ok=True)
    (runtime / "recovery.json").write_text(json.dumps({
        "schemaVersion": 1,
        "requestId": SESSION,
        "sessionId": SESSION,
        "profileId": PROFILE,
        "gamePid": PID,
        "gamePath": str(game),
        "backupPath": str(backup),
        "stage": stage,
        "originalFiles": {},
    }), encoding="utf-8")


def run_case(already_exited: bool, stage: str = "active_ready_deferred_restore") -> dict[str, object]:
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-test-") as td:
        root = Path(td)
        runtime = root / "runtime"
        backup = root / "backup"
        game = root / "Game" / "LastWar.exe"
        game.parent.mkdir(parents=True)
        game.write_bytes(b"fake")
        backup.mkdir()
        write_state(runtime, game, backup, stage)
        paths = {"runtime": runtime, "game": game, "evidence_root": root / "evidence"}
        selected_calls = 0
        def selected(_):
            nonlocal selected_calls
            selected_calls += 1
            if already_exited:
                return []
            return [{"pid": PID, "path": str(game)}] if selected_calls == 1 else []
        def clear_recovery(p, state):
            (runtime / "recovery.json").unlink(missing_ok=True)
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", side_effect=selected), \
             patch.object(ov.lr, "restore_backup", return_value={"restored": True, "packageSha256": "original"}) as restore, \
             patch.object(ov.lr, "clear_recovery", side_effect=clear_recovery), \
             patch.object(ov.lr, "update_recovery_stage", side_effect=lambda p, state, value: state.__setitem__("stage", value)), \
             patch.object(ov.lr, "close_owned_game_process_for_restore", return_value={
                 "method": "Process.CloseMainWindow", "pid": PID, "path": str(game),
                 "accepted": True, "processExited": True,
             }) as close:
            result = ov.run_stop(PROFILE, SESSION, PID, str(game), None)
            assert result["ok"] is True and result["installedFilesChanged"] is False
            assert result["restore"]["restored"] is True and restore.call_count == 1
            if already_exited:
                assert result["alreadyExited"] is True and close.call_count == 0
            else:
                assert result["alreadyExited"] is False and close.call_count == 1
            return result


def reject_foreign_session() -> None:
    with tempfile.TemporaryDirectory(prefix="lwbridge-ovl-stop-foreign-") as td:
        root = Path(td); runtime = root / "runtime"; backup = root / "backup"
        game = root / "Game" / "LastWar.exe"; game.parent.mkdir(parents=True); game.write_bytes(b"fake"); backup.mkdir()
        write_state(runtime, game, backup)
        paths = {"runtime": runtime, "game": game, "evidence_root": root / "evidence"}
        with patch.object(ov, "overview_paths", return_value=paths), \
             patch.object(ov.lr, "selected_game_processes", return_value=[]), \
             patch.object(ov.lr, "restore_backup") as restore:
            try:
                ov.run_stop(PROFILE, "foreign_session", PID, str(game), None)
            except ov.OverviewBridgeError as exc:
                assert "session/profile" in str(exc)
            else:
                raise AssertionError("foreign session unexpectedly accepted")
            assert restore.call_count == 0


def main() -> int:
    source = ov.bridge_source()
    assert not source.startswith(b"\xef\xbb\xbf")
    normal = run_case(False)
    exited = run_case(True)
    retry = run_case(True, "restoring_after_owned_game_exit")
    reject_foreign_session()
    print(json.dumps({
        "ok": True,
        "scope": "isolated Overview stop/deferred restoration; no app/game/process operation",
        "normalClose": normal["close"]["method"],
        "alreadyExited": exited["alreadyExited"],
        "retryStageAccepted": retry["alreadyExited"],
        "foreignSessionRejected": True,
        "bridgeSourceBomFree": True,
    }, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
