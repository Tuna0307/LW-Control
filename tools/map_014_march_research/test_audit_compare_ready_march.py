"""Adversarial same-session March source comparison; fixtures are SYNTHETIC."""

import copy
import unittest

from audit_compare_ready_march import ASSEMBLY_SHA256, audit_compare_ready
from test_audit_passive_march_receipt import SAMPLE, fake_complete


def complete_synthetic():
    result = fake_complete()
    result["launchSessionId"] = "only-test-session"
    result["marchSourceBefore"]["viewRect"] = {
        "xMin": 300, "yMin": 300, "xMax": 405, "yMax": 405,
    }
    result["marchSourceBefore"]["cameraViewRect"] = {
        "xMin": 305, "yMin": 305, "xMax": 400, "yMax": 400,
    }
    target_rect = copy.deepcopy(result["marchSourceAtPointReady"]["viewRect"])
    camera_rect = copy.deepcopy(result["marchSourceAfterProcess"]["cameraViewRect"])
    now = result["nativeServerTimeUnixMs"]

    def typed(uuid, x, y, train_type, train_uuid):
        return {
            "sourceObserved": True, "sourceClass": "WorldMarch.train",
            "uuid": uuid, "marchUuid": uuid, "trainUuid": train_uuid,
            "trainType": train_type, "ownerServer": 2212, "serverId": 2212,
            "worldId": 0, "x": x, "y": y, "arriveTs": now // 1000 + 3600,
        }

    def cohort(start, end, rows):
        return {
            "sourceObserved": True, "serverId": 2212, "worldId": 0,
            "observedStartRuntimeSeconds": start,
            "observedEndRuntimeSeconds": end,
            "count": len(rows), "capped": False,
            # Different native acquisition stages are independent evidence.
            # Do not alias fixture rows; mutation must model one stage only.
            "records": copy.deepcopy(rows),
        }

    first = typed("march-A", 35, 41, 1, "train-A")
    train = typed("march-B", 63, 81, 2, "train-B")
    result["compareReadyEvidence"] = {
        "installedAssemblySha256": ASSEMBLY_SHA256,
        "sessionId": result["launchSessionId"],
        "selectedTypes": ["truck", "railway"],
        "targetMarchRect": target_rect,
        "targetCameraViewRect": camera_rect,
        "serverClock": {
            "sourceObserved": True, "sourceDomain": "native-server-clock",
            "sourceRef": "synthetic-observer-clock",
            "serverId": 2212, "worldId": 0,
            "unixMilliseconds": now,
            "observedAtRuntimeSeconds": 100.51,
        },
        "pointReadyCohort": cohort(100.22, 100.26, [first]),
        "afterProcessCohort": cohort(100.4, 100.55, [first, train]),
        "independentNativeCohort": cohort(100.49, 100.57, [first, train]),
        "nativeMovementTransitions": [],
    }
    return result


class TypedMobileComparisonTests(unittest.TestCase):
    def assert_rejected(self, sample, fragment):
        result = audit_compare_ready(sample)
        self.assertFalse(result["sameSessionCompareContractPass"], result)
        self.assertIn(fragment, " | ".join(result["reasons"]), result)

    def test_real_native_receipt_is_not_simultaneous_compare_ready(self):
        result = audit_compare_ready(SAMPLE)
        self.assertFalse(result["sameSessionCompareContractPass"])
        self.assertFalse(result["sourceAuthenticityIndependentlyVerified"])
        self.assertFalse(result["globalMobileAccuracyQualified"])
        self.assert_rejected(SAMPLE, "underlying March")

    def test_complete_synthetic_contract_does_not_prove_source_or_world(self):
        result = audit_compare_ready(complete_synthetic())
        self.assertTrue(result["sameSessionCompareContractPass"], result["reasons"])
        self.assertFalse(result["sourceAuthenticityIndependentlyVerified"])
        self.assertFalse(result["globalMobileAccuracyQualified"])
        self.assertEqual(result["typedCounts"], {"point": 1, "after": 2, "oracle": 2})

    def test_absent_independent_server_clock_and_untyped_clock(self):
        for bad in (None, "1791916154000", True):
            value = complete_synthetic()
            value["compareReadyEvidence"]["serverClock"]["unixMilliseconds"] = bad
            self.assert_rejected(value, "typed independent native server clock")
        value = complete_synthetic()
        value["compareReadyEvidence"]["serverClock"]["sourceRef"] = ""
        self.assert_rejected(value, "typed independent native server clock")

    def test_wrong_assembly_or_old_session_fails(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["installedAssemblySha256"] = "wrong"
        self.assert_rejected(value, "assembly hash")
        value = complete_synthetic()
        value["compareReadyEvidence"]["sessionId"] = "old-session"
        self.assert_rejected(value, "different or missing session")

    def test_old_view_rect_even_with_matching_event_is_rejected(self):
        value = complete_synthetic()
        value["marchSourceBefore"]["viewRect"] = copy.deepcopy(
            value["compareReadyEvidence"]["targetMarchRect"])
        self.assert_rejected(value, "old manager rectangle")
        value = complete_synthetic()
        value["compareReadyEvidence"]["targetMarchRect"]["xMin"] = 20
        self.assert_rejected(value, "mismatched target view")

    def test_stale_stage_and_nonoverlapping_cohort_are_rejected(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["independentNativeCohort"][
            "observedStartRuntimeSeconds"] = 101.1
        value["compareReadyEvidence"]["independentNativeCohort"][
            "observedEndRuntimeSeconds"] = 101.2
        self.assert_rejected(value, "not temporally overlapping")
        value = complete_synthetic()
        value["compareReadyEvidence"]["serverClock"]["observedAtRuntimeSeconds"] = 93
        self.assert_rejected(value, "not temporally overlapping")
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"]["observedEndRuntimeSeconds"] = 99
        self.assert_rejected(value, "monotonic observation interval")

    def test_type_ambiguity_unknown_or_string_type_is_rejected(self):
        for bad in (0, 3, "1", None, True):
            value = complete_synthetic()
            value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["trainType"] = bad
            self.assert_rejected(value, "unknown/stale/untagged Truck/Train type")
        value = complete_synthetic()
        value["compareReadyEvidence"]["selectedTypes"] = ["truck"]
        self.assert_rejected(value, "outside selected kind")

    def test_expired_native_final_arrival_and_untyped_arrival_fails(self):
        for arrival in (0, "1791950000", None, True):
            value = complete_synthetic()
            value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["arriveTs"] = arrival
            self.assert_rejected(value, "unknown/stale/untagged")
        value = complete_synthetic()
        expire = value["nativeServerTimeUnixMs"] // 1000 - 1
        for cohort in ("afterProcessCohort", "independentNativeCohort"):
            value["compareReadyEvidence"][cohort]["records"][0]["arriveTs"] = expire
        self.assert_rejected(value, "final arrival expired")

    def test_current_manager_cache_identity_collision(self):
        value = complete_synthetic()
        duplicate = copy.deepcopy(value["compareReadyEvidence"]["afterProcessCohort"]["records"][0])
        duplicate["trainType"] = 2
        value["compareReadyEvidence"]["afterProcessCohort"]["records"].append(duplicate)
        value["compareReadyEvidence"]["afterProcessCohort"]["count"] += 1
        self.assert_rejected(value, "UUID cache collision")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["x"] = 62
        self.assert_rejected(value, "cache identity/type/position/lifetime collision")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["trainUuid"] = "train-Z"
        self.assert_rejected(value, "cache identity/type/position/lifetime collision")

    def test_manager_missing_or_extra_selected_uuid_fails(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"].pop()
        value["compareReadyEvidence"]["afterProcessCohort"]["count"] = 1
        self.assert_rejected(value, "population differs")
        value = complete_synthetic()
        value["marchSourceAfterProcess"]["marchUuids"] = ["march-A", "march-Z"]
        self.assert_rejected(value, "current manager cache")

    def test_manager_uuid_repurposed_between_stages_fails(self):
        for key, mutation in (
            ("trainType", 2), ("trainUuid", "train-X"), ("ownerServer", 2213)
        ):
            value = complete_synthetic()
            value["compareReadyEvidence"]["pointReadyCohort"]["records"][0][key] = mutation
            self.assert_rejected(value, "repurposed")

    def test_movement_requires_exact_observed_transition(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["x"] = 34
        self.assert_rejected(value, "no unique source-backed transition")
        value["compareReadyEvidence"]["nativeMovementTransitions"] = [{
            "uuid": "march-A", "sourceObserved": True,
            "sourceMethod": "WorldMarch.GetMarchCurPosIndex",
            "sourceMethodToken": "0x06003208",
            "serverId": 2212, "worldId": 0,
            "from": [34, 41], "to": [35, 41],
            "observedAtRuntimeSeconds": 100.36,
        }]
        result = audit_compare_ready(value)
        self.assertTrue(result["sameSessionCompareContractPass"], result["reasons"])
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceObserved"] = False
        self.assert_rejected(value, "movement transition stale/uncorrelated")
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceObserved"] = True
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceMethodToken"] = "0x06003209"
        self.assert_rejected(value, "movement transition stale/uncorrelated")

    def test_cross_server_or_unqualified_type_field_fails(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["independentNativeCohort"]["serverId"] = 999
        self.assert_rejected(value, "server/world")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["sourceClass"] = "WorldMarch"
        self.assert_rejected(value, "unknown/stale/untagged")


if __name__ == "__main__":
    unittest.main()
