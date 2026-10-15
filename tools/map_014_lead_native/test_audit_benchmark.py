"""Controlled auditor inverses; these are not simulated live Map positives."""
import json
import sqlite3
import tempfile
import unittest
from pathlib import Path

from audit_benchmark import audit


class BenchmarkAuditorTests(unittest.TestCase):
    def make_pair(self, directory, conflicting_server=False, duplicate=False):
        root = Path(directory)
        measurements = []
        camera = {'cameraSnapshot': {'fov': 10, 'world': 0}}
        for i in range(6):
            duplicates = duplicate and i % 2 == 1
            measurements.append({
                'round': i, 'boundedLens': bool(i % 2), 'seconds': 10 if i % 2 == 0 else 5,
                'cameraBefore': camera, 'cameraAfter': camera,
                'terminal': {'phase': 'completed', 'completedBlocks': 2500,
                             'failedBlocks': 0, 'unreadBlocks': 0, 'serverId': 2212,
                             'scanRunId': f'round-{i}',
                             'selectedTypes': ['city']},
                'summary': {'counts': {'city': 2 if duplicates else 1}},
            })
            (root / f'benchmark-{i}-probe-metrics.json').write_text(
                json.dumps({'uniqueCoveredAois': 10000, 'phase': 'prepared',
                            'source': 'CurrentClientMapBlockSource.ProbeFastCityBatchAsync',
                            'identity': {'RunId': f'round-{i}', 'ServerId': 2212,
                                         'WorldId': 0, 'SelectedTypesKey': 'city', 'ScanMode': 'normal'}}),
                encoding='utf8')
            db = sqlite3.connect(root / f'benchmark-{i}.db')
            db.execute('CREATE TABLE map_records (kind TEXT,server_id INTEGER,record_key TEXT,point_index INTEGER,updated_at INTEGER,data_json TEXT)')
            db.execute('INSERT INTO map_records VALUES (?,?,?,?,?,?)',
                       ('city', 2213 if conflicting_server and i == 0 else 2212, '1', 1, 1234567,
                        json.dumps({'x': 0, 'y': 999, 'ownerUid': 'player', 'pointId': 1})))
            if duplicates:
                db.execute('INSERT INTO map_records VALUES (?,?,?,?,?,?)',
                           ('city', 2212, '2', 2, 1234568,
                            json.dumps({'x': 999, 'y': 0, 'ownerUid': 'player', 'pointId': 2})))
            db.commit()
            db.close()
        (root / 'benchmark.json').write_text(json.dumps(measurements), encoding='utf8')
        return root

    def test_sqlite_owns_server_and_key_when_json_omits_them(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = audit(self.make_pair(tmp))
            self.assertEqual(result['pairs'][0]['kinds']['city']['common'], 1)
            self.assertEqual(result['candidateMedianSeconds'], 5)
            self.assertFalse(result['accuracyQualified'])

    def test_historical_duplicate_city_owner_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = audit(self.make_pair(tmp, duplicate=True))
            self.assertEqual(result['runs'][1]['duplicateOwnerLocations'], {'city': 1})
            self.assertEqual(result['runs'][1]['rawCounts']['city'], 2)
            self.assertEqual(result['runs'][1]['counts']['city'], 1)

    def test_wrong_server_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(ValueError, 'foreign server'):
                audit(self.make_pair(tmp, conflicting_server=True))

    def test_stale_metric_receipt_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = self.make_pair(tmp)
            old = json.loads((root / 'benchmark-1-probe-metrics.json').read_text(encoding='utf8'))
            old['identity']['RunId'] = 'round-0'
            (root / 'benchmark-1-probe-metrics.json').write_text(json.dumps(old), encoding='utf8')
            with self.assertRaisesRegex(ValueError, 'stale, foreign or incomplete'):
                audit(root)


if __name__ == '__main__':
    unittest.main()
