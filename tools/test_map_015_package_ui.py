"""Inert MAP-015 packaged City receipt admission checks; never starts UI/game."""

from contextlib import closing
from datetime import datetime, timezone, timedelta
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import tempfile
import time
import unittest
import importlib.util

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

    def test_all_ten_keeps_public_other_but_metrics_use_shared_treasure_census(self):
        selected = package_ui.MODES["all-ten"]["selected"]
        native_selected = sorted({"treasure" if kind == "other" else kind for kind in selected})
        results = [dict(kind=kind, serverId=self.server,
                        firstPageRows=0, total=0,
                        nativeSearchRequestId="real-source-request-" + kind,
                        positiveGhostRewardRendering=None)
                   for kind in selected]
        self.proof.update(selection="all-ten", selectedTypes=selected,
                          proof="actual_packaged_ui_all_ten_fullworld_scan_and_results",
                          browse=dict(ghostNativeRewardDisplayProven=False,
                                      scanRunId=self.run, serverId=self.server, selectedKinds=selected,
                                      cameraFree=dict(source="direct", completedAois=10000,
                                                      repliesCaptured=63, batchesCaptured=63,
                                                      requestsSent=63, cameraStable=True,
                                                      curViewRestored=True, metricsPhase="prepared"),
                                      results=results))
        self.proof["completedStatus"]["selectedTypes"] = selected
        self.metrics["identity"]["SelectedTypesKey"] = "\x1f".join(native_selected)
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE scan_runs SET selected_types=? WHERE id=?",
                             (json.dumps(selected, separators=(",", ":")), self.run))
            database.commit()
        self.save_receipts()
        receipt = package_ui.audit_city_camera_free(self.root, self.proof, self.restoration, "all-ten")
        self.assertEqual(receipt["runId"], self.run)
        self.assertIn("other", selected)
        self.assertNotIn("other", native_selected)

        self.metrics["identity"]["SelectedTypesKey"] = "\x1f".join(sorted(selected))
        self.save_receipts()
        with self.assertRaisesRegex(ValueError, "prepared metrics"):
            package_ui.audit_city_camera_free(self.root, self.proof, self.restoration, "all-ten")

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

    def test_qualified_fast_truck_proof_requires_exact_20_per_second_fence25(self):
        proof = dict(pushes=625, requestsPerSecond=20, responseFenceSize=25,
                     cameraStable=True, curViewRestored=True, trainRecordCount=3)
        package_ui.require_qualified_fast_truck_proof(proof)
        for key, value in (("requestsPerSecond", 15), ("responseFenceSize", 10)):
            with self.subTest(key=key, value=value):
                mutated = proof.copy()
                mutated[key] = value
                with self.assertRaisesRegex(ValueError, "20/s fence25"):
                    package_ui.require_qualified_fast_truck_proof(mutated)


class PackagedRailwaySourceTests(unittest.TestCase):
    """Inert owned receipts prove both positive Railway and genuine empty coverage."""

    # Reuse only inert owner/SQLite setup and file helpers, not the City tests.
    setUp = PackagedCityCameraFreeTests.setUp
    write = PackagedCityCameraFreeTests.write
    save_receipts = PackagedCityCameraFreeTests.save_receipts
    audit = PackagedCityCameraFreeTests.audit

    def railway_defaults(self, positive=True):
        capture_ms = int(time.time() * 1000)
        row = dict(uuid="native-railway", trainType=2, serverId=self.server, worldId=0,
                   x=620, y=420, trainQuality=5, trainCfgId=152, carriageNum=6,
                   capturedAtMs=capture_ms,
                   source="WorldScene.MarchDataManager.GetAllMarchesByCS+WorldMarch.train")
        identities = [row] if positive else []
        screenshot = self.root / "map014-railway-packaged.png"
        screenshot.write_bytes(b"\x89PNG\r\n\x1a\n")
        self.proof = dict(state="proven",
                          proof="actual_packaged_ui_fast_railway_fullworld_scan_and_results",
                          genuineGame=True, controlledProvider=False, selection="railway",
                          selectedTypes=["railway"], serverId=self.server,
                          completedStatus=dict(phase="completed", scanRunId=self.run,
                                               selectedTypes=["railway"], serverId=self.server,
                                               totalBlocks=2500, readBlocks=2500,
                                               failedBlocks=0, unreadBlocks=0),
                          selectedResponse=dict(payload=dict(kind="railway",
                                                             query=dict(serverId=self.server)),
                                                result=dict(total=len(identities), rows=[
                                                    dict(uuid=row["uuid"], serverId=self.server)
                                                    for row in identities])),
                          total=len(identities),
                          mountedDom=dict(tablePresent=True, displayedRows=len(identities)),
                          screenshot=str(screenshot))
        self.railway_native = dict(
            schemaVersion=1, state="proven", requestId="truck-native-railway",
            challenge=self.challenge, profileId=self.profile, launchSessionId=self.session,
            gamePid=self.pid, scanRunId=self.run, serverId=self.server, worldId=0,
            capturedAt=datetime.now(timezone.utc).isoformat(), cameraStable=True,
            curViewRestored=True, railwayOnly=True, railwayIncluded=True,
            initialRequestsSent=625, rectanglesCompleted=625, unprovenRectangles=0,
            requestsPerSecond=20, responseFenceSize=25,
            responseAttribution="bounded_sequential_fences",
            repairPasses=1, requestsSent=675, repairRequestsSent=50, pushesObserved=675,
            trainRecordCount=0, trainRecords=[], railwayRecordCount=len(identities),
            railwayRecords=identities)
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE scan_runs SET selected_types=?",
                             ('["railway"]',))
            database.execute("CREATE TABLE IF NOT EXISTS map_records("
                             "kind TEXT,server_id INTEGER,record_key TEXT,uuid TEXT,"
                             "updated_at INTEGER,data_json TEXT)")
            database.execute("DELETE FROM map_records")
            if positive:
                database.execute(
                    "INSERT INTO map_records VALUES (?,?,?,?,?,?)",
                    ("railway", self.server, row["uuid"], row["uuid"], capture_ms,
                     json.dumps(dict(uuid=row["uuid"], updatedAt=capture_ms,
                                     trainType=2, source=row["source"],
                                     captureProtocol="current_fast_railway_625_v1"))))
            database.commit()
        self.save_railway()

    def save_railway(self):
        self.save_receipts()
        self.write("live-resource/fast-truck-scan-result.json", self.railway_native)

    def test_railway_positive_and_verified_empty_have_exact_owned_provenance(self):
        for positive in (True, False):
            with self.subTest(positive=positive):
                self.railway_defaults(positive)
                receipt = self.audit("railway")["fastRailway"]
                self.assertEqual(receipt["railwayCount"], int(positive))
                self.assertEqual(receipt["verifiedEmpty"], not positive)
                self.assertEqual(receipt["rectanglesVerified"], 625)

    def test_railway_native_mutations_fail_closed_including_empty_coverage(self):
        cases = [
            ("missing rectangle", lambda: self.railway_native.update(rectanglesCompleted=624)),
            ("unverified rectangle", lambda: self.railway_native.update(unprovenRectangles=1)),
            ("not Railway-only", lambda: self.railway_native.update(railwayOnly=False)),
            ("not typed Railway", lambda: self.railway_native["railwayRecords"][0].update(trainType=1)),
            ("wrong requested server", lambda: self.railway_native.update(serverId=2211)),
            ("wrong native owner", lambda: self.railway_native.update(launchSessionId="other")),
            ("wrong native challenge", lambda: self.railway_native.update(challenge="other")),
            ("wrong scan", lambda: self.railway_native.update(scanRunId="other")),
            ("camera moved", lambda: self.railway_native.update(cameraStable=False)),
            ("camera memberships not restored", lambda: self.railway_native.update(curViewRestored=False)),
            ("no native type list", lambda: self.railway_native.update(railwayRecords=[])),
            ("forged extra native count", lambda: self.railway_native.update(railwayRecordCount=2)),
            ("wrong native source", lambda: self.railway_native["railwayRecords"][0].update(source="camera")),
            ("wrong capture time", lambda: self.railway_native["railwayRecords"][0].update(capturedAtMs=0)),
            ("repeated Railway UUID", lambda: (
                self.railway_native["railwayRecords"].append(
                    self.railway_native["railwayRecords"][0].copy()),
                self.railway_native.update(railwayRecordCount=2))),
        ]
        for name, mutate in cases:
            with self.subTest(case=name):
                self.railway_defaults(positive=True)
                mutate()
                self.save_railway()
                with self.assertRaises(ValueError):
                    self.audit("railway")

    def test_railway_empty_cannot_hide_camera_fallback_or_foreign_ui(self):
        for positive in (True, False):
            with self.subTest(positive=positive):
                self.railway_defaults(positive)
                self.write("live-resource/map015-direct-fallback.json",
                           dict(runId=self.run, serverId=self.server,
                                reason="managed importer failed; camera route used"))
                with self.assertRaisesRegex(ValueError, "camera fallback"):
                    self.audit("railway")
                (self.root / "live-resource/map015-direct-fallback.json").unlink()
        self.railway_defaults(positive=False)
        self.railway_native["rectanglesCompleted"] = 624
        self.save_railway()
        with self.assertRaisesRegex(ValueError, "625 verified"):
            self.audit("railway")
        self.railway_defaults(positive=False)
        self.proof["selectedResponse"]["result"]["total"] = 1
        self.proof["total"] = 1
        self.save_railway()
        with self.assertRaisesRegex(ValueError, "counts disagree"):
            self.audit("railway")

    def test_railway_published_camera_rows_and_wrong_ui_are_rejected(self):
        self.railway_defaults(positive=True)
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE map_records SET data_json=?",
                             (json.dumps(dict(uuid="native-railway",
                                              updatedAt=self.railway_native["railwayRecords"][0]["capturedAtMs"],
                                              trainType=2, source=self.railway_native["railwayRecords"][0]["source"],
                                              captureProtocol="camera")),))
            database.commit()
        with self.assertRaisesRegex(ValueError, "camera fallback"):
            self.audit("railway")
        self.railway_defaults(positive=True)
        self.proof["selectedResponse"]["payload"]["query"]["serverId"] = 2211
        self.save_railway()
        with self.assertRaisesRegex(ValueError, "same-server"):
            self.audit("railway")
        self.railway_defaults(positive=True)
        self.proof["screenshot"] = str(self.root / "unrelated.png")
        self.save_railway()
        with self.assertRaisesRegex(ValueError, "screenshot"):
            self.audit("railway")


class PackagedRemoteRailwayTests(unittest.TestCase):
    """Inert remote Auto witnesses never navigate to a stored row or Clear."""

    setUp = PackagedCityCameraFreeTests.setUp
    write = PackagedCityCameraFreeTests.write
    save_receipts = PackagedCityCameraFreeTests.save_receipts
    audit = PackagedCityCameraFreeTests.audit
    save_railway = PackagedRailwaySourceTests.save_railway
    railway_defaults = PackagedRailwaySourceTests.railway_defaults

    def remote_defaults(self, positive=True):
        self.server = 2212
        self.railway_defaults(positive)
        self.proof = dict(state="proven", proof="real_native_auto_remote_railway_2211_fast_625_return")
        screenshot = self.root / "map016-remote-railway-2211.png"
        screenshot.write_bytes(b"\x89PNG\r\n\x1a\n")
        self.original_server = self.server
        self.server = 2211
        self.railway_native["serverId"] = self.server
        for row in self.railway_native["railwayRecords"]:
            row["serverId"] = self.server
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("UPDATE scan_runs SET server_id=?", (self.server,))
            database.execute("UPDATE map_records SET server_id=?", (self.server,))
            database.commit()
        count = int(positive)
        payload = dict(kind="railway", query=dict(serverId=self.server))
        selected = dict(
            stage="railway_remote_ui_saved_result", scanRunId=self.run,
            serverId=self.server, selectedServerId=self.server if positive else None,
            selectedResponse=dict(payload=payload,
                                  result=dict(total=count,rows=(
                                      [dict(uuid="native-railway",serverId=self.server)]
                                      if positive else []))),
            completedStatus=dict(phase="completed",scanRunId=self.run,
                                 source="persisted_completed_scan_run",
                                 serverId=self.server,selectedTypes=["railway"],
                                 totalBlocks=2500,readBlocks=2500,failedBlocks=0,unreadBlocks=0),
            postReturnLiveStatus=dict(serverId=self.original_server,isReading=False,
                                      phase="idle"),
            total=count,mountedDom=dict(tablePresent=True,displayedRows=count,
                                        **(dict(serverOptionAbsent=True) if not positive else {})),
            screenshot=str(screenshot),
            source="real_packaged_webview_map_search" if positive
                   else "production_map_search_verified_empty")
        from datetime import timedelta
        t0 = datetime.now(timezone.utc) - timedelta(seconds=3)
        t1 = t0 + timedelta(seconds=1)
        t2 = t1 + timedelta(seconds=1)
        t3 = t2 + timedelta(seconds=1)
        stages = [
            dict(stage="railway_original_server",serverId=self.original_server),
            dict(stage="railway_auto_config_persisted",
                 serverIds=[self.server],selectedTypes=["railway"],
                 scanMode="fast",returnToOriginalServer=True),
            dict(stage="railway_auto_returned_home",
                 originalServerId=self.original_server,targetServerId=self.server,
                 liveServerId=self.original_server,scanRunId=self.run,
                 nativeRailwayRecords=count,nativeRectangles=625,
                 lastCycle=dict(outcome="completed", originalServerId=self.original_server,
                                targetServerIds=[self.server],completedServerIds=[self.server],
                                lastError=None),
                 triggeredAt=t0.isoformat(),sawRemoteAt=t1.isoformat(),
                 nativeReceiptAt=t2.isoformat(),observedHomeAt=t3.isoformat(),
                 switchObservedMilliseconds=1000,
                 sweepElapsedSeconds=31.5,returnObservedMilliseconds=1000),
            selected,
            dict(stage="cleanup",homeServer=self.original_server,
                 liveServerId=self.original_server,restored=True,
                 autoEnabled=False,autoRunning=False,noAdditionalNavigation=True)]
        self.remote_receipt = dict(
            proof=self.proof["proof"],genuineGame=True,controlledProvider=False,
            homeServer=self.original_server,remoteServer=self.server,
            profileId=self.profile,sessionId=self.session,gamePid=self.pid,
            results=stages)
        self.save_remote()

    def save_remote(self):
        self.save_railway()
        self.write("map014-remote-ui.json", self.remote_receipt)

    def test_positive_and_verified_empty_return_exact_original_server(self):
        for positive in (True,False):
            with self.subTest(positive=positive):
                self.remote_defaults(positive)
                result = self.audit("remote-railway")["remoteRailway"]
                self.assertEqual(result["railwayCount"], int(positive))
                self.assertEqual(result["restoredServerId"], self.original_server)
                self.assertTrue(result["noCoordinateJump"])
                self.assertTrue(result["noClear"])

    def test_remote_owner_return_and_unwanted_actions_fail_closed(self):
        for label, mutate in [
            ("wrong original", lambda: self.remote_receipt["results"][2].update(originalServerId=2211)),
            ("different completed target", lambda: self.remote_receipt["results"][2]["lastCycle"].update(completedServerIds=[2212])),
            ("failed return", lambda: self.remote_receipt["results"][-1].update(restored=False)),
            ("phantom empty server", lambda: self.remote_receipt["results"][-2].update(selectedServerId=2211)),
            ("replaced helper", lambda: self.remote_receipt.update(sessionId="other")),
            ("extra Jump", lambda: self.remote_receipt["results"].insert(3,dict(stage="remote_ui_stored_row_native_jump"))),
            ("extra Clear", lambda: self.remote_receipt["results"].insert(3,dict(stage="auto_clear_ui_correct_server"))),
            ("wrong source", lambda: self.remote_receipt["results"][-2].update(source="synthetic")),
            ("false return duration", lambda: self.remote_receipt["results"][2].update(returnObservedMilliseconds=0)),
            ("broken native coverage", lambda: self.railway_native.update(rectanglesCompleted=624)),
            ("forged persisted provenance", lambda: self.remote_receipt["results"][-2]["completedStatus"].update(source="ui_status")),
            ("active after return", lambda: self.remote_receipt["results"][-2]["postReturnLiveStatus"].update(isReading=True)),
        ]:
            with self.subTest(case=label):
                self.remote_defaults(positive=False)
                mutate()
                self.save_remote()
                with self.assertRaises(ValueError):
                    self.audit("remote-railway")


class PackagedAutoAllNineProofTests(unittest.TestCase):
    """Inert fail-closed Auto timing/ownership evidence; never starts game/app."""

    setUp = PackagedCityCameraFreeTests.setUp
    write = PackagedCityCameraFreeTests.write
    save_receipts = PackagedCityCameraFreeTests.save_receipts

    def make_auto(self, mode="auto-all-nine"):
        kinds = package_ui.MODES[mode]["selected"]
        self.assertTrue(package_ui.MODES[mode]["remote"])
        self.assertEqual(package_ui.MODES[mode]["args"], [mode])
        runner = (package_ui.ROOT / "tools/map_014_lead_native/run_ui.py").read_text(encoding="utf-8")
        env_name = "LWBRIDGE_MAP016_AUTO_ALL_TEN" if mode == "auto-all-ten" else "LWBRIDGE_MAP016_AUTO_ALL_NINE"
        self.assertIn(f'{env_name}="1" if mode == "{mode}"', runner)
        t = int(time.time() * 1000)
        metrics = dict(runId=self.run, serverId=2211, scanMode="fast",
                       sourceCaptureMilliseconds=150000,
                       checkpointMilliseconds=7000, publicationMilliseconds=2000,
                       wholeEngineMilliseconds=159000)
        proof_name = ("actual_packaged_ui_auto_all_ten_run_now_fullworld_scan_and_results"
                      if mode == "auto-all-ten" else
                      "actual_packaged_ui_auto_all_nine_run_now_fullworld_scan_and_results")
        self.proof = dict(state="proven", selection=mode,
                          proof=proof_name,
                          genuineGame=True, controlledProvider=False,
                          profileId=self.profile, gamePid=self.pid,
                          selectedTypes=kinds, serverId=2211,
                          completedStatus=dict(phase="completed", scanRunId=self.run,
                                               serverId=2211, selectedTypes=kinds,
                                               scanMode="fast", isReading=False,
                                               totalBlocks=2500, readBlocks=2500,
                                               failedBlocks=0, unreadBlocks=0),
                          auto=dict(originalServerId=2212, targetServerId=2211,
                                    restoredServerId=2212, runNowClicks=1,
                                    noIntervalRunBeforeExplicit=True,
                                    sawRunning=True, sawRemote=True,
                                    serverIds=[2211], selectedTypes=kinds,
                                    returnToOriginalServer=True, scanMode="fast",
                                    cleanupEnabled=False, cleanupRunning=False,
                                    triggeredAt=datetime.fromtimestamp((t - 1000) / 1000, timezone.utc).isoformat(),
                                    lastCycle=dict(outcome="completed", lastError=None,
                                                   originalServerId=2212, targetServerIds=[2211],
                                                   completedServerIds=[2211], startedAt=t,
                                                   completedAt=t + 160000)),
                          browse=dict(serverId=2211, scanRunId=self.run, results=[]),
                          timings=metrics)
        for kind in kinds:
            screenshot = self.root / (kind + ".png")
            screenshot.write_bytes(b"\x89PNG\r\n\x1a\n" + b"x" * 1200)
            self.proof["browse"]["results"].append(dict(
                kind=kind, serverId=2211, total=0, firstPageRows=0,
                nativeSearchRequestId="native-" + kind, screenshotPath=str(screenshot)))
        self.write("live-resource/map-engine-stage-metrics.json", metrics)

    def audit_auto(self, mode="auto-all-nine"):
        return package_ui.audit_auto_all_nine(self.root, self.proof, self.restoration, mode)

    def test_exact_one_auto_run_and_time_admission(self):
        self.make_auto()
        result = self.audit_auto()
        self.assertEqual(result["runNowClicks"], 1)
        self.assertEqual(result["restoredServerId"], 2212)
        self.assertEqual(result["scanRunId"], self.run)
        self.assertEqual(result["timings"]["wholeEngineMilliseconds"], 159000)

    def test_all_ten_auto_includes_other_and_fails_closed(self):
        self.make_auto("auto-all-ten")
        self.assertEqual(self.proof["selectedTypes"], package_ui.KIND_ORDER)
        self.assertIn("other", self.proof["selectedTypes"])
        result = self.audit_auto("auto-all-ten")
        self.assertEqual(result["scanRunId"], self.run)
        self.proof["auto"]["selectedTypes"] = [kind for kind in package_ui.KIND_ORDER if kind != "other"]
        with self.assertRaises(ValueError):
            self.audit_auto("auto-all-ten")

    def test_auto_cannot_substitute_interval_manual_or_foreign_owner(self):
        mutations = [
            ("two Run Now clicks", lambda: self.proof["auto"].update(runNowClicks=2)),
            ("interval started", lambda: self.proof["auto"].update(noIntervalRunBeforeExplicit=False)),
            ("no running observation", lambda: self.proof["auto"].update(sawRunning=False)),
            ("no server visit", lambda: self.proof["auto"].update(sawRemote=False)),
            ("not returned", lambda: self.proof["auto"].update(restoredServerId=2211)),
            ("still enabled", lambda: self.proof["auto"].update(cleanupEnabled=True)),
            ("wrong target", lambda: self.proof["auto"].update(targetServerId=2212)),
            ("wrong types", lambda: self.proof["auto"].update(selectedTypes=["city"])),
            ("missing return setting", lambda: self.proof["auto"].update(returnToOriginalServer=False)),
            ("failed Auto cycle", lambda: self.proof["auto"]["lastCycle"].update(outcome="failed")),
            ("not completed target", lambda: self.proof["auto"]["lastCycle"].update(completedServerIds=[])),
            ("cycle before click", lambda: self.proof["auto"]["lastCycle"].update(startedAt=100)),
            ("wrong SQLite selection", lambda: self.proof["completedStatus"].update(selectedTypes=["city"])),
            ("2499 blocks", lambda: self.proof["completedStatus"].update(readBlocks=2499)),
            ("stale run", lambda: self.proof["browse"].update(scanRunId="other")),
            ("wrong helper", lambda: self.proof.update(gamePid=777)),
            ("extra UI kind", lambda: self.proof["browse"]["results"].append(
                self.proof["browse"]["results"][0].copy())),
            ("foreign search", lambda: self.proof["browse"]["results"][2].update(serverId=2212)),
            ("missing search id", lambda: self.proof["browse"]["results"][0].update(nativeSearchRequestId=None)),
            ("foreign screenshot", lambda: self.proof["browse"]["results"][0].update(
                screenshotPath=str(self.root.parent / "foreign.png"))),
            ("wrong timing run", lambda: self.proof["timings"].update(runId="old-run")),
            ("missing time", lambda: self.proof["timings"].update(checkpointMilliseconds=0)),
            ("fake duration", lambda: self.proof["timings"].update(wholeEngineMilliseconds=1)),
        ]
        for label, mutate in mutations:
            with self.subTest(label=label):
                self.make_auto()
                mutate()
                with self.assertRaises((ValueError, FileNotFoundError)):
                    self.audit_auto()


class PackagedSavedClearReopenProofTests(unittest.TestCase):
    """Reopen and remote Clear cannot be proven by an old/foreign test run."""

    setUp = PackagedCityCameraFreeTests.setUp
    write = PackagedCityCameraFreeTests.write
    save_receipts = PackagedCityCameraFreeTests.save_receipts

    def make_saved(self):
        self.assertIn("saved-clear-reopen", package_ui.MODES)
        self.assertTrue(package_ui.MODES["saved-clear-reopen"]["remote"])
        self.assertIn('LWBRIDGE_MAP016_SAVED_CLEAR_REOPEN="1" if mode == "saved-clear-reopen"',
                      (package_ui.ROOT / "tools/map_014_lead_native/run_ui.py").read_text(encoding="utf-8"))
        self.assertIn('"saved-clear-reopen"', (package_ui.ROOT /
                      "tools/map_014_lead_native/run_ui.py").read_text(encoding="utf-8"))
        now = int(time.time() * 1000)
        self.proof = dict(state="proven",
                          proof="real_map016_saved_multi_server_clear_reopen",
                          profileId=self.profile, gamePid=self.pid)
        self.saved = dict(schemaVersion=1, proof=self.proof["proof"],
                          genuineGame=True, controlledProvider=False,
                          profileId=self.profile, sessionId=self.session, gamePid=self.pid,
                          homeServer=2212, remoteServer=2213,
                          stages=[
                              dict(stage="preserved_home", serverId=2212, count=3,
                                   priorRemote=0, beforeServers=[2212]),
                              dict(stage="two_server_auto_published",
                                   requestedAt=datetime.fromtimestamp((now-1000)/1000, timezone.utc).isoformat(),
                                   scan=dict(outcome="completed", lastError=None,
                                             startedAt=now, completedAt=now+120000,
                                             originalServerId=2212, targetServerIds=[2213],
                                             completedServerIds=[2213]),
                                   observedRemote=True, liveServerId=2212,
                                   homeRows=3, remoteRows=5, savedServers=[2212,2213]),
                              dict(stage="clear_remote_preserve_home", clearedServerId=2213,
                                   remoteRows=0, homeRows=3, savedServers=[2212],
                                   liveServerId=2212),
                              dict(stage="reopened_saved_dropdown", savedServers=[2212],
                                   absentServerId=2213, retainedServerId=2212,
                                   retainedRows=3, liveServerId=2212,
                                   reloadType="same_exact_owned_app_webview"),
                              dict(stage="cleanup", restored=True, autoEnabled=False,
                                   autoRunning=False, liveServerId=2212)])
        self.native.update(serverId=2213)
        self.metrics["identity"].update(ServerId=2213, SelectedTypesKey="resource", ScanMode="fast")
        self.metrics["uniqueRecordKeys"]["resource"] = 5
        self.metrics["source"] = "CurrentClientMapBlockSource.ProbeCameraFreeMapAsync"
        self.metrics.update(phase="prepared", validatedProbeResponses=63, uniqueCoveredAois=10000)
        self.stage = dict(runId=self.run, serverId=2213, scanMode="fast",
                          sourceCaptureMilliseconds=31000, checkpointMilliseconds=250,
                          publicationMilliseconds=150, wholeEngineMilliseconds=31400)
        self.write("live-resource/map-engine-stage-metrics.json", self.stage)
        self.write("source.json", dict(genuine=True, fixture=False,
                                     mode="saved-clear-reopen", sha256="a"*64))
        self.write("overview-evidence/owned-session-id/helper-start.json",
                   dict(profileId=self.profile, sessionId=self.session, gamePid=self.pid,
                        challengeSha256=hashlib.sha256(self.challenge.encode()).hexdigest()))
        for name in ("map016-saved-before-clear.png", "map016-saved-after-clear.png",
                     "map016-saved-reopened-manual.png", "map016-saved-reopened-auto.png",
                     "map016-saved-after-reopen.png"):
            (self.root / name).write_bytes(b"\x89PNG\r\n\x1a\n" + b"x"*1200)
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("CREATE TABLE IF NOT EXISTS map_records("
                             "kind TEXT,server_id INTEGER,record_key TEXT,uuid TEXT,"
                             "updated_at INTEGER,data_json TEXT)")
            database.execute("DELETE FROM map_records")
            database.executemany("INSERT INTO map_records VALUES (?,?,?,?,?,?)",
                                 [("resource",2212,str(i),str(i),now,"{}") for i in range(3)])
            database.commit()
        self.write("map016-saved-clear-reopen.json", self.saved)
        self.save_receipts()

    def audit_saved(self):
        return package_ui.audit_saved_clear_reopen(self.root, self.proof, self.restoration)

    def test_real_clear_saved_auto_and_reopen_require_source_receipt(self):
        self.make_saved()
        accepted = self.audit_saved()
        self.assertEqual(accepted["preservedResourceRows"], 3)
        self.assertEqual(accepted["nativeAois"], 10000)
        self.assertTrue(accepted["reopenedManualAndAuto"])

    def test_saved_clear_reopen_negative_mutations(self):
        cases = [
            ("foreign owner", lambda: self.saved.update(sessionId="old")),
            ("synthetic", lambda: self.saved.update(genuineGame=False)),
            ("wrong stage", lambda: self.saved["stages"][1].update(stage="extra_jump")),
            ("missing first home", lambda: self.saved["stages"][0].update(count=0)),
            ("remote preexists", lambda: self.saved["stages"][0].update(priorRemote=4)),
            ("not scanned remote", lambda: self.saved["stages"][1].update(remoteRows=0)),
            ("not returned", lambda: self.saved["stages"][1].update(liveServerId=2213)),
            ("wrong Auto target", lambda: self.saved["stages"][1]["scan"].update(targetServerIds=[2212])),
            ("scan before Run Now", lambda: self.saved["stages"][1]["scan"].update(startedAt=0)),
            ("clear home", lambda: self.saved["stages"][2].update(homeRows=0)),
            ("phantom saved 2213", lambda: self.saved["stages"][3].update(savedServers=[2212,2213])),
            ("no reload", lambda: self.saved["stages"][3].update(reloadType="fixture")),
            ("bad cleanup", lambda: self.saved["stages"][4].update(restored=False)),
            ("missing native AOI", lambda: self.native["batches"][0]["indices"].pop()),
            ("missing native reply", lambda: self.native.update(repliesCaptured=62)),
            ("wrong native server", lambda: self.native.update(serverId=2212)),
            ("invalid timing owner", lambda: self.stage.update(runId="other")),
            ("no Resource provenance", lambda: self.metrics["identity"].update(SelectedTypesKey="city")),
            ("missing screenshot", lambda: (self.root/"map016-saved-after-clear.png").unlink()),
            ("wrong sqlite retained", lambda: self.remove_resource_rows()),
        ]
        for label, mutate in cases:
            with self.subTest(label=label):
                self.make_saved()
                mutate()
                self.write("map016-saved-clear-reopen.json", self.saved)
                self.write("live-resource/camera-free-scan-result.json", self.native)
                self.write("live-resource/map-fullworld-probe-metrics.json", self.metrics)
                self.write("live-resource/map-engine-stage-metrics.json", self.stage)
                with self.assertRaises((ValueError, FileNotFoundError)):
                    self.audit_saved()

    def remove_resource_rows(self):
        with closing(sqlite3.connect(self.db_path)) as database:
            database.execute("DELETE FROM map_records WHERE server_id=2212")
            database.commit()


class OfficialSettleNoUpdaterAdmissionTests(unittest.TestCase):
    """Verifier copies byte-identical trusted settlement, never creates one."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="map016-official-source-", dir=package_ui.ARTIFACTS)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.proof = self.root / "prior"
        self.marker = self.proof / "overview-evidence/official-settled-client.json"
        self.marker.parent.mkdir(parents=True)
        self.game = self.root / "game"
        spec = importlib.util.spec_from_file_location(
            "map014_runner_for_inert_admission",
            package_ui.ROOT / "tools/map_014_lead_native/run_ui.py")
        self.runner = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.runner)
        self.hashes = self.runner.EXPECTED_26_CURRENT.copy()
        self.put_marker()

    def put_marker(self):
        self.marker.write_text(json.dumps(dict(
            schemaVersion=1, gameRoot=str(self.game),
            packageSha256=self.hashes["LWScripts.data"],
            settledAtUtc=datetime.now(timezone.utc).isoformat())), encoding="utf-8")
        (self.proof / "owned-exit.json").write_text(json.dumps(dict(
            exitCode=0, proof=True, error=None, recoveryJournalPresent=False,
            restorationAudit=dict(state="proven", profileId="map014-native-ui",
                                  diagnosticResultSucceeded=True,
                                  capturedOriginalHashes=self.hashes,
                                  installedHashes=self.hashes))), encoding="utf-8")

    def copy(self):
        return self.runner.copy_verified_official_settlement(
            self.root / "new", self.hashes, prior_proof=self.proof, game_root=self.game)

    def test_trusted_marker_copied_byte_for_byte(self):
        copied = self.copy()
        self.assertEqual(copied.read_bytes(), self.marker.read_bytes())
        self.assertEqual(self.runner.NO_UPDATER_MODES,
                         {"all-nine", "all-ten", "truck", "railway",
                          "auto-all-nine", "auto-all-ten", "saved-clear-reopen", "auto-interval-20m",
                          "boss-first", "boss-after-resource", "picker-empty", "once-two-server",
                          "feature018-tabs"})

    def test_mismatch_and_untrusted_marker_fail_before_app_launch(self):
        for name, action in [
            ("package mismatch", lambda: self.put_modified_marker(packageSha256="0"*64)),
            ("game root mismatch", lambda: self.put_modified_marker(gameRoot=str(self.root/"other-game"))),
            ("future settlement", lambda: self.put_modified_marker(settledAtUtc="2099-01-01T00:00:00+00:00")),
            ("unproved exit", lambda: self.put_modified_exit(exitCode=1)),
            ("changed original", lambda: self.hashes.update({"LWScripts.data": "0"*64})),
        ]:
            with self.subTest(name=name):
                self.hashes = self.runner.EXPECTED_26_CURRENT.copy()
                self.put_marker()
                action()
                with self.assertRaises(RuntimeError):
                    self.copy()
                self.assertFalse((self.root / "new/overview-evidence/official-settled-client.json").exists())

    def put_modified_marker(self, **change):
        item = json.loads(self.marker.read_text(encoding="utf-8"))
        item.update(change)
        self.marker.write_text(json.dumps(item), encoding="utf-8")

    def put_modified_exit(self, **change):
        path = self.proof / "owned-exit.json"
        item = json.loads(path.read_text(encoding="utf-8"))
        item.update(change)
        path.write_text(json.dumps(item), encoding="utf-8")


class PackagedAutoIntervalTests(PackagedRemoteRailwayTests):
    """Inert inverses for the independent 20 real-minute receipt auditor."""

    def interval_defaults(self):
        self.remote_defaults(False)
        enabled=datetime.now(timezone.utc)-timedelta(minutes=22)
        fire=enabled+timedelta(milliseconds=1_202_000)
        deadline=int(enabled.timestamp()*1000)+1_200_000
        samples=[dict(index=i,monotonicMilliseconds=i*1000,
                      autoEnabled=True,autoRunning=i>=1200,
                      autoCycleBusy=i>=1200,manualReading=False,
                      manualScanActive=False,autoUiMounted=True,
                      liveServerId=2212 if i<1200 else 2211,
                      secondaryServerId=2212,deadlineUtcMilliseconds=deadline)
                 for i in range(1202)]
        self.interval_proof=dict(
            proof="real_20_minute_enabled_auto_railway_scheduler_2212_to_2211_return",
            state="proven",genuineGame=True,controlledProvider=False,
            systemClockInjected=False,runNowCalls=0,manualStartCalls=0,
            originalServer=2212,targetServer=2211,intervalMinutes=20,
            profileId=self.profile,sessionId=self.session,gamePid=self.pid,
            enabledAt=enabled.isoformat(),firstFireAt=fire.isoformat(),
            deadlineUtcMilliseconds=deadline,
            firstFireElapsedMilliseconds=1_202_000,
            lastIdleElapsedMilliseconds=1_199_000,
            observedScheduledRun=True,observedTargetServer=True,
            observedHomeReturn=True, failure=None,cleanupFailure=None,
            runId=self.run,nativeRailwayRecords=0,savedRailwayRecords=0,
            samples=samples,events=[dict(stage=s) for s in (
                "scheduler_armed","first_real_scheduled_admission",
                "scheduler_cycle_settled","native_sqlite_railway_verified","cleanup_verified")])
        with closing(sqlite3.connect(self.db_path)) as db:
            db.execute("UPDATE scan_runs SET created_at=?,updated_at=?",
                       (deadline+1000,deadline+3000));db.commit()
        self.metric=dict(runId=self.run,serverId=2211,scanMode="fast",wholeEngineMilliseconds=30000)
        for name in ("map016-auto-interval-armed.png","map016-auto-interval-returned.png"):
            (self.root/name).write_bytes(b"\x89PNG\r\n\x1a\n"+b"X"*1024)
        self.save_interval()

    def save_interval(self):
        self.write("live-resource/fast-truck-scan-result.json",self.railway_native)
        self.write("live-resource/map-engine-stage-metrics.json",self.metric)
        self.write("map016-auto-interval-progress.json",self.interval_proof)
        self.write("native-ui-map.json",self.interval_proof)

    def test_full_real_interval_shape_and_inverse_failures(self):
        self.interval_defaults()
        result=package_ui.audit_auto_interval(self.root,self.interval_proof,self.restoration)
        self.assertEqual(result["firstFireMonotonicMilliseconds"],1_202_000)
        self.assertEqual(result["rectanglesVerified"],625)
        cases=[
            ("premature fire",lambda:self.interval_proof.update(firstFireElapsedMilliseconds=1_199_999)),
            ("fake clock",lambda:self.interval_proof.update(systemClockInjected=True)),
            ("Run Now",lambda:self.interval_proof.update(runNowCalls=1)),
            ("Manual Start",lambda:self.interval_proof.update(manualStartCalls=1)),
            ("early server move",lambda:self.interval_proof["samples"][120].update(liveServerId=2211)),
            ("unknown status without fallback",lambda:self.interval_proof["samples"][120].update(liveServerId=None,secondaryServerId=0)),
            ("status observation gap",lambda:self.interval_proof["samples"][120].update(monotonicMilliseconds=125000)),
            ("missing restoration",lambda:self.interval_proof.update(observedHomeReturn=False)),
            ("wrong source owner",lambda:self.railway_native.update(launchSessionId="foreign-session")),
            ("incomplete native",lambda:self.railway_native.update(rectanglesCompleted=624)),
            ("stale native timing",lambda:self.metric.update(runId="stale-run")),
        ]
        for label,mutate in cases:
            with self.subTest(case=label):
                self.interval_defaults()
                mutate();self.save_interval()
                with self.assertRaises(ValueError):
                    package_ui.audit_auto_interval(self.root,self.interval_proof,self.restoration)


if __name__ == "__main__":
    unittest.main()
