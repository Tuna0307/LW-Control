"""Run the bounded 13x13/two-batch World AOI proof and restore files exactly."""

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
    from .prepare_world_map_multi_batch_probe import verify
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
    from prepare_world_map_multi_batch_probe import verify
    from build_world_block_sender import build as build_world_block_sender


RUNTIME_FILES = {
    "heartbeat": "world-map-multi-batch-heartbeat.json",
    "status": "world-map-multi-batch-status.json",
    "result": "world-map-multi-batch-result.json",
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
    raise InstallRefused("LastWar.exe did not close after the bounded multi-batch probe")


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict[str, object]:
    if timeout_seconds < 15 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 15 and 120 seconds")
    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the bounded multi-batch probe")

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
        "maximum_probe_requests": 2,
        "requested_logical_blocks": 169,
        "expected_batch_logical_blocks": [156, 13],
        "serial_capability_probe_first": True,
        "camera_moves": 0,
        "retries": 0,
        "world_block_sender": bridge_build,
    }
    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])
        installed_hashes = {key: sha256_file(paths[key]) for key in ("data", "metadata", "version")}
        candidate_hashes = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if installed_hashes != candidate_hashes:
            raise InstallRefused("installed multi-batch candidate did not match exact prepared-file hashes")
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
            if runtime_paths["result"].is_file():
                break
            if isinstance(status, dict) and status.get("completed") is True:
                break
        result["game_started"] = game_started
        result["heartbeat"] = _read_json(runtime_paths["heartbeat"])
        result["status"] = _read_json(runtime_paths["status"])
        result["result"] = _read_json(runtime_paths["result"])
        result["result_present"] = runtime_paths["result"].is_file()
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
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=75)
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
