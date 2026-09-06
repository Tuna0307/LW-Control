"""Run the bounded 100x100 full-world World AOI proof and restore exactly."""

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


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict[str, object]:
    if timeout_seconds < 30 or timeout_seconds > 180:
        raise InstallRefused("timeout must be between 30 and 180 seconds")
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
        "maximum_probe_requests": 65,
        "requested_logical_blocks": 10000,
        "expected_batch_count": 65,
        "expected_full_batch_count": 60,
        "expected_tail_batch_count": 5,
        "expected_full_batch_logical_blocks": 160,
        "expected_tail_batch_logical_blocks": 80,
        "serial_capability_probe_first": True,
        "remaining_full_scan_requests": 64,
        "maximum_concurrent_requests": 8,
        "camera_moves": 0,
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
            }
        else:
            result["status_summary"] = final_status
        result["result_present"] = runtime_paths["result"].is_file()
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
        _stop_game()
        _restore_from_backup(paths, backup)
        if bridge_preexisting:
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
        or probe_result.get("camera_move_count") != 0
        or probe_result.get("retry_count") != 0
        or probe_result.get("response_hook_restored") is not True
        or probe_result.get("manager_flag_restored") is not True
    ):
        raise InstallRefused("full-scan probe result did not satisfy the recovered full-world contract")
    batch_results = probe_result.get("batch_results")
    point_records = probe_result.get("point_records")
    if not isinstance(batch_results, list) or len(batch_results) != 65:
        raise InstallRefused("full-scan probe did not record all 65 batch results")
    if not isinstance(point_records, list) or probe_result.get("accumulated_record_count") != len(point_records):
        raise InstallRefused("full-scan accumulated point-record count is inconsistent")
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
        "target_response_count": probe_result.get("target_response_count"),
        "rejected_response_count": probe_result.get("rejected_response_count"),
        "camera_move_count": probe_result["camera_move_count"],
        "retry_count": probe_result["retry_count"],
        "response_hook_restored": probe_result["response_hook_restored"],
        "manager_flag_restored": probe_result["manager_flag_restored"],
    }
    result["live_contract_verified"] = True
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=120)
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
