"""Offline distinguishing checks for the native MarchData hot-path hypothesis."""

import sys
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mobile_hotpath_bench as bench


class MobileHotpathFixtureChecks(unittest.TestCase):
    def test_production_contract_is_present_and_source_drift_fails_closed(self):
        self.assertEqual(len(bench.source_guard()), 64)
        with tempfile.TemporaryDirectory() as root:
            source = Path(root) / "not-real.lua"
            source.write_text("-- no native March source here\n", encoding="utf-8")
            with patch.object(bench, "PRODUCTION", source):
                with self.assertRaisesRegex(ValueError, "contract changed"):
                    bench.source_guard()

    def test_fence25_repair_does_not_claim_individual_missing_rectangles(self):
        trace = bench.make_trace(late_frames=1)
        self.assertEqual(trace.initial_requests, 625)
        self.assertEqual(trace.repair_requests, 50)
        self.assertEqual(trace.initial_fences_failed, 2)
        self.assertEqual(trace.repair_passes, 1)
        self.assertEqual(trace.expected_pushes, 673)
        self.assertEqual(trace.rectangles_verified, 625)
        self.assertEqual(len(trace.fences), 75)  # 25 initial + 50 singles
        first_pass = trace.fences[:25]
        self.assertEqual(sum(fence.pushes < fence.sends for fence in first_pass), 2)
        self.assertEqual(sum(len(fence.certified) for fence in first_pass), 575)
        self.assertEqual(sum(len(fence.certified) for fence in trace.fences[25:]), 50)

    def test_typed_rows_fields_arrival_and_managed_recheck_are_equivalent(self):
        trace = bench.make_trace(late_frames=1)
        for population in (0, 128):
            with self.subTest(background=population):
                old = bench.collect(trace, "baseline", background=population)
                new = bench.collect(trace, "handled_plus_one", background=population)
                bench.validate_equivalence(trace, old, new)
                self.assertEqual(bench.fingerprint(old.rows), bench.fingerprint(new.rows))
                self.assertEqual(len(old.rows), 125)
                self.assertEqual(sum(row["trainType"] == 2 for row in old.rows.values()), 9)
                self.assertGreater(old.publish_dropped, 0)
                self.assertTrue(all(row["ownerName"] and row["goods"] for row in old.rows.values()))

    def test_unstamped_native_cache_update_disproves_unconditional_gating(self):
        # Hypothetical cache mutation outside the handled-stamp path.
        # Without independent proof it cannot occur, a snapshot skip is unsafe.
        trace = bench.make_trace(unsolicited=True)
        old = bench.collect(trace, "baseline", background=24)
        new = bench.collect(trace, "handled_plus_one", background=24)
        self.assertIn("synthetic-march-0650", old.rows)
        self.assertNotIn("synthetic-march-0650", new.rows)
        with self.assertRaises(AssertionError):
            bench.validate_equivalence(trace, old, new)

    def test_deadline_seconds_milliseconds_and_either_expired(self):
        base = bench.synth_march(12)
        self.assertTrue(bench.truck_unarrived(base, base.end_time - 1))
        self.assertFalse(bench.truck_unarrived(base, base.end_time))
        self.assertFalse(bench.truck_unarrived(base, base.arrive_ts))
        self.assertEqual(bench.deadline_ms(1_800_000_000), 1_800_000_000_000)
        self.assertEqual(bench.deadline_ms(1_800_000_000_123), 1_800_000_000_123)

    def test_invalid_benchmark_parameters_fail(self):
        with self.assertRaises(ValueError):
            bench.make_trace(late_frames=-1)
        with self.assertRaises(ValueError):
            bench.benchmark(repeats=0)
        with self.assertRaises(ValueError):
            bench.collect(bench.make_trace(), "unsupported")


if __name__ == "__main__":
    unittest.main()
