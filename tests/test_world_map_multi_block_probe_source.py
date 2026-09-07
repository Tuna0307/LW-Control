from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_multi_block_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_multi_block_probe.py"
RUNNER = ROOT / "tools" / "run_world_map_multi_block_probe.py"
BRIDGE = ROOT / "tools" / "world_block_sender.cs"


class WorldMapMultiBlockProbeSourceTests(unittest.TestCase):
    def test_probe_sends_one_request_without_camera_or_retry_path(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertEqual(
            source.count("local invoked, invoke_error = send_aoi_with_bridge(point_manager, request)"),
            1,
        )
        self.assertIn("if request_sent_count ~= 0", source)
        self.assertIn("request_sent_count = 1", source)
        self.assertIn("retry_count = 0", source)
        self.assertIn("camera_move_count = 0", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)
        self.assertNotIn("GoToUtil", source)

    def test_probe_selects_three_adjacent_logical_blocks(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('reflected_value(point_manager, "_lastViewLBBlock")', source)
        self.assertIn('reflected_value(point_manager, "_lastViewRTBlock")', source)
        self.assertIn("local target_x = rt.x + 1", source)
        self.assertIn("for y = start_y, start_y + 2 do", source)
        self.assertIn("block_index = y * block_count + target_x", source)
        self.assertIn("requested_block_count = #target.blocks", source)

    def test_probe_pads_logical_coverage_to_recovered_minimum_transport(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("pad_axis(coverage.left, coverage.right, 5", source)
        self.assertIn("pad_axis(coverage.bottom, coverage.top, 4", source)
        self.assertIn("block_indexes[#block_indexes + 1]", source)
        self.assertIn("if #block_indexes < 1 or #block_indexes > 160", source)

    def test_probe_accumulates_coverage_until_every_logical_block_is_seen(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function initialize_requested_coverage", source)
        self.assertIn("local function mark_response_coverage", source)
        self.assertIn("requested_block_keys[key] and not covered_block_keys[key]", source)
        self.assertIn("covered_block_count = covered_block_count + 1", source)
        self.assertIn("covered_block_count == requested_block_count", source)
        self.assertIn('phase = "matched_response"', source)

    def test_probe_correlates_nested_response_envelopes_by_identity_and_overlap(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('safe_get(proxy, "DispatchResponse1")', source)
        self.assertIn('safe_get(proxy, "DispatchResponse2")', source)
        self.assertIn('call(value, "GetSFSArray", "serverPointArr")', source)
        self.assertIn("normalized.server_id == nil", source)
        self.assertIn("normalized.world_id == nil", source)
        self.assertIn("normalized.block_right >= requested_coverage.left", source)
        self.assertIn("normalized.block_left <= requested_coverage.right", source)
        self.assertIn("matched_responses[#matched_responses + 1]", source)

    def test_probe_restores_owned_hook_and_manager_flag(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("same_delegate(current, hook.installed)", source)
        self.assertIn("proxy[hook.field] = hook.original", source)
        self.assertIn('point_manager, "isRecvViewPoints", boxed_bool(manager_flag_original == true)', source)

    def test_candidate_round_trips_the_multi_block_probe(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('with_name("current_world_map_multi_block_probe.lua")', source)
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)

    def test_runner_is_bounded_and_restores_exact_pre_run_hashes(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn('"maximum_probe_requests": 1', source)
        self.assertIn('"requested_logical_blocks": 3', source)
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)
        self.assertIn('for key in ("data", "metadata", "version", "baseutils")', source)

    def test_managed_bridge_remains_exact_send_parameter_adapter(self):
        source = BRIDGE.read_text(encoding="utf-8")
        self.assertIn("Action<object[]> SendAoi", source)
        self.assertIn('method.Name != "SendAoiRequest"', source)
        self.assertIn("targetType.GetGenericTypeDefinition() == typeof(List<>)", source)


if __name__ == "__main__":
    unittest.main()
