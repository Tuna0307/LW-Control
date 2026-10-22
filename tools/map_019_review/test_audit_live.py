"""Inert audit inverses only; fixtures are never genuine performance evidence."""
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from audit_live import audit


class AuditLiveTests(unittest.TestCase):
    def make_round(self, root, ordinal, method, fields):
        run = f'run-{ordinal}'
        dbpath = root/f'benchmark-{ordinal}.db'
        with sqlite3.connect(dbpath) as conn:
            conn.executescript('CREATE TABLE scan_runs(id,status,selected_types,total_blocks,completed_blocks,failed_blocks,created_at,updated_at); CREATE TABLE map_records(kind,server_id,record_key,updated_at,data_json);')
            conn.execute('INSERT INTO scan_runs VALUES(?,?,?,?,?,?,?,?)', (run,'completed','["city"]',2500,2500,0,1000,2000))
            conn.execute('INSERT INTO map_records VALUES(?,?,?,?,?)', ('city',1,'point:1',1500,json.dumps(fields)))
        conn.close()
        return {'ordinal': ordinal, 'round': ordinal, 'workload': 'city', 'selectedTypes': ['city'], 'method': method,
                'effectiveMethod': method, 'seconds': 1, 'terminal': {'scanRunId': run, 'serverId': 1, 'phase': 'completed'},
                'summary': {'counts': {'city': 1}}, 'cameraBefore': {'cameraSnapshot': {'x': 1}},
                'cameraAfter': {'cameraSnapshot': {'x': 1}}}

    def test_failed_entry_needs_no_complete_db(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'benchmark.json').write_text(json.dumps([{'round': 0,'effectiveMethod':'unqualified','error':'failed','terminal':{'phase':'failed'}}]))
            result=audit(root)
            self.assertFalse(result['rounds'][0]['completed'])
            self.assertEqual(result['pairs'], [])
            self.assertTrue(result['provisionalUntilFinalStop'])

    def test_absent_null_are_not_equal_value_parity_and_missing_drift_unknown(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            entries=[self.make_round(root,0,'direct',{'ownerName':None}),self.make_round(root,1,'camera',{})]
            (root/'benchmark.json').write_text(json.dumps(entries))
            result=audit(root)
            pair=result['pairs'][0]
            self.assertEqual(pair['kinds']['city']['fields']['ownerName'], {'absentMismatch':1})
            self.assertEqual(pair['kinds']['city']['fields']['level'], {'bothAbsent':1})
            self.assertIsNone(pair['driftAssessment']['city']['identityDifferenceNoWorseThanBothDriftControls'])
            self.assertIsNone(pair['driftAssessment']['city']['accuracyQualified'])

    def test_fallback_runid_overrides_proven_discovery_method(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            entries=[self.make_round(root,0,'direct',{'ownerName':'x'})]
            entries[0]['fallbackEvidence']={'runId':'run-0','reason':'hydration incomplete'}
            (root/'benchmark.json').write_text(json.dumps(entries))
            result=audit(root)
            self.assertEqual(result['rounds'][0]['effectiveMethod'],'camera-fallback')

    def test_both_controls_uses_stricter_threshold_and_largest_is_separate(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            entries=[self.make_round(root,i,method,{'ownerName':'x'}) for i,method in enumerate(('direct','camera','direct','camera'))]
            for ordinal,key in ((1,'point:2'),(3,'point:3')):
                conn=sqlite3.connect(root/f'benchmark-{ordinal}.db')
                with conn:
                    conn.execute('UPDATE map_records SET record_key=?',(key,))
                conn.close()
            (root/'benchmark.json').write_text(json.dumps(entries))
            result=audit(root)
            pair=next(p for p in result['pairs'] if p['leftOrdinal']==0 and p['rightOrdinal']==1)
            values=pair['driftAssessment']['city']
            self.assertFalse(values['identityDifferenceNoWorseThanBothDriftControls'])
            self.assertTrue(values['identityDifferenceNoWorseThanLargestDriftControl'])

    def test_empty_rounds_do_not_qualify_zero_difference(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            entries=[self.make_round(root,i,method,{}) for i,method in enumerate(('direct','camera','direct','camera'))]
            for ordinal,entry in enumerate(entries):
                conn=sqlite3.connect(root/f'benchmark-{ordinal}.db')
                with conn:
                    conn.execute('DELETE FROM map_records')
                conn.close()
                entry['summary']['counts']['city']=0
            (root/'benchmark.json').write_text(json.dumps(entries))
            result=audit(root)
            pair=next(p for p in result['pairs'] if p['leftOrdinal']==0 and p['rightOrdinal']==1)
            self.assertIsNone(pair['driftAssessment']['city']['identityDifferenceNoWorseThanBothDriftControls'])

    def test_typed_storage_fields_are_observed_without_inventing_json_names(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            entries=[self.make_round(root,i,method,{}) for i,method in enumerate(('direct','camera'))]
            for ordinal in range(2):
                conn=sqlite3.connect(root/f'benchmark-{ordinal}.db')
                with conn:
                    conn.execute('ALTER TABLE map_records ADD COLUMN name TEXT')
                    conn.execute('ALTER TABLE map_records ADD COLUMN level INTEGER')
                    conn.execute('UPDATE map_records SET name=?,level=?',('private-test-owner',17))
                conn.close()
            (root/'benchmark.json').write_text(json.dumps(entries))
            result=audit(root)
            fields=result['pairs'][0]['kinds']['city']['fields']
            self.assertEqual(fields['ownerName'], {'bothAbsent':1})
            self.assertEqual(fields['$stored.name'], {'equalNonNull':1})
            self.assertEqual(fields['$stored.level'], {'equalNonNull':1})
            self.assertEqual(result['rounds'][0]['fieldCoverage']['city']['$stored.name']['nonNull'],1)
            self.assertNotIn('private-test-owner',json.dumps(result))


if __name__ == '__main__':
    unittest.main()
