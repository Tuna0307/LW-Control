"""Read-only, fail-closed identity gate for the supported Overview recovery helper.

This program NEVER acquires the operation lease, rewrites a journal, copies a
script, or launches a game. A PASS is a bounded precondition snapshot, not a
restoration receipt or a guarantee against changes after the check. The sole
live controller must rerun it immediately before the normal recovery helper.

Only constant error codes, booleans, and content hashes are printed. Session,
profile, challenge, game/account information and private paths stay local.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys


ROLES = {"data": "LWScripts.data", "metadata": "LWScripts.txt", "version": "version.txt"}
PROCESS_NAMES = {"lastwar.exe", "lastwarlauncher.exe", "lwbridge.desktop.exe",
                 "lwbridge.gamepipeadapter.exe"}
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
HEX32 = re.compile(r"[0-9a-f]{32}\Z")


class GateError(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


def require(condition: bool, code: str) -> None:
    if not condition:
        raise GateError(code)


def digest(path: Path) -> str:
    sha = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                sha.update(block)
    except (OSError, ValueError) as exc:
        raise GateError("FILE_UNREADABLE") from exc
    return sha.hexdigest()


def read_json(path: Path) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError, UnicodeError) as exc:
        raise GateError("RECORD_MISSING_OR_INVALID") from exc
    require(isinstance(value, dict), "RECORD_NOT_OBJECT")
    return value


def same_path(actual: object, expected: Path) -> bool:
    if not isinstance(actual, str) or not actual or not Path(actual).is_absolute():
        return False
    try:
        return os.path.normcase(os.path.abspath(actual)) == os.path.normcase(str(expected.resolve(strict=True)))
    except (OSError, ValueError):
        return False


def process_inventory() -> list[dict]:
    """A complete, error-checked native inventory. Unknown process metadata blocks."""
    if sys.platform != "win32":
        raise GateError("WINDOWS_PROCESS_INVENTORY_REQUIRED")
    script = (
        "$ErrorActionPreference='Stop';"
        "$rows=@(Get-CimInstance Win32_Process | Where-Object {"
        "$_.Name -in @('LastWar.exe','LastWarLauncher.exe',"
        "'LWBridge.Desktop.exe','LWBridge.GamePipeAdapter.exe') } | "
        "Select-Object Name,ProcessId,ExecutablePath);"
        "ConvertTo-Json -InputObject $rows -Compress -Depth 3"
    )
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True, text=True, timeout=25, check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise GateError("PROCESS_INVENTORY_UNAVAILABLE") from exc
    require(result.returncode == 0, "PROCESS_INVENTORY_FAILED")
    try:
        inventory = json.loads(result.stdout)
    except (ValueError, TypeError) as exc:
        raise GateError("PROCESS_INVENTORY_INVALID") from exc
    require(isinstance(inventory, list), "PROCESS_INVENTORY_INVALID")
    require(all(isinstance(item, dict) and isinstance(item.get("Name"), str)
                and isinstance(item.get("ProcessId"), int) for item in inventory),
            "PROCESS_INVENTORY_INVALID")
    require(all(item["Name"].lower() in PROCESS_NAMES for item in inventory),
            "PROCESS_INVENTORY_INVALID")
    return inventory


def verify(data_root: Path, game_root: Path, scripts_root: Path,
           *, inventory: list[dict] | None = None) -> dict:
    """Validate exact local ownership/backup originals without any writes."""
    data_root = data_root.resolve(strict=True)
    game_root = game_root.resolve(strict=True)
    scripts_root = scripts_root.resolve(strict=True)
    runtime = data_root / "overview-bridge"
    journal_path = runtime / "recovery.json"
    starting_journal_hash = digest(journal_path)
    journal = read_json(journal_path)
    config = read_json(data_root / "config.json")
    require(journal.get("schemaVersion") == 1 and
            journal.get("stage") == "active_ready_deferred_restore",
            "UNQUALIFIED_JOURNAL_STATE")
    require(isinstance(config.get("profileId"), str) and config["profileId"] and
            journal.get("profileId") == config["profileId"], "PROFILE_MISMATCH")
    session = journal.get("sessionId")
    require(isinstance(session, str) and HEX32.fullmatch(session) is not None and
            journal.get("requestId") == session, "SESSION_MISMATCH")
    pid = journal.get("gamePid")
    require(type(pid) is int and pid > 0 and
            isinstance(journal.get("gameStartedAtUtc"), str) and
            journal["gameStartedAtUtc"], "GAME_IDENTITY_INVALID")
    game_exe = game_root / "Game" / "LastWar.exe"
    require(game_exe.is_file() and (game_root / "LastWarLauncher.exe").is_file() and
            same_path(journal.get("gamePath"), game_exe), "SELECTED_GAME_MISMATCH")

    backup_root = (data_root / "overview-bridge-backups").resolve(strict=True)
    raw_backup = journal.get("backupPath")
    require(isinstance(raw_backup, str) and Path(raw_backup).is_absolute(),
            "BACKUP_PATH_INVALID")
    try:
        backup = Path(raw_backup).resolve(strict=True)
    except (OSError, ValueError) as exc:
        raise GateError("BACKUP_PATH_INVALID") from exc
    require(backup.is_dir() and backup.parent == backup_root and
            not Path(raw_backup).is_symlink(), "BACKUP_PATH_MISMATCH")
    manifest = read_json(backup / "manifest.json")
    originals = journal.get("originalFiles")
    require(isinstance(originals, dict) and set(originals) == set(ROLES),
            "ORIGINAL_RECORD_INVALID")
    require(manifest.get("schemaVersion") == 1 and
            manifest.get("state") == "backup_ready" and
            manifest.get("originalFiles") == originals, "BACKUP_MANIFEST_MISMATCH")
    for key, name in ROLES.items():
        entry = originals[key]
        require(isinstance(entry, dict) and set(entry) == {"path", "size", "sha256"} and
                isinstance(entry.get("sha256"), str) and
                HEX64.fullmatch(entry["sha256"]) is not None and
                type(entry.get("size")) is int and entry["size"] > 0,
                "ORIGINAL_RECORD_INVALID")
        installed = scripts_root / name
        saved = backup / name
        require(installed.is_file() and saved.is_file() and
                same_path(entry["path"], installed) and
                not installed.is_symlink() and not saved.is_symlink(),
                "ORIGINAL_PATH_MISMATCH")
        require(installed.stat().st_size == entry["size"] and
                saved.stat().st_size == entry["size"] and
                digest(installed) == entry["sha256"] and
                digest(saved) == entry["sha256"],
                "ORIGINAL_HASH_MISMATCH")
    candidate = journal.get("candidate")
    require(isinstance(candidate, dict) and isinstance(candidate.get("packageSha256"), str) and
            HEX64.fullmatch(candidate["packageSha256"]) is not None and
            candidate["packageSha256"] != originals["data"]["sha256"],
            "CANDIDATE_IDENTITY_INVALID")
    challenge_digest = journal.get("challengeSha256")
    require(isinstance(challenge_digest, str) and HEX64.fullmatch(challenge_digest) is not None,
            "CHALLENGE_IDENTITY_INVALID")

    session_dir = data_root / "overview-evidence" / session
    helper = read_json(session_dir / "helper-start.json")
    host = read_json(session_dir / "host-start.json")
    for record in (helper, host):
        require(record.get("sessionId") == session and
                record.get("profileId") == journal["profileId"] and
                record.get("gamePid") == pid and
                same_path(record.get("gamePath"), game_exe) and
                record.get("gameStartedAtUtc") == journal["gameStartedAtUtc"],
                "START_RECEIPT_IDENTITY_MISMATCH")
    require(helper.get("ok") is True and helper.get("installedFilesChanged") is True and
            helper.get("challengeSha256") == challenge_digest and
            isinstance(helper.get("candidate"), dict) and
            helper["candidate"].get("packageSha256") == candidate["packageSha256"] and
            isinstance(helper.get("currentClient"), dict) and
            helper["currentClient"].get("packageSha256") == originals["data"]["sha256"] and
            isinstance(helper.get("restore"), dict) and
            helper["restore"].get("stage") == journal["stage"] and
            helper["restore"].get("deferred") is True,
            "HELPER_START_INCONSISTENT")
    require(host.get("schemaVersion") == 1, "HOST_START_INVALID")
    adoption = read_json(runtime / "adoption.json")
    require(adoption.get("schemaVersion") == 1 and
            adoption.get("sessionId") == session and adoption.get("instanceId") == session and
            adoption.get("profileId") == journal["profileId"] and adoption.get("pid") == pid and
            same_path(adoption.get("gameExecutable"), game_exe) and
            adoption.get("processCreatedAt") == journal["gameStartedAtUtc"] and
            adoption.get("leaseRequired") is True, "ADOPTION_IDENTITY_MISMATCH")
    challenge = adoption.get("challenge")
    require(isinstance(challenge, str) and
            hashlib.sha256(challenge.encode("utf-8")).hexdigest() == challenge_digest,
            "CHALLENGE_IDENTITY_MISMATCH")
    for filename in ("ready.json", "heartbeat.json"):
        runtime_record = read_json(runtime / filename)
        require(runtime_record.get("schemaVersion") == 1 and
                runtime_record.get("sessionId") == session and
                runtime_record.get("profileId") == journal["profileId"] and
                runtime_record.get("gamePid") == pid and
                runtime_record.get("challenge") == challenge,
                "RUNTIME_IDENTITY_MISMATCH")
    require(not (runtime / "lease.txt").exists() and
            not (runtime / "operation-owner.json").exists(), "ACTIVE_LEASE_PRESENT")
    processes = process_inventory() if inventory is None else inventory
    require(isinstance(processes, list) and not processes, "TARGET_PROCESS_PRESENT")
    require(digest(journal_path) == starting_journal_hash,
            "JOURNAL_CHANGED_DURING_INSPECTION")
    return {
        "status": "READY_FOR_SUPPORTED_RECOVERY",
        "readOnly": True,
        "journalSha256": starting_journal_hash,
        "ownerChecksPassed": True,
        "startReceiptsCorrelated": True,
        "threeOriginalsVerified": True,
        "candidateIsInstalled": False,
        "targetProcessesPresent": False,
        "activeLeasePresent": False,
        "restorationPerformed": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-root", type=Path)
    parser.add_argument("--game-root", type=Path)
    parser.add_argument("--scripts-root", type=Path)
    args = parser.parse_args()
    try:
        local = Path(os.environ["LOCALAPPDATA"])
        profile = Path(os.environ["USERPROFILE"])
        data_root = args.data_root or local / "LWBridgeRebuild"
        game_root = args.game_root or local / "FunFly" / "Last War-Survival Game"
        scripts_root = args.scripts_root or profile / "AppData" / "LocalLow" / "FunFly" / "Last War-Survival Game" / "lwScripts"
        if os.environ.get("LWBRIDGE_REBUILD_DATA_ROOT"):
            require(Path(os.environ["LWBRIDGE_REBUILD_DATA_ROOT"]).resolve() == data_root.resolve(),
                    "HELPER_DATA_ROOT_OVERRIDE_MISMATCH")
        result = verify(data_root, game_root, scripts_root)
        print(json.dumps(result, sort_keys=True))
        return 0
    except GateError as exc:
        print(json.dumps({"status": "BLOCKED", "code": exc.code,
                          "readOnly": True, "restorationPerformed": False}, sort_keys=True))
        return 2
    except (OSError, ValueError, KeyError, TypeError) as exc:
        # Never put a raw path, request ID, challenge or private error text on stdout.
        print(json.dumps({"status": "BLOCKED", "code": "UNEXPECTED_LOCAL_STATE",
                          "readOnly": True, "restorationPerformed": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
