"""Inert check for the native passive-telemetry data-loss failure.

The actual Oct 14 owned game negative lost the pre-camera field when the
Lua producer reconstructed request.details. This distinguishes the recovery
from another false-positive single-stage source snapshot.
"""
import pathlib
import unittest

from luaparser import ast

ROOT = pathlib.Path(__file__).resolve().parents[2]


def producer_guard(source):
    ast.parse(source)
    start = source.index('function M._map014MarchSourceSnapshot(world)')
    end = source.index('local function train_march_aoi_records', start)
    observer = source[start:end]
    if any(term in observer for term in (
        'SendViewRequest', 'UpdateViewRequest(', 'WorldGetMarchInfos(',
        'WorldGetRectMarchInfos(', 'SetCameraPos', 'SendAoiRequest')):
        raise ValueError('March observer must remain passive')
    if 'request.observeMarchFreshness == true' not in source:
        raise ValueError('March diagnostic opt-in is missing')
    if 'marchSourceBefore = request.details and request.details.marchSourceBefore,' not in source:
        raise ValueError('March before state lost when request.details is reset')
    if 'details.marchSourceAtPointReady = M._map014MarchSourceSnapshot(world)' not in source:
        raise ValueError('March point-ready observation is absent')
    point_sample = source.index('details.marchSourceAtPointReady = M._map014MarchSourceSnapshot(world)')
    native_train_extraction = source.index('local train_records, train_records_error = train_march_aoi_records(')
    if point_sample >= native_train_extraction:
        raise ValueError('March point-ready observation happens after native Train extraction')
    if 'marchSourceBefore = details.marchSourceBefore,' not in source or \
            'marchSourceAtPointReady = details.marchSourceAtPointReady,' not in source:
        raise ValueError('March snapshots not present in serialized native response')


class MarchNativeTelemetryChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.probe = (ROOT / 'tools/current_live_resource_probe.lua').read_text(encoding='utf8')

    def test_reviewed_source_preserves_both_passive_snapshots(self):
        producer_guard(self.probe)

    def test_overwritten_before_state_rejected(self):
        corrupted = self.probe.replace(
            'marchSourceBefore = request.details and request.details.marchSourceBefore,',
            'lostSourceBefore = nil,', 1)
        with self.assertRaisesRegex(ValueError, 'before state lost'):
            producer_guard(corrupted)

    def test_active_march_request_added_to_observer_rejected(self):
        altered = self.probe.replace(
            'function M._map014MarchSourceSnapshot(world)',
            'function M._map014MarchSourceSnapshot(world)\n    WorldGetRectMarchInfos()', 1)
        with self.assertRaisesRegex(ValueError, 'must remain passive'):
            producer_guard(altered)

    def test_late_march_snapshot_after_enrichment_rejected(self):
        capture = 'details.marchSourceAtPointReady = M._map014MarchSourceSnapshot(world)'
        altered = self.probe.replace(capture, 'details.telemetryPending = true', 1)
        insert_before = 'details.trainMarchRecords = train_records'
        altered = altered.replace(insert_before, capture + '\n        ' + insert_before, 1)
        with self.assertRaisesRegex(ValueError, 'after native Train extraction|observation is absent'):
            producer_guard(altered)


if __name__ == '__main__':
    unittest.main()
