"""Run the bounded full-world AOI proof with raw current-build march-push capture."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import time

try:
    from .install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        sha256_file,
    )
    from .prepare_world_map_full_scan_probe import verify
    from .build_world_block_sender import build as build_world_block_sender
except ImportError:
    from install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        sha256_file,
    )
    from prepare_world_map_full_scan_probe import verify
    from build_world_block_sender import build as build_world_block_sender


RUNTIME_FILES = {
    "heartbeat": "world-map-full-scan-heartbeat.json",
    "status": "world-map-full-scan-status.json",
    "result": "world-map-full-scan-result.json",
    "monster_diagnostics": "world-map-full-scan-monster-diagnostics.json",
    "direct_diagnostics": "world-map-full-scan-direct-diagnostics.json",
}


def _hashes(paths: dict[str, Path]) -> dict[str, str]:
    return {
        key: sha256_file(paths[key])
        for key in ("data", "metadata", "version", "baseutils")
    }


def _stop_game() -> None:
    if not game_is_running():
        return
    subprocess.run(
        ["taskkill", "/IM", "LastWar.exe", "/F"],
        capture_output=True,
        text=True,
        check=False,
    )
    for _ in range(40):
        if not game_is_running():
            return
        time.sleep(0.25)
    raise InstallRefused("LastWar.exe did not close after the bounded full-scan probe")


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def _restore_scan_files_while_running(paths: dict[str, Path], backup: Path) -> None:
    """Restore only files changed by this scan without requiring process exit.

    The game has already loaded the candidate Lua package into memory when this
    runs.  Keeping the process alive is safe only if the three on-disk files can
    be restored exactly; any sharing violation or hash mismatch falls back to
    the established close-and-restore path in run_probe().
    """
    for key in ("data", "metadata", "version"):
        source = backup / paths[key].name
        if not source.is_file():
            raise InstallRefused(f"Backup is missing {source.name}")
    for key in ("data", "metadata", "version"):
        shutil.copy2(backup / paths[key].name, paths[key])


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict[str, object]:
    if timeout_seconds < 120 or timeout_seconds > 900:
        raise InstallRefused("timeout must be between 120 and 900 seconds")
    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the bounded full-scan probe")

    verified = verify(paths, candidate_dir)
    candidate_dir = Path(str(verified["candidate_dir"]))
    candidate_files = {
        "data": candidate_dir / "LWScripts.data",
        "metadata": candidate_dir / "LWScripts.txt",
        "version": candidate_dir / "version.txt",
    }
    before = _hashes(paths)
    backup = _make_backup(paths)
    runtime = paths["runtime"]
    runtime.mkdir(parents=True, exist_ok=True)
    bridge_path = runtime / "WorldBlockSender.dll"
    bridge_backup = backup / "WorldBlockSender.dll.preexisting"
    bridge_preexisting = bridge_path.is_file()
    if bridge_preexisting:
        shutil.copy2(bridge_path, bridge_backup)
    bridge_build = build_world_block_sender(bridge_path)
    runtime_paths = {key: runtime / name for key, name in RUNTIME_FILES.items()}
    for path in runtime_paths.values():
        path.unlink(missing_ok=True)

    result: dict[str, object] = {
        "candidate_dir": str(candidate_dir),
        "candidate_package_sha256": verified["package_sha256"],
        "probe_source_sha256": verified["probe_source_sha256"],
        "backup": str(backup),
        "timeout_seconds": timeout_seconds,
        "maximum_direct_block_requests": 65,
        "maximum_official_view_requests": 500,
        "maximum_managed_rect_march_requests": 0,
        "maximum_world_detail_requests": 50000,
        "world_detail_request_batch_size": 48,
        "world_detail_max_retries": 1,
        "maximum_total_network_requests": 50565,
        "requested_logical_blocks": 10000,
        "expected_batch_count": 65,
        "expected_full_batch_count": 60,
        "expected_tail_batch_count": 5,
        "expected_full_batch_logical_blocks": 160,
        "expected_tail_batch_logical_blocks": 80,
        "serial_capability_probe_first": True,
        "remaining_full_scan_requests": 64,
        "maximum_concurrent_requests": 8,
        "expected_camera_moves": 500,
        "expected_camera_restore": True,
        "retries": 0,
        "world_block_sender": bridge_build,
    }
    probe_result: object | None = None
    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])
        installed_hashes = {key: sha256_file(paths[key]) for key in ("data", "metadata", "version")}
        candidate_hashes = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if installed_hashes != candidate_hashes:
            raise InstallRefused("installed full-scan candidate did not match exact prepared-file hashes")
        result["installed_hashes"] = installed_hashes

        subprocess.Popen(
            [str(paths["launcher"])],
            cwd=str(paths["launcher"].parent),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        result["launched"] = True
        deadline = time.monotonic() + timeout_seconds
        game_started = False
        while time.monotonic() < deadline:
            time.sleep(0.25)
            game_started = game_started or game_is_running()
            status = _read_json(runtime_paths["status"])
            observed_result = _read_json(runtime_paths["result"])
            if isinstance(observed_result, dict) and observed_result.get("state") == "proven":
                probe_result = observed_result
                break
            if isinstance(status, dict) and status.get("completed") is True:
                if status.get("state") != "captured":
                    break
        result["game_started"] = game_started
        result["heartbeat"] = _read_json(runtime_paths["heartbeat"])
        final_status = _read_json(runtime_paths["status"])
        if isinstance(final_status, dict):
            result["status_summary"] = {
                "state": final_status.get("state"),
                "completed": final_status.get("completed"),
                "request_sent_count": final_status.get("request_sent_count"),
                "completed_batch_count": final_status.get("completed_batch_count"),
                "requested_block_count": final_status.get("requested_block_count"),
                "covered_block_count": final_status.get("covered_block_count"),
                "full_scan_peak_inflight": final_status.get("full_scan_peak_inflight"),
                "accumulated_record_count": final_status.get("accumulated_record_count"),
                "monster_discovery_source": final_status.get("monster_discovery_source"),
                "direct_monster_record_count": final_status.get("direct_monster_record_count"),
                "monster_view_index": final_status.get("monster_view_index"),
                "monster_view_count": final_status.get("monster_view_count"),
                "monster_camera_move_count": final_status.get("monster_camera_move_count"),
                "monster_official_request_count": final_status.get("monster_official_request_count"),
                "monster_march_request_count": final_status.get("monster_march_request_count"),
                "monster_march_response_count": final_status.get("monster_march_response_count"),
                "monster_march_foreign_send_count": final_status.get("monster_march_foreign_send_count"),
                "monster_march_duplicate_push_count": final_status.get("monster_march_duplicate_push_count"),
                "monster_view_diagnostic_count": final_status.get("monster_view_diagnostic_count"),
                "monster_capture_count": final_status.get("monster_capture_count"),
                "monster_views_with_bosses": final_status.get("monster_views_with_bosses"),
                "monster_camera_restored": final_status.get("monster_camera_restored"),
                "monster_march_hook_restored": final_status.get("monster_march_hook_restored"),
            }
        else:
            result["status_summary"] = final_status
        result["result_present"] = runtime_paths["result"].is_file()
        if runtime_paths["monster_diagnostics"].is_file():
            monster_diagnostics_path = candidate_dir / "live-monster-diagnostics.json"
            shutil.copy2(runtime_paths["monster_diagnostics"], monster_diagnostics_path)
            result["monster_diagnostics_path"] = str(monster_diagnostics_path)
        if runtime_paths["direct_diagnostics"].is_file():
            direct_diagnostics_path = candidate_dir / "live-direct-diagnostics.json"
            shutil.copy2(runtime_paths["direct_diagnostics"], direct_diagnostics_path)
            result["direct_diagnostics_path"] = str(direct_diagnostics_path)
        if probe_result is None:
            probe_result = _read_json(runtime_paths["result"])
        if isinstance(probe_result, dict) and probe_result.get("state") == "proven":
            live_result_path = candidate_dir / "live-result.json"
            live_status_path = candidate_dir / "live-status.json"
            live_heartbeat_path = candidate_dir / "live-heartbeat.json"
            shutil.copy2(runtime_paths["result"], live_result_path)
            if runtime_paths["status"].is_file():
                shutil.copy2(runtime_paths["status"], live_status_path)
            if runtime_paths["heartbeat"].is_file():
                shutil.copy2(runtime_paths["heartbeat"], live_heartbeat_path)
            result["live_result_path"] = str(live_result_path)
    finally:
        live_restore_ok = False
        result["live_restore_attempted"] = False
        result["live_restore_error"] = None
        if isinstance(probe_result, dict) and probe_result.get("state") == "proven" and game_is_running():
            result["live_restore_attempted"] = True
            try:
                _restore_scan_files_while_running(paths, backup)
                live_restore_ok = _hashes(paths) == before
                if not live_restore_ok:
                    result["live_restore_error"] = "protected hashes did not match after live restore"
            except (OSError, InstallRefused) as exc:
                result["live_restore_error"] = str(exc)
        if not live_restore_ok:
            _stop_game()
            _restore_from_backup(paths, backup)
        result["game_left_running"] = live_restore_ok and game_is_running()
        result["restore_mode"] = "live_process_preserved" if live_restore_ok else "closed_fallback"
        result["runtime_helper_cleanup_deferred"] = False
        if result["game_left_running"]:
            # WorldBlockSender.dll is loaded with Assembly.LoadFrom and Windows
            # may hold it open. It is an LWControl runtime helper, not a game
            # file. Leave it for the next closed-game scan/cleanup.
            result["runtime_helper_cleanup_deferred"] = True
        elif bridge_preexisting:
            shutil.copy2(bridge_backup, bridge_path)
        else:
            bridge_path.unlink(missing_ok=True)
        after = _hashes(paths)
        result["restored"] = True
        result["before_sha256"] = before
        result["after_sha256"] = after
        result["restore_hash_match"] = before == after

    if not result["restore_hash_match"]:
        raise InstallRefused("original game files did not restore to exact pre-run hashes")
    if not isinstance(probe_result, dict) or probe_result.get("state") != "proven":
        state = probe_result.get("state") if isinstance(probe_result, dict) else None
        raise InstallRefused(f"full-scan probe did not produce proven result (state={state!r})")
    if (
        probe_result.get("requested_block_count") != 10000
        or probe_result.get("covered_block_count") != 10000
        or probe_result.get("request_sent_count") != 65
        or probe_result.get("completed_batch_count") != 65
        or probe_result.get("maximum_concurrency") != 8
        or not isinstance(probe_result.get("full_scan_peak_inflight"), int)
        or not 1 <= probe_result["full_scan_peak_inflight"] <= 8
        or probe_result.get("monster_discovery_source") != "PushWorldMarchWorldGet.serverMarchArr.marchInfos"
        or not isinstance(probe_result.get("direct_monster_record_count"), int)
        or probe_result["direct_monster_record_count"] < 0
        or probe_result.get("camera_move_count") != 500
        or probe_result.get("monster_camera_view_count") != 500
        or probe_result.get("monster_official_request_count") != 500
        or probe_result.get("monster_march_request_count") != 0
        or probe_result.get("monster_march_response_count") != 500
        or probe_result.get("monster_march_foreign_send_count") != 0
        or probe_result.get("monster_march_hook_restored") is not True
        or probe_result.get("monster_capture_count") != 500
        or not isinstance(probe_result.get("monster_views_with_marches"), int)
        or probe_result["monster_views_with_marches"] < 1
        or not isinstance(probe_result.get("monster_views_with_bosses"), int)
        or probe_result["monster_views_with_bosses"] < 1
        or not isinstance(probe_result.get("monster_max_march_count"), int)
        or probe_result["monster_max_march_count"] < 1
        or probe_result.get("camera_restored") is not True
        or probe_result.get("retry_count") != 0
        or probe_result.get("response_hook_restored") is not True
        or probe_result.get("manager_flag_restored") is not True
        or probe_result.get("world_response_flag_restored") is not True
    ):
        raise InstallRefused("full-scan probe result did not satisfy the recovered full-world contract")
    batch_results = probe_result.get("batch_results")
    point_records = probe_result.get("point_records")
    monster_view_diagnostics = probe_result.get("monster_view_diagnostics")
    player_power_enrichment = probe_result.get("player_power_enrichment")
    if not isinstance(batch_results, list) or len(batch_results) != 65:
        raise InstallRefused("full-scan probe did not record all 65 batch results")
    if not isinstance(point_records, list) or probe_result.get("accumulated_record_count") != len(point_records):
        raise InstallRefused("full-scan accumulated point-record count is inconsistent")
    if not isinstance(monster_view_diagnostics, list) or len(monster_view_diagnostics) != 500:
        raise InstallRefused("passive AOI monster scan did not retain all 500 view diagnostics")
    if (
        not isinstance(player_power_enrichment, dict)
        or player_power_enrichment.get("source")
        != "DataCenter.WorldPointDetailManager.GetDetailByPointId"
        or player_power_enrichment.get("request_route")
        != "UI.UIWorldPoint.Controller.UIWorldPointCtrl.RequestWorldPointDetail"
        or player_power_enrichment.get("batch_size") != 48
        or player_power_enrichment.get("wait_seconds") != 0.5
        or player_power_enrichment.get("max_retries") != 1
        or player_power_enrichment.get("complete") is not True
        or player_power_enrichment.get("target_limit") is not None
        or player_power_enrichment.get("skipped_target_count") != 0
        or player_power_enrichment.get("capped") is not False
        or not isinstance(player_power_enrichment.get("request_count"), int)
        or not 0 <= player_power_enrichment["request_count"] <= 50000
        or not isinstance(player_power_enrichment.get("request_failure_count"), int)
        or not 0 <= player_power_enrichment["request_failure_count"] <= player_power_enrichment["request_count"]
        or not isinstance(player_power_enrichment.get("resolved_count"), int)
        or player_power_enrichment["resolved_count"] < 0
        or not isinstance(player_power_enrichment.get("unresolved_count"), int)
        or player_power_enrichment["unresolved_count"] < 0
        or player_power_enrichment.get("retry_count") not in (0, 1)
    ):
        raise InstallRefused("player-power enrichment did not satisfy the bounded current-build detail contract")
    direct_monsters = [
        record for record in point_records
        if isinstance(record, dict)
        and record.get("kind") == "monster"
        and record.get("source") == "WorldPointManager._pointInfos"
    ]
    if len(direct_monsters) != probe_result["direct_monster_record_count"]:
        raise InstallRefused("direct monster count does not match retained WorldPointInfo records")
    required_kinds = {"player_base", "resource_point", "alliance_building", "monster"}
    observed_kinds = {
        record.get("kind") for record in point_records if isinstance(record, dict)
    }
    if not required_kinds.issubset(observed_kinds):
        raise InstallRefused("full-scan did not retain every core World Scan record category")
    result["result_summary"] = {
        "state": probe_result["state"],
        "requested_block_count": probe_result["requested_block_count"],
        "covered_block_count": probe_result["covered_block_count"],
        "request_sent_count": probe_result["request_sent_count"],
        "completed_batch_count": probe_result["completed_batch_count"],
        "maximum_concurrency": probe_result["maximum_concurrency"],
        "full_scan_peak_inflight": probe_result["full_scan_peak_inflight"],
        "full_scan_wave_count": probe_result.get("full_scan_wave_count"),
        "accumulated_record_count": probe_result["accumulated_record_count"],
        "duplicate_record_count": probe_result.get("duplicate_record_count"),
        "post_response_capture_count": probe_result.get("post_response_capture_count"),
        "monster_discovery_source": probe_result["monster_discovery_source"],
        "direct_monster_record_count": probe_result["direct_monster_record_count"],
        "kind_counts": probe_result.get("kind_counts"),
        "point_type_counts": probe_result.get("point_type_counts"),
        "target_response_count": probe_result.get("target_response_count"),
        "rejected_response_count": probe_result.get("rejected_response_count"),
        "camera_move_count": probe_result["camera_move_count"],
        "monster_camera_view_count": probe_result["monster_camera_view_count"],
        "monster_official_request_count": probe_result["monster_official_request_count"],
        "monster_march_request_count": probe_result["monster_march_request_count"],
        "monster_march_response_count": probe_result["monster_march_response_count"],
        "monster_march_foreign_send_count": probe_result["monster_march_foreign_send_count"],
        "monster_march_duplicate_push_count": probe_result.get("monster_march_duplicate_push_count", 0),
        "monster_march_hook_restored": probe_result["monster_march_hook_restored"],
        "monster_view_diagnostic_count": len(monster_view_diagnostics),
        "monster_capture_count": probe_result["monster_capture_count"],
        "monster_views_with_marches": probe_result.get("monster_views_with_marches"),
        "monster_views_with_bosses": probe_result["monster_views_with_bosses"],
        "player_power_enrichment": player_power_enrichment,
        "monster_max_march_count": probe_result.get("monster_max_march_count"),
        "camera_restored": probe_result["camera_restored"],
        "camera_restore_distance": probe_result.get("camera_restore_distance"),
        "camera_restore_zoom_delta": probe_result.get("camera_restore_zoom_delta"),
        "retry_count": probe_result["retry_count"],
        "response_hook_restored": probe_result["response_hook_restored"],
        "manager_flag_restored": probe_result["manager_flag_restored"],
        "world_response_flag_restored": probe_result["world_response_flag_restored"],
    }
    result["live_contract_verified"] = True
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = run_probe(args.candidate_dir, args.timeout_seconds)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
