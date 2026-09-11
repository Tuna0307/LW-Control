#!/usr/bin/env python3
"""Bounded Overview launch/ready/close helper for the verified current Last War client.

This helper deliberately reuses the already live-proven v14 LuaEntry install/launch/
restore mechanics.  Its session challenge, host lease, ready files and game-side
message are LWBridge rebuild IMPLEMENTATION POLICY; they are not presented as the
unrecovered original LWBridge launch-proof or named-pipe grammar.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import tempfile
import time
import uuid

HERE = Path(__file__).resolve().parent
_RESOURCE_HELPER = HERE / "run_live_resource_probe.py"
_spec = importlib.util.spec_from_file_location("lwbridge_live_resource_probe", _RESOURCE_HELPER)
if _spec is None or _spec.loader is None:
    raise RuntimeError("could not load the shared live-resource helper")
lr = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(lr)

BRIDGE_VERSION = "lwbridge-overview-bridge-1"
LUA_ENTRY = lr.LUA_ENTRY
ORIGINAL_LUA_ENTRY = lr.ORIGINAL_LUA_ENTRY
MESSAGE = "LWbridge is running"

class OverviewBridgeError(RuntimeError):
    pass


def overview_paths(game_root: str | Path | None = None) -> dict[str, Path]:
    p = dict(lr.paths(game_root))
    local = Path(os.environ["LOCALAPPDATA"]).resolve()
    p["runtime"] = local / "LWBridgeRebuild" / "overview-bridge"
    p["backup_root"] = local / "LWBridgeRebuild" / "overview-bridge-backups"
    p["evidence_root"] = local / "LWBridgeRebuild" / "overview-evidence"
    return p


def bridge_source() -> bytes:
    data = (HERE / "current_overview_bridge.lua").read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        raise OverviewBridgeError("Overview bridge source must not contain a UTF-8 BOM")
    return data


def wrapper_source() -> bytes:
    prefix = b'''-- LWBRIDGE_OVERVIEW_LOADER\nlocal unpack_values = table.unpack or unpack\nlocal ok_original, original = pcall(require, "DataCenter.Global.LuaEntry_original")\nif not ok_original then error(original) end\nlocal bridge = (function()\n'''
    suffix = b'''\nend)()\nif type(bridge) ~= "table" then error("embedded overview bridge did not return a table") end\nlocal function pump_bridge()\n    if type(bridge.Register) == "function" then pcall(bridge.Register) end\n    if type(bridge.Pump) == "function" then pcall(bridge.Pump) end\nend\nlocal function wrap(name)\n    if type(original) ~= "table" or type(original[name]) ~= "function" then return end\n    local previous = original[name]\n    original[name] = function(...)\n        local values = { pcall(previous, ...) }\n        local ok = table.remove(values, 1)\n        pump_bridge()\n        if not ok then error(values[1]) end\n        return unpack_values(values)\n    end\nend\nfor _, method in ipairs({"init", "__InitCModule", "Async_Init", "Async_Update", "AsyncUpdate", "Update", "LateUpdate"}) do wrap(method) end\nrawset(_G, "LWBridgeOverviewBridge", bridge)\npump_bridge()\nreturn original\n'''
    return prefix + bridge_source() + suffix


def make_candidate(p: dict[str, Path], directory: Path) -> dict[str, object]:
    file_version, content_version, entries = lr.read_lwlf(p["data"])
    mapped = dict(entries)
    official = mapped.get(LUA_ENTRY)
    if official is None:
        raise OverviewBridgeError("current package is missing the official LuaEntry")
    if ORIGINAL_LUA_ENTRY in mapped:
        raise OverviewBridgeError("current package already contains the preserved LuaEntry marker")
    wrapper_plain = wrapper_source()
    wrapper = lr.encode_lenc(wrapper_plain)
    if lr.decode_lenc(wrapper) != wrapper_plain:
        raise OverviewBridgeError("Overview LuaEntry LENC round-trip failed")
    mapped[LUA_ENTRY] = wrapper
    output = [(name, mapped[name]) for name, _ in entries]
    output.append((ORIGINAL_LUA_ENTRY, official))
    data = directory / "LWScripts.data"
    metadata = directory / "LWScripts.txt"
    version = directory / "version.txt"
    lr.write_lwlf(data, file_version, content_version, output)
    verify_version, verify_content, verify_entries = lr.read_lwlf(data)
    verify_map = dict(verify_entries)
    if (verify_version, verify_content) != (file_version, content_version):
        raise OverviewBridgeError("Overview candidate LWLF header failed round-trip")
    if verify_map.get(ORIGINAL_LUA_ENTRY) != official or lr.decode_lenc(verify_map[LUA_ENTRY]) != wrapper_plain:
        raise OverviewBridgeError("Overview candidate LuaEntry preservation/serialization verification failed")
    package_crc = lr.crc32_file(data)
    metadata.write_text(f"{data.stat().st_size}|{package_crc}", encoding="utf-8")
    version.write_text(str(content_version), encoding="utf-8")
    return {
        "packageSha256": lr.sha256_file(data),
        "packageSize": data.stat().st_size,
        "packageCrc32": package_crc,
        "bridgeSourceSha256": hashlib.sha256(bridge_source()).hexdigest(),
        "wrapperPlaintextSha256": hashlib.sha256(wrapper_plain).hexdigest(),
        "entryCount": len(verify_entries),
    }


def require_token(value: str | None, field: str) -> str:
    text = value or ""
    if not text or len(text) > 128 or any(not (ch.isalnum() or ch in "_-") for ch in text):
        raise OverviewBridgeError(f"{field} is invalid")
    return text




def sanitize_evidence(value):
    if isinstance(value, dict):
        result = {}
        for key, item in value.items():
            if key == "challenge" and isinstance(item, str):
                result["challengeSha256"] = hashlib.sha256(item.encode("utf-8")).hexdigest()
            else:
                result[key] = sanitize_evidence(item)
        return result
    if isinstance(value, list):
        return [sanitize_evidence(item) for item in value]
    return value


def write_session_evidence(p: dict[str, Path], session_id: str, name: str, payload: dict[str, object]) -> str:
    session = require_token(session_id, "sessionId")
    directory = p["evidence_root"] / session
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / name
    lr.write_json_atomic(path, sanitize_evidence(payload))
    return str(path)

def write_kv_atomic(path: Path, values: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + ".tmp-" + uuid.uuid4().hex)
    temp.write_text("".join(f"{key}={value}\n" for key, value in values.items()), encoding="utf-8")
    os.replace(temp, path)


def write_control(p: dict[str, Path], profile_id: str, session_id: str, challenge: str, game_pid: int) -> None:
    common = {
        "schema": 1,
        "bridgeVersion": BRIDGE_VERSION,
        "profileId": require_token(profile_id, "profileId"),
        "sessionId": require_token(session_id, "sessionId"),
        "challenge": require_token(challenge, "challenge"),
        "gamePid": int(game_pid),
    }
    write_kv_atomic(p["runtime"] / "control.txt", common)
    write_lease(p, session_id, challenge)


def write_lease(p: dict[str, Path], session_id: str, challenge: str) -> None:
    write_kv_atomic(p["runtime"] / "lease.txt", {
        "schema": 1,
        "bridgeVersion": BRIDGE_VERSION,
        "sessionId": require_token(session_id, "sessionId"),
        "challenge": require_token(challenge, "challenge"),
        "updatedAt": int(time.time()),
    })


def clear_stale_runtime(p: dict[str, Path]) -> None:
    p["runtime"].mkdir(parents=True, exist_ok=True)
    for name in ("control.txt", "lease.txt", "ready.json", "heartbeat.json"):
        try:
            (p["runtime"] / name).unlink()
        except FileNotFoundError:
            pass


def read_json(path: Path) -> dict[str, object] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else None
    except (OSError, json.JSONDecodeError):
        return None


def await_ready(
    p: dict[str, Path], profile_id: str, session_id: str, challenge: str,
    game_pid: int, deadline: float,
) -> dict[str, object]:
    ready_path = p["runtime"] / "ready.json"
    started_epoch = int(time.time()) - 2
    while time.monotonic() < deadline:
        value = read_json(ready_path)
        if value is not None:
            if (
                value.get("schemaVersion") == 1 and
                value.get("bridgeVersion") == BRIDGE_VERSION and
                value.get("profileId") == profile_id and
                value.get("sessionId") == session_id and
                value.get("challenge") == challenge and
                value.get("gamePid") == game_pid and
                value.get("ready") is True and
                value.get("messageVisible") is True and
                value.get("messageText") == MESSAGE and
                isinstance(value.get("readyAt"), (int, float)) and
                int(value["readyAt"]) >= started_epoch
            ):
                return value
        write_lease(p, session_id, challenge)
        time.sleep(0.25)
    heartbeat = read_json(p["runtime"] / "heartbeat.json")
    detail = heartbeat.get("error") if isinstance(heartbeat, dict) else None
    suffix = f" ({detail})" if detail else ""
    raise OverviewBridgeError("same-session game-side bridge readiness did not arrive before timeout" + suffix)


def run_start(
    profile_id: str, session_id: str, challenge: str,
    timeout_seconds: int, game_root: str | Path | None,
) -> dict[str, object]:
    p = overview_paths(game_root)
    owner = {
        "schemaVersion": 1,
        "operation": "overview_start",
        "profileId": profile_id,
        "sessionId": session_id,
        "helperPid": os.getpid(),
        "startedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    p["runtime"].mkdir(parents=True, exist_ok=True)
    with lr.OperationLease(p["runtime"], owner):
        # Never attempt to overwrite a journaled candidate while the selected game
        # still owns the script files. Recovery is safe only after no selected game exists.
        lr.require_no_selected_game_process(p)
        interrupted_recovery = lr.recover_pending(p)
        current = lr.verify_current(p)
        backup = lr.make_backup(p)
        recovery_state = lr.arm_recovery(p, backup, session_id)
        candidate_root = Path(tempfile.mkdtemp(prefix="lwbridge-overview-"))
        recovery_armed = True
        candidate_info: dict[str, object] | None = None
        owned_game: dict[str, object] | None = None
        try:
            candidate_info = make_candidate(p, candidate_root)
            lr.install_candidate(
                p, candidate_root,
                lambda index, key: lr.update_recovery_stage(p, recovery_state, f"installed_{index}_{key}"),
            )
            clear_stale_runtime(p)
            launch_started = time.monotonic()
            launcher_process = lr.subprocess.Popen([str(p["launcher"])], cwd=str(p["launcher"].parent))
            deadline = launch_started + timeout_seconds
            owned_game = lr.await_owned_game_process(p, deadline)
            write_control(p, profile_id, session_id, challenge, int(owned_game["pid"]))
            ready = await_ready(p, profile_id, session_id, challenge, int(owned_game["pid"]), deadline)

            # IMPLEMENTATION POLICY: Windows keeps the active script package locked while
            # LastWar is running. Preserve the exact originals in the recovery journal and
            # defer restoration until Overview Close has released that exact owned process.
            recovery_state.update({
                "profileId": profile_id,
                "sessionId": session_id,
                "challengeSha256": hashlib.sha256(challenge.encode("utf-8")).hexdigest(),
                "gamePid": int(owned_game["pid"]),
                "gamePath": os.path.abspath(str(owned_game["path"])),
                "candidate": candidate_info,
            })
            lr.update_recovery_stage(p, recovery_state, "active_ready_deferred_restore")
            recovery_armed = False
            result = {
                "ok": True,
                "mode": "overview_install_launch_ready_deferred_restore",
                "bridgeVersion": BRIDGE_VERSION,
                "profileId": profile_id,
                "sessionId": session_id,
                "challengeSha256": hashlib.sha256(challenge.encode("utf-8")).hexdigest(),
                "gamePid": owned_game["pid"],
                "gamePath": owned_game["path"],
                "gameStartedAtUtc": owned_game.get("startedAtUtc"),
                "launcherPid": launcher_process.pid,
                "readyPath": str(p["runtime"] / "ready.json"),
                "heartbeatPath": str(p["runtime"] / "heartbeat.json"),
                "leasePath": str(p["runtime"] / "lease.txt"),
                "ready": ready,
                "currentClient": current,
                "candidate": candidate_info,
                "restore": {
                    "restored": False,
                    "deferred": True,
                    "stage": "active_ready_deferred_restore",
                    "backupPath": str(backup),
                    "originalFiles": recovery_state["originalFiles"],
                },
                "gameRunning": any(item.get("pid") == owned_game["pid"] for item in lr.selected_game_processes(p)),
                "installedFilesChanged": True,
                "interruptedRecovery": interrupted_recovery,
            }
            result["evidencePath"] = write_session_evidence(p, session_id, "helper-start.json", result)
            return result
        except Exception as run_error:
            try:
                write_session_evidence(p, session_id, "helper-start-error.json", {
                    "ok": False,
                    "mode": "overview_start_error",
                    "bridgeVersion": BRIDGE_VERSION,
                    "profileId": profile_id,
                    "sessionId": session_id,
                    "error": str(run_error),
                    "errorType": type(run_error).__name__,
                    "recordedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                })
            except Exception:
                pass
            if recovery_armed:
                first_restore_error: Exception | None = None
                try:
                    lr.update_recovery_stage(p, recovery_state, "restoring_after_failure")
                    lr.restore_backup(p, backup)
                except Exception as restore_error:
                    first_restore_error = restore_error

                close_error: Exception | None = None
                if owned_game is not None:
                    try:
                        if any(item.get("pid") == owned_game.get("pid") for item in lr.selected_game_processes(p)):
                            lr.update_recovery_stage(p, recovery_state, "closing_failed_owned_game")
                            lr.close_owned_game_process_for_restore(p, owned_game)
                    except Exception as exc:
                        close_error = exc

                retry_restore_error: Exception | None = None
                if first_restore_error is not None and close_error is None:
                    try:
                        lr.update_recovery_stage(p, recovery_state, "restoring_after_failed_owned_game_close")
                        lr.restore_backup(p, backup)
                        first_restore_error = None
                    except Exception as exc:
                        retry_restore_error = exc

                if first_restore_error is None and close_error is None:
                    lr.clear_recovery(p, recovery_state)
                    recovery_armed = False
                    clear_stale_runtime(p)
                else:
                    details = []
                    if first_restore_error is not None:
                        details.append(f"initial restore failed: {first_restore_error}")
                    if close_error is not None:
                        details.append(f"exact-PID normal close failed: {close_error}")
                    if retry_restore_error is not None:
                        details.append(f"restore retry failed: {retry_restore_error}")
                    raise OverviewBridgeError(
                        f"Overview start failed: {run_error}; cleanup incomplete: {'; '.join(details)}"
                    ) from run_error
            raise
        finally:
            shutil.rmtree(candidate_root, ignore_errors=True)


def run_stop(
    profile_id: str,
    session_id: str,
    game_pid: int,
    game_path: str,
    game_root: str | Path | None,
) -> dict[str, object]:
    p = overview_paths(game_root)
    expected = os.path.abspath(os.fspath(p["game"]))
    supplied = os.path.abspath(game_path)
    profile_id = require_token(profile_id, "profileId")
    session_id = require_token(session_id, "sessionId")
    if os.path.normcase(expected) != os.path.normcase(supplied):
        raise OverviewBridgeError("owned game path does not match the selected installation")
    owner = {
        "schemaVersion": 1,
        "operation": "overview_stop",
        "profileId": profile_id,
        "sessionId": session_id,
        "gamePid": game_pid,
        "helperPid": os.getpid(),
        "startedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    with lr.OperationLease(p["runtime"], owner):
        state = lr.read_json(lr.recovery_path(p))
        if not isinstance(state, dict):
            raise OverviewBridgeError("active Overview recovery journal is missing")
        allowed_stages = {
            "active_ready_deferred_restore",
            "closing_owned_game_for_restore",
            "restoring_after_owned_game_exit",
        }
        if state.get("stage") not in allowed_stages:
            raise OverviewBridgeError("Overview recovery journal is not an active/retryable ready session")
        if state.get("requestId") != session_id or state.get("sessionId") != session_id or state.get("profileId") != profile_id:
            raise OverviewBridgeError("Overview recovery journal does not match the requested session/profile")
        if state.get("gamePid") != game_pid:
            raise OverviewBridgeError("Overview recovery journal does not match the requested game PID")
        recorded_path = state.get("gamePath")
        if not isinstance(recorded_path, str) or os.path.normcase(os.path.abspath(recorded_path)) != os.path.normcase(supplied):
            raise OverviewBridgeError("Overview recovery journal does not match the requested game path")
        backup_path = state.get("backupPath")
        if not isinstance(backup_path, str) or not backup_path:
            raise OverviewBridgeError("Overview recovery journal is missing its exact backup path")

        selected = lr.selected_game_processes(p)
        if len(selected) > 1:
            raise OverviewBridgeError("multiple selected LastWar processes exist during Overview Close")
        if len(selected) == 1:
            current = selected[0]
            if current.get("pid") != game_pid or os.path.normcase(os.path.abspath(str(current.get("path", "")))) != os.path.normcase(supplied):
                raise OverviewBridgeError("selected LastWar identity changed before Overview Close")
            lr.update_recovery_stage(p, state, "closing_owned_game_for_restore")
            close = lr.close_owned_game_process_for_restore(p, {"pid": game_pid, "path": supplied})
            already_exited = False
        else:
            close = {
                "method": "already_exited",
                "pid": game_pid,
                "path": supplied,
                "accepted": False,
                "processExited": True,
                "alreadyExited": True,
            }
            already_exited = True

        lr.update_recovery_stage(p, state, "restoring_after_owned_game_exit")
        restored = lr.restore_backup(p, Path(backup_path))
        lr.clear_recovery(p, state)
        for name in ("lease.txt", "control.txt", "ready.json", "heartbeat.json"):
            try:
                (p["runtime"] / name).unlink()
            except FileNotFoundError:
                pass
        result = {
            "ok": True,
            "mode": "overview_exact_pid_close_restore",
            "bridgeVersion": BRIDGE_VERSION,
            "profileId": profile_id,
            "sessionId": session_id,
            "gamePid": game_pid,
            "gamePath": supplied,
            "close": close,
            "alreadyExited": already_exited,
            "restore": restored,
            "gameRunning": any(item.get("pid") == game_pid for item in lr.selected_game_processes(p)),
            "installedFilesChanged": False,
        }
        result["evidencePath"] = write_session_evidence(p, session_id, "helper-stop.json", result)
        summary = {
            "schemaVersion": 1,
            "status": "COMPLETE",
            "bridgeVersion": BRIDGE_VERSION,
            "profileId": profile_id,
            "sessionId": session_id,
            "gamePid": game_pid,
            "gamePath": supplied,
            "close": result["close"],
            "restore": restored,
            "gameRunning": result["gameRunning"],
            "installedFilesChanged": False,
            "completedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
        }
        result["summaryPath"] = write_session_evidence(p, session_id, "attempt-summary.json", summary)
        return result


def run_check_only(game_root: str | Path | None) -> dict[str, object]:
    p = overview_paths(game_root)
    candidate_root = Path(tempfile.mkdtemp(prefix="lwbridge-overview-check-"))
    try:
        current = lr.verify_current(p)
        candidate = make_candidate(p, candidate_root)
        return {
            "ok": True,
            "mode": "check_only",
            "bridgeVersion": BRIDGE_VERSION,
            "currentClient": current,
            "candidate": candidate,
            "installedFilesChanged": False,
        }
    finally:
        shutil.rmtree(candidate_root, ignore_errors=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    check = sub.add_parser("check-only")
    check.add_argument("--game-root")
    start = sub.add_parser("start")
    start.add_argument("--profile-id", required=True)
    start.add_argument("--session-id", required=True)
    start.add_argument("--challenge", required=True)
    start.add_argument("--timeout-seconds", type=int, default=120)
    start.add_argument("--game-root")
    stop = sub.add_parser("stop")
    stop.add_argument("--profile-id", required=True)
    stop.add_argument("--session-id", required=True)
    stop.add_argument("--game-pid", type=int, required=True)
    stop.add_argument("--game-path", required=True)
    stop.add_argument("--game-root")
    args = parser.parse_args()
    try:
        if args.command == "check-only":
            result = run_check_only(args.game_root)
        elif args.command == "start":
            if args.timeout_seconds < 10 or args.timeout_seconds > 180:
                raise OverviewBridgeError("timeout must be 10..180 seconds")
            result = run_start(
                require_token(args.profile_id, "profileId"),
                require_token(args.session_id, "sessionId"),
                require_token(args.challenge, "challenge"),
                args.timeout_seconds,
                args.game_root,
            )
        else:
            if args.game_pid <= 0:
                raise OverviewBridgeError("gamePid must be positive")
            result = run_stop(args.profile_id, args.session_id, args.game_pid, args.game_path, args.game_root)
    except Exception as exc:
        print(json.dumps({"ok": False, "error": str(exc), "errorType": type(exc).__name__}, separators=(",", ":")))
        return 2
    print(json.dumps(result, separators=(",", ":")))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
