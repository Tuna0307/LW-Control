"""Inert adversarial MAP017 evidence tests; fixtures never represent live proof."""
from copy import deepcopy
from contextlib import closing
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest

import map_017_audit as audit


class BossEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="map017-inert-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.now = int(datetime.now(timezone.utc).timestamp() * 1000)
        self.challenge = "INERT-not-a-live-proof"
        self.owner = dict(profileId=audit.PROFILE, sessionId="INERT-session", gamePid=123,
                          challengeSha256=hashlib.sha256(self.challenge.encode()).hexdigest(),
                          gameStartedAtUtc=self.iso(self.now - 3000), helperStoppedAtUtc=self.iso(self.now + 3000))
        self.db = self.root / "profiles" / audit.PROFILE / "map-data/map-data.db"
        self.db.parent.mkdir(parents=True)
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("CREATE TABLE scan_runs(id TEXT,server_id INT,selected_types TEXT,status TEXT,total_blocks INT,completed_blocks INT,failed_blocks INT,created_at INT,updated_at INT,error TEXT)")
            for table in ("map_records", "scan_records"):
                columns = "kind TEXT,server_id INT,record_key TEXT,uuid TEXT,data_json TEXT"
                db.execute(f"CREATE TABLE {table}({('run_id TEXT,' if table == 'scan_records' else '') + columns})")
            db.execute("INSERT INTO scan_runs VALUES(?,?,?,?,?,?,?,?,?,?)", ("INERT-run", 2212, '["zombie_boss"]', "completed", 2500, 2500, 0, self.now-1000, self.now+1000, None))
            self.row = dict(uuid="INERT-boss-uuid", serverId=2212, kind="zombie_boss", x=10, y=20, configSpecial=10)
            db.execute("INSERT INTO map_records VALUES(?,?,?,?,?)", ("zombie_boss", 2212, self.row["uuid"], self.row["uuid"], json.dumps(self.row)))
        self.stage = dict(label="boss", kind="zombie_boss", total=1, selectedTypes=["zombie_boss"],
            renderedSnapshot=dict(tablePresent=True,busy="false",activeTabIndex=9,displayedRows=1,resultCountValue=1),
            completedStatus=dict(scanRunId="INERT-run"), selectedResponse=dict(payload=dict(kind="zombie_boss", profileId=audit.PROFILE,
            query=dict(serverId=2212, page=1, pageSize=50)), result=dict(total=1, rows=[self.row])))
        self.proof = dict(mode="boss-first", stages=[self.stage])
        self.native = dict(state="proven", scanRunId="INERT-run", profileId=audit.PROFILE, launchSessionId="INERT-session",
            gamePid=123, serverId=2212, worldId=0, challenge=self.challenge, cameraStable=True, curViewRestored=True,
            completedAois=10000, repliesCaptured=63, requestsSent=63, capturedAt=self.iso(self.now),
            batches=[dict(indices=list(range(i, min(i+160, 10000))), monster_march_records=[]) for i in range(0, 10000, 160)])
        raw = dict(self.row, kind="monster", isBoss=False)
        self.native["batches"][0]["monster_march_records"].append(raw)
        self.metrics = dict(phase="prepared", source="CurrentClientMapBlockSource.ProbeCameraFreeMapAsync",
            identity=dict(RunId="INERT-run", ServerId=2212, SelectedTypesKey="zombie_boss"), uniqueCoveredAois=10000,
            validatedProbeResponses=63, attributableLuaProbeAttempts=63)
        self.save()

    def iso(self, value):
        return datetime.fromtimestamp(value/1000, timezone.utc).isoformat()

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data), encoding="utf-8")

    def image(self, name):
        # Only an inert header for file-presence admission; no real screenshot.
        (self.root / name).write_bytes(b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR" + (10).to_bytes(4,"big") * 2)

    def save(self):
        self.write("map017-boss-camera-free-scan-result.json", self.native)
        self.write("map017-boss-map-fullworld-probe-metrics.json", self.metrics)
        for name in ("map017-boss-selected.png", "map017-boss-results.png"):
            self.image(name)

    def run_audit(self):
        return audit.audit_boss(self.root, self.proof, self.owner)

    def test_exact_sqlite_raw_special10_positive_ignores_broken_isboss_flag(self):
        result = self.run_audit()
        self.assertTrue(result["accepted"])
        self.assertEqual(result["count"], 1)
        self.assertTrue(result["native"]["sourcePublicationIdentityMatches"])

    def test_equal_count_wrong_uuid_rejected(self):
        self.native["batches"][0]["monster_march_records"][0]["uuid"] = "INERT-different-boss"
        self.save()
        with self.assertRaisesRegex(ValueError, "UUID set"):
            self.run_audit()

    def test_owner_coverage_selection_freshness_and_field_inverses(self):
        original = deepcopy(self.native)
        mutations = [lambda j: j.update(gamePid=999), lambda j: j.update(launchSessionId="foreign"),
            lambda j: j.update(challenge="foreign"), lambda j: j.update(scanRunId="old-run"),
            lambda j: j.update(cameraStable=False), lambda j: j.update(completedAois=9999),
            lambda j: j.update(capturedAt="2000-01-01T00:00:00Z"),
            lambda j: j["batches"][1]["indices"].__setitem__(0,0),
            lambda j: j["batches"][0]["monster_march_records"][0].update(x=999),
            lambda j: j["batches"][0]["monster_march_records"][0].update(configSpecial=1)]
        for mutation in mutations:
            with self.subTest(mutation=mutation):
                self.native = deepcopy(original)
                mutation(self.native)
                self.save()
                with self.assertRaises(ValueError):
                    self.run_audit()

    def test_ui_count_and_foreign_firstpage_identity_rejected(self):
        self.stage["total"] = 10
        with self.assertRaisesRegex(ValueError, "SQLite"):
            self.run_audit()
        self.stage["total"] = 1
        self.stage["selectedResponse"]["result"]["rows"] = [dict(self.row, uuid="foreign")]
        with self.assertRaisesRegex(ValueError, "first-page"):
            self.run_audit()

    def test_extra_prior_scan_invalidates_fresh_first_scan(self):
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("INSERT INTO scan_runs VALUES(?,?,?,?,?,?,?,?,?,?)", ("older", 2212, '["resource"]', "completed", 2500,2500,0,self.now-2000,self.now-1500,None))
        with self.assertRaisesRegex(ValueError, "extra/missing"):
            self.run_audit()

    def test_zero_with_positive_raw_source_is_observed_failure_never_acceptance(self):
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("DELETE FROM map_records")
            db.execute("DELETE FROM scan_records")
        self.stage["total"] = self.stage["selectedResponse"]["result"]["total"] = 0
        self.stage["renderedSnapshot"].update(displayedRows=0,resultCountValue=0)
        self.stage["selectedResponse"]["result"]["rows"] = []
        result = self.run_audit()
        self.assertFalse(result["accepted"])
        self.assertEqual(result["state"], "observed_zero")
        self.assertEqual(result["native"]["sourceBossCount"], 1)
        self.assertFalse(result["native"]["sourcePublicationIdentityMatches"])

    def test_sqlite_partial_or_wrong_selection_rejected(self):
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("UPDATE scan_runs SET selected_types='[\"monster\"]'")
        with self.assertRaisesRegex(ValueError, "SQLite"):
            self.run_audit()

    def test_missing_or_nonpng_results_rejected(self):
        (self.root / "map017-boss-results.png").write_bytes(b"NOT A SCREENSHOT")
        with self.assertRaisesRegex(ValueError, "screenshot"):
            self.run_audit()

    def test_self_reported_picker_pass_cannot_override_checked_control(self):
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("DELETE FROM scan_runs")
        for prefix in ("manual", "auto"):
            for label in ("last-selected", "after-untick-last"):
                self.image(f"map017-{prefix}-{label}.png")
        stage = dict(stage="manual-last-untick", passed=True, snapshot=dict(selectedIndices=[9],actionDisabled=True,hint="Select a category"))
        proof = dict(mode="picker-empty", stages=[stage, dict(stage="auto-last-untick")])
        with self.assertRaisesRegex(ValueError, "self-reported"):
            audit.audit_picker(self.root, proof, self.owner)

    def test_resource_first_pruned_run_requires_independent_retained_source_join(self):
        resource = dict(pointId=99,kind="resource",serverId=2212,x=98,y=0)
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("INSERT INTO map_records VALUES(?,?,?,?,?)",("resource",2212,"99",None,json.dumps(resource)))
        native = deepcopy(self.native)
        native.update(scanRunId="INERT-resource-run",capturedAt=self.iso(self.now-1500))
        for batch in native["batches"]:
            batch["monster_march_records"] = []
            batch["point_records"] = []
        native["batches"][0]["point_records"] = [dict(resource,kind="resource_point")]
        metrics = deepcopy(self.metrics)
        metrics["identity"].update(RunId="INERT-resource-run",SelectedTypesKey="resource")
        self.write("map017-resource-first-camera-free-scan-result.json",native)
        self.write("map017-resource-first-map-fullworld-probe-metrics.json",metrics)
        stage = dict(label="resource-first",kind="resource",total=1,selectedTypes=["resource"],
            completedStatus=dict(scanRunId="INERT-resource-run"),
            persisted=dict(id="INERT-resource-run",serverId=2212,selectedTypes=["resource"],status="completed",
                totalBlocks=2500,completedBlocks=2500,failedBlocks=0,createdAt=self.now-1800,updatedAt=self.now-1200,error=None),
            selectedResponse=dict(payload=dict(kind="resource",profileId=audit.PROFILE,query=dict(serverId=2212,page=1)),result=dict(total=1,rows=[resource])),
            renderedSnapshot=dict(tablePresent=True,busy=False,activeTabIndex=1,displayedRows=1,resultCountValue=1))
        self.proof.update(mode="boss-after-resource",stages=[stage,self.stage])
        for label in ("selected","results"):
            self.image(f"map017-resource-first-{label}.png")
        self.assertTrue(self.run_audit()["accepted"])
        native["batches"][0]["point_records"][0]["pointId"] = 98
        self.write("map017-resource-first-camera-free-scan-result.json",native)
        with self.assertRaisesRegex(ValueError,"Resource-first raw identities"):
            self.run_audit()

    def test_missing_render_join_retains_observation_but_cannot_qualify_positive(self):
        self.stage.pop("renderedSnapshot")
        result = self.run_audit()
        self.assertFalse(result["accepted"])
        self.assertEqual(result["count"],1)
        self.assertEqual(result["state"],"observed_source_positive_without_render_join")


class OnceEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="map017-once-inert-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.owner = dict(profileId=audit.PROFILE)
        self.db = self.root / "profiles" / audit.PROFILE / "map-data/map-data.db"
        self.db.parent.mkdir(parents=True)
        self.config = dict(enabled=False,nextRunAt=0,serverIds=[2211,2213],selectedTypes=["railway"],returnToOriginalServer=True)
        self.last = dict(outcome="completed",lastError=None,originalServerId=2212,targetServerIds=[2211,2213],completedServerIds=[2211,2213])
        self.completed = dict(stage="one-off-completed",runNowClicks=1,originalServerId=2212,restoredServerId=2212,
            serversSeen=[2212,2211,2213],cycle=dict(config=self.config,running=False,lastCycle=self.last))
        self.proof = dict(mode="once-two-server",stages=[dict(stage="schedule-off-configured",configured=dict(config=self.config),
            snapshot=dict(actionDisabled=False)),self.completed])
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("CREATE TABLE scan_runs(id TEXT,server_id INT,selected_types TEXT,status TEXT,total_blocks INT,completed_blocks INT,failed_blocks INT,created_at INT,updated_at INT,error TEXT)")
            db.execute("CREATE TABLE scan_records(run_id TEXT,kind TEXT,server_id INT,record_key TEXT)")
            db.execute("CREATE TABLE map_records(kind TEXT,server_id INT,record_key TEXT)")
            for index, server in enumerate((2211,2213)):
                db.execute("INSERT INTO scan_runs VALUES(?,?,?,?,?,?,?,?,?,?)", (f"INERT-{server}",server,'["railway"]',"completed",2500,2500,0,1000+index*1000,1500+index*1000,None))
                db.execute("INSERT INTO map_records VALUES(?,?,?)", ("railway",server,f"INERT-{server}"))
                self.proof["stages"].append(dict(stage="saved-target-results",serverId=server,total=1,
                    selectedResponse=dict(payload=dict(kind="railway",profileId=audit.PROFILE,query=dict(serverId=server,page=1)),
                    result=dict(total=1,rows=[dict(uuid=f"INERT-{server}")]))))
        for name in ("map017-once-schedule-off-before.png","map017-once-completed-returned-home.png",
            "map017-once-saved-2211-railway.png","map017-once-saved-2213-railway.png","map017-once-empty-2213-no-saved-phantom.png"):
            (self.root / name).write_bytes(b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"+(10).to_bytes(4,"big")*2)
        self.persist()

    def persist(self):
        file = self.root / "profiles" / audit.PROFILE / "runtime/map-auto-scan.json"
        file.parent.mkdir(parents=True,exist_ok=True)
        file.write_text(json.dumps(dict(config=self.config,lastCycle=self.last)),encoding="utf-8")

    def run_audit(self):
        return audit.audit_once(self.root,self.proof,self.owner)

    def test_two_real_persisted_target_runs_required(self):
        self.assertTrue(self.run_audit()["accepted"])
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("DELETE FROM scan_runs WHERE server_id=2213")
        with self.assertRaisesRegex(ValueError,"two real runs"):
            self.run_audit()

    def test_fake_home_return_and_enabled_schedule_rejected(self):
        self.completed["restoredServerId"] = 2213
        with self.assertRaisesRegex(ValueError,"home return"):
            self.run_audit()
        self.completed["restoredServerId"] = 2212
        self.config["enabled"] = True
        with self.assertRaisesRegex(ValueError,"schedule/target"):
            self.run_audit()

    def test_zero_target_requires_native_and_sqlite_empty_and_no_phantom_saved(self):
        with closing(sqlite3.connect(self.db)) as db, db:
            db.execute("DELETE FROM map_records WHERE server_id=2213")
        stage = dict(stage="empty-target-results",serverId=2213,total=0,
            nativeQuery=dict(kind="railway",query=dict(serverId=2213,page=1,pageSize=50)),
            nativeResponse=dict(total=0,rows=[]),nativeSaved=dict(servers=[2211]),
            savedServerSnapshot=dict(optionValues=["","2211"]))
        self.proof["stages"][3] = stage
        self.assertTrue(self.run_audit()["accepted"])
        stage["savedServerSnapshot"]["optionValues"].append("2213")
        with self.assertRaisesRegex(ValueError,"phantom Saved"):
            self.run_audit()

    def test_persisted_schedule_cannot_disagree_with_receipt(self):
        path = self.root / "profiles" / audit.PROFILE / "runtime/map-auto-scan.json"
        saved = json.loads(path.read_text(encoding="utf-8"))
        saved["config"]["enabled"] = True
        path.write_text(json.dumps(saved),encoding="utf-8")
        with self.assertRaisesRegex(ValueError,"Persisted one-off"):
            self.run_audit()


class AggregateTests(unittest.TestCase):
    def receipt(self, index, mode="boss-first", accepted=True):
        return dict(root=f"root-{index}", sessionId=f"session-{index}", gamePid=index+1,
            gameStartedAtUtc=f"start-{index}", mode=mode, accepted=accepted,
            probeSourceSha256="same-INERT-probe-source", desktopAssemblySha256="same-INERT-assembly")

    def test_three_distinct_firsts_plus_each_other_gate_required(self):
        receipts = [self.receipt(i) for i in range(3)] + [self.receipt(i+3, mode) for i, mode in
            enumerate(("boss-after-resource", "all-nine", "picker-empty", "once-two-server"))]
        self.assertEqual(audit.aggregate(receipts)["state"], "PACKAGED_GATES_PASS")
        for index in range(len(receipts)):
            reduced = [deepcopy(r) for i,r in enumerate(receipts) if i != index]
            self.assertEqual(audit.aggregate(reduced)["state"], "PARTIAL")

    def test_reusing_root_or_session_is_not_three_fresh_launches(self):
        first = self.receipt(1)
        with self.assertRaisesRegex(ValueError, "Duplicate proof root"):
            audit.aggregate([first,first,first])
        second = self.receipt(2)
        second["sessionId"] = first["sessionId"]
        with self.assertRaisesRegex(ValueError, "Repeated game session"):
            audit.aggregate([first,second])

    def test_zero_negatives_do_not_satisfy_fresh_scan_gate(self):
        result = audit.aggregate([self.receipt(i, accepted=False) for i in range(3)])
        self.assertEqual(result["positiveFreshBossScans"],0)
        self.assertFalse(result["gates"]["threeFreshBossFirst"])


if __name__ == "__main__":
    unittest.main()
