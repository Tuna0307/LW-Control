"""Geometry/contract checks for the OFFLINE plan; no native accuracy positives."""
import unittest

import plan_requests as plan


class RequestPlanChecks(unittest.TestCase):
    def test_current_matches_63_linear_requests_including_tail(self):
        requests = plan.linear_points()
        self.assertEqual(len(requests), 63)
        self.assertEqual(requests[-1].indices, tuple(range(9920, 10000)))
        self.assertEqual(plan.stats(requests)["duplicateAoiEntries"], 0)
        self.assertTrue(plan.stats(requests)["fullWorldIndexCoverage"])

    def test_rejected_oversize_inputs_cannot_generate_plan(self):
        for size in (0, 161, 320, 640):
            with self.assertRaises(ValueError):
                plan.linear_points(size)
        with self.assertRaises(ValueError):
            plan.rectangles(10, 17)

    def test_combined_retains_exact_truck_envelopes_and_max_rate_is_not_changed(self):
        old, combined = plan.truck_rectangles(), plan.truck_rectangles(True)
        self.assertEqual(len(combined), 625)
        for before, after in zip(old, combined):
            self.assertEqual((before.x, before.y, before.left_bottom, before.right_top),
                             (after.x, after.y, after.left_bottom, after.right_top))
            self.assertEqual(len(after.indices), 16)
            self.assertIn(before.indices[0], after.indices)
            self.assertTrue(all(0 <= index < 10000 for index in after.indices))
        self.assertEqual(plan.stats(combined)["duplicateAoiEntries"], 0)
        self.assertTrue(plan.stats(combined)["fullWorldIndexCoverage"])
        self.assertFalse(plan.stats(old)["fullWorldIndexCoverage"])

    def test_compact_rectangle_edges_never_duplicate_or_omit_aoi(self):
        for rows, cols, count in ((10, 16, 70), (8, 20, 65), (4, 4, 625)):
            requests = plan.rectangles(rows, cols)
            self.assertEqual(len(requests), count)
            self.assertEqual(plan.stats(requests)["duplicateAoiEntries"], 0)
            self.assertTrue(plan.stats(requests)["fullWorldIndexCoverage"])

    def test_report_refuses_native_measurement_inference(self):
        report, _ = plan.build_report()
        self.assertFalse(report["nativeTrial"])
        self.assertFalse(report["canSendRequests"])
        combined = report["combinedHypothesis"]
        self.assertEqual(combined["requestReduction"], 63)
        self.assertEqual(combined["indexEntryReduction"], 625)
        self.assertIsNone(combined["nativeSpeedGainSeconds"])
        self.assertIsNone(combined["bytesOnWire"])
        self.assertFalse(combined["nativeAccuracyQualified"])


if __name__ == "__main__":
    unittest.main()
