"""Bounded live smoke for the persistent Daily Task runtime; sends no command."""

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
    from .prepare_daily_task_runtime import verify
    from .run_daily_task_claim_probe import _stop_game
except ImportError:
    from install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        sha256_file,
    )
    from prepare_daily_task_runtime import verify
    from run_daily_task_claim_probe import _stop_game


EXPECTED_VERSION = "lwcontrol-daily-task-runtime-1"


def _hashes(paths: dict[str, Path]) -> dict[str, str]:
    return {key: sha256_file(paths[key]) for key in ("data", "metadata", "version", "baseutils")}


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def run(candidate_dir: Path, timeout_seconds: int, exercise_command: bool = False) -> dict[str, object]:
    if timeout_seconds < 5 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 5 and 120 seconds")
    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the Daily Task runtime smoke")
    verified = verify(paths, candidate_dir)
    candidate_dir = Path(str(verified["candidate_dir"]))
    candidate_files = {key: candidate_dir / name for key, name in {
        "data": "LWScripts.data", "metadata": "LWScripts.txt", "version": "version.txt"
    }.items()}
    runtime = paths["runtime"]
    runtime.mkdir(parents=True, exist_ok=True)
    command_path = runtime / "daily-task-command.txt"
    if command_path.exists():
        raise InstallRefused("Daily Task command file already exists; smoke refuses to consume or replace it")
    heartbeat_path = runtime / "daily-task-runtime-heartbeat.json"
    status_path = runtime / "daily-task-runtime-status.json"
    heartbeat_path.unlink(missing_ok=True)
    status_path.unlink(missing_ok=True)

    before_hashes = _hashes(paths)
    backup = _make_backup(paths)
    result: dict[str, object] = {
        "candidate_dir": str(candidate_dir),
        "candidate_package_sha256": verified["package_sha256"],
        "backup": str(backup),
        "timeout_seconds": timeout_seconds,
        "commands_created": 0,
        "reward_claim_sends": 0,
    }
    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])
        installed = {key: sha256_file(paths[key]) for key in ("data", "metadata", "version")}
        expected = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if installed != expected:
            raise InstallRefused("installed Daily Task runtime candidate hash mismatch")
        result["installed_hashes"] = installed
        subprocess.Popen(
            [str(paths["launcher"])],
            cwd=str(paths["launcher"].parent),
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        result["launched"] = True
        deadline = time.monotonic() + timeout_seconds
        game_started = False
        heartbeat = None
        while time.monotonic() < deadline:
            time.sleep(0.25)
            game_started = game_started or game_is_running()
            heartbeat = _read_json(heartbeat_path)
            if isinstance(heartbeat, dict) and heartbeat.get("version") == EXPECTED_VERSION \
                    and heartbeat.get("registrationMethod"):
                break
        result["game_started"] = game_started
        result["heartbeat"] = heartbeat
        result["status"] = _read_json(status_path)
        result["runtime_registered"] = isinstance(heartbeat, dict) \
            and heartbeat.get("version") == EXPECTED_VERSION \
            and heartbeat.get("registrationMethod") in {
                "UpdateManager.AddUpdate", "GameEntry.Timer.RegisterTimerRepeat"
            }
        result["command_file_created"] = command_path.exists()
        if result["command_file_created"]:
            raise InstallRefused("Daily Task runtime smoke unexpectedly created a command file")
        if exercise_command:
            if not result["runtime_registered"]:
                raise InstallRefused("Daily Task runtime must register before command exercise")
            cli = subprocess.run(
                ["dotnet", "run", "--project", "src/LWControl.DailyTaskCli/LWControl.DailyTaskCli.csproj",
                 "--configuration", "Release", "--", "run-once", "1"],
                cwd=str(Path(__file__).resolve().parents[1]),
                capture_output=True,
                text=True,
                timeout=timeout_seconds,
                check=False,
            )
            result["command_exercise"] = {
                "exit_code": cli.returncode,
                "stdout": cli.stdout.strip(),
                "stderr": cli.stderr.strip(),
            }
            if cli.returncode != 0:
                raise InstallRefused(f"Daily Task C# command client failed: {cli.stderr.strip()}")
            try:
                command_result = json.loads(cli.stdout)
            except json.JSONDecodeError as exc:
                raise InstallRefused(f"Daily Task C# client returned invalid JSON: {exc}") from exc
            result["command_result"] = command_result
            result["commands_created"] = 1
            result["reward_claim_sends"] = command_result.get("RewardSendCount")
            if command_result.get("State") != "completed" or command_result.get("Message") != "no_eligible_target":
                raise InstallRefused("Daily Task command smoke did not finish as no_eligible_target")
            if command_result.get("RewardSendCount") != 0 or command_result.get("ConfirmedClaims") != 0:
                raise InstallRefused("Daily Task command smoke unexpectedly sent or confirmed a reward claim")
    finally:
        _stop_game()
        _restore_from_backup(paths, backup)
        after_hashes = _hashes(paths)
        result["restored"] = True
        result["before_sha256"] = before_hashes
        result["after_sha256"] = after_hashes
        result["restore_hash_match"] = before_hashes == after_hashes
    if not result["restore_hash_match"]:
        raise InstallRefused("official game files did not restore exactly after Daily Task runtime smoke")
    if not result.get("runtime_registered"):
        raise InstallRefused("Daily Task runtime did not prove recurring registration during smoke")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=90)
    parser.add_argument("--exercise-command", action="store_true")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = run(args.candidate_dir, args.timeout_seconds, args.exercise_command)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
