"""Claim-free startup reliability check for the installed Daily Task runtime.

This tool never creates a Daily Task command. It requires the persistent runtime
installation to match its manifest, launches Last War normally, waits for a new
runtime heartbeat, records the observed registration, then stops the game before
the next cycle. Installed game hashes must remain unchanged across every cycle.
"""

from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import io
import json
from pathlib import Path
import subprocess
import time

try:
    from .install_daily_task_runtime import _hashes, inspect as inspect_install
    from .install_loader_probe import InstallRefused, discover_paths, game_is_running
    from .run_daily_task_claim_probe import _stop_game
except ImportError:
    from install_daily_task_runtime import _hashes, inspect as inspect_install
    from install_loader_probe import InstallRefused, discover_paths, game_is_running
    from run_daily_task_claim_probe import _stop_game


EXPECTED_VERSION = "lwcontrol-daily-task-runtime-1"
ALLOWED_REGISTRATIONS = {"UpdateManager.AddUpdate", "GameEntry.Timer.RegisterTimerRepeat"}


def _read_json(path: Path) -> dict | None:
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError):
        return None
    return value if isinstance(value, dict) else None


def _heartbeat_time(value: dict | None) -> int | None:
    observed = value.get("updatedAt") if value else None
    return observed if isinstance(observed, int) else None


def _game_pids() -> set[int]:
    completed = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq LastWar.exe", "/FO", "CSV", "/NH"],
        capture_output=True,
        text=True,
        check=False,
    )
    if completed.returncode != 0:
        return set()
    pids: set[int] = set()
    for row in csv.reader(io.StringIO(completed.stdout)):
        if len(row) >= 2 and row[0].casefold() == "lastwar.exe":
            try:
                pids.add(int(row[1]))
            except ValueError:
                pass
    return pids


def _launch(paths: dict[str, Path]) -> None:
    subprocess.Popen(
        [str(paths["launcher"])],
        cwd=str(paths["launcher"].parent),
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def _wait_for_heartbeat(
    heartbeat_path: Path,
    before_updated_at: int | None,
    timeout_seconds: int,
    before_pids: set[int] | None = None,
) -> tuple[bool, bool, bool, dict | None]:
    deadline = time.monotonic() + timeout_seconds
    game_started = False
    process_changed = before_pids is None
    heartbeat = None
    success = False
    while time.monotonic() < deadline:
        time.sleep(0.25)
        current_pids = _game_pids()
        game_started = game_started or bool(current_pids)
        if before_pids is not None and current_pids and current_pids != before_pids:
            process_changed = True
        heartbeat = _read_json(heartbeat_path)
        updated_at = _heartbeat_time(heartbeat)
        if (
            game_started
            and process_changed
            and heartbeat is not None
            and heartbeat.get("version") == EXPECTED_VERSION
            and heartbeat.get("registrationMethod") in ALLOWED_REGISTRATIONS
            and updated_at is not None
            and (before_updated_at is None or updated_at > before_updated_at)
        ):
            success = True
            break
    return success, game_started, process_changed, heartbeat


def run(cycles: int, timeout_seconds: int, mode: str = "cold") -> dict[str, object]:
    if cycles < 1 or cycles > 20:
        raise InstallRefused("cycles must be between 1 and 20")
    if timeout_seconds < 5 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 5 and 120 seconds")

    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the startup reliability check")

    installation = inspect_install(paths)
    if not installation.get("installed"):
        raise InstallRefused("Persistent Daily Task runtime installation is not verified")

    runtime = paths["runtime"]
    command_path = runtime / "daily-task-command.txt"
    heartbeat_path = runtime / "daily-task-runtime-heartbeat.json"
    if command_path.exists():
        raise InstallRefused("Daily Task command file exists; refusing a startup-only check")

    baseline_hashes = _hashes(paths)
    results: list[dict[str, object]] = []
    if mode == "relaunch":
        before_heartbeat = _read_json(heartbeat_path)
        before_updated_at = _heartbeat_time(before_heartbeat)
        started_at = datetime.now(timezone.utc)
        _launch(paths)
        success, game_started, _, heartbeat = _wait_for_heartbeat(
            heartbeat_path, before_updated_at, timeout_seconds
        )
        observed_at = datetime.now(timezone.utc)
        if not success:
            _stop_game()
            raise InstallRefused("baseline cold launch did not register before relaunch testing")
        results.append({
            "cycle": 0,
            "kind": "baseline_cold_start",
            "startedAt": started_at.isoformat().replace("+00:00", "Z"),
            "observedAt": observed_at.isoformat().replace("+00:00", "Z"),
            "elapsedSeconds": round((observed_at - started_at).total_seconds(), 3),
            "gameStarted": game_started,
            "processChanged": True,
            "heartbeatAdvanced": True,
            "heartbeat": heartbeat,
            "commandFilePresent": command_path.exists(),
        })

        for cycle in range(1, cycles + 1):
            if not game_is_running():
                raise InstallRefused(f"relaunch cycle {cycle}: LastWar.exe is not running")
            if command_path.exists():
                raise InstallRefused(f"relaunch cycle {cycle}: a Daily Task command appeared unexpectedly")
            before_pids = _game_pids()
            before_heartbeat = _read_json(heartbeat_path)
            before_updated_at = _heartbeat_time(before_heartbeat)
            started_at = datetime.now(timezone.utc)
            _launch(paths)
            success, game_started, process_changed, heartbeat = _wait_for_heartbeat(
                heartbeat_path, before_updated_at, timeout_seconds, before_pids
            )
            observed_at = datetime.now(timezone.utc)
            results.append({
                "cycle": cycle,
                "kind": "launcher_relaunch",
                "startedAt": started_at.isoformat().replace("+00:00", "Z"),
                "observedAt": observed_at.isoformat().replace("+00:00", "Z"),
                "elapsedSeconds": round((observed_at - started_at).total_seconds(), 3),
                "beforePids": sorted(before_pids),
                "afterPids": sorted(_game_pids()),
                "gameStarted": game_started,
                "processChanged": process_changed,
                "heartbeatAdvanced": success,
                "heartbeat": heartbeat,
                "commandFilePresent": command_path.exists(),
            })
            if _hashes(paths) != baseline_hashes:
                raise InstallRefused(f"relaunch cycle {cycle}: installed game hashes changed")
            if not success:
                break
        _stop_game()
        passed = len(results) == cycles + 1 and all(item["heartbeatAdvanced"] for item in results)
        return {
            "schemaVersion": 1,
            "mode": mode,
            "runtimeVersion": EXPECTED_VERSION,
            "cyclesRequested": cycles,
            "cyclesCompleted": max(0, len(results) - 1),
            "allPassed": passed,
            "commandsCreated": 0,
            "rewardClaimSends": 0,
            "installedHashes": baseline_hashes,
            "cycles": results,
        }

    for cycle in range(1, cycles + 1):
        if game_is_running():
            raise InstallRefused(f"cycle {cycle}: LastWar.exe was unexpectedly already running")
        if command_path.exists():
            raise InstallRefused(f"cycle {cycle}: a Daily Task command appeared unexpectedly")

        before_heartbeat = _read_json(heartbeat_path)
        before_updated_at = _heartbeat_time(before_heartbeat)
        started_at = datetime.now(timezone.utc)
        _launch(paths)
        success, game_started, _, heartbeat = _wait_for_heartbeat(
            heartbeat_path, before_updated_at, timeout_seconds
        )

        observed_at = datetime.now(timezone.utc)
        cycle_result = {
            "cycle": cycle,
            "startedAt": started_at.isoformat().replace("+00:00", "Z"),
            "observedAt": observed_at.isoformat().replace("+00:00", "Z"),
            "elapsedSeconds": round((observed_at - started_at).total_seconds(), 3),
            "gameStarted": game_started,
            "heartbeatAdvanced": success,
            "heartbeat": heartbeat,
            "commandFilePresent": command_path.exists(),
        }
        results.append(cycle_result)

        _stop_game()
        time.sleep(1.0)
        if command_path.exists():
            raise InstallRefused(f"cycle {cycle}: startup-only check observed a command file")
        if _hashes(paths) != baseline_hashes:
            raise InstallRefused(f"cycle {cycle}: installed game hashes changed during startup check")
        if not success:
            break

    passed = len(results) == cycles and all(item["heartbeatAdvanced"] for item in results)
    return {
        "schemaVersion": 1,
        "mode": mode,
        "runtimeVersion": EXPECTED_VERSION,
        "cyclesRequested": cycles,
        "cyclesCompleted": len(results),
        "allPassed": passed,
        "commandsCreated": 0,
        "rewardClaimSends": 0,
        "installedHashes": baseline_hashes,
        "cycles": results,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cycles", type=int, default=3)
    parser.add_argument("--timeout-seconds", type=int, default=60)
    parser.add_argument("--mode", choices=("cold", "relaunch"), default="cold")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        result = run(args.cycles, args.timeout_seconds, args.mode)
    except (InstallRefused, OSError, ValueError, KeyError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0 if result["allPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
