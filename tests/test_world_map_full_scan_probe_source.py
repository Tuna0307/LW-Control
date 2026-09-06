from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools" / "current_world_map_full_scan_probe.lua"
PREPARER = ROOT / "tools" / "prepare_world_map_full_scan_probe.py"
RUNNER = ROOT / "tools" / "run_world_map_full_scan_probe.py"


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

    def test_probe_fails_closed_per_batch_and_has_no_retry_or_camera_fallback(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function timed_out_full_scan_sequence", source)
        self.assertIn("now - entry.sent_at >= RESPONSE_TIMEOUT_SECONDS", source)
        self.assertIn("retry_count = 0", source)
        self.assertIn("camera_move_count = 0", source)
        self.assertNotIn("StartViewRequest", source)
        self.assertNotIn("UpdateViewRequest", source)
        self.assertNotIn("GoToUtil", source)

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

    def test_runner_declares_full_world_contract_and_exact_restore(self):
        source = RUNNER.read_text(encoding="utf-8")
        self.assertIn('"maximum_probe_requests": 65', source)
        self.assertIn('"requested_logical_blocks": 10000', source)
        self.assertIn('"expected_batch_count": 65', source)
        self.assertIn('"maximum_concurrent_requests": 8', source)
        self.assertIn('probe_result.get("covered_block_count") != 10000', source)
        self.assertIn('observed_result.get("state") == "proven"', source)
        self.assertIn('status.get("state") != "captured"', source)
        self.assertNotIn('if runtime_paths["result"].is_file():\n                break', source)
        self.assertIn('result["status_summary"] = {', source)
        self.assertIn('result["result_summary"] = {', source)
        self.assertIn('live_result_path = candidate_dir / "live-result.json"', source)
        self.assertIn("finally:", source)
        self.assertIn("_restore_from_backup(paths, backup)", source)
        self.assertIn('result["restore_hash_match"] = before == after', source)

    def test_status_payload_stays_compact_during_full_scan(self):
        source = SOURCE.read_text(encoding="utf-8")
        self.assertIn("local function public_scan_summary", source)
        status_start = source.index("local function status_payload")
        status_end = source.index("local function cleanup", status_start)
        status = source[status_start:status_end]
        self.assertIn("scan = public_scan_summary(scan_request)", status)
        self.assertNotIn("scan = public_scan(scan_request)", status)
        self.assertNotIn("requests = public_requests(batch_requests)", status)


if __name__ == "__main__":
    unittest.main()
