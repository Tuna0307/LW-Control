from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_full_scan_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_full_scan_probe.py"
RUNNER = ROOT / "tools" / "run_world_map_full_scan_probe.py"
PERSISTENT_RUNNER = ROOT / "tools" / "run_persistent_world_scan_acceptance.py"


class WorldMapFullScanProbeSourceTests(unittest.TestCase):
    def test_probe_requires_recovered_full_grid_plan(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("#target.blocks ~= 10000", source)
        self.assertIn("#batches ~= 65", source)
        self.assertIn("full_batches ~= 60 or narrow_batches ~= 5", source)
        self.assertIn("requested_block_count ~= 10000", source)

    def test_probe_uses_recovered_serial_then_eight_way_scheduler(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local MAX_CONCURRENCY = 8", source)
        self.assertIn('"serial send helper is reserved for capability batch 1"', source)
        serial = source.index("record_completed_batch(now)")
        scheduler = source.index("launch_full_scan_batches(now, point_manager)", serial)
        self.assertLess(serial, scheduler)
        self.assertIn("full_scan_inflight_count() < MAX_CONCURRENCY", source)
        self.assertIn("launched < MAX_CONCURRENCY", source)

    def test_each_full_scan_response_matches_one_exact_request(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function match_full_scan_entry", source)
        self.assertIn(
            "tonumber(normalized.left_bottom) == tonumber(value.left_bottom_index)",
            source,
        )
        self.assertIn(
            "tonumber(normalized.right_top) == tonumber(value.right_top_index)",
            source,
        )
        self.assertIn('"full_scan_response_ambiguous"', source)

    def test_probe_accumulates_post_response_point_records_with_global_dedupe(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function accumulate_scan_records", source)
        self.assertIn("scan_record_keys[key] ~= true", source)
        self.assertIn("scan_records[#scan_records + 1] = point", source)
        self.assertIn("accumulate_scan_records(world, point_manager)", source)
        wrapper = source.index("local wrapper1 = function(event)")
        original = source.index("invoke_dispatch_delegate(original1, event)", wrapper)
        observe = source.index('pcall(observe, command_value, payload, "DispatchResponse1")', original)
        self.assertLess(original, observe)

    def test_probe_fails_closed_per_batch_and_uses_recovered_official_monster_views(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function timed_out_full_scan_sequence", source)
        self.assertIn("now - entry.sent_at >= CFG.RESPONSE_TIMEOUT_SECONDS", source)
        self.assertIn("retry_count = 0", source)
        self.assertIn("local EXPECTED_MONSTER_CAMERA_VIEWS = 500", source)
        self.assertIn("view_width ~= 5 or view_height ~= 4", source)
        self.assertIn("monster_camera_move_count = monster_camera_move_count + 1", source)
        self.assertIn('call(point_manager, "StartViewRequest")', source)
        self.assertIn('call(point_manager, "UpdateViewRequest", true)', source)
        self.assertIn("monster_official_request_count = monster_official_request_count + 1", source)
        self.assertIn("MONSTER_VIEW_RESPONSE_TIMEOUT_SECONDS = 4", source)
        self.assertNotIn("GoToUtil", source)

    def test_probe_stays_below_current_lua_top_level_local_limit(self):
        source = SOURCE.read_text(encoding="utf-8")
        top_level_locals = sum(1 for line in source.splitlines() if line.startswith("local "))
        self.assertLessEqual(top_level_locals, 190)
        self.assertIn("local CFG = {", source)
        self.assertIn("local restore_state = {", source)

    def test_monster_phase_observes_current_aoi_march_push_then_restores(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('phase == "monster_waiting_response"', source)
        self.assertIn("monster_view_response_received = true", source)
        self.assertIn("if #monster_views > 0 then\n            return\n        end", source)
        self.assertIn('safe_get(cs, "PushWorldMarchWorldGet")', source)
        self.assertIn('call(push, "GetMsgId")', source)
        self.assertIn('protocol ~= "push.world.march.world.get.new"', source)
        self.assertIn('command_text == tostring(monster_march_protocol)', source)
        self.assertIn('monster_current_diagnostic.march_response_route = result.route', source)
        self.assertIn('call(payload, "GetSFSArray", "serverMarchArr")', source)
        self.assertIn('call(server_value, "GetSFSArray", "marchInfos")', source)
        self.assertIn('response_number(info, "startPos")', source)
        self.assertIn('response_number(info, "targetPos")', source)
        self.assertIn('response_number(info, "mainPointId")', source)
        self.assertIn('phase = "monster_waiting_march_push"', source)
        self.assertIn("monster_march_response_count = monster_march_response_count + 1", source)
        self.assertNotIn("WorldGetRectMarchInfosMessage", source)
        self.assertNotIn('safe_get(msg_defines, "WorldGetMarchInfos")', source)
        self.assertNotIn("monster_march_request_count = monster_march_request_count + 1", source)
        march_wait = source.index('if phase == "monster_waiting_march_push" then')
        raw_capture = source.index("monster_current_diagnostic.raw_march_records", march_wait)
        self.assertLess(march_wait, raw_capture)
        self.assertNotIn("capture_world_monsters(world, scan_request)", source[march_wait:])
        self.assertIn("write_monster_diagnostics(now", source)
        self.assertIn("restore_monster_camera(", source)
        self.assertIn("distance <= 3", source)
        self.assertIn("zoom_delta <= 0.05", source)
        self.assertIn("restore_monster_march_hooks()", source)
        self.assertIn("restore_monster_world_flag(world)", source)
        self.assertIn("manager_flag_restored = flag_restored", source)
        self.assertIn("world_response_flag_restored = world_flag_restored", source)

    def test_completion_requires_all_10000_blocks_and_all_65_batches(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("requested_block_count ~= 10000", source)
        self.assertIn("request_sent_count ~= 65", source)
        self.assertIn("completed_batch_count ~= 65", source)
        self.assertIn("next_batch_index ~= 66", source)
        self.assertIn("full_scan_peak_inflight > MAX_CONCURRENCY", source)
        self.assertIn("#full_scan_send_events ~= 64", source)

    def test_candidate_round_trips_full_scan_probe(self):
        source = PREPARER.read_text(encoding="utf-8")
        self.assertIn('with_name("current_world_map_full_scan_probe.lua")', source)
        self.assertIn("encode_lenc_bytes", source)
        self.assertIn("decode_lenc_bytes", source)
        self.assertIn("mapped[ORIGINAL_LUA_ENTRY] = bytes(official)", source)
        self.assertIn("local probe = (function()", source)
        self.assertIn("return prefix + probe_source + suffix", source)
        self.assertIn("embedded full-scan probe did not return a table", source)
        self.assertIn("refusing to append an unproven module name", source)
        self.assertNotIn("mapped[PROBE_ENTRY] = probe", source)
        self.assertNotIn('pcall(require, "LWControlProbe")', source)
        self.assertNotIn('pcall(require, "LWControlDailyTaskRuntime")', source)

    def test_runner_declares_full_world_contract_and_exact_restore(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn('"maximum_direct_block_requests": 65', source)
        self.assertIn('"maximum_official_view_requests": 500', source)
        self.assertIn('"maximum_managed_rect_march_requests": 0', source)
        self.assertIn('"maximum_world_detail_requests": 50000', source)

        self.assertIn('"world_detail_request_batch_size": 48', source)
        self.assertIn('"world_detail_max_retries": 1', source)
        self.assertIn('"maximum_total_network_requests": 50565', source)
        self.assertIn('"requested_logical_blocks": 10000', source)
        self.assertIn('"expected_batch_count": 65', source)
        self.assertIn('"maximum_concurrent_requests": 8', source)
        self.assertIn('probe_result.get("monster_discovery_source") != "PushWorldMarchWorldGet.serverMarchArr.marchInfos"', source)
        self.assertIn('probe_result.get("monster_camera_view_count") != 500', source)
        self.assertIn('probe_result.get("monster_official_request_count") != 500', source)
        self.assertIn('probe_result.get("monster_march_request_count") != 0', source)
        self.assertIn('probe_result.get("monster_march_response_count") != 500', source)
        self.assertIn('probe_result.get("monster_march_foreign_send_count") != 0', source)
        self.assertIn('probe_result.get("monster_march_hook_restored") is not True', source)
        self.assertIn('monster_view_diagnostics', source)
        self.assertIn('passive AOI monster scan did not retain all 500 view diagnostics', source)
        self.assertIn('probe_result.get("camera_restored") is not True', source)
        self.assertIn('probe_result.get("world_response_flag_restored") is not True', source)
        self.assertIn('record.get("source") == "WorldPointManager._pointInfos"', source)
        self.assertIn('required_kinds = {"player_base", "resource_point", "alliance_building", "monster"}', source)
        self.assertIn('probe_result.get("covered_block_count") != 10000', source)
        self.assertIn('observed_result.get("state") == "proven"', source)
        self.assertIn('status.get("state") != "captured"', source)
        self.assertNotIn('if runtime_paths["result"].is_file():\n                break', source)
        self.assertIn('result["status_summary"] = {', source)
        self.assertIn('result["result_summary"] = {', source)
        self.assertIn('live_result_path = candidate_dir / "live-result.json"', source)
        self.assertIn('monster_diagnostics_path = candidate_dir / "live-monster-diagnostics.json"', source)
        self.assertIn("finally:", source)
        self.assertIn("_restore_scan_files_while_running(paths, backup)", source)
        self.assertIn('result["game_left_running"] = live_restore_ok and game_is_running()', source)
        self.assertIn('result["restore_mode"] = "live_process_preserved" if live_restore_ok else "closed_fallback"', source)
        self.assertIn("if not live_restore_ok:", source)
        self.assertIn("_stop_game()", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)

    def test_persistent_acceptance_stages_verified_world_block_sender(self):
        source = PERSISTENT_RUNNER.read_text(encoding="utf-8")
        self.assertIn("build_world_block_sender", source)
        self.assertIn('root = discover_paths()["runtime"]', source)
        self.assertIn('root / "WorldBlockSender.dll"', source)
        self.assertIn("does not match the current verified source", source)
        self.assertIn("_coff_timestamp_offset", source)
        self.assertIn("EXPECTED_MVID_SIZE = 16", source)
        self.assertIn('EXPECTED_VERSION = "lwcontrol-world-full-scan-probe-9"', source)
        self.assertIn("HEARTBEAT_MAX_AGE_SECONDS = 15", source)
        self.assertIn("resourceTargetLimit", source)
        self.assertIn('"resource_detail_enrichment": result.get("resource_detail_enrichment")', source)
        self.assertIn('"world_block_sender": sender', source)
        self.assertIn("before_hashes = _protected_hashes()", source)

    def test_completed_probe_keeps_verified_coordinate_focus_route(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn('PATHS.focus_command = root .. [[\\world-map-focus-command.txt]]', source)
        self.assertIn('PATHS.focus_result = root .. [[\\world-map-focus-result.json]]', source)
        self.assertIn("function M.ProcessFocus(now)", source)
        self.assertIn('values.schema ~= "1"', source)
        self.assertIn('move_official_monster_view(world, pending)', source)
        self.assertIn('view_distance(observed, pending) <= 3', source)
        self.assertIn('state = "completed"', source)
        self.assertIn("if completed then\n        M.ProcessFocus(now)\n        return true\n    end", source)

    def test_status_payload_stays_compact_during_full_scan(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function public_scan_summary", source)
        status_start = source.index("local function status_payload")
        status_end = source.index("local function cleanup", status_start)
        status = source[status_start:status_end]
        self.assertIn("scan = public_scan_summary(scan_request)", status)
        self.assertNotIn("scan = public_scan(scan_request)", status)
        self.assertNotIn("requests = public_requests(batch_requests)", status)

    def test_rich_records_use_current_payloads_and_recovered_original_enrichers(self):
        source = SOURCE.read_text(encoding="utf-8")
        # Current-build generated WorldPointInfo payloads are the preferred source.
        self.assertIn('object_field(info, { "buildInfo", "BuildInfo" })', source)
        self.assertIn('object_field(info, { "collectResourceInfo", "CollectResourceInfo" })', source)
        self.assertIn('object_field(info, { "resourceInfo", "ResourceInfo" })', source)
        self.assertIn('"WorldPointInfo.BuildInfo.protectEndTime"', source)
        # Original v108 classifier/getters are retained as recovered behavior.
        self.assertIn('if code == 6 then return "player_base" end', source)
        self.assertIn('if code == 4 or code == 5 or code == 22 or code == 1003 then return "monster" end', source)
        self.assertIn('if code == 1 or code == 7 or code == 26 then return "resource_point" end', source)
        self.assertIn('call(info, "GetResLevel")', source)
        self.assertIn('call(info, "GetResType")', source)
        self.assertIn('call(info, "GetResPointType")', source)
        self.assertIn('call(manager, "GetMonsterTemplate", candidate)', source)
        self.assertIn('reflected_value(manager, "allMarchUuids")', source)
        self.assertIn('call(manager, "GetMarch", raw_uuid)', source)
        self.assertIn('call(info, "IsMonsterOrBoss")', source)
        self.assertIn('"PushWorldMarchWorldGet.serverMarchArr.marchInfos"', source)
        # Current-build player power is requested through the recovered controller
        # shape, then read only from the current WorldPointDetailManager cache.
        self.assertIn("POWER_DETAIL_BATCH_SIZE = 48", source)
        self.assertIn("POWER_DETAIL_WAIT_SECONDS = 0.5", source)
        self.assertIn("POWER_DETAIL_MAX_RETRIES = 1", source)
        self.assertIn("function M.CachedWorldDetailPower(point_id)", source)
        self.assertIn('call(manager, "GetDetailByPointId", math.floor(numeric_point))', source)
        self.assertIn('"DataCenter.WorldPointDetailManager.GetDetailByPointId"', source)
        self.assertIn("function M.RequestWorldPointDetail(record)", source)
        self.assertIn('require, "UI.UIWorldPoint.Controller.UIWorldPointCtrl"', source)
        self.assertIn('safe_get(controller, "RequestWorldPointDetail")', source)
        self.assertIn('phase = "player_power_request"', source)
        self.assertIn('phase = "player_power_wait"', source)
        self.assertIn("record.powerSource = source", source)
        self.assertIn("values.powerTargetLimit", source)
        self.assertIn("M.power_target_limit = power_target_limit", source)
        # Content-v12 WorldPointDetailData.ParseData normalizes resource detail
        # amount aliases into remainRes/initRes. The scanner only consumes those
        # normalized fields from the same authoritative detail cache.
        self.assertIn("function M.CachedWorldDetailResource(point_id)", source)
        self.assertIn('safe_get(detail, "remainRes")', source)
        self.assertIn('safe_get(detail, "initRes")', source)
        self.assertIn("record.resourceRemaining = remaining", source)
        self.assertIn("record.resourceCapacity = capacity", source)
        self.assertIn("record.resourceAmountSource = source", source)
        self.assertIn('phase = "resource_detail_request"', source)
        self.assertIn('phase = "resource_detail_wait"', source)
        self.assertIn("values.resourceTargetLimit", source)
        self.assertIn("M.resource_target_limit = resource_target_limit", source)
        self.assertIn('resource_detail_enrichment = {', source)
        self.assertIn('parse_contract = "WorldPointDetailData.ParseData: remainRes|reserve -> remainRes; initRes|initReserve -> initRes"', source)
        self.assertIn("skipped_target_count", source)
        self.assertIn("all_resolved", source)


if __name__ == "__main__":
    unittest.main()
