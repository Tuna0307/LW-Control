"""Run one bounded current-game daily-task reward claim and restore originals."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
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
    from .prepare_daily_task_claim_probe import verify
except ImportError:
    from install_loader_probe import (
        InstallRefused,
        _make_backup,
        _restore_from_backup,
        discover_paths,
        game_is_running,
        sha256_file,
    )
    from prepare_daily_task_claim_probe import verify


RUNTIME_FILES = {
    "heartbeat": "daily-task-claim-heartbeat.json",
    "status": "daily-task-claim-status.json",
    "before": "daily-task-claim-before.json",
    "after": "daily-task-claim-after.json",
    "result": "daily-task-claim-result.json",
    "wire": "daily-task-claim-wire.json",
}


def _hashes(paths: dict[str, Path]) -> dict[str, str]:
    return {key: sha256_file(paths[key]) for key in ("data", "metadata", "version", "baseutils")}


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
    raise InstallRefused("LastWar.exe did not close after the bounded daily-task claim")


def _read_json(path: Path) -> object | None:
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return {"parse_error": str(exc), "path": str(path)}


def _parse_time(value: object) -> datetime | None:
    if not isinstance(value, str):
        return None
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)
    except ValueError:
        return None


def _task_state(snapshot: dict, task_id: str) -> str | None:
    for task in snapshot.get("tasks", []):
        if isinstance(task, dict) and str(task.get("taskId")) == task_id:
            state = task.get("state")
            return state if isinstance(state, str) else None
    return None


def _box_state(snapshot: dict, stage: int) -> str | None:
    for box in snapshot.get("boxes", []):
        if isinstance(box, dict) and box.get("index") == stage:
            state = box.get("state")
            return state if isinstance(state, str) else None
    return None


def verify_effect(before: object, after: object, claim: object) -> tuple[bool, str]:
    if not isinstance(before, dict) or not isinstance(after, dict) or not isinstance(claim, dict):
        return False, "missing structured before/after/result evidence"
    target = claim.get("target")
    if not isinstance(target, dict):
        return False, "claim target is missing"
    if claim.get("state") != "claimed" or claim.get("sendCount") != 1:
        return False, "claim did not finish in the one-send claimed state"
    if claim.get("verificationMode") != "fresh_daily_quest_list_state":
        return False, "claim did not use fresh daily-task list state verification"
    if claim.get("postRefreshRequested") is not True or claim.get("postRefreshObserved") is not True:
        return False, "fresh post-claim daily-task list refresh was not observed"
    if claim.get("responseHadError") is True or claim.get("handlerOk") is False:
        return False, "observed claim response or handler reported an error"
    if claim.get("effectConfirmed") is not True:
        return False, "in-game probe did not confirm the target state transition"
    if target.get("captureId") != before.get("captureId") or claim.get("beforeCaptureId") != before.get("captureId"):
        return False, "selected target is not bound to the pre-claim capture"
    if claim.get("afterCaptureId") != after.get("captureId"):
        return False, "result is not bound to the post-claim capture"
    before_at = _parse_time(before.get("capturedAt"))
    after_at = _parse_time(after.get("capturedAt"))
    if before_at is None or after_at is None or after_at < before_at:
        return False, "post-claim capture timestamp precedes or cannot be compared with pre-state"

    kind = target.get("kind")
    if kind == "DailyTask":
        task_id = str(target.get("taskId") or "")
        if not task_id:
            return False, "task claim has no explicit task id"
        ok = _task_state(before, task_id) == "CanReceive" and _task_state(after, task_id) == "Received"
        return (ok, "task transitioned CanReceive -> Received" if ok else "task state transition did not match")
    if kind == "DailyQuestStage":
        stage = target.get("stage")
        if not isinstance(stage, int) or not 1 <= stage <= 5:
            return False, "daily chest stage is outside 1..5"
        before_received = stage in before.get("receivedStages", [])
        after_received = stage in after.get("receivedStages", [])
        ok = (
            _box_state(before, stage) == "CanReceive"
            and not before_received
            and _box_state(after, stage) == "Received"
            and after_received
        )
        return (ok, "chest transitioned CanReceive -> Received" if ok else "chest state transition did not match")
    return False, "unsupported claim target kind"


def run_probe(candidate_dir: Path, timeout_seconds: int) -> dict[str, object]:
    if timeout_seconds < 5 or timeout_seconds > 120:
        raise InstallRefused("timeout must be between 5 and 120 seconds")
    paths = discover_paths()
    if game_is_running():
        raise InstallRefused("LastWar.exe is already running; close it before the bounded daily-task claim")

    verified = verify(paths, candidate_dir)
    candidate_dir = Path(str(verified["candidate_dir"]))
    candidate_files = {
        "data": candidate_dir / "LWScripts.data",
        "metadata": candidate_dir / "LWScripts.txt",
        "version": candidate_dir / "version.txt",
    }
    before_hashes = _hashes(paths)
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
        "builder_source_sha256": verified["builder_source_sha256"],
        "backup": str(backup),
        "timeout_seconds": timeout_seconds,
        "maximum_reward_claim_sends": 1,
        "retry_count": 0,
    }

    try:
        for key in ("data", "metadata", "version"):
            shutil.copy2(candidate_files[key], paths[key])
        installed_hashes = {key: sha256_file(paths[key]) for key in ("data", "metadata", "version")}
        candidate_hashes = {key: sha256_file(candidate_files[key]) for key in ("data", "metadata", "version")}
        if installed_hashes != candidate_hashes:
            raise InstallRefused("installed daily claim candidate did not match exact prepared-file hashes")
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
            if runtime_paths["result"].is_file():
                break
        result["game_started"] = game_started
        result["heartbeat"] = _read_json(runtime_paths["heartbeat"])
        result["status"] = _read_json(runtime_paths["status"])
        result["before"] = _read_json(runtime_paths["before"])
        result["after"] = _read_json(runtime_paths["after"])
        result["claim"] = _read_json(runtime_paths["result"])
        result["wire"] = _read_json(runtime_paths["wire"])
        result["claim_result_present"] = runtime_paths["result"].is_file()
        proof, proof_reason = verify_effect(result["before"], result["after"], result["claim"])
        result["proof_confirmed"] = proof
        result["proof_reason"] = proof_reason
    finally:
        _stop_game()
        _restore_from_backup(paths, backup)
        after_hashes = _hashes(paths)
        result["restored"] = True
        result["before_sha256"] = before_hashes
        result["after_sha256"] = after_hashes
        result["restore_hash_match"] = before_hashes == after_hashes

    if not result["restore_hash_match"]:
        raise InstallRefused("original game files did not restore to exact pre-run hashes")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate-dir", type=Path, required=True)
    parser.add_argument("--timeout-seconds", type=int, default=90)
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
