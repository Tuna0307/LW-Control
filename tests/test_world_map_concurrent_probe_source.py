from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_concurrent_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_concurrent_probe.py"
RUNNER = ROOT / "tools" / "run_world_map_concurrent_probe.py"
BRIDGE = ROOT / "tools" / "world_block_sender.cs"


class WorldMapConcurrentProbeSourceTests(unittest.TestCase):
    def test_probe_selects_bounded_nineteen_by_nineteen_window(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("for y = start_y, start_y + 18 do", source)
        self.assertIn("for x = start_x, start_x + 18 do", source)
        self.assertIn("#target.blocks ~= 361", source)
        self.assertIn("requested_block_count ~= 361", source)

    def test_recovered_planner_split_is_152_152_57(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("for batch_width = 1, math.min(width, 160) do", source)
        self.assertIn("math.floor(160 / batch_width)", source)
        self.assertIn("#batches ~= 3 or batch_size ~= 152", source)
        self.assertIn("output[1].requested_block_count ~= 152", source)
        self.assertIn("output[2].requested_block_count ~= 152", source)
        self.assertIn("output[3].requested_block_count ~= 57", source)

    def test_probe_hard_caps_three_sends_without_retry_or_camera(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("if request_sent_count >= 3", source)
        self.assertIn("request_sent_count ~= 3", source)
        self.assertIn("retry_count = 0", source)
        self.assertIn("camera_move_count = 0", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)
        self.assertNotIn("GoToUtil", source)

    def test_first_batch_must_complete_before_concurrent_wave(self):
        source = SOURCE.read_text(encoding="utf-8")
        matched = source.index('if phase == "batch_response_matched" then')
        recorded = source.index("record_completed_batch(now)", matched)
        launched = source.index("launch_concurrent_wave(now, point_manager)", recorded)
        self.assertLess(matched, recorded)
        self.assertLess(recorded, launched)
        self.assertIn(
            '"concurrent wave requires one completed serial capability batch"', source
        )

    def test_concurrent_wave_contains_exactly_two_remaining_requests(self):
        source = SOURCE.read_text(encoding="utf-8")
        start = source.index("local function launch_concurrent_wave")
        end = source.index("local function start_concurrent_scan", start)
        body = source[start:end]
        self.assertIn("for index = 2, 3 do", body)
        self.assertIn("concurrent_entries = {", body)
        self.assertIn("[2] = new_concurrent_entry(batch_requests[2])", body)
        self.assertIn("[3] = new_concurrent_entry(batch_requests[3])", body)
        self.assertNotIn("reflected_set_value", body)

    def test_concurrent_responses_match_unique_exact_request_bounds(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function match_concurrent_entry", source)
        self.assertIn(
            "tonumber(normalized.left_bottom) == tonumber(value.left_bottom_index)",
            source,
        )
        self.assertIn(
            "tonumber(normalized.right_top) == tonumber(value.right_top_index)",
            source,
        )
        self.assertIn('"concurrent_response_ambiguous"', source)

    def test_completion_requires_both_sends_before_first_response(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("concurrent_peak_inflight ~= 2", source)
        self.assertIn("concurrent_response_before_wave_complete == true", source)
        self.assertIn("item.completed_event >= concurrent_first_response_event", source)
        self.assertIn("concurrent_first_response_event = response_event", source)
        self.assertIn("concurrent_wave_launch_completed_event = next_event()", source)

    def test_each_concurrent_batch_has_independent_logical_coverage(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function new_concurrent_entry", source)
        self.assertIn("entry.covered_keys[key] = true", source)
        self.assertIn("entry.covered_count = entry.covered_count + 1", source)
        self.assertIn(
            "entry.complete = entry.expected_count > 0 and entry.covered_count == entry.expected_count",
            source,
        )

    def test_probe_restores_owned_hook_and_manager_flag(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("same_delegate(current, hook.installed)", source)
        self.assertIn("proxy[hook.field] = hook.original", source)
        self.assertIn(
            'point_manager, "isRecvViewPoints", boxed_bool(manager_flag_original == true)',
            source,
        )

    def test_candidate_round_trips_concurrent_probe(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('with_name("current_world_map_concurrent_probe.lua")', source)
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)

    def test_runner_declares_bounded_three_request_contract_and_exact_restore(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn('"maximum_probe_requests": 3', source)
        self.assertIn('"requested_logical_blocks": 361', source)
        self.assertIn('"expected_batch_logical_blocks": [152, 152, 57]', source)
        self.assertIn('"maximum_concurrent_requests": 2', source)
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)

    def test_managed_bridge_remains_exact_send_parameter_adapter(self):
        source = BRIDGE.read_text(encoding="utf-8")
        self.assertIn("Action<object[]> SendAoi", source)
        self.assertIn('method.Name != "SendAoiRequest"', source)
        self.assertIn("Action<object[]> SendRectMarch", source)
        self.assertIn('gameAssembly.GetType("WorldGetRectMarchInfosMessage", true)', source)
        self.assertIn('gameAssembly.GetType("WorldGetRectMarchInfosMessage+Request", true)', source)
        self.assertIn('parameters[0].ParameterType == typeof(object[])', source)
        self.assertIn('send.Invoke(message, new object[] { new object[] { request } })', source)


if __name__ == "__main__":
    unittest.main()
