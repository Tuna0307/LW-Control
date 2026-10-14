"""Inert byte/receipt inverses; no subprocess, desktop or game operations."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import run_ui


class RestorationAuditTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.installed = self.root / "installed"
        self.game = self.root / "game"
        self.backup = self.root / "overview-bridge-backups" / "owned-backup"
        self.evidence = self.root / "overview-evidence"
        self.session = self.evidence / "owned-session"
        for path in (self.installed, self.backup, self.session):
            path.mkdir(parents=True)
        self.bytes24 = {name: ("old-" + name).encode() for name in run_ui.EXPECTED}
        self.bytes26 = {name: ("new-" + name).encode() for name in run_ui.EXPECTED}
        self.bytes26["version.txt"] = self.bytes24["version.txt"]
        self.h24 = {name: hashlib.sha256(data).hexdigest() for name, data in self.bytes24.items()}
        self.h26 = {name: hashlib.sha256(data).hexdigest() for name, data in self.bytes26.items()}
        self.patches = [patch.object(run_ui, "EXPECTED", self.h24), patch.object(run_ui, "EXPECTED_26", self.h26)]
        for item in self.patches:
            item.start()
        for name, data in self.bytes26.items():
            (self.installed / name).write_bytes(data)
            (self.backup / name).write_bytes(data)
        files = {role: dict(path=str(self.installed / name), sha256=self.h26[name], size=len(self.bytes26[name]))
                 for role, name in run_ui.FILE_ROLES.items()}
        self.client = dict(contentVersion=26, entryCount=18743, packageSha256=self.h26["LWScripts.data"],
            compatibilityPolicy="lwbridge-current-client-critical-anchors-3",
            gameSha256="905c98c1f89841f90b492556192ba0642f3d209a873cb8c1f7b3c340aca0733d",
            assemblyCSharpSha256="bfb740b4570c58bd2bcc7fb83f9b83d8121ce10fb1bf49040e9fb8b08e958b3e",
            xluaSha256="d22d912f031c60f2649fdaf76d359d695511f7a37b93cd637b557f8346569d45",
            luaEntrySha256="50f3ae906a8e9898549c4ea740eedc772a88eb2979e165eb35733192d100a137")
        identity = dict(profileId="map014-native-ui", sessionId="owned-session", gamePid=101,
                        gamePath=str(self.game / "Game/LastWar.exe"), gameStartedAtUtc="2026-10-14T08:28:15Z")
        self.write(self.root / "owned-process.json", dict(pid=202, publish=str(self.root / "publish/app.exe")))
        self.write(self.root / "owned-exit.json", dict(pid=202, exitCode=0, proof=True, error=None,
                   originalScriptsRestored={name: False for name in self.h24}))
        self.write(self.root / "config.json", dict(profileId="map014-native-ui", gameRoot=str(self.game)))
        self.start = dict(identity, ok=True, challengeSha256="challenge-sha", currentClient=self.client,
                          candidate=dict(packageSha256="c" * 64))
        self.stop = dict(identity, ok=True, gameRunning=False,
            close=dict(pid=101, path=identity["gamePath"], startedAtUtc=identity["gameStartedAtUtc"],
                       processExited=True, accepted=True, exitProof=dict(established=True)),
            restore=dict(restored=True, packageSha256=self.h26["LWScripts.data"],
                         originalFiles=deepcopy(files), restoredFiles=deepcopy(files)))
        self.write(self.session / "helper-start.json", self.start)
        self.write(self.session / "helper-stop.json", self.stop)
        self.write(self.session / "host-start.json", dict(identity, hostProcess=dict(path=str(self.root / "publish/app.exe"))))
        self.write(self.session / "host-stop.json", dict(profileId=identity["profileId"], sessionId=identity["sessionId"],
            helper=dict(self.stop, evidencePath=str(self.session / "helper-stop.json"))))
        self.write(self.backup / "manifest.json", dict(identity, requestId="owned-session", challengeSha256="challenge-sha",
            backupPath=str(self.backup), stage="restored", originalFiles=files, candidate=deepcopy(self.start["candidate"])))
        self.write(self.evidence / "official-settled-client.json", dict(schemaVersion=1, gameRoot=str(self.game),
                   packageSha256=self.h26["LWScripts.data"], settledAtUtc="2026-10-14T08:27:58+00:00"))

    def tearDown(self):
        for item in reversed(self.patches):
            item.stop()
        self.temporary.cleanup()

    @staticmethod
    def write(path, data):
        path.write_text(json.dumps(data), encoding="utf-8")

    def audit(self, prior=None, processes=None):
        return run_ui.audit_session_restoration(self.root, prior or self.h24, installed_dir=self.installed,
            game_root=self.game, process_rows=processes or [], normal_recovery_journal=self.root / "normal-recovery.json")

    def test_same_session_settled_original_passes_and_historical_failure_unchanged(self):
        old_receipt = (self.root / "owned-exit.json").read_bytes()
        result = self.audit()
        self.assertTrue(result["officialSettledOriginalChangedBeforeInstall"])
        self.assertEqual(result["contentVersion"], 26)
        self.assertEqual(result["installedHashes"], self.h26)
        self.assertEqual((self.root / "owned-exit.json").read_bytes(), old_receipt)
        self.assertFalse(self.audit(prior=self.h26)["officialSettledOriginalChangedBeforeInstall"])

    def test_inverse_receipt_and_byte_failures(self):
        mutations = {
            "foreign PID": (self.session / "helper-stop.json", lambda value: value.update(gamePid=999)),
            "foreign session": (self.session / "helper-stop.json", lambda value: value.update(sessionId="foreign")),
            "failed Stop": (self.session / "helper-stop.json", lambda value: value.update(ok=False)),
            "unproven exit": (self.session / "helper-stop.json", lambda value: value["close"].update(processExited=False)),
            "foreign profile": (self.backup / "manifest.json", lambda value: value.update(profileId="owner-profile")),
            "wrong creation time": (self.backup / "manifest.json", lambda value: value.update(gameStartedAtUtc="other")),
            "foreign challenge": (self.backup / "manifest.json", lambda value: value.update(challengeSha256="foreign")),
            "candidate journal": (self.backup / "manifest.json", lambda value: value.update(stage="active")),
            "foreign candidate": (self.backup / "manifest.json", lambda value: value["candidate"].update(packageSha256="f" * 64)),
            "wrong backup hash": (self.backup / "manifest.json", lambda value: value["originalFiles"]["data"].update(sha256="bad")),
            "unsupported version": (self.session / "helper-start.json", lambda value: value["currentClient"].update(contentVersion=27)),
            "changed critical client": (self.session / "helper-start.json", lambda value: value["currentClient"].update(xluaSha256="bad")),
            "late settled record": (self.evidence / "official-settled-client.json", lambda value: value.update(settledAtUtc="2026-10-14T08:29:00+00:00")),
        }
        for label, (path, mutate) in mutations.items():
            with self.subTest(label=label):
                before = path.read_bytes()
                value = json.loads(before)
                mutate(value)
                self.write(path, value)
                with self.assertRaises((RuntimeError, KeyError, ValueError)):
                    self.audit()
                path.write_bytes(before)
        for path in (self.backup / "LWScripts.data", self.installed / "LWScripts.data"):
            before = path.read_bytes()
            path.write_bytes(b"candidate-or-corrupted-bytes")
            with self.assertRaises(RuntimeError):
                self.audit()
            path.write_bytes(before)
        missing = self.backup / "LWScripts.txt"
        before = missing.read_bytes()
        missing.unlink()
        with self.assertRaises(OSError):
            self.audit()
        missing.write_bytes(before)
        self.audit()  # Every inverse restored its own inert inputs.

    def test_missing_transition_journal_and_surviving_process_rejected(self):
        settled = self.evidence / "official-settled-client.json"
        before = settled.read_bytes()
        settled.unlink()
        with self.assertRaises(OSError):
            self.audit()
        settled.write_bytes(before)
        for journal in (self.root / "overview-bridge/recovery.json", self.root / "normal-recovery.json"):
            journal.parent.mkdir(exist_ok=True)
            journal.write_text("{}")
            with self.assertRaises(RuntimeError):
                self.audit()
            journal.unlink()
        for row in (dict(ProcessId=101), dict(ProcessId=202),
                    dict(ProcessId=303, ExecutablePath=str(self.game / "LastWarLauncher.exe")),
                    dict(ProcessId=304, Name="LastWar.exe", ExecutablePath=None)):
            with self.assertRaises(RuntimeError):
                self.audit(processes=[row])
        self.audit(processes=[dict(ProcessId=404, ExecutablePath=str(self.root / "unrelated/app.exe"))])


if __name__ == "__main__":
    unittest.main()
