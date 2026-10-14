"""Offline adversarial contract tests: python -m unittest discover -s tools/map_014_shared_transport_audit -v"""

from copy import deepcopy
from datetime import datetime, timedelta, timezone
import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from audit_group_transport import (  # noqa: E402
    ARCHIVES, LUA, OVERVIEW, PROVIDER, BoundaryError, ProducerContract,
    archive_rejection, audit_current_producer_group_request,
    audit_current_producer_group_result, audit_future_shape_only,
    audit_one_probe_correlation, audit_other_source_transports,
    audit_pipe_result_correlation_source, correlate_pipe_status_result_only,
)


NOW = datetime(2026, 10, 14, 2, 0, tzinfo=timezone.utc)


def one_probe():
    command = {
        "schema": "1", "probeVersion": "lwbridge-live-resource-probe-2",
        "requestId": "fastcity-current", "profileId": "owned-profile",
        "launchSessionId": "owned-launch", "challenge": "owned-challenge",
        "gamePid": 1234, "serverId": 2212, "scanRunId": "owned-run",
        "requestMode": "coverage", "targetTileX": 535, "targetTileY": 485,
    }
    result = {
        "schemaVersion": 1, "probeVersion": command["probeVersion"],
        "requestId": command["requestId"], "profileId": command["profileId"],
        "launchSessionId": command["launchSessionId"], "challenge": command["challenge"],
        "gamePid": command["gamePid"], "requestMode": command["requestMode"],
        "state": "proven", "capturedAt": "2026-10-14T02:00:00Z",
    }
    return command, result


def future_shape():
    """Synthetic schema example only; no data/packet in this fixture is authentic."""
    owner = {
        "scanRunId": "controlled-run", "profileId": "controlled-profile",
        "launchSessionId": "controlled-launch", "challenge": "controlled-challenge",
        "gamePid": 1234, "serverId": 2212, "worldId": 0,
        "groupGeneration": "controlled-generation",
    }
    base = 1_700_000_000_000
    stages = []
    for i in range(3):
        stages.append({
            **owner, "regionOrdinal": i, "requestId": f"controlled-stage-{i}",
            "nativeReplyRequestId": f"controlled-stage-{i}",
            "nativeReplyState": "matched",
            "requestStartedAtUnixMs": base + 100*i,
            "nativeReplyAtUnixMs": base + 100*i + 10,
        })
    blocks = []
    aois = []
    for block in range(2500):
        row, col = divmod(block, 50)
        native_ids = [(row*2+dy)*100+(col*2+dx) for dy in range(2) for dx in range(2)]
        response_id = f"controlled-reply-{block}"
        reply_time = base+300
        for native_idx in native_ids:
            aois.append({**owner, "nativeAoiIndex": native_idx,
                         "nativeReplyRequestId": response_id,
                         "nativeReplyAtUnixMs": reply_time,
                         "nativeSource": "WorldPointManager._curViewIndex"})
        blocks.append({"blockIndex": block, "kind": "city",
                       "source": "WorldPointManager._pointInfos",
                       "nativeReplyRequestId": response_id,
                       "nativeReplyAtUnixMs": reply_time})
    return {
        "owner": owner, "stages": stages,
        "aoiProofs": aois, "kindBlocks": blocks,
        "homeAck": {**owner, "homeRestoreRequestId": "controlled-home",
                    "homeNativeReplyRequestId": "controlled-home",
                    "nativeHomeReplyState": "matched",
                    "originalCameraRestored": True,
                    "homeNativeReplyAtUnixMs": base+500},
    }


class ProducerContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = ProducerContract.read_source()

    def test_real_reader_whitelist_and_one_shot_writer(self):
        self.assertEqual(self.contract.modes,
                         {"native", "expanded", "direct", "coverage", "anchor", "edge", "zoom"})
        self.assertEqual(self.contract.command_path, "bulk-aoi-diagnostic.txt")
        self.assertEqual(self.contract.result_path, "bulk-aoi-diagnostic-result.json")
        self.assertNotIn("scanRunId", self.contract.output_keys)
        self.assertNotIn("serverId", self.contract.output_keys)
        self.assertNotIn("worldId", self.contract.output_keys)
        self.assertNotIn("groupGeneration", self.contract.output_keys)
        self.assertNotIn("homeNativeReplyRequestId", self.contract.output_keys)
        self.assertNotIn("kindBlocks", self.contract.output_keys)
        self.assertIn("nativeCurrentSetCount", self.contract.output_keys)
        self.assertIn("point_records", self.contract.output_keys)

    def test_detect_unsupported_new_lua_mode_instead_of_reusing_old_conclusion(self):
        source = LUA.read_text(encoding="utf-8-sig")
        changed = source.replace('request.requestMode ~= "zoom")',
                                 'request.requestMode ~= "zoom" and request.requestMode ~= "group")')
        self.assertNotEqual(changed, source)
        with self.assertRaisesRegex(BoundaryError, "whitelist changed"):
            ProducerContract.read_source(lua=changed)

    def test_detect_group_result_writer_change_instead_of_stale_negative(self):
        source = LUA.read_text(encoding="utf-8-sig")
        marker = "local function write_bulk_aoi_result(request, state, error_text, details)"
        self.assertIn(marker, source)
        changed = source.replace(marker, marker + "\n    local impossible = 0", 1)
        changed = changed.replace('write_json(bulk_aoi_diagnostic_result_path, {',
                                  'write_json(bulk_aoi_diagnostic_result_path, {\n        groupGeneration = request.groupGeneration,', 1)
        with self.assertRaisesRegex(BoundaryError, "new group fields"):
            ProducerContract.read_source(lua=changed)

    def test_bulk_aoi_owner_predicate_weakened_to_and_requires_requalification(self):
        source = LUA.read_text(encoding="utf-8-sig")
        exact = ("request.profileId ~= identity.profileId or "
                 "request.launchSessionId ~= identity.sessionId or")
        marker = "local function read_bulk_aoi_diagnostic(now)"
        start = source.index(marker)
        end = source.index("local function collection_contains_int", start)
        self.assertIn(exact, source[start:end])
        changed = (source[:start] +
                   source[start:end].replace(exact, exact.replace(" or ", " and ", 1), 1) +
                   source[end:])
        with self.assertRaisesRegex(BoundaryError, "owner guard changed"):
            ProducerContract.read_source(lua=changed)

    def test_bulk_aoi_owner_mismatch_result_must_remain_fail_closed(self):
        source = LUA.read_text(encoding="utf-8-sig")
        exact = 'request.error = "bulk_aoi_diagnostic_identity_mismatch"'
        self.assertIn(exact, source)
        changed = source.replace(exact, 'request.error = nil', 1)
        with self.assertRaisesRegex(BoundaryError, "owner guard changed"):
            ProducerContract.read_source(lua=changed)

    def test_current_producer_supports_correlated_one_shot(self):
        cmd, result = one_probe()
        audit_one_probe_correlation(cmd, result, NOW-timedelta(seconds=1),
                                    NOW, self.contract)

    def test_old_per_probe_file_rejected_on_request_id(self):
        cmd, result = one_probe()
        result["requestId"] = "fastcity-old"
        with self.assertRaisesRegex(BoundaryError, "old or foreign"):
            audit_one_probe_correlation(cmd, result, NOW, NOW, self.contract)

    def test_correct_request_id_does_not_rescue_stale_timestamp(self):
        cmd, result = one_probe()
        result["capturedAt"] = "2026-10-13T23:59:00Z"
        with self.assertRaisesRegex(BoundaryError, "stale or future"):
            audit_one_probe_correlation(cmd, result, NOW, NOW, self.contract)

    def test_future_capture_timestamp_rejected(self):
        cmd, result = one_probe()
        result["capturedAt"] = "2026-10-14T02:00:07Z"
        with self.assertRaisesRegex(BoundaryError, "stale or future"):
            audit_one_probe_correlation(cmd, result, NOW, NOW, self.contract)

    def test_mixed_profile_and_launch_session_each_rejected(self):
        for field in ("profileId", "launchSessionId", "challenge", "gamePid"):
            with self.subTest(field=field):
                cmd, result = one_probe()
                result[field] = "foreign" if field != "gamePid" else 9999
                with self.assertRaisesRegex(BoundaryError, "owner/profile/session"):
                    audit_one_probe_correlation(cmd, result, NOW, NOW, self.contract)

    def test_group_request_mode_is_not_in_producer_whitelist(self):
        cmd, _ = one_probe()
        cmd["requestMode"] = "group"
        with self.assertRaisesRegex(BoundaryError, "unsupported producer group command"):
            audit_current_producer_group_request(cmd, self.contract)

    def test_forged_selected_stage_echo_on_legal_coverage_request(self):
        cmd, _ = one_probe()
        cmd["selectedStageEcho"] = "A"
        with self.assertRaisesRegex(BoundaryError, "forged group/stage"):
            audit_current_producer_group_request(cmd, self.contract)

    def test_even_plain_coverage_mode_is_never_a_group(self):
        cmd, _ = one_probe()
        with self.assertRaisesRegex(BoundaryError, "one-shot producer"):
            audit_current_producer_group_request(cmd, self.contract)

    def test_forged_receipt_group_fields_cannot_be_from_lua_writer(self):
        _, result = one_probe()
        result.update({"groupGeneration": "invented", "homeNativeReplyRequestId": "invented"})
        with self.assertRaisesRegex(BoundaryError, "cannot emit"):
            audit_current_producer_group_result(result, self.contract)

    def test_unforged_producer_receipt_cannot_prove_group(self):
        _, result = one_probe()
        self.assertTrue(set(result).issubset(self.contract.output_keys))
        with self.assertRaisesRegex(BoundaryError, "one-probe result"):
            audit_current_producer_group_result(result, self.contract)

    def test_real_archival_abc_runs_rejected_as_live_group(self):
        observed = 0
        for archive in ARCHIVES:
            if not all((archive/f"sequence-region-{i}.json").exists() for i in range(1,4)):
                continue
            observed += 1
            with self.subTest(archive=archive.name):
                outcome = archive_rejection(archive, self.contract)
                self.assertEqual(outcome["stageCount"], 3)
                self.assertFalse(outcome["groupQualified"])
                self.assertEqual(len(outcome["reasons"]), 3)
                self.assertEqual(outcome["producerSnapshot"], "experimental-resource-probe.lua")
                self.assertTrue(all("one-probe result lacks" in reason
                                    for reason in outcome["reasons"]))
                if archive.name.endswith("004916"):
                    self.assertEqual(outcome["summary"]["nativeCoverage"], 10000.0)
                    self.assertEqual(outcome["summary"]["probes"], 119.0)
        if observed == 0:
            self.skipTest("archived native evidence is not mounted; no fabricated native result")


class FutureControlledShapeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = future_shape()

    def check_corruption(self, change, reason):
        claim = deepcopy(self.fixture)
        change(claim)
        with self.assertRaisesRegex(BoundaryError, reason):
            audit_future_shape_only(claim)

    def test_complete_fixture_is_only_structurally_coherent(self):
        audit_future_shape_only(self.fixture)
        # No boolean field or marker turns controlled schema into native packets.
        self.assertFalse(self.fixture.get("authenticated", False))

    def test_missing_native_stage_reply(self):
        self.check_corruption(lambda c: c["stages"][1].pop("nativeReplyRequestId"),
                              "native stage reply")

    def test_echoed_selected_stage_has_no_native_reply(self):
        def change(c):
            c["stages"][1]["selectedStageEcho"] = "B"
            c["stages"][1]["nativeReplyRequestId"] = ""
        self.check_corruption(change, "native stage reply")

    def test_duplicate_stage_request_ids(self):
        self.check_corruption(lambda c: c["stages"][1].update({
            "requestId": c["stages"][0]["requestId"],
            "nativeReplyRequestId": c["stages"][0]["requestId"],
        }), "duplicated or foreign native stage reply")

    def test_stale_stage_response(self):
        self.check_corruption(lambda c: c["stages"][1].update({
            "requestStartedAtUnixMs": c["stages"][0]["nativeReplyAtUnixMs"]}),
            "stale or overlapping")

    def test_mixed_stage_profile_and_server(self):
        for field in ("profileId", "launchSessionId", "serverId", "scanRunId", "groupGeneration"):
            self.check_corruption(lambda c, k=field: c["stages"][1].update({k: "other"}),
                                  "mixed stage profile/session/run")

    def test_repeated_aoi_cannot_fill_missing_identity(self):
        self.check_corruption(lambda c: c["aoiProofs"][1].update({
            "nativeAoiIndex": c["aoiProofs"][0]["nativeAoiIndex"]}),
            "duplicate, missing or invalid native AOI")

    def test_reduced_9999_aoi_evidence(self):
        self.check_corruption(lambda c: c["aoiProofs"].pop(), "10000 per-AOI")

    def test_wrong_aoi_reply_owner(self):
        self.check_corruption(lambda c: c["aoiProofs"][0].update({"profileId": "foreign"}),
                              "mixed native AOI owner")

    def test_missing_aoi_native_source_must_fail(self):
        self.check_corruption(lambda c: c["aoiProofs"][0].pop("nativeSource"),
                              "native AOI source")

    def test_one_reply_cannot_certify_all_10000_aoi(self):
        def change(c):
            for proof in c["aoiProofs"]:
                proof["nativeReplyRequestId"] = "claimed-all-world"
            for block in c["kindBlocks"]:
                block["nativeReplyRequestId"] = "claimed-all-world"
        self.check_corruption(change, "native AOI reply cohort")

    def test_same_reply_id_cannot_reappear_with_inconsistent_native_time(self):
        def change(c):
            for proof in c["aoiProofs"][:4]:
                proof["nativeReplyRequestId"] = "shared-multi-time"
            c["kindBlocks"][0]["nativeReplyRequestId"] = "shared-multi-time"
            for proof in c["aoiProofs"][4:8]:
                proof["nativeReplyRequestId"] = "shared-multi-time"
                proof["nativeReplyAtUnixMs"] += 1
            c["kindBlocks"][1]["nativeReplyRequestId"] = "shared-multi-time"
            c["kindBlocks"][1]["nativeReplyAtUnixMs"] += 1
        self.check_corruption(change, "native AOI reply cohort")

    def test_final_home_reply_cannot_double_as_native_aoi_reply(self):
        def change(c):
            home_id = c["homeAck"]["homeNativeReplyRequestId"]
            for proof in c["aoiProofs"][:4]:
                proof["nativeReplyRequestId"] = home_id
            c["kindBlocks"][0]["nativeReplyRequestId"] = home_id
        self.check_corruption(change, "home reply reused as native AOI evidence")

    def test_four_native_aois_same_logical_block_must_correlate(self):
        self.check_corruption(lambda c: c["aoiProofs"][1].update({"nativeReplyRequestId": "other"}),
                              "four AOIs")

    def test_2499_kind_block_results_not_2500(self):
        self.check_corruption(lambda c: c["kindBlocks"].pop(), "2500 type-enriched")

    def test_duplicate_kind_block_same_count(self):
        self.check_corruption(lambda c: c["kindBlocks"][1].update({"blockIndex": 0}),
                              "logical block type/source/reply")

    def test_kind_block_missing_received_reply(self):
        self.check_corruption(lambda c: c["kindBlocks"][10].update({"nativeReplyRequestId": ""}),
                              "logical block type/source/reply")

    def test_home_receipt_absent(self):
        self.check_corruption(lambda c: c.pop("homeAck"), "native final home ACK")

    def test_home_reply_missing_or_unmatched(self):
        self.check_corruption(lambda c: c["homeAck"].update({"nativeHomeReplyState": "pending"}),
                              "native final home reply absent")

    def test_home_reply_reused_from_stage(self):
        self.check_corruption(lambda c: c["homeAck"].update({
            "homeRestoreRequestId": c["stages"][2]["requestId"],
            "homeNativeReplyRequestId": c["stages"][2]["requestId"],
        }), "native final home reply absent")

    def test_home_confirmation_wrong_session(self):
        self.check_corruption(lambda c: c["homeAck"].update({"launchSessionId": "foreign"}),
                              "final home owner changed")

    def test_home_camera_only_no_fresh_reply(self):
        self.check_corruption(lambda c: c["homeAck"].update({
            "originalCameraRestored": True, "homeNativeReplyRequestId": ""}),
            "native final home reply absent")


class AlternateProducerTraceTests(unittest.TestCase):
    def test_current_authenticated_pipe_is_get_status_only(self):
        observed = audit_other_source_transports()
        self.assertTrue(observed["namedPipeTransportExists"])
        self.assertEqual(observed["namedPipeLuaFunctions"], ["getStatus"])
        self.assertEqual(observed["namedPipeAcceptedEnvelopeTypes"],
                         ["command", "hello.ack"])
        self.assertFalse(observed["pipeNativeGroupProducer"])
        self.assertFalse(observed["producerNativeGroupPump"])
        self.assertFalse(observed["Map317ProductionGroupFactoryConfigured"])
        self.assertFalse(observed["futureGroupIngressImplementationPresent"])

    def test_new_native_group_pipe_call_cannot_be_silently_reported_as_absent(self):
        original = OVERVIEW.read_text(encoding="utf-8-sig")
        observed = 'function_name ~= "getStatus"'
        self.assertIn(observed, original)
        changed = original.replace(observed,
                                   '(function_name ~= "getStatus" and function_name ~= "nativeGroup")',
                                   1)
        with self.assertRaisesRegex(BoundaryError, "named-pipe (Lua receiver|command rejection guard) changed"):
            audit_other_source_transports(overview=changed)

    def test_new_pipe_packet_consumer_needs_requalification(self):
        original = OVERVIEW.read_text(encoding="utf-8-sig")
        observed = 'elseif message_type == "command" then'
        self.assertIn(observed, original)
        changed = original.replace(
            observed, 'elseif message_type == "native.group.result" then\n'
                      '            return\n'
                      '        ' + observed, 1)
        with self.assertRaisesRegex(BoundaryError, "named-pipe Lua receiver changed"):
            audit_other_source_transports(overview=changed)

    def test_pipe_command_guard_operator_weakening_must_fail(self):
        original = OVERVIEW.read_text(encoding="utf-8-sig")
        exact = 'kind ~= "call" or function_name ~= "getStatus" or'
        self.assertIn(exact, original)
        changed = original.replace(exact,
                                   'kind ~= "call" and function_name ~= "getStatus" or', 1)
        with self.assertRaisesRegex(BoundaryError, "named-pipe command rejection guard"):
            audit_other_source_transports(overview=changed)

    def test_pipe_owner_guard_operator_weakening_must_fail(self):
        original = OVERVIEW.read_text(encoding="utf-8-sig")
        exact = 'version ~= 1 or profile_id ~= control.profileId or'
        self.assertIn(exact, original)
        changed = original.replace(exact,
                                   'version ~= 1 and profile_id ~= control.profileId or', 1)
        with self.assertRaisesRegex(BoundaryError, "named-pipe owner rejection guard"):
            audit_other_source_transports(overview=changed)

    def test_new_lua_native_group_pump_cannot_hide_behind_bulk_aoi_whitelist(self):
        source = LUA.read_text(encoding="utf-8-sig")
        observed = "function M._sharedRuntimeOwnership.PumpVerified(now)"
        self.assertIn(observed, source)
        changed = source.replace(observed, observed + "\n    pump_native_group(now)", 1)
        with self.assertRaisesRegex(BoundaryError, "Lua probe dispatch changed"):
            audit_other_source_transports(probe=changed)

    def test_production_factory_enablement_requires_new_evidence(self):
        source = PROVIDER.read_text(encoding="utf-8-sig")
        observed = "createSourceForAcceptedRun = null"
        self.assertIn(observed, source)
        changed = source.replace(observed, "createSourceForAcceptedRun = ProductionNativeGroupSource", 1)
        with self.assertRaisesRegex(BoundaryError, "production Map317"):
            audit_other_source_transports(provider=changed)

    def test_new_production_group_source_instantiation_is_not_missed(self):
        other_cs = {"NewNativeGroupRoute.cs":
                    "return new Map014NativeSharedReceiptSource(ingress, session, run, generation);"}
        with self.assertRaisesRegex(BoundaryError, "production native group ingress or wiring"):
            audit_other_source_transports(desktop_sources=other_cs)

    def test_new_production_ingress_implementation_is_not_missed(self):
        other_cs = {"ReceivedNativeGroupIngress.cs":
                    "internal sealed class RealSource : IMap014ReceivedNativeSharedGroupIngress { }"}
        with self.assertRaisesRegex(BoundaryError, "production native group ingress or wiring"):
            audit_other_source_transports(desktop_sources=other_cs)


class ActualPipeResultCorrelationTests(unittest.TestCase):
    """The REAL C# Result correlation boundary; modeled receipt is NOT a game packet."""

    def test_source_result_is_route_plus_payload_id_not_outer_envelope(self):
        contract = audit_pipe_result_correlation_source()
        self.assertEqual(contract["pendingLookup"], "result.payload.id")
        self.assertEqual(contract["ownerBinding"], "authenticated.pipe.profileId+instanceId")
        self.assertFalse(contract["outerEnvelopeRequestIdCorrelated"])
        self.assertFalse(contract["outerEnvelopeProfileAndInstanceCorrelated"])
        self.assertFalse(contract["nativeGroupReceiptAvailable"])

    def test_pinned_get_status_reply_correlates_but_is_not_native_proof(self):
        pending = {"cmd_7": ("profile-a", "instance-a")}
        result = {"version": 1, "type": "result", "requestId": "cmd_7",
                  "profileId": "profile-a", "instanceId": "instance-a",
                  "timestamp": 1700000000000,
                  "payload": {"id": "cmd_7", "ok": True, "result": {"gameReady": True}}}
        self.assertTrue(correlate_pipe_status_result_only(
            pending, ("profile-a", "instance-a"), result)["pendingCallCorrelated"])
        self.assertEqual(pending, {})

    def test_outer_envelope_mismatch_is_not_checked_by_existing_host_registry(self):
        pending = {"cmd_7": ("profile-a", "instance-a")}
        result = {"version": 1, "type": "result", "requestId": "different",
                  "profileId": "other-profile", "instanceId": "other-instance",
                  "timestamp": 100,
                  "payload": {"id": "cmd_7", "ok": True, "result": {}}}
        admitted = correlate_pipe_status_result_only(
            pending, ("profile-a", "instance-a"), result)
        self.assertTrue(admitted["pendingCallCorrelated"])
        self.assertFalse(admitted["outerEnvelopeMatches"])
        self.assertFalse(admitted["nativeGroupReceiptProven"])

    def test_foreign_session_unknown_id_and_replay_do_not_correlate(self):
        good = {"version": 1, "type": "result", "requestId": "cmd_7",
                "profileId": "profile-a", "instanceId": "instance-a",
                "timestamp": 1700000000000,
                "payload": {"id": "cmd_7", "ok": True, "result": {}}}
        for authenticated_route in (("profile-z", "instance-a"),
                                    ("profile-a", "instance-z")):
            pending = {"cmd_7": ("profile-a", "instance-a")}
            self.assertFalse(correlate_pipe_status_result_only(
                pending, authenticated_route, good)["pendingCallCorrelated"])
            self.assertEqual(len(pending), 1)
        pending = {"cmd_7": ("profile-a", "instance-a")}
        unknown = deepcopy(good)
        unknown["payload"]["id"] = "cmd_unknown"
        self.assertFalse(correlate_pipe_status_result_only(
            pending, ("profile-a", "instance-a"), unknown)["pendingCallCorrelated"])
        self.assertTrue(correlate_pipe_status_result_only(
            pending, ("profile-a", "instance-a"), good)["pendingCallCorrelated"])
        self.assertFalse(correlate_pipe_status_result_only(
            pending, ("profile-a", "instance-a"), good)["pendingCallCorrelated"])

    def test_route_owner_reject_source_drift_requires_requalification(self):
        from audit_group_transport import PIPE_CALL_REGISTRY
        source = PIPE_CALL_REGISTRY.read_text(encoding="utf-8-sig")
        original = 'StringComparison.Ordinal) ||\n                !string.Equals('
        self.assertIn(original, source)
        drifted = source.replace(original,
                                 'StringComparison.Ordinal) &&\n                !string.Equals(', 1)
        with self.assertRaisesRegex(BoundaryError, "pipe result route owner guard"):
            audit_pipe_result_correlation_source(registry=drifted)

    def test_payload_id_lookup_drift_requires_requalification(self):
        from audit_group_transport import PIPE_CALL_REGISTRY
        source = PIPE_CALL_REGISTRY.read_text(encoding="utf-8-sig")
        original = "pending.TryGetValue(result.Id, out entry)"
        self.assertIn(original, source)
        drifted = source.replace(original,
                                 "pending.TryGetValue(result.RequestId, out entry)", 1)
        with self.assertRaisesRegex(BoundaryError, "pipe result pending lookup"):
            audit_pipe_result_correlation_source(registry=drifted)

    def test_lua_result_payload_id_drift_requires_requalification(self):
        source = OVERVIEW.read_text(encoding="utf-8-sig")
        marker = "local function write_pipe_result(control, request_id, result)"
        self.assertIn(marker, source)
        changed = source.replace("id = request_id,\n            ok = true",
                                 "id = 'fabricated',\n            ok = true", 1)
        with self.assertRaisesRegex(BoundaryError, "Lua pipe Result writer"):
            audit_pipe_result_correlation_source(overview=changed)

    def test_host_parser_payload_id_origin_drift_requires_requalification(self):
        from audit_group_transport import PIPE_PROTOCOL
        source = PIPE_PROTOCOL.read_text(encoding="utf-8-sig")
        original = 'string id = RequireString(body, "id");'
        self.assertIn(original, source)
        changed = source.replace(original,
                                 'string id = RequireString(body, "requestId");', 1)
        with self.assertRaisesRegex(BoundaryError, "pipe Result parser body id"):
            audit_pipe_result_correlation_source(protocol=changed)

    def test_host_session_route_substitution_requires_requalification(self):
        from audit_group_transport import PIPE_SESSION_TRANSPORT
        source = PIPE_SESSION_TRANSPORT.read_text(encoding="utf-8-sig")
        original = 'connection.ProfileId,\n                            connection.InstanceId,'
        self.assertIn(original, source)
        changed = source.replace(original,
                                 'result.ProfileId,\n                            connection.InstanceId,', 1)
        with self.assertRaisesRegex(BoundaryError, "pipe Result authenticated session routing"):
            audit_pipe_result_correlation_source(transport=changed)

    def test_pending_result_cannot_be_completed_twice_after_removal_change(self):
        from audit_group_transport import PIPE_CALL_REGISTRY
        source = PIPE_CALL_REGISTRY.read_text(encoding="utf-8-sig")
        original = 'pending.Remove(result.Id);\n            entry.Timeout.Cancel();'
        self.assertIn(original, source)
        changed = source.replace(original, 'entry.Timeout.Cancel();', 1)
        with self.assertRaisesRegex(BoundaryError, "pipe result pending lookup/removal"):
            audit_pipe_result_correlation_source(registry=changed)


if __name__ == "__main__":
    unittest.main()
