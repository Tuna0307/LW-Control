"""Run one bounded loaded-world snapshot capture and restore installed files.

The candidate must pass prepare_world_map_snapshot_probe.py --verify-dir. The
probe only reads already-loaded WorldPointManager state; it contains no AOI,
view, or explicit network-send path. Installed script files are backed up and
restored to exact pre-run hashes in a finally block.
"""

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
    from .prepare_world_map_snapshot_probe import verify
except ImportError:
    from install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        sha256_file,
    )
    from prepare_world_map_snapshot_probe import verify


RUNTIME_FILES = {
    "heartbeat": "world-map-loader-probe.json",
    "snapshot": "world-map-snapshot.json",
    "status": "world-map-snapshot-status.json",
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
    raise InstallRefused("LastWar.exe did not close after the bounded world-map probe")


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict[str, object]:
    if timeout_seconds < 5 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 5 and 120 seconds")

    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the bounded world-map probe")

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
    runtime_paths = {key: runtime / name for key, name in RUNTIME_FILES.items()}
    for path in runtime_paths.values():
        path.unlink(missing_ok=True)

    result: dict[str, object] = {
        "candidate_dir": str(candidate_dir),
        "candidate_package_sha256": verified["package_sha256"],
        "probe_source_sha256": verified["probe_source_sha256"],
        "backup": str(backup),
        "timeout_seconds": timeout_seconds,
        "probe_explicit_network_sends": 0,
        "probe_aoi_requests": 0,
    }

    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])
        installed_hashes = {key: sha256_file(paths[key]) for key in ("data", "metadata", "version")}
        candidate_hashes = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if installed_hashes != candidate_hashes:
            raise InstallRefused("installed world-map candidate did not match exact prepared-file hashes")
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
            if runtime_paths["snapshot"].is_file():
                break

        result["game_started"] = game_started
        result["heartbeat"] = _read_json(runtime_paths["heartbeat"])
        result["status"] = _read_json(runtime_paths["status"])
        result["snapshot"] = _read_json(runtime_paths["snapshot"])
        result["snapshot_present"] = runtime_paths["snapshot"].is_file()
    finally:
        _stop_game()
        _restore_from_backup(paths, backup)
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
    parser.add_argument("--timeout-seconds", type=int, default=60)
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
