"""Never confuse an accurate local AOI footprint with a native response."""
import unittest
from map013_native_view_audit import audit_receipt


def make_receipt(rows=0, message_has_target=False, local_has_target=True):
    return {
        "sequenceSnapshots": [
            {"targetTileX": 455, "targetTileY": 425, "pointRecords": [{}]*28,
             "monsterMarchRecords": [{}]*26, "trainMarchRecords": [], "requestedIndices": [4245]},
            {"targetTileX": 545, "targetTileY": 495, "pointRecords": [{}]*rows,
             "monsterMarchRecords": [], "trainMarchRecords": [], "requestedIndices": [4954,4955]},
        ],
        "secondReceiverObservations": [
            {"hasTarget": local_has_target, "addHasTarget": local_has_target,
             "currentCount": 80, "msgCount": 9, "msgHasTarget": message_has_target, "flagsReady": True}
        ],
        "resourceCompleteness": {"rejectionReasonCounts": {"outside_selected_aoi": 7}},
    }


class NativeViewAuditTests(unittest.TestCase):
    def test_exact_observed_source_divergence(self):
        result = audit_receipt(make_receipt())
        self.assertTrue(result["firstRegion"]["hasPositiveSourceWitness"])
        self.assertEqual(result["firstRegion"]["sourceRows"], {"point": 28, "monster": 26, "train": 0})
        self.assertEqual(result["secondRegion"]["rejectedOutsideTargetAoiPointRows"], 7)
        self.assertTrue(result["secondRegion"]["localTargetFootprintPresent"])
        self.assertFalse(result["secondRegion"]["msgViewTargetObserved"])
        self.assertFalse(result["qualifiesConsecutiveAcquisition"])
        self.assertEqual(result["observedBoundary"],
                         "local_target_aoi_does_not_match_native_message_view_and_second_source_rows_empty")

    def test_invented_positive_and_ready_flags_do_not_qualify(self):
        for rows, has_msg in [(0, True), (12, True), (12, False)]:
            with self.subTest(rows=rows, message_has_target=has_msg):
                result = audit_receipt(make_receipt(rows, has_msg))
                self.assertFalse(result["qualifiesConsecutiveAcquisition"])
                self.assertEqual(result["secondRegion"]["nativeTargetResponseSegmentsAttributed"], "unobserved")
                self.assertEqual(result["secondRegion"]["nativeSourceServerAndGenerationAttributed"], "unobserved")

    def test_absent_or_insufficient_source_observation_rejected(self):
        receipt = make_receipt()
        receipt["sequenceSnapshots"] = receipt["sequenceSnapshots"][:1]
        with self.assertRaises(ValueError):
            audit_receipt(receipt)
        receipt = make_receipt()
        receipt["secondReceiverObservations"] = []
        with self.assertRaises(ValueError):
            audit_receipt(receipt)


if __name__ == "__main__":
    unittest.main()
