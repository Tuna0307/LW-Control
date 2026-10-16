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

    def test_eight_kind_ghost_requires_source_calculated_plunder_share(self):
        selected = package_ui.MODES["all-nontruck"]["selected"]
        witness = dict(displayed=True, sourceCalculatedPlunderShareMatches=True,
                       formula="floor(sum(base_reward_show count for item) * steal_reward_ratio / 100)",
                       stealRewardRatio=30.0, nativeRewardCounts=[300, 42], rowIndex=0)
        results = [dict(kind=kind, serverId=self.server, firstPageRows=1, total=1,
                        nativeSearchRequestId="real-source-request-" + kind,
                        positiveGhostRewardRendering=witness if kind == "ghost" else None)
                   for kind in selected]
        self.proof.update(selection="all-nontruck", selectedTypes=selected,
                          proof="actual_packaged_ui_all_nontruck_fullworld_scan_and_results",
                          browse=dict(ghostNativeRewardDisplayProven=True,
                                      scanRunId=self.run, serverId=self.server, selectedKinds=selected,
                                      cameraFree=dict(source="direct", completedAois=10000,
                                                      repliesCaptured=63, batchesCaptured=63,
                                                      requestsSent=63, cameraStable=True,
                                                      curViewRestored=True, metricsPhase="prepared"),
                                      results=results))
        self.proof["completedStatus"]["selectedTypes"] = selected
        self.metrics["identity"]["SelectedTypesKey"] = "\x1f".join(sorted(selected))
        self.metrics["uniqueRecordKeys"]["ghost"] = 4
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE scan_runs SET selected_types=? WHERE id=?",
                             (json.dumps(selected, separators=(",", ":")), self.run))
            database.commit()
        self.save_receipts()
        self.assertEqual(self.audit("all-nontruck")["cameraFree"]["runId"], self.run)

        for name, mutate in [
            ("missing calculated share", lambda: witness.pop("sourceCalculatedPlunderShareMatches")),
            ("owner rewards mislabeled as share", lambda: witness.update(sourceCalculatedPlunderShareMatches=False)),
            ("missing ratio", lambda: witness.pop("stealRewardRatio")),
            ("invalid ratio", lambda: witness.update(stealRewardRatio=150)),
            ("missing formula", lambda: witness.pop("formula")),
            ("missing actual quantities", lambda: witness.update(nativeRewardCounts=[])),
        ]:
            with self.subTest(name=name):
                original = witness.copy()
                mutate()
                self.save_receipts()
                with self.assertRaisesRegex(ValueError, "plunder-share"):
                    self.audit("all-nontruck")
                witness.clear()
                witness.update(original)

    def test_mixed_eight_truck_filter_requires_fresh_matching_rows(self):
        selected = package_ui.MODES["mixed-eight"]["selected"]
        uuid = "native-truck-witness"
        reward_keys = ["reward:1:1", "reward:7:200364"]
        truck_row = dict(uuid=uuid, serverId=self.server, power=3_000_000,
                         currentGoods=[dict(key=key, count=10) for key in reward_keys])
        witness = dict(gate="genuine_truck_combined_filters", witnessKeys=reward_keys,
                       payload=dict(kind="truck", query=dict(serverId=self.server,
                                    escortPowerMaxMillions=25, itemKeys=reward_keys)),
                       result=dict(total=1, rows=[truck_row]))
        self.proof = dict(proof="normal_production_map_window_start_scan_search_render",
                          state="proven", selectedTypes=selected, serverId=self.server,
                          scan=dict(phase="completed", scanRunId=self.run, serverId=self.server))
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE scan_runs SET selected_types=? WHERE id=?",
                             (json.dumps(selected, separators=(",", ":")), self.run))
            database.execute("CREATE TABLE map_records(kind TEXT,server_id INTEGER,uuid TEXT)")
            database.execute("INSERT INTO map_records VALUES('truck',?,?)", (self.server, uuid))
            database.commit()
        self.write("map014-ui-extras.json", dict(receipts=[witness]))
        (self.root / "map014-truck-filtered.png").write_bytes(b"\x89PNG\r\n\x1a\n")
        self.save_receipts()
        self.assertEqual(self.audit("mixed-eight")["freshTruckFilter"]["scannedTruckCount"], 1)

        for name, mutate in [
            ("unpublished Truck", lambda: truck_row.update(uuid="different-native-uuid")),
            ("power outside filter", lambda: truck_row.update(power=30_000_000)),
            ("missing selected goods", lambda: truck_row.update(currentGoods=[])),
            ("wrong scan", lambda: self.proof["scan"].update(scanRunId="old-run")),
            ("wrong item filters", lambda: witness["payload"]["query"].update(itemKeys=[])),
        ]:
            with self.subTest(name=name):
                self.proof["scan"]["scanRunId"] = self.run
                truck_row.update(uuid=uuid, power=3_000_000,
                                 currentGoods=[dict(key=key, count=10) for key in reward_keys])
                witness["payload"]["query"]["itemKeys"] = reward_keys[:]
                mutate()
                self.save_receipts()
                self.write("map014-ui-extras.json", dict(receipts=[witness]))
                with self.assertRaises(ValueError, msg=name):
                    self.audit("mixed-eight")


class PackagedPublishedFastTruckSourceTests(unittest.TestCase):
    """A completed camera fallback may coexist with a proven native receipt."""

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="map015-fast-publication-", dir=package_ui.ARTIFACTS)
        self.addCleanup(self.temporary.cleanup)
        self.database = Path(self.temporary.name) / "map-data.db"
        self.server = 2212
        self.captured = int(time.time() * 1000)
        with closing(sqlite3.connect(self.database)) as database:
            database.execute("CREATE TABLE map_records(kind TEXT,server_id INTEGER,updated_at INTEGER,data_json TEXT)")
            database.commit()

    def put(self, data):
        with closing(sqlite3.connect(self.database)) as database:
            database.execute("DELETE FROM map_records")
            database.execute("INSERT INTO map_records VALUES('truck',?,?,?)",
                             (self.server, self.captured, json.dumps(data)))
            database.commit()

    def test_verified_fast_publication_and_future_arrival(self):
        self.put(dict(captureProtocol="current_fast_truck_625_v1",
                      arriveTs=self.captured + 100_000))
        self.assertEqual(package_ui.audit_published_fast_truck_rows(self.database, self.server), 1)

    def test_camera_fallback_with_proven_native_receipt_is_rejected(self):
        self.put(dict(arriveTs=self.captured + 100_000))
        with self.assertRaisesRegex(ValueError, "camera fallback"):
            package_ui.audit_published_fast_truck_rows(self.database, self.server)
        self.put(dict(captureProtocol="camera", arriveTs=self.captured + 100_000))
        with self.assertRaisesRegex(ValueError, "camera fallback"):
            package_ui.audit_published_fast_truck_rows(self.database, self.server)

    def test_arrived_at_capture_or_equal_deadline_is_rejected(self):
        for arrived in (self.captured, self.captured - 1):
            with self.subTest(arrived=arrived):
                self.put(dict(captureProtocol="current_fast_truck_625_v1", arriveTs=arrived))
                with self.assertRaisesRegex(ValueError, "already arrived"):
                    package_ui.audit_published_fast_truck_rows(self.database, self.server)


if __name__ == "__main__":
    unittest.main()
