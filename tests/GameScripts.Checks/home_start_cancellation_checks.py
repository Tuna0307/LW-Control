"""Inert genuine-helper cleanup gates for an explicit pre-ready Stop.

No game, process creation, installed files or native process API is used.
"""
from __future__ import annotations

from contextlib import ExitStack, nullcontext
_REPO = __import__('pathlib').Path(__file__).resolve().parents[2]
_SCRIPTS = _REPO / 'src' / 'LWBridge.GameScripts'
_RESEARCH = _REPO / 'tools' / 'research'
__import__('sys').path.append(str(_SCRIPTS))
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("home_cancel_helper", (_SCRIPTS / "run_overview_bridge.py"))
assert spec and spec.loader
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)


class CancellationChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="home-cancel-inert-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.p = {"runtime": self.root, "game": self.root / "LastWar.exe", "launcher": self.root / "Launcher.exe"}
        self.owned = {"pid": 4242, "path": str(self.p["game"]), "startedAtUtc": "2026-10-23T00:00:00.0000000Z"}

    def marker(self, challenge="nonce-A"):
        (self.root / "cancel-start.txt").write_text("schema=1\nsessionId=owner-A\nchallenge=" + challenge + "\n", encoding="utf8")

    def test_explicit_cancel_uses_exact_stop_and_requires_exit_and_no_survivor(self):
        self.marker()
        result = {"exitProof": {"established": True}}
        with mock.patch.object(helper.lr, "terminate_owned_game_process_for_stop", return_value=result) as stop, \
             mock.patch.object(helper.lr, "close_owned_game_process_for_restore") as graceful, \
             mock.patch.object(helper.lr, "selected_game_processes", return_value=[]):
            self.assertIs(helper.close_failed_start_game(self.p, self.owned, "owner-A", "nonce-A"), result)
        stop.assert_called_once_with(self.p, self.owned)
        graceful.assert_not_called()

    def test_wrong_marker_and_no_marker_keep_existing_graceful_policy(self):
        for marker in (None, "wrong-nonce"):
            if marker is not None:
                self.marker(marker)
            with self.subTest(marker=marker), \
                 mock.patch.object(helper.lr, "terminate_owned_game_process_for_stop") as stop, \
                 mock.patch.object(helper.lr, "close_owned_game_process_for_restore", return_value={}) as graceful:
                helper.close_failed_start_game(self.p, self.owned, "owner-A", "nonce-A")
                stop.assert_not_called()
                graceful.assert_called_once_with(self.p, self.owned)

    def test_unproven_exit_and_survivor_withhold_restoration(self):
        self.marker()
        for proof, survivors in ((False, []), (True, [{"pid": 9999}])):
            with self.subTest(proof=proof, survivors=survivors), \
                 mock.patch.object(helper.lr, "terminate_owned_game_process_for_stop", return_value={"exitProof": {"established": proof}}), \
                 mock.patch.object(helper.lr, "selected_game_processes", return_value=survivors):
                with self.assertRaisesRegex(helper.OverviewBridgeError, "restoration withheld"):
                    helper.close_failed_start_game(self.p, self.owned, "owner-A", "nonce-A")

    def run_genuine_start_cancellation(self, proof):
        alive = True
        def cancel_while_awaiting_ready(*args):
            self.marker()
            raise helper.OverviewBridgeError("Overview start cancelled by closing LWBridge")
        def terminate(*args):
            nonlocal alive
            if proof:
                alive = False
            return {"exitProof": {"established": proof}}
        with ExitStack() as stack:
            def patch(obj, name, **kwargs):
                return stack.enter_context(mock.patch.object(obj, name, **kwargs))
            patch(helper, "overview_paths", return_value=self.p)
            patch(helper.lr, "OperationLease", return_value=nullcontext())
            for obj, name in ((helper,"require_no_fresh_foreign_lease"), (helper.lr,"require_no_selected_game_process"),
                              (helper,"clear_abandoned_runtime"), (helper.lr,"install_candidate"),
                              (helper,"clear_stale_runtime"), (helper,"write_control"), (helper,"write_game_report"),
                              (helper.lr,"update_recovery_stage"), (helper,"write_session_evidence")):
                patch(obj, name, return_value=None)
            patch(helper.lr, "recover_pending", return_value=None)
            patch(helper.lr, "verify_current", return_value={})
            patch(helper.lr, "make_backup", return_value=self.root / "backup")
            patch(helper.lr, "arm_recovery", return_value={"originalFiles": {}})
            patch(helper, "make_candidate", return_value={})
            patch(helper, "launcher_log_offset", return_value=0)
            patch(helper, "file_cursor", return_value={})
            patch(helper, "selected_launcher_processes", return_value=[])
            patch(helper, "spawn_selected_launcher", return_value=mock.Mock(pid=1122))
            patch(helper, "await_owned_game_process_update_aware", return_value=self.owned)
            patch(helper, "await_ready", side_effect=cancel_while_awaiting_ready)
            patch(helper, "close_helper_owned_launcher_processes", return_value=[])
            patch(helper.lr, "selected_game_processes", side_effect=lambda p: [self.owned] if alive else [])
            stop = patch(helper.lr, "terminate_owned_game_process_for_stop", side_effect=terminate)
            graceful = patch(helper.lr, "close_owned_game_process_for_restore", side_effect=AssertionError("paused child cannot service graceful close"))
            restore = patch(helper.lr, "restore_backup", side_effect=[PermissionError("selected game still owns files"), {"restored": True}])
            cleared = patch(helper.lr, "clear_recovery")
            with self.assertRaises(helper.OverviewBridgeError):
                helper.run_start("profile-A", "owner-A", "nonce-A", 30, None)
            stop.assert_called_once_with(self.p, self.owned)
            graceful.assert_not_called()
            self.assertEqual(restore.call_count, 2 if proof else 1)
            self.assertEqual(cleared.call_count, 1 if proof else 0)

    def test_real_start_exception_restores_only_after_exact_cancel_exit(self):
        self.run_genuine_start_cancellation(True)

    def test_real_start_exception_keeps_journal_when_exit_is_unproven(self):
        self.run_genuine_start_cancellation(False)


if __name__ == "__main__":
    unittest.main()
