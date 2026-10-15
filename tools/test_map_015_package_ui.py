"""Inert MAP-015 packaged City receipt admission checks; never starts UI/game."""

from contextlib import closing
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import time
import unittest

import map_015_package_ui as package_ui


class PackagedCityCameraFreeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="map015-package-ui-", dir=package_ui.ARTIFACTS)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.profile = "map014-native-ui"
        self.session = "owned-session-id"
        self.run = "owned-scan-run-id"
        self.pid = 5432
        self.challenge = "owned-challenge-fixture"
        self.server = 2212
        self.executable = self.root / "LWBridge.Desktop.exe"
        self.native_path = self.root / "live-resource" / "camera-free-scan-result.json"
        self.metrics_path = self.root / "live-resource" / "map-fullworld-probe-metrics.json"
        self.db_path = self.root / "profiles" / self.profile / "map-data" / "map-data.db"
        now = int(time.time() * 1000)
        self.created_ms, self.updated_ms = now - 3000, now + 3000
        self.proof = dict(state="proven", selection="city", selectedTypes=["city"], serverId=self.server,
                          genuineGame=True, controlledProvider=False,
                          proof="actual_packaged_ui_fullworld_selected_scan_and_results",
                          total=20,
                          completedStatus=dict(phase="completed", scanRunId=self.run,
                                               selectedTypes=["city"], serverId=self.server, scanMode="normal"))
        self.native = dict(schemaVersion=1, state="proven", requestId="direct-fixture",
                           challenge=self.challenge,
                           profileId=self.profile, launchSessionId=self.session, gamePid=self.pid,
                           scanRunId=self.run, serverId=self.server, worldId=0,
                           cameraStable=True, curViewRestored=True, completedAois=10000,
                           requestsSent=63, repliesCaptured=63,
                           batches=[dict(indices=list(range(start, min(start + 160, 10000))))
                                    for start in range(0, 10000, 160)],
                           capturedAt=datetime.now(timezone.utc).isoformat())
        self.metrics = dict(schemaVersion=1, source="CurrentClientMapBlockSource.ProbeCameraFreeMapAsync",
                            acquisitionPolicy="direct", phase="prepared",
                            identity=dict(RunId=self.run, ServerId=self.server, WorldId=0,
                                          SelectedTypesKey="city", ScanMode="normal"),
                            uniqueCoveredAois=10000, requiredAois=10000, newlyCoveredAois=10000,
                            uniqueRecordKeys={"city": 20},
                            validatedProbeResponses=63, attributableLuaProbeAttempts=63)
        self.restoration = dict(state="proven", diagnosticResultSucceeded=True, profileId=self.profile,
                                sessionId=self.session, gamePid=self.pid, hostPid=9876)
        self.package = dict(executable=str(self.executable))
        self.write("overview-evidence/owned-session-id/helper-start.json",
                   dict(profileId=self.profile, sessionId=self.session, gamePid=self.pid,
                        challengeSha256=hashlib.sha256(self.challenge.encode()).hexdigest()))
        self.db_path.parent.mkdir(parents=True)
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("CREATE TABLE scan_runs (id TEXT,server_id INTEGER,selected_types TEXT,"
                             "status TEXT,total_blocks INTEGER,completed_blocks INTEGER,failed_blocks INTEGER,"
                             "created_at INTEGER,updated_at INTEGER)")
            database.execute("INSERT INTO scan_runs VALUES(?,?,?,?,?,?,?,?,?)",
                             (self.run, self.server, '["city"]', "completed", 2500, 2500, 0,
                              self.created_ms, self.updated_ms))
            database.commit()
        self.save_receipts()

    def write(self, name, object_):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(object_), encoding="utf-8")

    def save_receipts(self):
        self.write("native-ui-map.json", self.proof)
        self.write("owned-process.json", dict(pid=9876, publish=str(self.executable)))
        self.write("owned-exit.json", dict(exitCode=0, proof=True, error=None,
             restorationAudit=self.restoration, originalScriptsRestored={"LWScripts.data": True},
             recoveryJournalPresent=False))
        self.write("live-resource/camera-free-scan-result.json", self.native)
        self.write("live-resource/map-fullworld-probe-metrics.json", self.metrics)

    def audit(self, mode="city"):
        return package_ui.audit_receipts(self.root, mode, self.package)

    def test_city_proves_exact_direct_sweep_and_sqlite_fallback(self):
        receipt = self.audit()
        self.assertEqual(receipt["cameraFree"]["runId"], self.run)
        self.assertEqual(receipt["cameraFree"]["successfulBatches"], 63)
        self.assertEqual(receipt["cameraFree"]["completedAois"], 10000)
        self.proof["completedStatus"].pop("scanRunId")
        self.native["requestsSent"] = self.metrics["attributableLuaProbeAttempts"] = 65
        self.save_receipts()
        self.assertEqual(self.audit()["cameraFree"]["requestsSent"], 65)

    def test_missing_or_fallback_receipts_fail_closed(self):
        self.native["state"] = "failed"
        self.save_receipts()
        with self.assertRaisesRegex(ValueError, "camera-free acquisition"):
            self.audit()
        self.native["state"] = "proven"
        self.metrics["source"] = "CurrentClientMapBlockSource.ProbeFastCityBatchAsync"
        self.save_receipts()
        with self.assertRaisesRegex(ValueError, "prepared metrics"):
            self.audit()
        self.metrics_path.unlink()
        with self.assertRaises(FileNotFoundError):
            self.audit()

    def test_identity_selection_coverage_and_freshness_rejected(self):
        cases = [
            ("native scan ID", lambda: self.native.update(scanRunId="previous-run")),
            ("wrong native PID", lambda: self.native.update(gamePid=9000)),
            ("wrong challenge", lambda: self.native.update(challenge="old-challenge")),
            ("foreign session", lambda: self.native.update(launchSessionId="old-session")),
            ("UI run ID", lambda: self.proof["completedStatus"].update(scanRunId="other-run")),
            ("UI selected types", lambda: self.proof.update(selectedTypes=["resource"])),
            ("completed selection", lambda: self.proof["completedStatus"].update(selectedTypes=["truck"])),
            ("metric run ID", lambda: self.metrics["identity"].update(RunId="stale-run")),
            ("metric phase", lambda: self.metrics.update(phase="covered")),
            ("metrics incomplete", lambda: self.metrics.update(validatedProbeResponses=62)),
            ("missing City records", lambda: self.metrics["uniqueRecordKeys"].update(city=0)),
            ("requests lower bound", lambda: self.native.update(requestsSent=62)),
            ("requests upper bound", lambda: self.native.update(requestsSent=66)),
            ("missing native reply", lambda: self.native.update(repliesCaptured=62)),
            ("unrestored camera", lambda: self.native.update(cameraStable=False)),
            ("missing AOI", lambda: self.native["batches"][0]["indices"].pop()),
            ("overlapping AOI", lambda: self.native["batches"][1]["indices"].__setitem__(0, 1)),
            ("outdated capture", lambda: self.native.update(capturedAt="2020-01-01T00:00:00Z")),
        ]
        for name, mutate in cases:
            with self.subTest(name=name):
                self.setUp_receipt_defaults()
                mutate()
                self.save_receipts()
                with self.assertRaises((ValueError, FileNotFoundError), msg=name):
                    self.audit()

    def setUp_receipt_defaults(self):
        self.proof["selectedTypes"] = ["city"]
        self.proof["completedStatus"] = dict(phase="completed", scanRunId=self.run,
                                              selectedTypes=["city"], serverId=self.server,
                                              scanMode="normal")
        self.native.update(state="proven", scanRunId=self.run, gamePid=self.pid,
                           launchSessionId=self.session, challenge=self.challenge,
                           cameraStable=True,
                           completedAois=10000, requestsSent=63, repliesCaptured=63,
                           capturedAt=datetime.now(timezone.utc).isoformat(),
                           batches=[dict(indices=list(range(start, min(start + 160, 10000))))
                                    for start in range(0, 10000, 160)])
        self.metrics.update(source="CurrentClientMapBlockSource.ProbeCameraFreeMapAsync",
                            phase="prepared", validatedProbeResponses=63,
                            attributableLuaProbeAttempts=63)
        self.metrics["identity"]["RunId"] = self.run
        self.metrics["uniqueRecordKeys"]["city"] = 20

    def test_stale_metrics_and_non_city_modes(self):
        self.save_receipts()
        old = (self.created_ms - 120_000) / 1000
        os.utime(self.metrics_path, (old, old))
        with self.assertRaisesRegex(ValueError, "stale"):
            self.audit()
        # Older Other proof is supported and intentionally does not require a
        # camera-free artifact; mixed and remote proof code remains untouched.
        self.proof.update(selection="other", selectedTypes=["other"])
        self.proof["completedStatus"].update(selectedTypes=["other"])
        self.write("native-ui-map.json", self.proof)
        self.assertNotIn("cameraFree", self.audit("other"))


if __name__ == "__main__":
    unittest.main()
