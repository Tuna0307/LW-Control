import copy
import unittest

from march_freshness_gate import assess_region


def receipt():
    rect = [200, 400, 300, 500]
    common = {"sourceObserved": True, "serverId": 2212, "worldId": 0, "rect": rect}
    return {
        "pointReady": True, "serverId": 2212, "worldId": 0,
        "targetMarchRect": rect,
        "request": {**common, "observerLocalSequence": 10, "atMs": 1000},
        "response": {**common, "observerLocalSequence": 10, "atMs": 1200},
        "processing": {**common, "observerLocalSequence": 10, "atMs": 1300,
                       "processingFinished": True, "pendingQueueLength": 0},
        "snapshot": {**common, "atMs": 1400, "marchUUIDs": ["truck-A", "train-B"]},
        "independentOracle": {**common, "atMs": 1401, "activeMarchUUIDs": ["truck-A", "train-B"]},
    }


class SourceCausalGateTests(unittest.TestCase):
    def test_only_exact_source_attributed_barrier_allows_region_inverse(self):
        result = assess_region(receipt())
        self.assertTrue(result["barrierPass"])
        self.assertTrue(result["sameTimeInversePass"])
        self.assertFalse(result["globalMobileAccuracyQualified"])

    def test_point_ready_without_separate_march_reply_fails_closed(self):
        value = receipt()
        value["response"] = None
        self.assertFalse(assess_region(value)["barrierPass"])

    def test_prior_region_reply_fails_correlation_and_rectangle(self):
        value = receipt()
        value["response"]["observerLocalSequence"] = 9
        value["response"]["rect"] = [0, 0, 100, 100]
        self.assertFalse(assess_region(value)["barrierPass"])

    def test_queue_pending_even_after_response_does_not_pass(self):
        value = receipt()
        value["processing"]["pendingQueueLength"] = 1
        self.assertFalse(assess_region(value)["barrierPass"])

    def test_native_march_processing_after_snapshot_does_not_pass(self):
        value = receipt()
        value["snapshot"]["atMs"] = 1250
        self.assertFalse(assess_region(value)["barrierPass"])

    def test_duplicate_march_oracle_identity_is_not_evidence(self):
        value = receipt()
        value["independentOracle"]["activeMarchUUIDs"].append("truck-A")
        self.assertFalse(assess_region(value)["sameTimeInversePass"])

    def test_missing_active_march_fails_even_with_full_point_aoi(self):
        value = receipt()
        value["pointAoiCoverage"] = 10000
        value["snapshot"]["marchUUIDs"] = ["truck-A"]
        result = assess_region(value)
        self.assertTrue(result["barrierPass"])
        self.assertFalse(result["sameTimeInversePass"])
        self.assertIn("independent active march UUID missing despite point readiness", result["reasons"])

    def test_same_time_oracle_required_even_after_drained_queue(self):
        value = receipt()
        value["independentOracle"] = None
        self.assertTrue(assess_region(value)["barrierPass"])
        self.assertFalse(assess_region(value)["sameTimeInversePass"])

    def test_stale_oracle_cannot_claim_same_time_even_with_matching_uuid(self):
        for skew in (-1, 5000):
            value = receipt()
            value["independentOracle"]["atMs"] = value["snapshot"]["atMs"] + skew
            self.assertTrue(assess_region(value)["barrierPass"])
            self.assertFalse(assess_region(value)["sameTimeInversePass"])

    def test_unrelated_world_and_owner_fails(self):
        for component in ("request", "response", "processing", "snapshot", "independentOracle"):
            value = copy.deepcopy(receipt())
            value[component]["serverId"] = 999
            result = assess_region(value)
            self.assertFalse(result["sameTimeInversePass"], component)


if __name__ == "__main__":
    unittest.main()
