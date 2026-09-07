from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_one_block_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_one_block_probe.py"
RUNNER = ROOT / "tools" / "run_world_map_one_block_probe.py"
BRIDGE = ROOT / "tools" / "world_block_sender.cs"


class WorldMapOneBlockProbeSourceTests(unittest.TestCase):
    def test_probe_is_one_request_without_camera_or_view_sweep(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertEqual(
            source.count("local invoked, invoke_error = send_aoi_with_bridge(point_manager, request)"),
            1,
        )
        self.assertIn("if request_sent_count ~= 0", source)
        self.assertIn("request_sent_count = 1", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)
        self.assertNotIn("GoToUtil", source)
        self.assertNotIn("Camera =", source)

    def test_probe_chooses_adjacent_block_from_live_bounds(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('reflected_value(point_manager, "_lastViewLBBlock")', source)
        self.assertIn('reflected_value(point_manager, "_lastViewRTBlock")', source)
        self.assertIn("block_index = candidate.y * block_count + candidate.x", source)
        self.assertIn('side = "right"', source)
        self.assertIn("outside and candidate.x >= 0", source)

    def test_probe_uses_recovered_minimum_5_by_4_transport_viewport(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("pad_axis(block.x, block.x, 5", source)
        self.assertIn("pad_axis(block.y, block.y, 4", source)
        self.assertIn("block_indexes[#block_indexes + 1]", source)
        self.assertIn("transport_index_count", source)

    def test_probe_requires_authoritative_scene_and_normal_world_size(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('safe_get(manager, "CurrSceneID")', source)
        self.assertIn('name ~= "WorldScene"', source)
        self.assertIn("if size ~= 1000", source)
        self.assertIn('rawget(_G, "SceneUtils")', source)
        self.assertIn('safe_get(scene_utils, "ChangeToWorld")', source)

    def test_probe_registers_the_same_proven_recurring_update_paths(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("function M.Register()", source)
        self.assertIn('safe_get(instance, "AddUpdate")', source)
        self.assertIn('safe_get(timer, "RegisterTimerRepeat")', source)
        self.assertIn("M.Register()", source)

    def test_probe_correlates_response_bounds_and_identity(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('safe_get(proxy, "DispatchResponse1")', source)
        self.assertIn('safe_get(proxy, "DispatchResponse2")', source)
        self.assertIn('call(message, "GetMsgId")', source)
        self.assertIn('call(value, "GetSFSArray", "serverPointArr")', source)
        self.assertIn('call(target, "ContainsKey", key)', source)
        self.assertIn('call(value, "GetKeys")', source)
        self.assertIn('visit_array(array, source .. ".array[" .. key_text .. "]")', source)
        self.assertIn("local normalized = normalize_response_envelope(world, envelope)", source)
        self.assertIn("normalized.server_id == nil", source)
        self.assertIn("normalized.world_id == nil", source)
        self.assertIn("request.block_x >= normalized.block_left", source)
        self.assertIn("request.block_x <= normalized.block_right", source)
        self.assertIn("request.block_y >= normalized.block_bottom", source)
        self.assertIn("request.block_y <= normalized.block_top", source)

    def test_probe_restores_owned_hook_and_manager_flag(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("same_delegate(current, hook.installed)", source)
        self.assertIn("proxy[hook.field] = hook.original", source)
        self.assertIn('point_manager, "isRecvViewPoints", boxed_bool(manager_flag_original == true)', source)
        self.assertIn("manager_flag_original", source)

    def test_candidate_round_trips_encrypted_payload(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)
        self.assertIn("changed_installed_files", source)

    def test_runner_restores_exact_pre_run_hashes_in_finally(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)
        self.assertIn('for key in ("data", "metadata", "version", "baseutils")', source)

    def test_managed_bridge_converts_indexes_to_exact_send_parameter(self):
        source = BRIDGE.read_text(encoding="utf-8")
        self.assertIn("Action<object[]> SendAoi", source)
        self.assertIn('method.Name != "SendAoiRequest"', source)
        self.assertIn("targetType.GetGenericTypeDefinition() == typeof(List<>)", source)
        self.assertIn("result.Add(Convert.ToInt32(item))", source)


if __name__ == "__main__":
    unittest.main()
