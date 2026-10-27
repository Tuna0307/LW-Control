"""Inert checks for the exact-owner HOME stability fault controller.

No process handles are opened and no application/game process is launched.
"""

from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

import home_stability_faults as faults


STARTED = "2026-10-23T12:34:56.1234567Z"


class OwnedFaultHarnessChecks(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="lw-home-owned-faults-")
        self.base = Path(self.temporary.name)
        self.root = self.base / "isolated"
        self.game = self.base / "game"
        self.runtime = self.root / "overview-bridge"
        self.backup = self.root / "overview-bridge-backups" / "session-backup"
        self.scripts = self.base / "scripts"
        self.external_backup = self.base / "external-backup"
        self.output = self.base / "evidence" / "result.json"
        self.runtime.mkdir(parents=True)
        self.backup.mkdir(parents=True)
        self.scripts.mkdir()
        self.external_backup.mkdir()
        (self.game / "Game").mkdir(parents=True)
        (self.game / "Game" / "LastWar.exe").write_bytes(b"not-an-executable-fixture")
        self.profile = "isolated-home-stability"
        self.session = "0123456789abcdef0123456789abcdef"
        self.pid = 4242
        (self.root / "config.json").write_text(json.dumps({
            "schemaVersion": 1,
            "owner": "LWBridgeRebuild",
            "profileId": self.profile,
            "gameRoot": str(self.game),
            "autoLaunchGame": False,
            "autoReconnect": True,
            "gameDesiredRunning": True,
        }), encoding="utf-8")
        (self.runtime / "adoption.json").write_text(json.dumps({
            "profileId": self.profile,
            "instanceId": self.session,
            "pid": self.pid,
            "gameExecutable": str(self.game / "Game" / "LastWar.exe"),
            "processCreatedAt": STARTED,
        }), encoding="utf-8")
        (self.runtime / "recovery.json").write_text(json.dumps({
            "profileId": self.profile,
            "sessionId": self.session,
            "gamePid": self.pid,
            "gamePath": str(self.game / "Game" / "LastWar.exe"),
            "gameStartedAtUtc": STARTED,
            "backupPath": str(self.backup),
            "originalFiles": {"data": {"sha256": "fixture"}},
            "stage": "active_ready_deferred_restore",
        }), encoding="utf-8")
        (self.runtime / "heartbeat.json").write_text(json.dumps({
            "bridgeVersion": faults.BRIDGE_VERSION,
            "profileId": self.profile,
            "sessionId": self.session,
            "gamePid": self.pid,
            "ready": True,
            "gameStateObserved": True,
            "gameReady": True,
            "loggedIn": True,
            "connected": True,
            "connecting": False,
            "gameUid": "fixture-only-never-emitted",
            "serverId": 1,
            "worldPos": 1,
        }), encoding="utf-8")

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_exact_owner_requires_profile_session_pid_path_start_and_restore_journal(self) -> None:
        identity = faults.read_owned_identity(self.root, self.game, require_healthy=True)
        self.assertEqual(self.profile, identity.profile)
        self.assertEqual(self.session, identity.session)
        self.assertEqual(self.pid, identity.pid)
        self.assertEqual(faults.creation_ticks(STARTED), identity.started_ticks)
        self.assertEqual(self.backup.resolve(), identity.recovery_backup)

        recovery = json.loads((self.runtime / "recovery.json").read_text(encoding="utf-8"))
        recovery["gameStartedAtUtc"] = "2026-10-23T12:34:56.1234568Z"
        (self.runtime / "recovery.json").write_text(json.dumps(recovery), encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "creation identity differs"):
            faults.read_owned_identity(self.root, self.game, require_healthy=False)

    def test_owner_rejects_foreign_selected_path_and_unhealthy_heartbeat(self) -> None:
        adoption = json.loads((self.runtime / "adoption.json").read_text(encoding="utf-8"))
        adoption["gameExecutable"] = str(self.base / "foreign" / "LastWar.exe")
        (self.runtime / "adoption.json").write_text(json.dumps(adoption), encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "game path differs"):
            faults.read_owned_identity(self.root, self.game, require_healthy=False)

        adoption["gameExecutable"] = str(self.game / "Game" / "LastWar.exe")
        (self.runtime / "adoption.json").write_text(json.dumps(adoption), encoding="utf-8")
        heartbeat = json.loads((self.runtime / "heartbeat.json").read_text(encoding="utf-8"))
        heartbeat["connected"] = False
        (self.runtime / "heartbeat.json").write_text(json.dumps(heartbeat), encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "healthy game/account heartbeat"):
            faults.read_owned_identity(self.root, self.game, require_healthy=True)

    def test_external_backup_is_dynamic_and_byte_exact(self) -> None:
        for index, name in enumerate(faults.SCRIPT_NAMES):
            data = (f"current-official-{index}" * 17).encode("utf-8")
            (self.external_backup / name).write_bytes(data)
            (self.scripts / name).write_bytes(data)
        hashes = faults.validate_backup(self.external_backup, self.scripts)
        self.assertEqual(set(faults.SCRIPT_NAMES), set(hashes))

        (self.scripts / "version.txt").write_bytes(b"changed")
        with self.assertRaisesRegex(RuntimeError, "not restored exactly"):
            faults.validate_backup(self.external_backup, self.scripts)

    def test_previous_request_receipts_are_preserved_before_next_request(self) -> None:
        request = self.root / faults.REQUEST_NAME
        consumed = Path(str(request) + ".consumed")
        receipt = Path(str(consumed) + ".result.json")
        consumed.write_text("old-request", encoding="utf-8")
        receipt.write_text("old-receipt", encoding="utf-8")
        returned_request, returned_consumed, returned_receipt = faults.archive_previous_request(
            self.root, self.output)
        self.assertEqual(request, returned_request)
        self.assertEqual(consumed, returned_consumed)
        self.assertEqual(receipt, returned_receipt)
        self.assertFalse(consumed.exists())
        self.assertFalse(receipt.exists())
        history = list((self.output.parent / "home-stability-request-history").iterdir())
        self.assertEqual(2, len(history))
        self.assertEqual({"old-request", "old-receipt"},
                         {path.read_text(encoding="utf-8") for path in history})

        request.write_text("pending", encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "already pending"):
            faults.archive_previous_request(self.root, self.output)

    def test_fresh_game_report_and_ready_match_require_exact_session_pid(self) -> None:
        report = {
            "schema": "1",
            "bridgeVersion": faults.BRIDGE_VERSION,
            "sessionId": self.session,
            "challenge": "fixture-challenge",
            "gamePid": str(self.pid),
            "deadlineMilliseconds": "123456789",
        }
        (self.runtime / "ready.json").write_text(json.dumps({
            "sessionId": self.session,
            "gamePid": self.pid,
            "ready": True,
        }), encoding="utf-8")
        self.assertTrue(faults.ready_matches_report(self.runtime, report))
        report["sessionId"] = "successor"
        self.assertFalse(faults.ready_matches_report(self.runtime, report))

    def test_creation_parser_requires_exact_seven_digit_100ns_fraction(self) -> None:
        self.assertIsInstance(faults.creation_ticks(STARTED), int)
        self.assertIsNone(faults.creation_ticks("2026-10-23T12:34:56.123Z"))
        self.assertIsNone(faults.creation_ticks("2026-10-23T12:34:56Z"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
