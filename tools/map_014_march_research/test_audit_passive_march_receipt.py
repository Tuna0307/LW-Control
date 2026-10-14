"""Offline adversarial inverses: no game, network, source edits, or UUID dump."""

import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import unittest

from audit_passive_march_receipt import (
    TICKS_AT_UNIX_EPOCH, audit_receipt, compare_archived,
)


ROOT = Path(__file__).resolve().parents[2]
ACTUAL = ROOT / "artifacts/map014-lead-live/native-march-source-20261014-102638"
MIXED = ROOT / "artifacts/map014-lead-live/native-benchmark-mixed-20261014-021149"
SAMPLE = json.loads((ACTUAL / "live-resource/bulk-aoi-diagnostic-result.json").read_text("utf-8"))
CAMERA_ONLY = json.loads((ACTUAL / "march-source-before-camera.json").read_text("utf-8"))


def fake_complete():
    when = datetime(2026, 10, 14, 2, 29, 14, tzinfo=timezone.utc)
    captured_ms = round(when.timestamp() * 1000)
    epoch_ticks = TICKS_AT_UNIX_EPOCH + captured_ms * 10000
    rect = {"xMin": -5, "yMin": -5, "xMax": 105, "yMax": 105}
    camera = {"xMin": 0, "yMin": 0, "xMax": 100, "yMax": 100}
    queue_proof = {
        "fieldToken": "0x04002561",
        "runtimeType": "GameKit.Base.StepProcessQueue",
        "memberName": "Count",
        "getterParameterCount": 0,
        "readOnlyGetterVerified": True,
        "exactInstalledTypeVerified": True,
    }
    merged_proof = {
        "fieldToken": "0x04002562",
        "runtimeType": "System.Collections.Generic.Dictionary",
        "memberName": "Count",
        "getterParameterCount": 0,
        "readOnlyGetterVerified": True,
        "exactInstalledTypeVerified": True,
    }

    def stage(moment, get_offset, handle_offset, uuids):
        return {
            "available": True, "observedAtRuntimeSeconds": moment,
            "launchSessionId": "only-test-session", "serverId": 2212, "worldId": 0,
            "viewRect": rect, "cameraViewRect": camera,
            "lastGetBlockMessageTicks": epoch_ticks + get_offset,
            "lastHandleBlockMessageTicks": epoch_ticks + handle_offset,
            "pendingQueuePresent": True, "pendingQueueCount": 0,
            "mergedQueuePresent": True, "mergedQueueCount": 0,
            "pendingQueueCountProvenance": queue_proof,
            "mergedQueueCountProvenance": merged_proof,
            "marchDictionaryCount": len(uuids),
            "marchUuids": uuids, "marchUuidSampleCapped": False,
        }

    def event(phase, moment, tick_offset):
        return {
            "phase": phase, "sourceObserved": True,
            "sourceCorrelationId": "native-marchevent-10",
            "serverId": 2212, "worldId": 0, "rect": rect,
            "launchSessionId": "only-test-session",
            "sourceClass": ("WorldMarchDataManager" if phase in ("process", "handler")
                            else "WorldGetRectMarchInfosMessage"),
            "methodToken": {"request": "0x060019a6", "response": "0x060019a7",
                            "handler": "0x0600327b",
                            "process": "0x06003277"}[phase],
            "method": {
                "request": "WorldGetRectMarchInfosMessage.CSSetData",
                "response": "WorldGetRectMarchInfosMessage.CSHandleResponse",
                "handler": "WorldMarchDataManager.HandleWorldGetRectMarchInfos",
                "process": "WorldMarchDataManager.HandleMessage",
            }[phase],
            "observationPoint": "method-exit" if phase in ("request", "handler", "process") else "method-entry",
            "observedAtRuntimeSeconds": moment, "nativeTicks": epoch_ticks + tick_offset,
        }

    return {
        "capturedAt": when.isoformat(),
        "launchSessionId": "only-test-session",
        "serverId": 2212, "worldId": 0,
        "nativeServerTimeUnixMs": captured_ms,
        "isRecvViewPoints": True, "hasReceiveViewPointsReply": True,
        "responseFlagsTransitioned": True,
        "marchSourceBefore": stage(100, -4000000, -4000000, ["march-A"]),
        "marchSourceAtPointReady": stage(100.25, -3000000, -3000000, ["march-A"]),
        "marchSourceAfterProcess": stage(100.5, -1500000, -1000000, ["march-A", "march-B"]),
        "marchRectEvents": [
            event("request", 100.1, -3500000),
            event("response", 100.3, -2500000),
            event("handler", 100.35, -2200000),
            event("process", 100.4, -2000000),
        ],
        "independentActiveMarches": {
            "sourceObserved": True, "observedAtUnixMs": captured_ms,
            "launchSessionId": "only-test-session",
            "serverId": 2212, "worldId": 0, "rect": rect,
            "activeMarches": [
                {"uuid": "march-A", "serverId": 2212, "worldId": 0,
                 "arriveTs": (captured_ms // 1000) + 3600,
                 "observedAtUnixMs": captured_ms},
                {"uuid": "march-B", "serverId": 2212, "worldId": 0,
                 "arriveTs": (captured_ms // 1000) + 3600,
                 "observedAtUnixMs": captured_ms},
            ],
        },
    }


class PassiveReceiptAuditChecks(unittest.TestCase):
    def assert_failed(self, fixture, expected):
        assessment = audit_receipt(fixture)
        self.assertFalse(assessment["mobileFreshnessQualified"], assessment)
        self.assertTrue(any(expected in message for message in assessment["failureReasons"]),
                        assessment["failureReasons"])

    def test_actual_native_receipt_is_real_snapshot_but_incomplete(self):
        result = audit_receipt(SAMPLE)
        self.assertTrue(result["receiptHasActualPointReadyMarchManagerSnapshot"])
        self.assertEqual(result["observedMarchDictionaryCount"], 60)
        self.assert_failed(SAMPLE, "marchSourceBefore")
        self.assert_failed(SAMPLE, "March rect request/response/handler/process")
        self.assert_failed(SAMPLE, "StepProcessQueue.Count getter provenance")
        self.assert_failed(SAMPLE, "numeric native DateTime ticks unavailable")
        self.assertEqual(len(SAMPLE.get("train_march_records", [])), 0)

    def test_separate_before_camera_checkpoint_is_not_march_source(self):
        self.assertEqual(CAMERA_ONLY.get("mode"), "camera_checkpoint")
        self.assertNotIn("marchUuids", CAMERA_ONLY)
        candidate = copy.deepcopy(SAMPLE)
        candidate["marchSourceBefore"] = CAMERA_ONLY
        self.assert_failed(candidate, "marchSourceBefore")
        self.assertNotEqual(CAMERA_ONLY["requestId"], SAMPLE["requestId"])

    def test_complete_hypothetical_single_region_pass_still_not_global(self):
        verdict = audit_receipt(fake_complete())
        self.assertTrue(verdict["mobileFreshnessQualified"], verdict["failureReasons"])
        self.assertFalse(verdict["globallyQualified"])

    def test_before_source_missing_fails(self):
        value = fake_complete()
        del value["marchSourceBefore"]
        self.assert_failed(value, "marchSourceBefore")

    def test_no_post_process_snapshot_fails(self):
        value = fake_complete()
        del value["marchSourceAfterProcess"]
        self.assert_failed(value, "marchSourceAfterProcess")

    def test_missing_request_or_response_or_processing_fails(self):
        for phase in ("request", "response", "handler", "process"):
            with self.subTest(phase=phase):
                value = fake_complete()
                value["marchRectEvents"] = [event for event in value["marchRectEvents"]
                                             if event["phase"] != phase]
                self.assert_failed(value, "March rect request/response/handler/process")

    def test_reply_entry_does_not_certify_separate_manager_handler(self):
        # Installed CSHandleResponse at 0x060019a7 has an error short-circuit;
        # its entry is no proof that the separate manager handler ran.
        value = fake_complete()
        value["marchRectEvents"] = [e for e in value["marchRectEvents"]
                                    if e["phase"] != "handler"]
        self.assert_failed(value, "request/response/handler/process")

    def test_manager_handler_must_follow_reply_and_precede_process(self):
        value = fake_complete()
        value["marchRectEvents"][2]["observedAtRuntimeSeconds"] = 100.29
        self.assert_failed(value, "runtime order invalid")
        value = fake_complete()
        value["marchRectEvents"][2]["nativeTicks"] = (
            value["marchRectEvents"][3]["nativeTicks"] + 10000)
        self.assert_failed(value, "native timestamps invalid")

    def test_try_execute_entry_cannot_claim_queue_handler_completion(self):
        # TryExcuteMarchMessage returns bool and contains merge-dictionary
        # early exits; no method-entry witness certifies HandleMessage exit.
        value = fake_complete()
        event = value["marchRectEvents"][3]
        event["method"] = "WorldMarchDataManager.TryExcuteMarchMessage"
        event["methodToken"] = "0x06003276"
        self.assert_failed(value, "March process method/source/server/world")
        value = fake_complete()
        value["marchRectEvents"][3]["observationPoint"] = "method-entry"
        self.assert_failed(value, "March process method/source/server/world")

    def test_foreign_manager_stage_and_oracle_run_are_not_same_session(self):
        for stage in ("marchSourceBefore", "marchSourceAtPointReady", "marchSourceAfterProcess"):
            for field, bad in (("launchSessionId", "stale-launch"), ("serverId", 2213),
                               ("worldId", 1)):
                with self.subTest(stage=stage, field=field):
                    value = fake_complete()
                    value[stage][field] = bad
                    self.assert_failed(value, "native manager source session/server/world mismatch")
        value = fake_complete()
        value["marchRectEvents"][2]["launchSessionId"] = "stale-launch"
        self.assert_failed(value, "March handler method/source/server/world")
        value = fake_complete()
        value["independentActiveMarches"]["launchSessionId"] = "stale-launch"
        self.assert_failed(value, "independent March oracle belongs")

    def test_merged_dictionary_zero_without_native_field_getter_is_false_completion(self):
        value = fake_complete()
        del value["marchSourceAfterProcess"]["mergedQueueCountProvenance"]
        self.assert_failed(value, "merged dictionary Count getter provenance")
        value = fake_complete()
        value["marchSourceAtPointReady"]["mergedQueueCountProvenance"]["fieldToken"] = "0x04002561"
        self.assert_failed(value, "merged dictionary Count getter provenance")

    def test_wall_tick_and_runtime_lifecycle_cannot_disagree_by_minutes(self):
        value = fake_complete()
        # Each clock series is monotonic and every native tick remains within
        # five minutes of the capture, but native elapsed time differs from
        # the same pair of runtime observations by two minutes.
        for event in value["marchRectEvents"][1:]:
            event["nativeTicks"] += 120 * 10000000
        value["marchSourceAfterProcess"]["lastHandleBlockMessageTicks"] += 120 * 10000000
        self.assert_failed(value, "native event/runtime duration contradicts")

    def test_unverified_request_response_process_correlation_fails(self):
        value = fake_complete()
        value["marchRectEvents"][1]["sourceCorrelationId"] = "other"
        self.assert_failed(value, "not correlated")

    def test_foreign_rectangle_or_native_source_method_fails(self):
        value = fake_complete()
        value["marchRectEvents"][1]["rect"] = {
            "xMin": 1, "yMin": 1, "xMax": 2, "yMax": 2,
        }
        self.assert_failed(value, "rectangle")
        value = fake_complete()
        value["marchRectEvents"][0]["method"] = "WorldPointManager.UpdateViewRequest"
        self.assert_failed(value, "request method/source/server/world")

    def test_native_march_rect_must_match_manager_view_not_narrow_camera(self):
        value = fake_complete()
        # Both are valid native rectangles, but only viewRect represents the
        # March manager subscription; cameraViewRect is deliberately smaller.
        camera = value["marchSourceAtPointReady"]["cameraViewRect"]
        for event in value["marchRectEvents"]:
            event["rect"] = camera
        self.assert_failed(value, "active manager view rectangle")

    def test_march_source_rect_must_remain_bound_through_consumption(self):
        value = fake_complete()
        value["marchSourceAfterProcess"]["viewRect"] = {
            "xMin": 300, "yMin": 300, "xMax": 405, "yMax": 405,
        }
        # Legacy oracle accepts the camera rectangle as a substitute, so
        # remove that incidental failure and isolate the manager seam.
        value["independentActiveMarches"]["rect"] = copy.deepcopy(
            value["marchSourceAfterProcess"]["cameraViewRect"])
        self.assert_failed(value, "March manager rectangle changed before processing")

    def test_forged_phase_method_class_and_token_cannot_pass(self):
        for phase in ("request", "response", "handler", "process"):
            for key, invalid in (("sourceClass", "WorldPointManager"),
                                 ("methodToken", "0x06000001")):
                with self.subTest(phase=phase, key=key):
                    value = fake_complete()
                    next(e for e in value["marchRectEvents"] if e["phase"] == phase)[key] = invalid
                    self.assert_failed(value, f"March {phase} method/source/server/world")

    def test_old_native_event_ticks_cannot_pretend_to_be_current_runtime(self):
        value = fake_complete()
        # Wall-clock event timestamps a whole day before their recorded
        # runtime observations otherwise satisfy the existing ordering check.
        for event in value["marchRectEvents"][:2]:
            event["nativeTicks"] -= 24 * 60 * 60 * 10000000
        self.assert_failed(value, "March rect event native timestamp outside capture window")

    def test_stale_region_server_world_is_rejected(self):
        for field in ("serverId", "worldId"):
            value = fake_complete()
            value["marchRectEvents"][1][field] = 999
            self.assert_failed(value, "response method/source/server/world")

    def test_queue_zero_without_getter_provenance_is_ambiguous(self):
        value = fake_complete()
        del value["marchSourceAtPointReady"]["pendingQueueCountProvenance"]
        self.assert_failed(value, "StepProcessQueue.Count getter provenance")
        value = fake_complete()
        value["marchSourceAtPointReady"]["pendingQueueCountProvenance"]["getterParameterCount"] = 1
        self.assert_failed(value, "StepProcessQueue.Count getter provenance")
        value = fake_complete()
        value["marchSourceAfterProcess"]["pendingQueueCount"] = 1
        self.assert_failed(value, "backlog not drained")

    def test_native_timestamp_strings_or_mismatched_ticks_fail(self):
        value = fake_complete()
        del value["marchSourceAtPointReady"]["lastHandleBlockMessageTicks"]
        value["marchSourceAtPointReady"]["lastHandleBlockMessageTime"] = (
            "10/14/2026 2:29:13 AM: -417153764"
        )
        self.assert_failed(value, "numeric native DateTime ticks unavailable")
        value = fake_complete()
        value["marchSourceAfterProcess"]["lastGetBlockMessageTicks"] += 27 * 3600 * 10000000
        self.assert_failed(value, "native get timestamp incompatible")
        value = fake_complete()
        value["marchSourceAfterProcess"]["lastGetBlockMessageTicks"] -= 10 * 60 * 10000000
        self.assert_failed(value, "native get timestamp incompatible")
        value = fake_complete()
        value["marchRectEvents"][3]["nativeTicks"] += 2000000
        self.assert_failed(value, "native handle timestamp does not corroborate")
        value = fake_complete()
        value["marchRectEvents"][1]["observedAtRuntimeSeconds"] = 100.05
        self.assert_failed(value, "runtime order invalid")

    def test_missing_stale_or_expired_uuid_fails(self):
        value = fake_complete()
        value["marchSourceAfterProcess"]["marchUuids"] = ["march-A", "stale-other"]
        self.assert_failed(value, "missing or stale native March UUID")
        value = fake_complete()
        value["independentActiveMarches"]["activeMarches"][0]["arriveTs"] = (
            value["nativeServerTimeUnixMs"] // 1000 - 1
        )
        self.assert_failed(value, "stale, expired or uncorrelated")
        value = fake_complete()
        value["independentActiveMarches"]["observedAtUnixMs"] -= 6000
        self.assert_failed(value, "oracle stale")
        value = fake_complete()
        value["independentActiveMarches"]["activeMarches"][0]["observedAtUnixMs"] -= 6000
        self.assert_failed(value, "stale, expired or uncorrelated")

    def test_native_uuid_dup_or_incomplete_sample_fails(self):
        value = fake_complete()
        value["marchSourceAfterProcess"]["marchUuids"] = ["march-A", "march-A"]
        self.assert_failed(value, "duplicate, or capped")
        value = fake_complete()
        value["marchSourceAtPointReady"]["marchUuidSampleCapped"] = True
        self.assert_failed(value, "duplicate, or capped")

    def test_time_separated_archived_overlap_is_only_counts(self):
        result = compare_archived(SAMPLE, MIXED)
        self.assertFalse(result["sameTime"])
        self.assertFalse(result["accuracyOrExpiryInferred"])
        self.assertEqual(result["laterObservedMarchUUIDCount"], 60)
        self.assertEqual(len(result["rounds"]), 6)
        for item in result["rounds"]:
            self.assertGreater(item["elapsedBetweenArchiveFinishAndLaterObservationSeconds"], 0)
            for kind in ("truck", "railway"):
                self.assertEqual(item["kinds"][kind]["overlapWithLaterUnclassifiedManagerUUIDs"], 0)
        self.assertEqual(
            [r["kinds"]["truck"]["archivedCount"] for r in result["rounds"]],
            [103, 39, 95, 39, 89, 36],
        )


if __name__ == "__main__":
    unittest.main()
