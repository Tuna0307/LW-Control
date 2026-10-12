"""Independent fail-closed checks for the finite MAP-013 method decision.

Reads retained compact evidence, not source-authored synthetic game records.
Historical raw second-region receipts are separately audited when present;
neither substitutes for actual native packet-to-reply provenance.
"""
from __future__ import annotations

import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
DECISION = ROOT / "docs/proof/MAP-013-METHOD-SELECTION-2026-10-12.json"
BENCHMARK = ROOT / "docs/proof/MAP-008-NATIVE-METHOD-BENCHMARKS-2026-10-11.json"


class Map013MethodSelectionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.decision = json.loads(DECISION.read_text(encoding="utf-8"))
        cls.benchmarks = json.loads(BENCHMARK.read_text(encoding="utf-8"))

    def test_baseline_is_scoped_and_no_unqualified_acceleration_enabled(self):
        d = self.decision
        self.assertEqual(d["status"], "PARTIAL")
        self.assertFalse(d["productionSourceModified"])
        self.assertFalse(d["historicMeasuredScope"]["qualifiedAlternativeForThreeAlternatingBenchmarks"])
        self.assertFalse(d["historicMeasuredScope"]["fullPopulationRecallProven"])
        selected = [r for r in d["routeClassifications"] if r["class"] == "QUALIFIED"]
        self.assertTrue(selected)
        self.assertTrue(all("existing" in r["route"] for r in selected))
        allowed = {
            "QUALIFIED", "COMPLEMENTARY_ONLY", "REJECTED_FOR_OBSERVED_CASE",
            "UNRESOLVED_WITH_EXACT_EDGE", "OWNER_DEFERRED",
        }
        self.assertTrue(all(r["class"] in allowed and r["scope"]
                            for r in d["routeClassifications"]))
        self.assertTrue(any(r["class"] == "OWNER_DEFERRED" for r in d["routeClassifications"]))

    def test_second_region_empty_is_not_native_attribution(self):
        r = self.decision["r15r16ArchivedComparison"]
        assert r["firstRegion"]["pointRecords"] == 28
        assert r["firstRegion"]["monsterMarchRecords"] == 26
        second = r["secondRegion"]
        self.assertEqual(second["targetAoiIndex"], 4954)
        self.assertEqual(second["localCurrentAndAddAoiCount"], 80)
        self.assertEqual(second["nativeMessageViewObservedCount"], 9)
        self.assertFalse(second["nativeMessageViewObservedContainsTarget"])
        self.assertEqual(sum(second[k] for k in (
            "secondTargetNativePointRecords",
            "secondTargetNativeMonsterMarchRecords",
            "secondTargetNativeTrainMarchRecords")), 0)
        self.assertEqual(second["matchingNativeReplyOrSegments"], "unobserved, not proven absent")
        self.assertEqual(r["exactUnderlyingCause"].split(";")[0], "unresolved")

    def test_comparison_numbers_are_historical_not_new_speedup(self):
        d = self.decision["historicMeasuredScope"]
        self.assertEqual(d["serialLuaProbes"], 340)
        self.assertEqual(d["measuredCameraInflight"], 1)
        self.assertEqual(d["fullWorldAois"], 10000)
        self.assertEqual(d["logicalBlocks"], 2500)
        self.assertEqual(d["allEightNormal"]["n"], 2)
        self.assertEqual(d["allEightFast"]["n"], 2)
        self.assertEqual(self.benchmarks["rawNetworkPacketCount"], None)
        self.assertEqual(self.benchmarks["measuredCameraInflight"], 1)
        self.assertFalse(d["qualifiedAlternativeForThreeAlternatingBenchmarks"])
        self.assertTrue(all(item["n"] == 1 for item in d["twoServerPassiveCycleExamples"]))

    def test_remote_known_region_full_are_three_separate_unproved_scopes(self):
        routes = self.decision["routeClassifications"]
        remote = [r for r in routes if r["route"].startswith("remote ")]
        self.assertEqual(len(remote), 3)
        self.assertTrue(all(r["class"] == "UNRESOLVED_WITH_EXACT_EDGE"
                            for r in remote))
        self.assertIn("server/connection", remote[0]["scope"])


if __name__ == "__main__":
    unittest.main()
