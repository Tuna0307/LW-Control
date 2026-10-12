"""MAP-012 native LOD0 coordinate witness and independent adversity tests."""
from dataclasses import replace
import unittest

from scanner_accuracy_012 import Cell, aoi_index, audit


# Actual R13 v24 second-camera observation, not a synthetic successful reply.
R13_OLD_VIEW = (4244, 4245, 4246, 4344, 4345, 4346, 4444, 4445, 4446)
R13_SECOND_TARGET = (545, 495)
TS = 1_800_000
NATIVE = (Cell("city", 2212, 0, "city:a", TS, (("level", "29"), ("power", "400"))),
          Cell("resource", 2212, 0, "resource:b", TS, (("remaining", "18"), ("level", "8"))))


class ScannerAccuracy012Tests(unittest.TestCase):
    def check(self, stored, category, native=NATIVE):
        differences = audit(native, stored, server=2212, world=0,
                            earliest_ms=TS - 1000, latest_ms=TS + 1000)
        self.assertIn(category, [difference.category for difference in differences], differences)

    def test_native_lod0_index_space_and_r13_first_view_reject(self):
        self.assertEqual(aoi_index(455, 425), 4245)
        self.assertEqual(aoi_index(*R13_SECOND_TARGET), 4954)
        self.assertEqual(len(set(R13_OLD_VIEW)), 9)
        self.assertTrue(all(index // 100 in {42, 43, 44} for index in R13_OLD_VIEW))
        self.assertTrue(all(index % 100 in {44, 45, 46} for index in R13_OLD_VIEW))
        self.assertNotIn(4954, R13_OLD_VIEW)
        # This tests the actual observed native current-view footprint,
        # independently of the receiver's flags-ready and packet counters.

    def test_no_differences_when_independent_fields_match(self):
        self.assertEqual(audit(NATIVE, NATIVE, server=2212, world=0,
                               earliest_ms=TS - 1000, latest_ms=TS + 1000), [])

    def test_missing_city_and_extra_city(self):
        self.check(NATIVE[1:], "missing")
        self.check(NATIVE + (Cell("city", 2212, 0, "city:extra", TS, (("level", "1"),)),), "extra")

    def test_duplicate_record(self):
        self.check(NATIVE + NATIVE[:1], "duplicate")

    def test_wrong_server_and_world(self):
        self.check((replace(NATIVE[0], server=2213), NATIVE[1]), "wrong-context")
        self.check((replace(NATIVE[0], world=2), NATIVE[1]), "wrong-context")

    def test_stale_and_future_capture(self):
        self.check((replace(NATIVE[0], captured_ms=TS - 2000), NATIVE[1]), "stale")
        self.check((replace(NATIVE[0], captured_ms=TS + 2000), NATIVE[1]), "stale")

    def test_incorrect_field_value_and_missing_field(self):
        self.check((replace(NATIVE[0], fields=(("level", "30"), ("power", "400"))), NATIVE[1]), "field-mismatch")
        self.check((replace(NATIVE[0], fields=(("level", "29"),)), NATIVE[1]), "field-mismatch")

    def test_expiring_or_moving_object_requires_time_bracket(self):
        monster = Cell("monster", 2212, 0, "monster:uuid1", TS, (("level", "10"),))
        difference = audit((monster,), (), server=2212, world=0,
                           earliest_ms=TS - 1000, latest_ms=TS + 1000)
        self.assertEqual(difference[0].category, "time-bracket-needed")

    def test_bad_geometry_never_wraps_to_valid_aoi(self):
        for xy in ((1000, 1), (1, 1000), (-1, 50), (0, -1)):
            with self.subTest(tile=xy), self.assertRaises(ValueError):
                aoi_index(*xy)


if __name__ == "__main__":
    unittest.main()
