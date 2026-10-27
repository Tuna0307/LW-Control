"""Bounded exact-owner HOME connection-stability fault controller.

This controller is intentionally limited to an already-running, explicitly
isolated LW-Control instance whose HOME004 test surface was enabled with
LWBRIDGE_HOME004_ISOLATED_ROUTE_FAULT=1.  It never automates the UI and never
targets a process by image name.  Every destructive/pause action owns one
Windows process handle after validating the exact LastWar path and creation
FILETIME.  Ready-session actions additionally require adoption + recovery
journal agreement on profile/session/PID/path/start.

Live actions (run only from the designated live controller):
  check              Verify the current exact ready owner; no mutation.
  pause              Suspend the exact ready game briefly; always resume.
  exit-recovery      Terminate the exact ready game and wait for one healthy
                     automatic successor.
  stop               Call production profile_instance_stop for the exact owner
                     through the isolated request surface and prove cleanup.
  stop-reconnect     Drop only the exact authenticated route while retaining its
                     registration, immediately call production Stop, then prove
                     exit/restoration and absence of a successor.
  late-ready         Call production Start while idle, suspend the newly reported
                     exact game before ready.json, resume it after a bounded hold,
                     and prove the same PID/path/start/session becomes healthy.

No account identifier, challenge, pipe token, raw heartbeat, or raw Player.log
content is written to the sanitized result.  Session continuity is represented
only by a short SHA-256 digest.
"""

from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
from dataclasses import dataclass
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import time
from typing import Any

from home_stability_sampler import safe_json


REQUEST_NAME = "home004-drop-route.request.json"
SCRIPT_NAMES = ("LWScripts.data", "LWScripts.txt", "version.txt")
BRIDGE_VERSION = "lwbridge-overview-bridge-1"
QUERY_LIMITED = 0x1000
PROCESS_TERMINATE = 0x0001
PROCESS_SUSPEND_RESUME = 0x0800
SYNCHRONIZE = 0x00100000
WAIT_OBJECT_0 = 0
WAIT_TIMEOUT = 258


class FILETIME(ctypes.Structure):
    _fields_ = [("low", wintypes.DWORD), ("high", wintypes.DWORD)]


if sys.platform == "win32":
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    ntdll = ctypes.WinDLL("ntdll")
    kernel32.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = (wintypes.HANDLE,)
    kernel32.QueryFullProcessImageNameW.argtypes = (
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD))
    kernel32.GetProcessTimes.argtypes = (
        wintypes.HANDLE, ctypes.POINTER(FILETIME), ctypes.POINTER(FILETIME),
        ctypes.POINTER(FILETIME), ctypes.POINTER(FILETIME))
    kernel32.TerminateProcess.argtypes = (wintypes.HANDLE, wintypes.UINT)
    kernel32.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
    ntdll.NtSuspendProcess.argtypes = (wintypes.HANDLE,)
    ntdll.NtSuspendProcess.restype = wintypes.LONG
    ntdll.NtResumeProcess.argtypes = (wintypes.HANDLE,)
    ntdll.NtResumeProcess.restype = wintypes.LONG


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def normalized(path: str | Path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(path)))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def session_digest(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:20]


def filetime_ticks(value: FILETIME) -> int:
    return (int(value.high) << 32) | int(value.low)


def creation_ticks(value: str | None) -> int | None:
    if not value or not value.endswith("Z") or "T" not in value:
        return None
    try:
        head, dot, fraction = value[:-1].partition(".")
        if not dot or len(fraction) != 7 or not fraction.isdigit():
            return None
        instant = dt.datetime.fromisoformat(head).replace(tzinfo=dt.timezone.utc)
        epoch = dt.datetime(1601, 1, 1, tzinfo=dt.timezone.utc)
        delta = instant - epoch
        return ((delta.days * 86400 + delta.seconds) * 10_000_000 + int(fraction))
    except ValueError:
        return None


def safe_kv(path: Path) -> tuple[dict[str, str] | None, str]:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return None, "missing"
    except (OSError, UnicodeError):
        return None, "unavailable"
    if len(text) > 8192:
        return None, "oversize"
    values: dict[str, str] = {}
    for line in text.splitlines():
        if "=" not in line:
            return None, "invalid"
        key, value = line.split("=", 1)
        if not key or key in values:
            return None, "invalid"
        values[key] = value
    return values, "ok"


def load_config(root: Path, game_root: Path | None = None) -> dict[str, Any]:
    value, status = safe_json(root / "config.json")
    if status != "ok" or value is None:
        raise RuntimeError("isolated config.json is unavailable")
    if value.get("schemaVersion") != 1 or value.get("owner") != "LWBridgeRebuild":
        raise RuntimeError("isolated config does not have the LWBridge owner/schema")
    profile = value.get("profileId")
    if not isinstance(profile, str) or not profile:
        raise RuntimeError("isolated config has no profile identity")
    if game_root is not None:
        configured = value.get("gameRoot")
        if not isinstance(configured, str) or normalized(configured) != normalized(game_root):
            raise RuntimeError("isolated config gameRoot differs from the requested game root")
    return value


@dataclass(frozen=True)
class OwnedIdentity:
    profile: str
    session: str
    pid: int
    game_path: Path
    started_at: str
    recovery_backup: Path

    @property
    def started_ticks(self) -> int:
        value = creation_ticks(self.started_at)
        if value is None:
            raise RuntimeError("owned process creation timestamp is not canonical 100ns UTC")
        return value


def heartbeat_healthy(runtime: Path, identity: OwnedIdentity) -> bool:
    heartbeat, status = safe_json(runtime / "heartbeat.json")
    if status != "ok" or heartbeat is None:
        return False
    try:
        age = time.time() - (runtime / "heartbeat.json").stat().st_mtime
    except OSError:
        return False
    uid = heartbeat.get("gameUid")
    return (
        -1.0 <= age <= 5.0
        and heartbeat.get("bridgeVersion") == BRIDGE_VERSION
        and heartbeat.get("profileId") == identity.profile
        and heartbeat.get("sessionId") == identity.session
        and heartbeat.get("gamePid") == identity.pid
        and heartbeat.get("ready") is True
        and heartbeat.get("gameStateObserved") is True
        and heartbeat.get("gameReady") is True
        and heartbeat.get("loggedIn") is True
        and heartbeat.get("connected") is True
        and heartbeat.get("connecting") is False
        and isinstance(uid, str) and bool(uid)
        and isinstance(heartbeat.get("serverId"), int) and heartbeat["serverId"] > 0
        and isinstance(heartbeat.get("worldPos"), int) and heartbeat["worldPos"] > 0
    )


def read_owned_identity(root: Path, game_root: Path, require_healthy: bool = True) -> OwnedIdentity:
    runtime = root / "overview-bridge"
    config = load_config(root, game_root)
    adoption, adoption_status = safe_json(runtime / "adoption.json")
    recovery, recovery_status = safe_json(runtime / "recovery.json")
    if adoption_status != "ok" or adoption is None:
        raise RuntimeError("exact adoption record is unavailable")
    if recovery_status != "ok" or recovery is None:
        raise RuntimeError("exact restoration journal is unavailable")

    profile = config["profileId"]
    if adoption.get("profileId") != profile or recovery.get("profileId") != profile:
        raise RuntimeError("profile identity differs across config/adoption/recovery")
    session = adoption.get("instanceId") or adoption.get("sessionId")
    if not isinstance(session, str) or not session or recovery.get("sessionId") != session:
        raise RuntimeError("session identity differs across adoption/recovery")
    pid = adoption.get("pid") if isinstance(adoption.get("pid"), int) else adoption.get("gamePid")
    if not isinstance(pid, int) or pid <= 0 or recovery.get("gamePid") != pid:
        raise RuntimeError("game PID differs across adoption/recovery")
    started = adoption.get("processCreatedAt")
    if not isinstance(started, str) or recovery.get("gameStartedAtUtc") != started:
        raise RuntimeError("process creation identity differs across adoption/recovery")
    if creation_ticks(started) is None:
        raise RuntimeError("process creation identity is not canonical 100ns UTC")
    expected_game = game_root / "Game" / "LastWar.exe"
    adoption_path = adoption.get("gameExecutable")
    recovery_path = recovery.get("gamePath")
    if (not isinstance(adoption_path, str) or not isinstance(recovery_path, str) or
            normalized(adoption_path) != normalized(expected_game) or
            normalized(recovery_path) != normalized(expected_game)):
        raise RuntimeError("game path differs across selected root/adoption/recovery")
    backup = recovery.get("backupPath")
    if not isinstance(backup, str) or not backup:
        raise RuntimeError("recovery journal has no exact backup path")
    backup_path = Path(backup).resolve()
    if not backup_path.is_dir() or not isinstance(recovery.get("originalFiles"), dict):
        raise RuntimeError("recovery backup/original-file manifest is unavailable")
    if recovery.get("stage") != "active_ready_deferred_restore":
        raise RuntimeError("recovery journal is not the active ready deferred-restore owner")
    identity = OwnedIdentity(profile, session, pid, expected_game.resolve(), started, backup_path)
    if require_healthy and not heartbeat_healthy(runtime, identity):
        raise RuntimeError("exact owner does not have a fresh healthy game/account heartbeat")
    return identity


class ExactProcessHandle:
    def __init__(self, pid: int, expected_path: Path, expected_started_ticks: int | None,
                 *, suspend: bool = False, terminate: bool = False) -> None:
        if sys.platform != "win32":
            raise RuntimeError("exact process actions are Windows-only")
        rights = QUERY_LIMITED | SYNCHRONIZE
        if suspend:
            rights |= PROCESS_SUSPEND_RESUME
        if terminate:
            rights |= PROCESS_TERMINATE
        handle = kernel32.OpenProcess(rights, False, pid)
        if not handle:
            raise RuntimeError("exact owned PID handle could not be opened; no action")
        self.handle = handle
        self.pid = pid
        self.suspended = False
        try:
            image_buffer = ctypes.create_unicode_buffer(32768)
            length = wintypes.DWORD(len(image_buffer))
            if not kernel32.QueryFullProcessImageNameW(handle, 0, image_buffer, ctypes.byref(length)):
                raise RuntimeError("opened handle image identity is unavailable; no action")
            self.image_path = Path(image_buffer.value)
            if normalized(self.image_path) != normalized(expected_path):
                raise RuntimeError("opened handle belongs to a different executable; no action")
            created, exited, kernel_time, user_time = (FILETIME() for _ in range(4))
            if not kernel32.GetProcessTimes(
                    handle, ctypes.byref(created), ctypes.byref(exited),
                    ctypes.byref(kernel_time), ctypes.byref(user_time)):
                raise RuntimeError("opened handle creation identity is unavailable; no action")
            self.created_ticks = filetime_ticks(created)
            if expected_started_ticks is not None and self.created_ticks != expected_started_ticks:
                raise RuntimeError("opened handle creation identity changed; no action")
            if kernel32.WaitForSingleObject(handle, 0) == WAIT_OBJECT_0:
                raise RuntimeError("exact owned process already exited; no action")
        except Exception:
            kernel32.CloseHandle(handle)
            self.handle = None
            raise

    def __enter__(self) -> "ExactProcessHandle":
        return self

    def __exit__(self, exc_type, exc, tb) -> None:
        try:
            if self.suspended and not self.exited():
                self.resume()
        finally:
            if self.handle:
                kernel32.CloseHandle(self.handle)
                self.handle = None

    def exited(self) -> bool:
        return kernel32.WaitForSingleObject(self.handle, 0) == WAIT_OBJECT_0

    def wait_exit(self, timeout_seconds: float) -> bool:
        milliseconds = max(0, min(int(timeout_seconds * 1000), 0xFFFFFFFE))
        result = kernel32.WaitForSingleObject(self.handle, milliseconds)
        if result == WAIT_OBJECT_0:
            return True
        if result == WAIT_TIMEOUT:
            return False
        raise RuntimeError(f"WaitForSingleObject failed for exact owned handle: {result}")

    def suspend(self) -> None:
        if self.suspended:
            raise RuntimeError("exact process handle is already suspended")
        status = int(ntdll.NtSuspendProcess(self.handle))
        if status != 0:
            raise RuntimeError(f"exact owned process suspend failed: NTSTATUS {status}")
        self.suspended = True

    def resume(self) -> None:
        if not self.suspended:
            return
        status = int(ntdll.NtResumeProcess(self.handle))
        if status != 0:
            raise RuntimeError(f"exact owned process resume failed: NTSTATUS {status}")
        self.suspended = False

    def terminate(self) -> None:
        if not kernel32.TerminateProcess(self.handle, 0):
            raise RuntimeError("exact owned process terminate failed")


def ensure_output_outside(root: Path, output: Path) -> None:
    root = root.resolve()
    output = output.resolve()
    if output == root or root in output.parents:
        raise RuntimeError("sanitized output must be outside the isolated application root")


def write_result(output: Path, result: dict[str, Any]) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.name + f".tmp-{os.getpid()}")
    temporary.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, output)


def archive_previous_request(root: Path, output: Path) -> tuple[Path, Path, Path]:
    request = root / REQUEST_NAME
    consumed = Path(str(request) + ".consumed")
    receipt = Path(str(consumed) + ".result.json")
    if request.exists():
        raise RuntimeError("a HOME004 isolated request is already pending; preserve it and inspect before another action")
    existing = [path for path in (consumed, receipt) if path.exists()]
    if existing:
        history = output.parent / "home-stability-request-history"
        history.mkdir(parents=True, exist_ok=True)
        stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
        for path in existing:
            target = history / f"{stamp}-{path.name}"
            shutil.move(str(path), str(target))
    return request, consumed, receipt


def send_request(root: Path, output: Path, payload: dict[str, Any]) -> tuple[Path, Path]:
    request, consumed, receipt = archive_previous_request(root, output)
    temporary = request.with_name(request.name + f".tmp-{os.getpid()}")
    temporary.write_text(json.dumps(payload), encoding="utf-8")
    os.replace(temporary, request)
    return consumed, receipt


def wait_receipt(receipt: Path, timeout: float) -> dict[str, Any]:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        value, status = safe_json(receipt)
        if status == "ok" and value is not None:
            return value
        time.sleep(0.05)
    raise TimeoutError("isolated lifecycle/fault receipt was not produced before the bound")


def validate_backup(backup_dir: Path, scripts_root: Path) -> dict[str, str]:
    backup_dir = backup_dir.resolve(strict=True)
    scripts_root = scripts_root.resolve(strict=True)
    hashes: dict[str, str] = {}
    for name in SCRIPT_NAMES:
        backup = backup_dir / name
        installed = scripts_root / name
        if not backup.is_file() or not installed.is_file():
            raise RuntimeError(f"restoration oracle is missing {name}")
        backup_hash = sha256(backup)
        installed_hash = sha256(installed)
        if backup_hash != installed_hash or backup.stat().st_size != installed.stat().st_size:
            raise RuntimeError(f"installed official script was not restored exactly: {name}")
        hashes[name] = installed_hash
    return hashes


def wait_same_owner_healthy(root: Path, game_root: Path, expected: OwnedIdentity,
                            timeout: float) -> OwnedIdentity:
    deadline = time.monotonic() + timeout
    last_error = "unobserved"
    while time.monotonic() < deadline:
        try:
            current = read_owned_identity(root, game_root, require_healthy=True)
            if (current.session == expected.session and current.pid == expected.pid and
                    current.started_at == expected.started_at):
                with ExactProcessHandle(
                        current.pid, current.game_path, current.started_ticks):
                    return current
            last_error = "owner identity changed"
        except RuntimeError as error:
            last_error = str(error)
        time.sleep(0.25)
    raise TimeoutError("same exact owner did not return healthy: " + last_error)


def wait_successor(root: Path, game_root: Path, previous: OwnedIdentity,
                   timeout: float) -> OwnedIdentity:
    deadline = time.monotonic() + timeout
    last_error = "unobserved"
    while time.monotonic() < deadline:
        try:
            current = read_owned_identity(root, game_root, require_healthy=True)
            if current.session == previous.session and current.started_at == previous.started_at:
                last_error = "old owner still published"
            else:
                with ExactProcessHandle(
                        current.pid, current.game_path, current.started_ticks):
                    if current.started_ticks == previous.started_ticks:
                        raise RuntimeError("successor reused the old creation identity")
                    return current
        except RuntimeError as error:
            last_error = str(error)
        time.sleep(0.5)
    raise TimeoutError("healthy automatic successor was not observed: " + last_error)


def wait_cleanup(root: Path, timeout: float) -> None:
    runtime = root / "overview-bridge"
    deadline = time.monotonic() + timeout
    owner_files = (
        "adoption.json", "recovery.json", "lease.txt", "control.txt", "ready.json",
        "heartbeat.json", "registration-confirmed.txt", "game-reported.txt", "cancel-start.txt",
    )
    while time.monotonic() < deadline:
        config = load_config(root)
        if (config.get("gameDesiredRunning") is False and
                not any((runtime / name).exists() for name in owner_files)):
            return
        time.sleep(0.25)
    remaining = [name for name in owner_files if (runtime / name).exists()]
    raise TimeoutError("exact Stop cleanup did not settle; remaining runtime files: " + repr(remaining))


def result_identity(identity: OwnedIdentity) -> dict[str, Any]:
    return {
        "gamePid": identity.pid,
        "sessionDigest": session_digest(identity.session),
        "pathMatchedSelectedRoot": True,
        "creationIdentityCanonical100ns": True,
    }


def action_check(args: argparse.Namespace) -> dict[str, Any]:
    identity = read_owned_identity(args.isolated_root, args.game_root, require_healthy=True)
    with ExactProcessHandle(identity.pid, identity.game_path, identity.started_ticks):
        pass
    return {"schemaVersion": 1, "action": "check", "exactOwner": result_identity(identity)}


def action_start(args: argparse.Namespace) -> dict[str, Any]:
    config = load_config(args.isolated_root, args.game_root)
    runtime = args.isolated_root / "overview-bridge"
    adoption, adoption_status = safe_json(runtime / "adoption.json")
    recovery, recovery_status = safe_json(runtime / "recovery.json")
    if adoption_status == "ok" or recovery_status == "ok" or config.get("gameDesiredRunning") is True:
        raise RuntimeError("Start requires a fully idle/restored isolated profile")

    started = time.monotonic()
    requested_at = utc_now()
    _, receipt_path = send_request(args.isolated_root, args.output, {
        "operation": "start-exact-isolated-profile",
        "profileId": config["profileId"],
    })
    receipt = wait_receipt(receipt_path, args.timeout)
    if (receipt.get("succeeded") is not True or
            receipt.get("operation") != "start-exact-isolated-profile" or
            receipt.get("lifecycleStartCompleted") is not True or
            not isinstance(receipt.get("instanceId"), str) or
            not isinstance(receipt.get("gamePid"), int)):
        raise RuntimeError("production lifecycle Start did not return a fresh exact connected owner")

    deadline = started + args.timeout
    last_error = "full game/account health not yet observed"
    identity = None
    while time.monotonic() < deadline:
        try:
            current = read_owned_identity(
                args.isolated_root, args.game_root, require_healthy=True)
            if current.session != receipt["instanceId"] or current.pid != receipt["gamePid"]:
                raise RuntimeError("production Start receipt differs from the healthy exact owner")
            identity = current
            break
        except RuntimeError as error:
            last_error = str(error)
        time.sleep(0.25)
    if identity is None:
        raise TimeoutError("production Start did not reach full game/account health: " + last_error)
    with ExactProcessHandle(identity.pid, identity.game_path, identity.started_ticks):
        pass
    return {
        "schemaVersion": 1,
        "action": "start",
        "requestedUtc": requested_at,
        "secondsToHealthyConnected": round(time.monotonic() - started, 3),
        "exactOwner": result_identity(identity),
        "productionLifecycleStartCompleted": True,
    }


def action_pause(args: argparse.Namespace) -> dict[str, Any]:
    if args.hold_seconds <= 0 or args.hold_seconds > 25:
        raise RuntimeError("pause hold must be >0 and <=25 seconds (below the 30s hang threshold)")
    identity = read_owned_identity(args.isolated_root, args.game_root, require_healthy=True)
    suspended_at = None
    resumed_at = None
    with ExactProcessHandle(
            identity.pid, identity.game_path, identity.started_ticks, suspend=True) as process:
        process.suspend()
        suspended_at = utc_now()
        try:
            deadline = time.monotonic() + args.hold_seconds
            while time.monotonic() < deadline:
                if process.exited():
                    raise RuntimeError("exact game exited while paused")
                time.sleep(min(0.25, max(0.01, deadline - time.monotonic())))
        finally:
            if not process.exited():
                process.resume()
                resumed_at = utc_now()
    wait_same_owner_healthy(
        args.isolated_root, args.game_root, identity, args.timeout)
    return {
        "schemaVersion": 1,
        "action": "pause",
        "exactOwner": result_identity(identity),
        "holdSeconds": args.hold_seconds,
        "suspendedUtc": suspended_at,
        "resumedUtc": resumed_at,
        "sameExactOwnerHealthyAfterResume": True,
    }


def action_exit_recovery(args: argparse.Namespace) -> dict[str, Any]:
    config = load_config(args.isolated_root, args.game_root)
    if config.get("autoReconnect") is not True or config.get("gameDesiredRunning") is not True:
        raise RuntimeError("exit/recovery requires autoReconnect=true and an explicitly desired-running owner")
    previous = read_owned_identity(args.isolated_root, args.game_root, require_healthy=True)
    exited_at = None
    with ExactProcessHandle(
            previous.pid, previous.game_path, previous.started_ticks, terminate=True) as process:
        process.terminate()
        if not process.wait_exit(10):
            raise TimeoutError("exact owned game did not exit within 10 seconds")
        exited_at = utc_now()
    successor = wait_successor(
        args.isolated_root, args.game_root, previous, args.timeout)
    return {
        "schemaVersion": 1,
        "action": "exit-recovery",
        "oldOwner": result_identity(previous),
        "exactOldHandleExitedUtc": exited_at,
        "successor": result_identity(successor),
        "successorSessionChanged": successor.session != previous.session,
        "successorCreationChanged": successor.started_ticks != previous.started_ticks,
        "healthyAutomaticSuccessor": True,
    }


def request_stop(args: argparse.Namespace, reconnect_fault: bool) -> dict[str, Any]:
    identity = read_owned_identity(args.isolated_root, args.game_root, require_healthy=True)
    operation = (
        "drop-exact-authenticated-route-retain-registration-and-stop"
        if reconnect_fault else "stop-exact-owned-instance"
    )
    with ExactProcessHandle(identity.pid, identity.game_path, identity.started_ticks) as process:
        _, receipt_path = send_request(args.isolated_root, args.output, {
            "operation": operation,
            "profileId": identity.profile,
            "instanceId": identity.session,
            "gamePid": identity.pid,
        })
        receipt = wait_receipt(receipt_path, args.timeout)
        if (receipt.get("succeeded") is not True or
                receipt.get("operation") != operation or
                receipt.get("gamePid") != identity.pid or
                receipt.get("instanceId") != identity.session or
                receipt.get("lifecycleStopCompleted") is not True):
            raise RuntimeError("isolated exact Stop request failed or returned mismatched ownership")
        if reconnect_fault and (
                receipt.get("retainedRegistration") is not True or
                not isinstance(receipt.get("routeDroppedUtc"), str)):
            raise RuntimeError("Stop-during-reconnect did not prove the retained-registration route drop")
        if not process.wait_exit(10):
            raise TimeoutError("production Stop returned without exact game-handle exit")
    wait_cleanup(args.isolated_root, min(args.timeout, 60.0))
    restoration_hashes = validate_backup(args.backup_dir, args.scripts_root)
    settle_deadline = time.monotonic() + args.settle_seconds
    while time.monotonic() < settle_deadline:
        adoption, status = safe_json(args.isolated_root / "overview-bridge" / "adoption.json")
        if status == "ok" and adoption is not None:
            raise RuntimeError("a successor owner appeared after exact user Stop")
        time.sleep(0.25)
    return {
        "schemaVersion": 1,
        "action": "stop-reconnect" if reconnect_fault else "stop",
        "oldOwner": result_identity(identity),
        "retainedRegistrationRouteDropped": reconnect_fault,
        "routeDroppedUtc": receipt.get("routeDroppedUtc"),
        "productionLifecycleStopCompleted": True,
        "exactOldHandleExited": True,
        "desiredRunningCleared": True,
        "noSuccessorDuringSettle": True,
        "settleSeconds": args.settle_seconds,
        "officialScriptsRestored": True,
        "restoredScriptSha256": restoration_hashes,
    }


def report_signature(path: Path) -> tuple[int, int, str] | None:
    try:
        stat = path.stat()
        data = path.read_bytes()
    except OSError:
        return None
    return stat.st_mtime_ns, stat.st_size, hashlib.sha256(data).hexdigest()


def wait_new_game_report(runtime: Path, prior: tuple[int, int, str] | None,
                         timeout: float) -> dict[str, str]:
    path = runtime / "game-reported.txt"
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        signature = report_signature(path)
        values, status = safe_kv(path)
        if signature is not None and signature != prior and status == "ok" and values is not None:
            try:
                pid = int(values.get("gamePid", "0"))
                deadline_ms = int(values.get("deadlineMilliseconds", "0"))
            except ValueError:
                pid = 0
                deadline_ms = 0
            if (values.get("schema") == "1" and values.get("bridgeVersion") == BRIDGE_VERSION and
                    values.get("sessionId") and values.get("challenge") and pid > 0 and deadline_ms > 0):
                return values
        time.sleep(0.025)
    raise TimeoutError("fresh exact-session game-reported.txt was not observed")


def ready_matches_report(runtime: Path, report: dict[str, str]) -> bool:
    ready, status = safe_json(runtime / "ready.json")
    if status != "ok" or ready is None:
        return False
    try:
        pid = int(report["gamePid"])
    except (KeyError, ValueError):
        return False
    return (
        ready.get("sessionId") == report.get("sessionId") and
        ready.get("gamePid") == pid and ready.get("ready") is True
    )


def action_late_ready(args: argparse.Namespace) -> dict[str, Any]:
    if args.hold_seconds <= 0 or args.hold_seconds > 25:
        raise RuntimeError("late-ready hold must be >0 and <=25 seconds")
    config = load_config(args.isolated_root, args.game_root)
    runtime = args.isolated_root / "overview-bridge"
    adoption, adoption_status = safe_json(runtime / "adoption.json")
    recovery, recovery_status = safe_json(runtime / "recovery.json")
    if adoption_status not in {"missing"} or recovery_status not in {"missing"}:
        raise RuntimeError(
            "late-ready Start requires adoption/recovery files to be cleanly absent; unreadable state is not idle")
    if config.get("gameDesiredRunning") is True:
        raise RuntimeError("late-ready Start requires a fully idle/restored isolated profile")

    report_path = runtime / "game-reported.txt"
    prior_report = report_signature(report_path)
    _, receipt_path = send_request(args.isolated_root, args.output, {
        "operation": "start-exact-isolated-profile",
        "profileId": config["profileId"],
    })
    report = wait_new_game_report(runtime, prior_report, min(args.timeout, 90.0))
    pid = int(report["gamePid"])
    session = report["sessionId"]
    expected_game = (args.game_root / "Game" / "LastWar.exe").resolve(strict=True)
    suspended_at = None
    resumed_at = None
    with ExactProcessHandle(pid, expected_game, None, suspend=True) as process:
        if ready_matches_report(runtime, report):
            raise RuntimeError("same-session ready.json already existed; refusing to fake a late-readiness sample")
        captured_ticks = process.created_ticks
        process.suspend()
        suspended_at = utc_now()
        try:
            deadline = time.monotonic() + args.hold_seconds
            while time.monotonic() < deadline:
                if process.exited():
                    raise RuntimeError("exact newly reported game exited during bounded late-ready hold")
                time.sleep(min(0.25, max(0.01, deadline - time.monotonic())))
        finally:
            if not process.exited():
                process.resume()
                resumed_at = utc_now()

        receipt = wait_receipt(receipt_path, args.timeout)
        if (receipt.get("succeeded") is not True or
                receipt.get("operation") != "start-exact-isolated-profile" or
                receipt.get("lifecycleStartCompleted") is not True or
                receipt.get("instanceId") != session or receipt.get("gamePid") != pid):
            raise RuntimeError("production lifecycle Start did not complete for the delayed exact session")
        if process.exited():
            raise RuntimeError("late-ready Start replaced/exited the exact reported process")

    # Lifecycle Start can complete as soon as the exact bridge owner is ready,
    # while the game's own login/server heartbeat may need a few additional
    # frames after a deliberately suspended pre-ready process resumes.  Keep the
    # same exact reported owner fenced, but wait within the existing caller bound
    # for full account/game health instead of doing a single race-prone read.
    owner_deadline = time.monotonic() + args.timeout
    expected_identity = None
    last_owner_error = "same reported owner not yet published"
    while time.monotonic() < owner_deadline:
        try:
            current = read_owned_identity(
                args.isolated_root, args.game_root, require_healthy=False)
            if (current.session != session or current.pid != pid or
                    current.started_ticks != captured_ticks):
                raise RuntimeError(
                    "late-ready publication changed the exact reported PID/path/start/session")
            expected_identity = current
            break
        except RuntimeError as error:
            last_owner_error = str(error)
        time.sleep(0.25)
    if expected_identity is None:
        raise TimeoutError(
            "late-ready exact owner was not published before the bound: " + last_owner_error)

    identity = wait_same_owner_healthy(
        args.isolated_root,
        args.game_root,
        expected_identity,
        max(0.25, owner_deadline - time.monotonic()),
    )
    with ExactProcessHandle(identity.pid, identity.game_path, identity.started_ticks):
        pass
    return {
        "schemaVersion": 1,
        "action": "late-ready",
        "exactOwner": result_identity(identity),
        "holdSeconds": args.hold_seconds,
        "suspendedBeforeReady": True,
        "suspendedUtc": suspended_at,
        "resumedUtc": resumed_at,
        "sameReportedPidPathStartSessionReachedHealthy": True,
        "productionLifecycleStartCompleted": True,
    }


def default_scripts_root() -> Path:
    return (Path(os.environ.get("USERPROFILE", "")) / "AppData" / "LocalLow" /
            "FunFly" / "Last War-Survival Game" / "lwScripts")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=(
        "check", "start", "pause", "exit-recovery", "stop", "stop-reconnect", "late-ready"))
    parser.add_argument("--isolated-root", required=True, type=Path)
    parser.add_argument("--game-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--hold-seconds", type=float, default=15.0)
    parser.add_argument("--timeout", type=float, default=180.0)
    parser.add_argument("--settle-seconds", type=float, default=10.0)
    parser.add_argument("--backup-dir", type=Path)
    parser.add_argument("--scripts-root", type=Path, default=default_scripts_root())
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if sys.platform != "win32":
        raise SystemExit("Windows only")
    args.isolated_root = args.isolated_root.expanduser().resolve(strict=True)
    args.game_root = args.game_root.expanduser().resolve(strict=True)
    args.output = args.output.expanduser().resolve()
    args.scripts_root = args.scripts_root.expanduser().resolve()
    if args.backup_dir is not None:
        args.backup_dir = args.backup_dir.expanduser().resolve(strict=True)
    ensure_output_outside(args.isolated_root, args.output)
    if args.timeout <= 0 or args.settle_seconds < 0:
        raise SystemExit("timeout must be positive and settle-seconds must be nonnegative")
    if args.action in {"stop", "stop-reconnect"} and args.backup_dir is None:
        raise SystemExit("--backup-dir is required for Stop actions so exact official restoration is proven")

    dispatch = {
        "check": action_check,
        "start": action_start,
        "pause": action_pause,
        "exit-recovery": action_exit_recovery,
        "stop": lambda ns: request_stop(ns, reconnect_fault=False),
        "stop-reconnect": lambda ns: request_stop(ns, reconnect_fault=True),
        "late-ready": action_late_ready,
    }
    result = dispatch[args.action](args)
    result["completedUtc"] = utc_now()
    write_result(args.output, result)
    print(json.dumps({
        "tool": "home_stability_faults",
        "action": args.action,
        "ok": True,
        "output": str(args.output),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
