"""Synthetic inverse fixtures: never counted as native Resource evidence."""

import json
from pathlib import Path
import sqlite3
import tempfile
import unittest

from audit_resource_freshness import audit


def point(x, y, occupied=False):
    identity = y * 1000 + x + 1
    row = {
        "kind": "resource", "sourceKind": "resource_point",
        "source": "WorldPointManager._pointInfos+GatherResource+LuaEntry.Player.IsInBlackRange",
        "resourceSourceType": "ResPointInfo", "runtimeClass": "ResPointInfo",
        "pointType": 7, "pointId": identity, "pointIndex": identity,
        "recordKey": str(identity), "serverId": 2212, "srcServerId": 2212,
        "worldId": 0, "x": x, "y": y, "resourceTypeId": 14,
        "resourceConfigId": 20106, "resourceMaxAmount": 216000,
        "resourceNameKey": "100317", "level": 6,
        "blackTileKnown": True, "isBlackTile": False,
        "gatherOccupancyKnown": True, "gatherOccupied": occupied,
        "rebuildGatherOccupancyKnown": True, "rebuildGatherOccupied": occupied,
    }
    if not occupied:
        row.update(resourceDetailKnown=True, resourceRemainingAmount=216000,
                   resourceFullAmount=216000, resourceFull=True,
                   resourceCapacityMatchesConfig=True)
    return row


def fixture(root, *, sequences=None):
    snapshots = sequences or [[point(0, 999)] for _ in range(6)]
    measurements = []
    camera = {"cameraSnapshot": {"worldId": 0, "x": 50, "fov": 120}}
    for i, source_rows in enumerate(snapshots):
        counts = len(source_rows)
        measurements.append({
            "round": i, "boundedLens": bool(i % 2), "seconds": 140 if i % 2 == 0 else 70,
            "cameraBefore": camera, "cameraAfter": camera,
            "terminal": {
                "phase": "completed", "isReading": False, "totalBlocks": 2500,
                "completedBlocks": 2500, "unreadBlocks": 0, "failedBlocks": 0,
                "nativeDroppedRecords": 0, "selectedTypes": ["resource"],
                "serverId": 2212, "scanRunId": f"resource-run-{i}",
            },
            "summary": {"counts": {"resource": counts}},
        })
        metrics = {
            "phase": "prepared", "source": "CurrentClientMapBlockSource.ProbeFastCityBatchAsync",
            "identity": {"RunId": f"resource-run-{i}", "ServerId": 2212,
                         "WorldId": 0, "ScanMode": "normal", "SelectedTypesKey": "resource"},
            "uniqueCoveredAois": 10000, "requiredAois": 10000,
            "actualPeakConcurrentCameraProbes": 1,
            "validatedProbeResponses": 120, "attributableLuaProbeAttempts": 120,
            "attributableRetries": 0,
            "resourceDetailNativeRequests": 0,
            "resourceDetailNativeReady": sum(not row["gatherOccupied"] for row in source_rows),
            "uniqueRecordKeys": {"resource": counts},
            "acquisitionMilliseconds": 70000, "resourceHydrationMilliseconds": 1000,
        }
        (root / f"benchmark-{i}-probe-metrics.json").write_text(json.dumps(metrics), encoding="utf8")
        db = sqlite3.connect(root / f"benchmark-{i}.db")
        db.execute("CREATE TABLE map_records(kind TEXT,server_id INTEGER,record_key TEXT,"
                   "point_index INTEGER,updated_at INTEGER,data_json TEXT)")
        for data in source_rows:
            row = {**data, "updatedAt": 1000000 + i * 200000}
            db.execute("INSERT INTO map_records VALUES (?,?,?,?,?,?)",
                       ("resource", 2212, row["recordKey"], row["pointIndex"],
                        row["updatedAt"], json.dumps(row)))
        db.commit()
        db.close()
    (root / "benchmark.json").write_text(json.dumps(measurements), encoding="utf8")
    return root


class ResourceEvidenceTests(unittest.TestCase):
    def test_paired_resource_completeness_and_geometry(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = audit(fixture(Path(tmp)))
            self.assertEqual([pair["common"] for pair in result["pairs"]], [1, 1, 1])
            self.assertEqual(result["runs"][0]["resourceBearingAois"], 1)
            self.assertEqual(result["runs"][0]["unoccupiedHydrated"], 1)
            self.assertFalse(result["resourceFov20AccuracyQualified"])

    def test_departing_occupied_is_not_called_proven_causal_churn(self):
        with tempfile.TemporaryDirectory() as tmp:
            old = point(0, 999, occupied=True)
            newcomer = point(5, 20)
            sequences = [
                [old], [newcomer], [newcomer], [newcomer],
                [newcomer], [newcomer],
            ]
            result = audit(fixture(Path(tmp), sequences=sequences))
            pair = result["pairs"][0]
            self.assertEqual(pair["missingByPriorOccupation"], {"True": 1})
            self.assertEqual(pair["extraByCandidateOccupation"], {"False": 1})
            self.assertEqual(pair["missingIdentities"][0]["observedInOtherRounds"], [])
            self.assertEqual(pair["candidateExtraIdentities"][0]["observedInOtherRounds"],
                             [2, 3, 4, 5])
            self.assertIn("not native spawn", result["causality"])

    def test_invalid_point_coordinate_identity_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [[{**point(0, 999), "x": 999}] for _ in range(6)]
            with self.assertRaisesRegex(ValueError, "invalid Resource geometry"):
                audit(fixture(Path(tmp), sequences=rows))

    def test_unoccupied_without_hydration_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [[{k: v for k, v in point(0, 999).items() if k != "resourceDetailKnown"}]
                    for _ in range(6)]
            with self.assertRaisesRegex(ValueError, "missing real hydrated detail"):
                audit(fixture(Path(tmp), sequences=rows))

    def test_bad_metric_identity_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = fixture(Path(tmp))
            filename = root / "benchmark-1-probe-metrics.json"
            metrics = json.loads(filename.read_text(encoding="utf8"))
            metrics["identity"]["RunId"] = "not-current-run"
            filename.write_text(json.dumps(metrics), encoding="utf8")
            with self.assertRaisesRegex(ValueError, "stale"):
                audit(root)

    def test_wrong_db_server_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = fixture(Path(tmp))
            db = sqlite3.connect(root / "benchmark-3.db")
            db.execute("UPDATE map_records SET server_id=2213")
            db.commit()
            db.close()
            with self.assertRaisesRegex(ValueError, "foreign kind/server"):
                audit(root)

    def test_shared_immutable_field_drift_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [[point(0, 999)] for _ in range(6)]
            rows[1] = [{**point(0, 999), "resourceMaxAmount": 216001}]
            with self.assertRaisesRegex(ValueError, "immutable Resource fields changed"):
                audit(fixture(Path(tmp), sequences=rows))

    def test_occupied_does_not_require_unavailable_amounts(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [[point(0, 999, occupied=True)] for _ in range(6)]
            result = audit(fixture(Path(tmp), sequences=rows))
            self.assertEqual(result["runs"][0]["occupied"], 1)
            self.assertEqual(result["runs"][0]["unoccupiedHydrated"], 0)

    def test_unoccupied_missing_not_dismissed_as_occupied_churn(self):
        with tempfile.TemporaryDirectory() as tmp:
            old = point(0, 999)
            newer = point(1, 999)
            result = audit(fixture(Path(tmp), sequences=[
                [old], [newer], [newer], [newer], [newer], [newer],
            ]))
            self.assertEqual(result["pairs"][0]["missingByPriorOccupation"], {"False": 1})
            self.assertTrue(result["pairs"][0]["missingIdentities"][0]["resourceDetailKnown"])

    def test_false_source_provenance_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            rows = [[{**point(0, 999), "source": "guessed"}] for _ in range(6)]
            with self.assertRaisesRegex(ValueError, "identity/source"):
                audit(fixture(Path(tmp), sequences=rows))

    def test_non_monotonic_observation_time_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = fixture(Path(tmp))
            db = sqlite3.connect(root / "benchmark-2.db")
            row = db.execute("SELECT data_json FROM map_records").fetchone()[0]
            fields = json.loads(row)
            fields["updatedAt"] = 1
            db.execute("UPDATE map_records SET updated_at=?,data_json=?", (1, json.dumps(fields)))
            db.commit()
            db.close()
            with self.assertRaisesRegex(ValueError, "not ordered"):
                audit(root)


if __name__ == "__main__":
    unittest.main()
