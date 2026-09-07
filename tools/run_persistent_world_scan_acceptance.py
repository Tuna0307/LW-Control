"""Run one persistent World Scan command and leave Last War running."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
import time
import uuid

try:
    from .build_world_block_sender import build as build_world_block_sender
    from .install_loader_probe import InstallRefused, discover_paths, game_is_running, sha256_file
except ImportError:
    from build_world_block_sender import build as build_world_block_sender
    from install_loader_probe import InstallRefused, discover_paths, game_is_running, sha256_file


EXPECTED_VERSION = "lwcontrol-world-full-scan-probe-9"
EXPECTED_MVID_SIZE = 16
HEARTBEAT_MAX_AGE_SECONDS = 15
HEARTBEAT_FUTURE_TOLERANCE_SECONDS = 5


def _read_json(path: Path) -> dict | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else None
    except (OSError, UnicodeDecodeError, json.JSONDecodeError):
        return None


def _protected_hashes() -> dict[str, str]:
    paths = discover_paths()
    return {key: sha256_file(paths[key]) for key in ("data", "metadata", "version", "baseutils")}


def _coff_timestamp_offset(payload: bytes) -> int:
    if len(payload) < 0x40 or payload[:2] != b"MZ":
        raise InstallRefused("persistent World Scan sender helper is not a valid PE image")
    pe_offset = struct.unpack_from("<I", payload, 0x3C)[0]
    if pe_offset + 12 > len(payload) or payload[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise InstallRefused("persistent World Scan sender helper has an invalid PE header")
    return pe_offset + 8


def _normalized_sender_hash(
    payload: bytes,
    timestamp_offset: int,
    variable_start: int,
    variable_end: int,
) -> str:
    if timestamp_offset < 0 or timestamp_offset + 4 > len(payload):
        raise InstallRefused("persistent World Scan sender helper timestamp range is invalid")
    if variable_start < 0 or variable_end <= variable_start or variable_end > len(payload):
        raise InstallRefused("persistent World Scan sender helper MVID range is invalid")
    normalized = bytearray(payload)
    normalized[timestamp_offset:timestamp_offset + 4] = b"\0" * 4
    normalized[variable_start:variable_end] = b"\0" * (variable_end - variable_start)
    return hashlib.sha256(normalized).hexdigest()


def _derive_sender_identity(first: bytes, second: bytes) -> dict[str, object]:
    if len(first) != len(second):
        raise InstallRefused("two verified WorldBlockSender builds have different sizes")
    first_timestamp_offset = _coff_timestamp_offset(first)
    second_timestamp_offset = _coff_timestamp_offset(second)
    if first_timestamp_offset != second_timestamp_offset:
        raise InstallRefused("verified WorldBlockSender builds disagree on PE timestamp location")
    first_for_compare = bytearray(first)
    second_for_compare = bytearray(second)
    first_for_compare[first_timestamp_offset:first_timestamp_offset + 4] = b"\0" * 4
    second_for_compare[second_timestamp_offset:second_timestamp_offset + 4] = b"\0" * 4
    differences = [
        index
        for index, pair in enumerate(zip(first_for_compare, second_for_compare))
        if pair[0] != pair[1]
    ]
    if len(differences) != EXPECTED_MVID_SIZE:
        raise InstallRefused(
            "WorldBlockSender compiler nondeterminism is outside the expected 16-byte MVID"
        )
    variable_start = differences[0]
    variable_end = variable_start + EXPECTED_MVID_SIZE
    if differences != list(range(variable_start, variable_end)):
        raise InstallRefused(
            "WorldBlockSender compiler nondeterminism is not one contiguous 16-byte MVID"
        )
    first_hash = _normalized_sender_hash(
        first, first_timestamp_offset, variable_start, variable_end
    )
    second_hash = _normalized_sender_hash(
        second, second_timestamp_offset, variable_start, variable_end
    )
    if first_hash != second_hash:
        raise InstallRefused("verified WorldBlockSender builds disagree after MVID normalization")
    return {
        "normalized_sha256": first_hash,
        "coff_timestamp_offset": first_timestamp_offset,
        "mvid_offset": variable_start,
        "mvid_size": EXPECTED_MVID_SIZE,
    }


def _ensure_world_block_sender(root: Path) -> dict[str, object]:
    root.mkdir(parents=True, exist_ok=True)
    bridge_path = root / "WorldBlockSender.dll"
    with tempfile.TemporaryDirectory(prefix="lwcontrol-world-sender-") as temporary_dir:
        temporary_root = Path(temporary_dir)
        candidate = temporary_root / "first" / "WorldBlockSender.dll"
        second_candidate = temporary_root / "second" / "WorldBlockSender.dll"
        build = build_world_block_sender(candidate)
        second_build = build_world_block_sender(second_candidate)
        if build["source_sha256"] != second_build["source_sha256"]:
            raise InstallRefused("WorldBlockSender source changed between verification builds")
        candidate_payload = candidate.read_bytes()
        identity = _derive_sender_identity(candidate_payload, second_candidate.read_bytes())
        timestamp_offset = int(identity["coff_timestamp_offset"])
        variable_start = int(identity["mvid_offset"])
        variable_end = variable_start + int(identity["mvid_size"])
        expected_normalized_hash = str(identity["normalized_sha256"])
        if bridge_path.is_file():
            actual_payload = bridge_path.read_bytes()
            actual_hash = hashlib.sha256(actual_payload).hexdigest()
            if len(actual_payload) != len(candidate_payload) or _normalized_sender_hash(
                actual_payload, timestamp_offset, variable_start, variable_end
            ) != expected_normalized_hash:
                raise InstallRefused(
                    "persistent World Scan sender helper exists but does not match the current verified source after MVID normalization"
                )
            return {
                "path": str(bridge_path),
                "sha256": actual_hash,
                **identity,
                "source_sha256": build["source_sha256"],
                "staged": False,
            }
        bridge_path.write_bytes(candidate_payload)
        actual_hash = hashlib.sha256(bridge_path.read_bytes()).hexdigest()
        if actual_hash != build["sha256"]:
            bridge_path.unlink(missing_ok=True)
            raise InstallRefused("persistent World Scan sender helper did not stage with the verified hash")
        return {
            "path": str(bridge_path),
            "sha256": actual_hash,
            **identity,
            "source_sha256": build["source_sha256"],
            "staged": True,
        }


def run(
    timeout_seconds: int,
    power_target_limit: int | None = 96,
    resource_target_limit: int | None = 96,
) -> dict[str, object]:
    if timeout_seconds < 30 or timeout_seconds > 1200:
        raise InstallRefused("timeout must be between 30 and 1200 seconds")
    if power_target_limit is not None and (power_target_limit < 1 or power_target_limit > 50000):
        raise InstallRefused("power target limit must be between 1 and 50000")
    if resource_target_limit is not None and (resource_target_limit < 1 or resource_target_limit > 50000):
        raise InstallRefused("resource target limit must be between 1 and 50000")
    if not game_is_running():
        raise InstallRefused("LastWar.exe is not running; start the game before persistent World Scan")
    root = discover_paths()["runtime"]
    sender = _ensure_world_block_sender(root)
    before_hashes = _protected_hashes()
    heartbeat_path = root / "world-map-full-scan-heartbeat.json"
    command_path = root / "world-map-scan-command.txt"
    status_path = root / "world-map-full-scan-status.json"
    result_path = root / "world-map-full-scan-result.json"
    heartbeat = _read_json(heartbeat_path)
    heartbeat_updated = heartbeat.get("updated_at") if isinstance(heartbeat, dict) else None
    heartbeat_age = time.time() - heartbeat_updated if isinstance(heartbeat_updated, (int, float)) else None
    if not isinstance(heartbeat, dict) or heartbeat.get("version") != EXPECTED_VERSION \
            or heartbeat.get("persistent") is not True or not heartbeat.get("registrationMethod") \
            or heartbeat_age is None or heartbeat_age > HEARTBEAT_MAX_AGE_SECONDS \
            or heartbeat_age < -HEARTBEAT_FUTURE_TOLERANCE_SECONDS:
        raise InstallRefused("persistent World Scan heartbeat is not ready")
    if command_path.exists():
        raise InstallRefused("a World Scan command is already pending")

    command_id = f"accept-{uuid.uuid4().hex}"
    result_path.unlink(missing_ok=True)
    temporary = root / f".world-scan-accept-{uuid.uuid4().hex}.tmp"
    command_lines = ["schema=1", f"commandId={command_id}", "mode=run_once"]
    if power_target_limit is not None:
        command_lines.append(f"powerTargetLimit={power_target_limit}")
    if resource_target_limit is not None:
        command_lines.append(f"resourceTargetLimit={resource_target_limit}")
    temporary.write_text("\n".join(command_lines) + "\n", encoding="utf-8")
    os.replace(temporary, command_path)

    deadline = time.monotonic() + timeout_seconds
    latest_status = None
    while time.monotonic() < deadline:
        time.sleep(0.25)
        latest_status = _read_json(status_path)
        if isinstance(latest_status, dict) and latest_status.get("commandId") == command_id \
                and latest_status.get("completed") is True \
                and latest_status.get("state") != "captured":
            raise InstallRefused(
                f"persistent World Scan failed: {latest_status.get('error') or latest_status.get('state')}"
            )
        result = _read_json(result_path)
        if isinstance(result, dict) and result.get("commandId") == command_id:
            after_hashes = _protected_hashes()
            if before_hashes != after_hashes:
                raise InstallRefused("protected runtime hashes changed during persistent World Scan")
            return {
                "command_id": command_id,
                "state": result.get("state"),
                "records": result.get("accumulated_record_count"),
                "kind_counts": result.get("kind_counts"),
                "covered_blocks": result.get("covered_block_count"),
                "completed_batches": result.get("completed_batch_count"),
                "camera_moves": result.get("camera_move_count"),
                "camera_restored": result.get("camera_restored"),
                "response_hook_restored": result.get("response_hook_restored"),
                "manager_flag_restored": result.get("manager_flag_restored"),
                "world_response_flag_restored": result.get("world_response_flag_restored"),
                "player_power_enrichment": result.get("player_power_enrichment"),
                "resource_detail_enrichment": result.get("resource_detail_enrichment"),
                "world_block_sender": sender,
                "protected_hash_match": True,
                "protected_hashes": after_hashes,
                "game_running_after_scan": game_is_running(),
                "result_path": str(result_path),
            }
    raise InstallRefused(f"persistent World Scan timed out; latest status={latest_status}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout-seconds", type=int, default=720)
    parser.add_argument("--power-target-limit", type=int, default=96)
    parser.add_argument("--resource-target-limit", type=int, default=96)
    parser.add_argument(
        "--uncapped-details",
        action="store_true",
        help="exercise the product default with no player/resource detail target cap",
    )
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()
    try:
        result = run(
            args.timeout_seconds,
            None if args.uncapped_details else args.power_target_limit,
            None if args.uncapped_details else args.resource_target_limit,
        )
    except (InstallRefused, OSError, ValueError) as exc:
        payload = {"ok": False, "error": str(exc), "error_type": type(exc).__name__}
        print(json.dumps(payload, indent=2) if args.json else f"REFUSED: {exc}")
        return 2
    payload = {"ok": True, **result}
    print(json.dumps(payload, indent=2) if args.json else payload)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
