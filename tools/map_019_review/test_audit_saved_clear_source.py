"""Inert fixture provenance and startup policy counterexamples.

All synthetic DB creation/deletion in these tests is restricted to disposable
temporary directories; no repository source or owner DB is touched.
"""

from __future__ import annotations

import json
from contextlib import closing
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import audit_saved_clear_source as audit


class SavedClearSourceChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="map016-inert-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.run = self.root / "owned-accepted"
        self.db = audit.db_file(self.run)
        self.db.parent.mkdir(parents=True, exist_ok=True)
        self.selected = ["city", "resource", "truck"]
        self.run_id = "inert-only-run-id"
        with closing(sqlite3.connect(self.db)) as database:
            database.executescript("""
                CREATE TABLE map_records (
                    kind TEXT,server_id INTEGER,record_key TEXT,
                    updated_at INTEGER NOT NULL,data_json TEXT,
                    PRIMARY KEY(kind,server_id,record_key));
                CREATE TABLE scan_runs (
                    id TEXT PRIMARY KEY,server_id INTEGER,status TEXT,total_blocks INTEGER,
                    completed_blocks INTEGER,failed_blocks INTEGER,selected_types TEXT,
                    created_at INTEGER);
                CREATE TABLE scan_blocks (run_id TEXT,block_index INTEGER);
                CREATE TABLE scan_records (run_id TEXT,kind TEXT);
                CREATE TABLE treasure_claim_states (server_id INTEGER,player_uid TEXT);
                CREATE TABLE app_settings (key TEXT,value_json TEXT);
            """)
            database.execute("INSERT INTO scan_runs VALUES (?,?,?,?,?,?,?,?)",
                             (self.run_id, audit.HOME, "completed", 2500, 2500, 0,
                              json.dumps(self.selected), 1000))
            database.executemany("INSERT INTO map_records VALUES (?,?,?,?,?)",
                                 [("resource", audit.HOME, f"genuine-synthetic-{i}",
                                   1000, "{}") for i in range(3)])
            database.execute("INSERT INTO app_settings VALUES ('keep_me','{}')")
            database.commit()
        self.proof = {
            "state": "proven", "genuineGame": True, "controlledProvider": False,
            "profileId": audit.PROFILE, "serverId": audit.HOME,
            "selectedTypes": self.selected,
            "completedStatus": {"scanRunId": self.run_id, "phase": "completed",
                                "serverId": audit.HOME, "totalBlocks": 2500,
                                "completedBlocks": 2500, "failedBlocks": 0},
        }
        self.exit = {
            "exitCode": 0, "proof": True, "error": None,
            "recoveryJournalPresent": False,
            "restorationAudit": {"state": "proven", "profileId": audit.PROFILE},
            "originalScriptsRestored": {key: True for key in (
                "LWScripts.data", "LWScripts.txt", "version.txt")},
        }
        self.source = {"genuine": True, "fixture": False}
        self.write_receipts()

    def write_receipts(self):
        for name, value in (("native-ui-map.json", self.proof),
                            ("owned-exit.json", self.exit),
                            ("source.json", self.source)):
            (self.run / name).write_text(json.dumps(value), encoding="utf8")

    def test_positive_source_and_no_file_mutation(self):
        before = self.db.read_bytes()
        candidate = audit.inspect_accepted_source(self.run)
        self.assertEqual(candidate["homeResourceRows"], 3)
        self.assertEqual(candidate["savedServers"], [2212])
        self.assertEqual(candidate["remoteAnyRows"], 0)
        self.assertTrue(candidate["acceptedProof"])
        self.assertEqual(self.db.read_bytes(), before)
        self.assertFalse(Path(str(self.db) + "-wal").exists())

    def test_actual_production_startup_contract_prevents_clone_only_proof(self):
        contract = audit.static_startup_contract()
        self.assertTrue(contract["normalStartupClearsMapRows"])
        self.assertTrue(contract["savedClearRequiresNewSameSessionHomeScan"])
        self.assertFalse(contract["historicalCloneAloneCanSatisfyProof"])
        clone = self.root / "simulated-owned-app" / "map-data.db"
        clone.parent.mkdir(parents=True)
        with closing(sqlite3.connect(self.db)) as source, closing(sqlite3.connect(clone)) as target:
            source.backup(target)
        self.assertEqual(audit.inspect_db(clone)["homeResourceRows"], 3)
        # NEVER apply this production statement to source. Only the fresh
        # temporary clone receives the actual R18 startup deletion SQL.
        with closing(sqlite3.connect(clone)) as target:
            target.executescript(
                "DELETE FROM scan_records; DELETE FROM scan_blocks; "
                "DELETE FROM scan_runs; DELETE FROM map_records; "
                "DELETE FROM treasure_claim_states;")
            target.commit()
        cleaned = audit.inspect_db(clone)
        self.assertEqual(cleaned["totalRows"], 0)
        self.assertEqual(cleaned["tableTotals"]["scan_runs"], 0)
        with closing(sqlite3.connect(clone)) as target:
            self.assertEqual(target.execute("SELECT COUNT(*) FROM app_settings").fetchone()[0], 1)
        self.assertEqual(audit.inspect_db(self.db)["homeResourceRows"], 3)

    def test_reject_foreign_server_rows_and_published_server_list(self):
        with closing(sqlite3.connect(self.db)) as source:
            source.execute("INSERT INTO map_records VALUES (?,?,?,?,?)",
                           ("monster", 2213, "forbidden", 1, "{}"))
            source.commit()
        with self.assertRaisesRegex(ValueError, "contaminated"):
            audit.inspect_accepted_source(self.run)

    def test_reject_fake_provenance_and_inconsistent_run(self):
        for label, filename, key, value in [
            ("fake source", "source.json", "fixture", True),
            ("missing native proof", "native-ui-map.json", "genuineGame", False),
            ("foreign owner", "native-ui-map.json", "profileId", "foreign"),
            ("bad exit", "owned-exit.json", "exitCode", 1),
            ("unrestored scripts", "owned-exit.json", "originalScriptsRestored", {}),
            ("wrong selection", "native-ui-map.json", "selectedTypes", ["resource"]),
        ]:
            with self.subTest(label=label):
                original = {"source.json": self.source, "native-ui-map.json": self.proof,
                            "owned-exit.json": self.exit}[filename]
                original[key] = value
                self.write_receipts()
                with self.assertRaises(ValueError):
                    audit.inspect_accepted_source(self.run)
                original[key] = ({
                    "source.json": {"genuine": True, "fixture": False},
                    "native-ui-map.json": {
                        "genuineGame": True, "profileId": audit.PROFILE,
                        "selectedTypes": self.selected},
                    "owned-exit.json": {
                        "exitCode": 0, "originalScriptsRestored":
                        {x: True for x in ("LWScripts.data", "LWScripts.txt", "version.txt")}},
                }[filename])[key]
        self.write_receipts()
        self.assertTrue(audit.inspect_accepted_source(self.run)["acceptedProof"])

    def test_reject_uncommitted_wal_without_reading_or_merging(self):
        wal = Path(str(self.db) + "-wal")
        wal.write_bytes(b"test-pending-native-wal")
        with self.assertRaisesRegex(ValueError, "nonempty WAL"):
            audit.inspect_db(self.db)
        wal.unlink()
        self.assertEqual(audit.inspect_db(self.db)["homeResourceRows"], 3)

    def test_artifact_directory_boundary(self):
        with self.assertRaisesRegex(ValueError, "task artifact"):
            audit.audit(self.run)
        expected = audit.audit(self.run, enforce_source_dir=False)
        self.assertEqual(expected["sourceAudit"]["homeResourceRows"], 3)
        self.assertFalse(expected["startupContract"]["historicalCloneAloneCanSatisfyProof"])


if __name__ == "__main__":
    unittest.main()
