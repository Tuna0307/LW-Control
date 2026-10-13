"""Inverse real source receipts: a fast stale or wrong-world row never qualifies."""
import copy
import unittest

from audit_scanner_phase_evidence import POSITIVE, NEGATIVE, load, qualify_region


class NativePhaseClassification(unittest.TestCase):
    def test_real_restored_positive_source_and_adversarial_mutations(self):
        original = load(POSITIVE[0])
        self.assertEqual(qualify_region(original), [])
        changes = (
            (lambda d: d.update(map013NativeReplyState="waiting"), "reply_not_source_matched"),
            (lambda d: d.update(cameraGeometryRestored=False), "camera_not_restored"),
            (lambda d: d.update(serverLod=2), "wrong_native_lod_or_geometry"),
            (lambda d: d["point_records"][0].update(serverId=2213), "wrong_server_or_world_row"),
            (lambda d: d["point_records"][0].update(worldId=1), "wrong_server_or_world_row"),
            (lambda d: d["point_records"].append(copy.deepcopy(d["point_records"][0])), "duplicate_source_ids"),
            (lambda d: d["point_records"][0].update(x=999, y=999), "row_outside_qualified_footprint"),
            (lambda d: d.update(point_records=[]), "no_positive_rows"),
        )
        for mutate, expected in changes:
            with self.subTest(expected=expected):
                changed = copy.deepcopy(original)
                mutate(changed)
                self.assertIn(expected, qualify_region(changed))

    def test_historical_second_region_and_zoom_not_accepted(self):
        for path in NEGATIVE:
            with self.subTest(path=path):
                failures = qualify_region(load(path))
                self.assertIn("reply_not_source_matched", failures)
                self.assertIn("no_positive_rows", failures)


if __name__ == "__main__":
    unittest.main()
