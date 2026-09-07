"""Run one bounded current-game daily-task state capture and restore originals.

The supplied candidate must already pass install_loader_probe.py --verify-dir.
The injected Lua probe may queue one copy of the game's own DailyQuestLs refresh
when the manager has not populated its daily-task state yet. This runner never
queues reward-claim commands. The installed script package and BaseUtils.rdl are
backed up before launch and restored in a finally block.
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
        preflight,
        sha256_file,
        verify_offline_candidate,
    )
except ImportError:  # direct script execution
    from install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        preflight,
        sha256_file,
        verify_offline_candidate,
    )


RUNTIME_FILES = {
    "heartbeat": "loader-probe.json",
    "snapshot": "daily-task-snapshot.json",
    "status": "daily-task-snapshot-status.json",
    "refresh": "daily-task-refresh.json",
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
    raise InstallRefused("LastWar.exe did not close after the bounded probe run")


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict:
    if timeout_seconds < 5 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 5 and 120 seconds")

    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the bounded state probe")

    verified = verify_offline_candidate(paths, candidate_dir)
    candidate_dir = Path(verified["candidate_dir"])
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
        "backup": str(backup),
        "timeout_seconds": timeout_seconds,
        "read_only_refresh_limit": 1,
        "reward_claims_sent": 0,
    }

    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])

        installed = preflight(paths)
        if not installed.get("already_installed") or not installed.get("probe_payloads_verified"):
            raise InstallRefused("Installed daily-task probe candidate failed exact payload verification")
        result["installed_preflight"] = installed

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
        result["refresh"] = _read_json(runtime_paths["refresh"])
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
        raise InstallRefused("Original game files did not restore to their exact pre-run hashes")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=60)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = run_probe(args.candidate_dir, args.timeout_seconds)
    except (InstallRefused, OSError, ValueError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
