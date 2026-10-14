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
            "worldId": 0, "x": x, "y": y, "pointIndex": y * 1000 + x + 1,
            "positionSource": "WorldMarch.GetMarchCurPosIndex",
            "positionMethodToken": "0x06003208",
            "positionConversion": "WorldScene.IndexToTilePos",
            "positionConversionToken": "0x060039c3",
            "arriveTs": now // 1000 + 3600,
        }

    def cohort(start, end, rows, manager_count=None):
        return {
            "sourceObserved": True, "serverId": 2212, "worldId": 0,
            "sessionId": "only-test-session",
            "observedStartRuntimeSeconds": start,
            "observedEndRuntimeSeconds": end,
            "count": len(rows), "capped": False,
            # A complete typed filter must account for *every* member of
            # GetAllMarchesByCS, including non-Train/other Train kinds.
            "managerDictionaryCount": len(rows) if manager_count is None else manager_count,
            "managerEnumerationExhausted": True,
            "excludedManagerMarches": [],
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
            "sessionId": "only-test-session",
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

    def test_current_manager_truck_omitted_from_both_matching_typed_cohorts(self):
        value = complete_synthetic()
        # The old checker only required typed rows ⊆ native dictionary.
        # The omitted active March remains in the current native manager,
        # but both self-reported typed cohorts agree that it is absent.
        after_manager = value["marchSourceAfterProcess"]
        after_manager["marchUuids"].append("march-omitted-truck")
        after_manager["marchDictionaryCount"] += 1
        self.assert_rejected(value, "manager March identity not fully classified")

    def test_unknown_or_unexhausted_native_march_enumeration_is_not_coverage(self):
        for stage in ("pointReadyCohort", "afterProcessCohort"):
            value = complete_synthetic()
            value["compareReadyEvidence"][stage]["managerEnumerationExhausted"] = False
            self.assert_rejected(value, "complete native manager enumeration unproven")
            value = complete_synthetic()
            value["compareReadyEvidence"][stage]["managerDictionaryCount"] = 999
            self.assert_rejected(value, "manager dictionary count mismatch")

    def test_source_observed_non_train_can_be_excluded_but_unknown_cannot(self):
        value = complete_synthetic()
        stage = value["marchSourceAfterProcess"]
        stage["marchUuids"].append("march-unrelated")
        stage["marchDictionaryCount"] += 1
        cohort = value["compareReadyEvidence"]["afterProcessCohort"]
        cohort["managerDictionaryCount"] += 1
        unrelated = {
            "uuid": "march-unrelated", "sessionId": "only-test-session",
            "serverId": 2212, "worldId": 0, "sourceObserved": True,
            "sourceClass": "WorldMarch.train", "trainIsNull": True,
            "trainType": None, "observedAtRuntimeSeconds": 100.5,
        }
        cohort["excludedManagerMarches"].append(unrelated)
        self.assertTrue(audit_compare_ready(value)["sameSessionCompareContractPass"])
        for field, incorrect in (("trainIsNull", None), ("trainType", 1),
                                 ("sessionId", "prior-session"),
                                 ("sourceClass", "WorldPointManager")):
            altered = copy.deepcopy(value)
            altered["compareReadyEvidence"]["afterProcessCohort"][
                "excludedManagerMarches"][0][field] = incorrect
            self.assert_rejected(altered, "invalid native manager classification")

    def test_selected_truck_cannot_be_excluded_as_other_train_type(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["selectedTypes"] = ["truck"]
        train = value["compareReadyEvidence"]["afterProcessCohort"]["records"].pop()
        value["compareReadyEvidence"]["afterProcessCohort"]["count"] = 1
        value["compareReadyEvidence"]["independentNativeCohort"]["records"].pop()
        value["compareReadyEvidence"]["independentNativeCohort"]["count"] = 1
        excluded = {
            "uuid": "march-B", "sessionId": "only-test-session",
            "serverId": 2212, "worldId": 0, "sourceObserved": True,
            "sourceClass": "WorldMarch.train", "trainIsNull": False,
            "trainType": 2, "observedAtRuntimeSeconds": 100.5,
        }
        value["compareReadyEvidence"]["afterProcessCohort"]["excludedManagerMarches"] = [excluded]
        self.assertTrue(audit_compare_ready(value)["sameSessionCompareContractPass"])
        value["compareReadyEvidence"]["afterProcessCohort"]["excludedManagerMarches"][0]["trainType"] = 1
        self.assert_rejected(value, "invalid native manager classification")

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

    def test_independent_cohort_or_clock_from_other_session_fails(self):
        for label in ("pointReadyCohort", "afterProcessCohort", "independentNativeCohort"):
            value = complete_synthetic()
            value["compareReadyEvidence"][label]["sessionId"] = "earlier-session"
            self.assert_rejected(value, "native session")
        value = complete_synthetic()
        value["compareReadyEvidence"]["serverClock"]["sessionId"] = "earlier-session"
        self.assert_rejected(value, "server clock/source unavailable")

    def test_arrival_future_at_clock_but_expired_at_oracle_capture_tail(self):
        value = complete_synthetic()
        # A source clock nearly two seconds ahead of the independent row
        # observation window is accepted by the current one-second skew check.
        # Arrival +2s is no longer safe after elapsed window + margin.
        value["compareReadyEvidence"]["serverClock"]["observedAtRuntimeSeconds"] = 99.5
        for cohort in ("pointReadyCohort", "afterProcessCohort", "independentNativeCohort"):
            for record in value["compareReadyEvidence"][cohort]["records"]:
                record["arriveTs"] = value["nativeServerTimeUnixMs"] // 1000 + 2
        self.assert_rejected(value, "final arrival expired or untyped server clock")

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

    def test_wide_cohort_windows_cannot_claim_contemporaneous_complete_population(self):
        value = complete_synthetic()
        # The manager was read at 100.5 but a multi-minute collecting window
        # cannot attest that all listed UUIDs existed together at that time.
        value["compareReadyEvidence"]["afterProcessCohort"][
            "observedEndRuntimeSeconds"] = 400.0
        self.assert_rejected(value, "capture duration exceeds observer bound")
        value = complete_synthetic()
        value["compareReadyEvidence"]["independentNativeCohort"][
            "observedStartRuntimeSeconds"] = 0.0
        self.assert_rejected(value, "capture duration exceeds observer bound")

    def test_source_clock_cannot_predate_long_late_cohort_while_arrival_is_near(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["independentNativeCohort"][
            "observedEndRuntimeSeconds"] = 103.0
        value["compareReadyEvidence"]["serverClock"]["observedAtRuntimeSeconds"] = 100.51
        for cohort in ("pointReadyCohort", "afterProcessCohort", "independentNativeCohort"):
            value["compareReadyEvidence"][cohort]["records"][0]["arriveTs"] = (
                value["nativeServerTimeUnixMs"] // 1000 + 2)
        self.assert_rejected(value, "capture duration exceeds observer bound")

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
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["pointIndex"] = 41063
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

    def test_point_ready_unexpired_truck_disappears_but_terminal_stage_agrees(self):
        # The older gate only compared AFTER/ORACLE and silently dropped this
        # same-session point-ready native Truck that has future final arriveTs.
        value = complete_synthetic()
        point = value["compareReadyEvidence"]["pointReadyCohort"]
        vanished = copy.deepcopy(point["records"][0])
        vanished.update(uuid="march-vanished", marchUuid="march-vanished",
                        trainUuid="train-vanished")
        point["records"].append(vanished)
        point["count"] += 1
        stage = value["marchSourceAtPointReady"]
        stage["marchUuids"].append("march-vanished")
        stage["marchDictionaryCount"] += 1
        self.assert_rejected(value, "previously observed unexpired March vanished")

    def test_same_train_rebound_to_new_march_uuid_across_point_and_after(self):
        # WorldMarch UUID changes while trainUuid is reused: after/oracle
        # agree but source identity has collided during the interval.
        value = complete_synthetic()
        prior = value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]
        prior["uuid"] = prior["marchUuid"] = "march-prior"
        stage = value["marchSourceAtPointReady"]
        stage["marchUuids"] = ["march-prior"]
        self.assert_rejected(value, "train UUID rebound to different March")

    def test_point_ready_typed_cohort_does_not_exist_in_native_manager(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["uuid"] = "march-not-in-manager"
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["marchUuid"] = "march-not-in-manager"
        self.assert_rejected(value, "point-ready typed March absent from manager")

    def test_stale_per_stage_cohort_interval_must_bracket_manager_read(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"][
            "observedStartRuntimeSeconds"] = 90.0
        value["compareReadyEvidence"]["pointReadyCohort"][
            "observedEndRuntimeSeconds"] = 91.0
        self.assert_rejected(value, "point-ready typed cohort does not cover manager read")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"][
            "observedStartRuntimeSeconds"] = 100.60
        value["compareReadyEvidence"]["afterProcessCohort"][
            "observedEndRuntimeSeconds"] = 100.67
        self.assert_rejected(value, "after-process typed cohort does not cover manager read")
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"][
            "observedEndRuntimeSeconds"] = 100.45
        self.assert_rejected(value, "capture windows overlap or reverse")

    def test_untyped_optional_count_cannot_claim_complete_native_cohort(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"].pop("count")
        self.assert_rejected(value, "unverified or inconsistent typed count")

    def test_movement_requires_exact_observed_transition(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["x"] = 34
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["pointIndex"] = 41035
        self.assert_rejected(value, "no unique source-backed transition")
        value["compareReadyEvidence"]["nativeMovementTransitions"] = [{
            "uuid": "march-A", "sourceObserved": True,
            "sourceMethod": "WorldMarch.GetMarchCurPosIndex",
            "sourceMethodToken": "0x06003208",
            "serverId": 2212, "worldId": 0,
            "from": [34, 41], "to": [35, 41],
            "fromPointIndex": 41035, "toPointIndex": 41036,
            "sessionId": "only-test-session",
            "observedAtRuntimeSeconds": 100.36,
        }]
        result = audit_compare_ready(value)
        self.assertTrue(result["sameSessionCompareContractPass"], result["reasons"])
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceObserved"] = False
        self.assert_rejected(value, "movement transition stale/uncorrelated")
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceObserved"] = True
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sourceMethodToken"] = "0x06003209"
        self.assert_rejected(value, "movement transition stale/uncorrelated")

    def test_pre_point_and_post_after_movement_cannot_explain_stage_shift(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["x"] = 34
        value["compareReadyEvidence"]["pointReadyCohort"]["records"][0]["pointIndex"] = 41035
        valid = {
            "uuid": "march-A", "sessionId": "only-test-session",
            "sourceObserved": True, "sourceMethod": "WorldMarch.GetMarchCurPosIndex",
            "sourceMethodToken": "0x06003208", "serverId": 2212, "worldId": 0,
            "from": [34, 41], "to": [35, 41],
            "fromPointIndex": 41035, "toPointIndex": 41036,
            "observedAtRuntimeSeconds": 100.36,
        }
        value["compareReadyEvidence"]["nativeMovementTransitions"] = [valid]
        self.assertTrue(audit_compare_ready(value)["sameSessionCompareContractPass"])
        for moment in (100.23, 100.54):
            value["compareReadyEvidence"]["nativeMovementTransitions"][0][
                "observedAtRuntimeSeconds"] = moment
            self.assert_rejected(value, "movement transition stale/uncorrelated")
        value["compareReadyEvidence"]["nativeMovementTransitions"][0][
            "observedAtRuntimeSeconds"] = 100.36
        value["compareReadyEvidence"]["nativeMovementTransitions"][0]["sessionId"] = "prior-run"
        self.assert_rejected(value, "movement transition stale/uncorrelated")

    def test_cross_server_or_unqualified_type_field_fails(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["independentNativeCohort"]["serverId"] = 999
        self.assert_rejected(value, "server/world")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["sourceClass"] = "WorldMarch"
        self.assert_rejected(value, "unknown/stale/untagged")

    def test_current_position_index_not_an_xy_pair_or_unknown_conversion(self):
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0]["pointIndex"] = 1
        self.assert_rejected(value, "unknown/stale/untagged Truck/Train type")
        value = complete_synthetic()
        value["compareReadyEvidence"]["afterProcessCohort"]["records"][0][
            "positionConversionToken"] = "0x060039c4"
        self.assert_rejected(value, "unknown/stale/untagged Truck/Train type")

    def test_final_arrival_inside_observation_clock_skew_is_not_live_proof(self):
        value = complete_synthetic()
        end_soon = value["nativeServerTimeUnixMs"] // 1000 + 1
        for cohort in ("pointReadyCohort", "afterProcessCohort", "independentNativeCohort"):
            value["compareReadyEvidence"][cohort]["records"][0]["arriveTs"] = end_soon
        self.assert_rejected(value, "final arrival expired or untyped")


if __name__ == "__main__":
    unittest.main()
