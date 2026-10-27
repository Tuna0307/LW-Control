"""Inert exact-owner recovery admission positives and distinguishing negatives."""

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


_spec = importlib.util.spec_from_file_location(
    "qualify_pending_recovery", Path(__file__).with_name("qualify_pending_recovery.py"))
gate = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(gate)


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


class GateChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="recovery-gate-fixture-")
        self.addCleanup(self.temp.cleanup)
        base = Path(self.temp.name)
        self.data_root = base / "appdata" / "LWBridgeRebuild"
        self.runtime = self.data_root / "overview-bridge"
        self.game_root = base / "game-installation"
        self.script_root = base / "locallow" / "lwScripts"
        self.backup = self.data_root / "overview-bridge-backups" / "test-backup"
        self.game_exe = self.game_root / "Game" / "LastWar.exe"
        self.game_exe.parent.mkdir(parents=True)
        self.game_exe.write_bytes(b"test-game")
        (self.game_root / "LastWarLauncher.exe").write_bytes(b"test-launcher")
        self.runtime.mkdir(parents=True)
        self.script_root.mkdir(parents=True)
        self.backup.mkdir(parents=True)
        self.profile = "sandbox-test-profile"
        self.session = "a" * 32
        self.challenge = "only-in-ephemeral-tests"
        self.pid = 73001
        self.started = "2026-10-23T04:23:59.4623424Z"
        originals = {}
        for role, name in gate.ROLES.items():
            content = ("test:" + role).encode("utf-8")
            installed = self.script_root / name
            installed.write_bytes(content)
            (self.backup / name).write_bytes(content)
            originals[role] = {
                "path": str(installed), "size": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        self.originals = originals
        self.candidate = hashlib.sha256(b"candidate").hexdigest()
        self.challenge_sha = hashlib.sha256(self.challenge.encode()).hexdigest()
        write_json(self.data_root / "config.json",
                   {"schemaVersion": 1, "profileId": self.profile, "gameRoot": None})
        self.manifest_path = self.backup / "manifest.json"
        write_json(self.manifest_path,
                   {"schemaVersion": 1, "state": "backup_ready", "originalFiles": originals})
        self.journal_path = self.runtime / "recovery.json"
        self.journal = {
            "schemaVersion": 1, "stage": "active_ready_deferred_restore",
            "requestId": self.session, "sessionId": self.session,
            "profileId": self.profile, "gamePid": self.pid,
            "gamePath": str(self.game_exe), "gameStartedAtUtc": self.started,
            "backupPath": str(self.backup), "originalFiles": originals,
            "candidate": {"packageSha256": self.candidate},
            "challengeSha256": self.challenge_sha,
        }
        write_json(self.journal_path, self.journal)
        self.session_dir = self.data_root / "overview-evidence" / self.session
        self.helper = {
            "ok": True, "sessionId": self.session, "profileId": self.profile,
            "gamePid": self.pid, "gamePath": str(self.game_exe),
            "gameStartedAtUtc": self.started, "installedFilesChanged": True,
            "challengeSha256": self.challenge_sha,
            "candidate": {"packageSha256": self.candidate},
            "currentClient": {"packageSha256": originals["data"]["sha256"]},
            "restore": {"stage": "active_ready_deferred_restore", "deferred": True},
        }
        self.host = {
            "schemaVersion": 1, "sessionId": self.session, "profileId": self.profile,
            "gamePid": self.pid, "gamePath": str(self.game_exe),
            "gameStartedAtUtc": self.started,
        }
        write_json(self.session_dir / "helper-start.json", self.helper)
        write_json(self.session_dir / "host-start.json", self.host)
        self.adoption = {
            "schemaVersion": 1, "sessionId": self.session, "instanceId": self.session,
            "profileId": self.profile, "pid": self.pid, "gameExecutable": str(self.game_exe),
            "processCreatedAt": self.started, "leaseRequired": True,
            "challenge": self.challenge,
        }
        write_json(self.runtime / "adoption.json", self.adoption)
        for name in ("ready.json", "heartbeat.json"):
            write_json(self.runtime / name, {
                "schemaVersion": 1, "sessionId": self.session, "profileId": self.profile,
                "gamePid": self.pid, "challenge": self.challenge,
            })

    def validate(self, processes=None):
        return gate.verify(self.data_root, self.game_root, self.script_root,
                           inventory=[] if processes is None else processes)

    def assert_blocked(self, code):
        with self.assertRaises(gate.GateError) as caught:
            self.validate()
        self.assertEqual(caught.exception.code, code)

    def test_valid_interrupted_owner_is_ready_with_no_writes(self):
        before = {p: p.read_bytes() for p in (
            self.journal_path, self.manifest_path, *self.script_root.iterdir())}
        result = self.validate()
        self.assertEqual(result["status"], "READY_FOR_SUPPORTED_RECOVERY")
        self.assertFalse(result["restorationPerformed"])
        self.assertNotIn(self.session, str(result))
        self.assertNotIn(self.profile, str(result))
        for path, content in before.items():
            self.assertEqual(path.read_bytes(), content)

    def test_target_process_blocks_even_when_other_details_match(self):
        with self.assertRaises(gate.GateError) as caught:
            self.validate([{"Name": "LastWar.exe", "ProcessId": self.pid,
                            "ExecutablePath": str(self.game_exe)}])
        self.assertEqual(caught.exception.code, "TARGET_PROCESS_PRESENT")

    def test_installed_script_replacement_blocks(self):
        (self.script_root / "LWScripts.data").write_bytes(b"candidate")
        self.assert_blocked("ORIGINAL_HASH_MISMATCH")

    def test_corrupt_backup_blocks(self):
        (self.backup / "LWScripts.txt").write_bytes(b"tampered")
        self.assert_blocked("ORIGINAL_HASH_MISMATCH")

    def test_wrong_profile_blocks(self):
        config = {"profileId": "some-other-profile"}
        write_json(self.data_root / "config.json", config)
        self.assert_blocked("PROFILE_MISMATCH")

    def test_wrong_root_blocks(self):
        self.journal["gamePath"] = str(self.game_exe.parent / "DifferentGame.exe")
        write_json(self.journal_path, self.journal)
        self.assert_blocked("SELECTED_GAME_MISMATCH")

    def test_foreign_backup_directory_blocks(self):
        other = self.data_root / "unrelated" / "backup"
        other.mkdir(parents=True)
        self.journal["backupPath"] = str(other)
        write_json(self.journal_path, self.journal)
        self.assert_blocked("BACKUP_PATH_MISMATCH")

    def test_manifest_owner_record_mismatch_blocks(self):
        manifest = read_json_for_test(self.manifest_path)
        manifest["originalFiles"]["version"]["size"] = 200
        write_json(self.manifest_path, manifest)
        self.assert_blocked("BACKUP_MANIFEST_MISMATCH")

    def test_fake_receipt_or_adoption_blocks(self):
        self.host["gamePid"] = self.pid + 1
        write_json(self.session_dir / "host-start.json", self.host)
        self.assert_blocked("START_RECEIPT_IDENTITY_MISMATCH")
        write_json(self.session_dir / "host-start.json", {**self.host, "gamePid": self.pid})
        self.adoption["challenge"] = "not-this-owner"
        write_json(self.runtime / "adoption.json", self.adoption)
        self.assert_blocked("CHALLENGE_IDENTITY_MISMATCH")

    def test_stale_or_unknown_journal_fails_closed(self):
        self.journal["stage"] = "restored"
        write_json(self.journal_path, self.journal)
        self.assert_blocked("UNQUALIFIED_JOURNAL_STATE")
        self.journal["stage"] = "some-new-stage"
        write_json(self.journal_path, self.journal)
        self.assert_blocked("UNQUALIFIED_JOURNAL_STATE")

    def test_remaining_foreign_lease_blocks(self):
        (self.runtime / "lease.txt").write_text("schema=1\n", encoding="utf-8")
        self.assert_blocked("ACTIVE_LEASE_PRESENT")

    def test_absent_start_receipt_blocks(self):
        (self.session_dir / "helper-start.json").unlink()
        self.assert_blocked("RECORD_MISSING_OR_INVALID")


def read_json_for_test(path):
    return json.loads(path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
