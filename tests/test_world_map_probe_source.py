from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_snapshot_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_snapshot_probe.py"


class WorldMapProbeSourceTests(unittest.TestCase):
    def test_probe_uses_recovered_reflection_enumeration_without_action_paths(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('GetField(name, REFLECTION_FLAGS)', source)
        self.assertIn('reflected_collection(point_manager, "_pointInfos")', source)
        self.assertIn('call(collection, "GetEnumerator")', source)
        self.assertIn('call(enumerator, "MoveNext")', source)
        self.assertIn('safe_get(enumerator, "Current")', source)
        self.assertNotIn("SendMessage", source)
        self.assertNotIn("SendAoiRequest", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)

    def test_probe_fails_closed_on_partial_or_duplicate_enumeration(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("enumeration did not match _pointInfos.Count", source)
        self.assertIn("duplicate world/server/point identity", source)
        self.assertIn("MAX_POINTS = 50000", source)

    def test_loader_probe_has_no_explicit_world_scan_request_path(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("world-map-snapshot.json", source)
        self.assertIn("FindObjectsOfTypeAll", source)
        self.assertNotIn("SendAoiRequest", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)
        self.assertNotIn('SendMessage"', source)

    def test_loader_uses_one_bounded_current_sceneutils_world_transition(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('static_call(resources, "FindObjectsOfTypeAll", reflected_type)', source)
        self.assertIn(
            "gameframework/ui/uicontainer/uiresource/uimain/safearea/bottomlayer/worldbtn/btn",
            source,
        )
        self.assertIn('string.sub(lower, -13) == "/worldbtn/btn"', source)
        self.assertIn('rawget(_G, "SceneUtils")', source)
        self.assertIn('safe_get(scene_utils, "ChangeToWorld")', source)
        self.assertIn('transition_method = "SceneUtils.ChangeToWorld"', source)
        self.assertIn("pcall(change_to_world)", source)
        self.assertNotIn("button.onClick:Invoke()", source)
        self.assertIn("if transition_requested then", source)
        self.assertIn("transition_invocations = 1", source)
        self.assertIn('transition_state = "waiting_for_world"', source)

    def test_loader_waits_for_loaded_world_point_stability_before_capture(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("STABLE_SECONDS = 2", source)
        self.assertIn('GetField("_pointInfos", 52)', source)
        self.assertIn("world_is_stable(manager, now)", source)
        self.assertIn('transition_state = "world_stable"', source)

    def test_loader_requires_authoritative_current_scene_id_before_capture(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('safe_get(scene_manager, "CurrSceneID")', source)
        self.assertIn('rawget(_G, "SceneManagerSceneID")', source)
        self.assertIn('safe_get(scene_ids, "City")', source)
        self.assertIn('safe_get(scene_ids, "World")', source)
        self.assertIn('scene_state = "world"', source)
        self.assertIn('scene_state = "city"', source)
        self.assertIn('if active_scene_state ~= "world" then', source)
        self.assertIn('if active_scene_state == "city" then', source)
        self.assertIn('transition_state = "world_scene_id_confirmed"', source)
        self.assertIn('"current_scene_id":', source)
        self.assertIn('"world_scene_id":', source)

    def test_loader_records_current_aoi_geometry_without_requesting_blocks(self):
        source = PREPARER.read_text(encoding="utf-8")
        for field in (
            '"_lwAoiBlockSize"',
            '"_lwAoiBlockCount"',
            '"LOD"',
            '"svLod"',
            '"reqPos"',
            '"_lastViewLBBlock"',
            '"_lastViewRTBlock"',
            '"once_max_request_count"',
            '"firstTimeReqAoi"',
            '"battleFieldFirst"',
        ):
            self.assertIn(field, source)
        self.assertIn("update_aoi_diagnostics(manager)", source)
        self.assertIn('"aoi_block_size":', source)
        self.assertIn('"aoi_block_count":', source)

    def test_loader_uses_recovered_active_world_scene_resolution(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("local function is_runtime_world_scene(candidate)", source)
        self.assertIn('name == "WorldScene"', source)
        self.assertIn("if is_runtime_world_scene(world) then", source)
        self.assertIn('return world, "CS.SceneManager.World"', source)
        self.assertIn('return world, "SceneManager.World"', source)
        self.assertIn('return candidate, "Resources.FindObjectsOfTypeAll"', source)
        self.assertIn('safe_get(candidate, "isActiveAndEnabled") == true', source)
        self.assertIn('"world_source":', source)

    def test_loader_uses_recovered_manager_candidate_set_fail_closed_on_point_infos(self):
        source = PREPARER.read_text(encoding="utf-8")
        for name in (
            '"CurScene"',
            '"SceneInterface"',
            '"WorldPointManager"',
            '"WorldScene"',
            '"SceneManager"',
            '"GameEntry"',
        ):
            self.assertIn(name, source)
        self.assertIn('reflected_private_collection(value, "_pointInfos")', source)
        self.assertIn('"manager_source":', source)
        self.assertNotIn('pcall(require, "WorldPointManager")', source)
        self.assertNotIn('pcall(require, "WorldScene")', source)
        self.assertNotIn('pcall(require, "SceneInterface")', source)

    def test_loader_records_bounded_read_only_manager_diagnostics(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn("reflected_type:GetMembers(52)", source)
        self.assertIn("#names >= 96", source)
        self.assertIn("now - manager_diagnostics_at < 5", source)
        self.assertIn('"world_type":', source)
        self.assertIn('"world_members":', source)
        self.assertIn('"manager_candidates":', source)
        self.assertIn('call(world, "GetAllMainBaseList")', source)
        self.assertIn('call(world, "GetMainByScreen")', source)
        self.assertIn('"fallback_all_main_count":', source)
        self.assertIn('"fallback_screen_count":', source)

    def test_loader_records_original_scene_root_visibility_signals(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('lower_path == "city"', source)
        self.assertIn('string.find(lower_path, "city/", 1, true) == 1', source)
        self.assertIn('string.lower(root_name) == "world" or unity_kind(root_name) ~= nil', source)
        self.assertIn('"city_visible":', source)
        self.assertIn('"world_visible":', source)
        self.assertIn('"scene_scanned":', source)

    def test_loader_registers_recovered_repeating_pump_paths(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('rawget(_G, "UpdateManager")', source)
        self.assertIn('safe_get(instance, "AddUpdate")', source)
        self.assertIn('safe_get(timer, "RegisterTimerRepeat")', source)
        self.assertIn("function M.Register()", source)
        self.assertIn("M.Register()", source)


if __name__ == "__main__":
    unittest.main()
