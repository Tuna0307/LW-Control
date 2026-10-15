"""Inert inverses for the archived march-pair evidence classifier."""

import json
import sqlite3
import tempfile
import unittest
from pathlib import Path

from audit_march_pairs import _arrival_seconds, audit


class MarchAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        measurements = []
        for index in range(6):
            is_candidate = bool(index % 2)
            measurements.append({
                "round": index,
                "boundedLens": is_candidate,
                "seconds": 30,
                "terminal": {
                    "phase": "completed", "failedBlocks": 0,
                    "completedBlocks": 2500,
                    "selectedTypes": ["truck", "railway"],
                    "startedAt": 1791915000 + index * 100,
                    "serverId": 2212,
                },
            })
            (self.root / f"benchmark-{index}-probe-metrics.json").write_text(
                json.dumps({"uniqueCoveredAois": 10000}), encoding="utf-8"
            )
            self._write_database(index, is_candidate)
        (self.root / "benchmark.json").write_text(json.dumps(measurements), encoding="utf-8")

    def _write_database(self, index, is_candidate):
        conn = sqlite3.connect(self.root / f"benchmark-{index}.db")
        try:
            conn.execute("CREATE TABLE map_records (kind TEXT, server_id INTEGER, "
                         "record_key TEXT, updated_at INTEGER, data_json TEXT)")
            sample = {"shared": 1791919000}
            if not is_candidate:
                sample.update({"not_arrived": 1791919000,
                               "already_arrived": 1791914000,
                               "unknown": None})
            for key, arrival in sample.items():
                data = {"uuid": key, "x": 10, "y": 20, "endTime": 1791912000}
                if arrival is not None:
                    data["arriveTs"] = arrival
                conn.execute("INSERT INTO map_records VALUES (?,?,?,?,?)",
                             ("truck", 2212, key, 1791915000000, json.dumps(data)))
            conn.execute("INSERT INTO map_records VALUES (?,?,?,?,?)",
                         ("railway", 2212, "rail", 1791915000000,
                          json.dumps({"uuid": "rail", "arriveTs": 1791919000})))
            conn.commit()
        finally:
            conn.close()

    def test_future_arrival_is_not_explained_by_march_leg_endtime(self):
        self.assertEqual(_arrival_seconds(1791919000000), 1791919000)
        result = audit(self.root)
        truck = result["pairs"][0]["kinds"]["truck"]
        self.assertEqual(truck["missingBaselineUUIDs"], 3)
        self.assertEqual(truck["missingWithFutureSourceArrival"], 1)
        self.assertEqual(truck["missingWithPastSourceArrival"], 1)
        self.assertEqual(truck["missingWithoutUsableArrival"], 1)
        self.assertEqual(result["pairs"][0]["kinds"]["railway"]["missingBaselineUUIDs"], 0)
        self.assertFalse(result["mobileAccuracyQualified"])

    def test_point_aoi_coverage_cannot_replace_march_evidence(self):
        path = self.root / "benchmark-1-probe-metrics.json"
        path.write_text(json.dumps({"uniqueCoveredAois": 9999}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "POINT AOI coverage"):
            audit(self.root)

    def test_wrong_march_uuid_is_rejected(self):
        db = sqlite3.connect(self.root / "benchmark-1.db")
        try:
            db.execute("UPDATE map_records SET data_json=? WHERE record_key='shared'",
                       (json.dumps({"uuid": "wrong"}),))
            db.commit()
        finally:
            db.close()
        with self.assertRaisesRegex(ValueError, "mismatched native march UUID"):
            audit(self.root)


if __name__ == "__main__":
    unittest.main()
