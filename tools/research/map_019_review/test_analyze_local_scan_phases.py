"""Checks that the diagnostic rejects incomplete and uncorrelated evidence."""

import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location(
    "analyze_local_scan_phases", Path(__file__).with_name("analyze_local_scan_phases.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def valid_round(method="direct"):
    result = dict(round="A", effectiveMethod=method, completed=True,
                  terminalPhase="completed", seconds=90.0,
                  stageMetricsCorrelated=True,
                  stageMetrics=dict(sourceCaptureMilliseconds=88000,
                                    checkpointMilliseconds=1000,
                                    publicationMilliseconds=500),
                  process={"processCpuSeconds": 100, "sampledPeakPrivateBytes": 1048576},
                  counts=dict(city=12, resource=5))
    if method == "direct":
        result.update(metricsCorrelated=True,
                      metrics=dict(uniqueCoveredAois=10000, requiredAois=10000,
                                   acquisitionMilliseconds=27000,
                                   resourceHydrationMilliseconds=1400,
                                   projectionMilliseconds=100),
                      **{"direct-evidence": dict(state="proven", sameRunCorrelated=True),
                         "mobile-evidence": dict(state="proven", sameRunCorrelated=True,
                                                 elapsedSeconds=56.5, rectanglesCompleted=625,
                                                 initialRequestsSent=625, repairRequestsSent=0,
                                                 requestsSent=625)})
    return result


class SummaryTests(unittest.TestCase):
    def test_reconciles_distinct_stage_clocks_and_avoids_raw_records(self):
        result = module.summarize({"rounds": [valid_round(), valid_round("camera")]})
        first = result["rounds"][0]
        self.assertEqual(first["mobileSweepSeconds"], 56.5)
        self.assertEqual(first["nativeAoiSeconds"], 27)
        self.assertEqual(first["detailHydrationSeconds"], 1.4)
        self.assertEqual(first["remainingCaptureSecondsApprox"], 3)
        self.assertEqual(first["sampledPeakPrivateMiB"], 1)
        self.assertNotIn("game_uid", str(result))

    def test_refuses_incomplete_or_uncorrelated_direct(self):
        for key, value in (("completed", False), ("metricsCorrelated", False)):
            row = valid_round()
            row[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                module.summarize({"rounds": [row]})
        row = valid_round()
        row["mobile-evidence"]["rectanglesCompleted"] = 624
        with self.assertRaises(ValueError):
            module.summarize({"rounds": [row]})

    def test_refuses_impossible_durations(self):
        row = valid_round()
        row["stageMetrics"]["sourceCaptureMilliseconds"] = 200000
        with self.assertRaises(ValueError):
            module.summarize({"rounds": [row]})

    def test_refuses_all_ten_validation_error(self):
        row = valid_round()
        row["validationErrors"] = ["missing_source"]
        with self.assertRaises(ValueError):
            module.summarize({"rounds": [row]})


if __name__ == "__main__":
    unittest.main()
