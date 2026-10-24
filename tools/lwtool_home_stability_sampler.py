"""Correlated, sanitized Home connection-stability sampler.

This tool is intentionally passive.  It reads only the isolated LW-Control
runtime/evidence files, the exact process identity already recorded by the
product, process counters, and selected Last War log markers.  It never writes
game/runtime mailboxes and never persists profile/session/challenge/token/UID
or raw log lines.
"""

from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
import datetime as dt
import json
import os
from pathlib import Path
import re
import sys
import time
from typing import Any

import r29_part_c_sampler as process_metrics


SCHEMA_VERSION = 1
LOG_MARKERS = {
    "connect_to_server_failed": "connect to server failed",
    "e005": "E005",
    "loading_e109": "Loading error : E109",
    "login_error": "OnLoginError",
}
MAILBOX_RE = re.compile(r"^pipe-(inbound|outbound)-(\d{8})\.json$")


class FILETIME(ctypes.Structure):
    _fields_ = [("low", wintypes.DWORD), ("high", wintypes.DWORD)]


if sys.platform == "win32":
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)
    ]
    kernel32.GetProcessTimes.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(FILETIME), ctypes.POINTER(FILETIME),
        ctypes.POINTER(FILETIME), ctypes.POINTER(FILETIME),
    ]
    user32.EnumWindows.argtypes = [ctypes.c_void_p, wintypes.LPARAM]
    user32.IsHungAppWindow.argtypes = [wintypes.HWND]
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat()


def safe_json(path: Path) -> tuple[dict[str, Any] | None, str]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return (value if isinstance(value, dict) else None), "ok"
    except FileNotFoundError:
        return None, "missing"
    except (OSError, UnicodeError, json.JSONDecodeError):
        return None, "unavailable"


def safe_text(path: Path, maximum: int = 4096) -> tuple[str | None, str]:
    try:
        text = path.read_text(encoding="utf-8")
        if len(text) > maximum:
            return None, "oversize"
        return text, "ok"
    except FileNotFoundError:
        return None, "missing"
    except (OSError, UnicodeError):
        return None, "unavailable"


def read_kv(path: Path) -> tuple[dict[str, str] | None, str]:
    text, status = safe_text(path)
    if text is None:
        return None, status
    result: dict[str, str] = {}
    for line in text.splitlines():
        if "=" not in line:
            return None, "invalid"
        key, value = line.split("=", 1)
        if not key or key in result:
            return None, "invalid"
        result[key] = value
    return result, "ok"


def normalize_adapter_state(raw: str | None) -> dict[str, Any]:
    if raw is None:
        return {"state": None, "errorClass": None}
    value = raw.strip()
    if value in {"idle", "connecting", "connected", "closed"}:
        return {"state": value, "errorClass": None}
    if value.startswith("error:") or value.startswith("send_error:"):
        detail = value.split(":", 1)[1]
        categories = (
            "host_lease_stale", "PeekNamedPipe", "WaitNamedPipeW", "CreateFileW",
            "EndOfStreamException", "pipe_heartbeat", "pipe_result", "pipe_",
        )
        category = next((item for item in categories if item in detail), "other")
        return {"state": "error", "errorClass": category}
    return {"state": "other", "errorClass": None}


def normalize_transport(value: dict[str, Any] | None, status: str) -> dict[str, Any]:
    if value is None:
        return {"readStatus": status}
    error = value.get("error")
    error_class = normalize_adapter_state("error:" + str(error))["errorClass"] if error else None
    return {
        "readStatus": status,
        "state": value.get("state"),
        "clientConnected": value.get("clientConnected") is True,
        "adapterLoaded": value.get("adapterLoaded") is True,
        "adapterActive": value.get("adapterActive") is True,
        "directCapable": value.get("directCapable") is True,
        "directActive": value.get("directActive") is True,
        "errorClass": error_class,
    }


def file_age_seconds(path: Path, now: float) -> float | None:
    try:
        return max(0.0, now - path.stat().st_mtime)
    except OSError:
        return None


def mailbox_summary(runtime: Path) -> dict[str, Any]:
    result = {
        "inboundCount": 0, "outboundCount": 0,
        "inboundHighestSequence": 0, "outboundHighestSequence": 0,
    }
    try:
        for path in runtime.iterdir():
            if not path.is_file():
                continue
            match = MAILBOX_RE.match(path.name)
            if match is None:
                continue
            direction, sequence_text = match.groups()
            sequence = int(sequence_text)
            key = "inbound" if direction == "inbound" else "outbound"
            result[key + "Count"] += 1
            result[key + "HighestSequence"] = max(result[key + "HighestSequence"], sequence)
    except OSError:
        result["enumerationUnavailable"] = True
    return result


def filetime_ticks(value: FILETIME) -> int:
    return (int(value.high) << 32) | int(value.low)


def iso_to_filetime_ticks(value: str | None) -> int | None:
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


def actual_process_identity(pid: int, expected_path: Path | None, expected_started: str | None) -> dict[str, Any]:
    if sys.platform != "win32" or pid <= 0:
        return {"queried": False}
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    handle = kernel32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not handle:
        return {"queried": True, "alive": False}
    try:
        image_buffer = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(image_buffer))
        image_ok = bool(kernel32.QueryFullProcessImageNameW(handle, 0, image_buffer, ctypes.byref(length)))
        created, exited, kernel, user = (FILETIME() for _ in range(4))
        time_ok = bool(kernel32.GetProcessTimes(
            handle, ctypes.byref(created), ctypes.byref(exited), ctypes.byref(kernel), ctypes.byref(user)
        ))
        expected_ticks = iso_to_filetime_ticks(expected_started)
        actual_ticks = filetime_ticks(created) if time_ok else None
        image_path = Path(image_buffer.value) if image_ok else None
        return {
            "queried": True,
            "alive": True,
            "imageName": image_path.name if image_path else None,
            "imageMatchesExpected": (
                expected_path is not None and image_path is not None and
                os.path.normcase(os.path.abspath(image_path)) ==
                os.path.normcase(os.path.abspath(expected_path))
            ),
            "creationMatchesAdoption": (
                expected_ticks is not None and actual_ticks is not None and expected_ticks == actual_ticks
            ),
        }
    finally:
        kernel32.CloseHandle(handle)


def hung_window(pid: int) -> bool | None:
    if sys.platform != "win32" or pid <= 0:
        return None
    found = False
    hung = False
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    @callback_type
    def visit(hwnd: wintypes.HWND, _parameter: wintypes.LPARAM) -> bool:
        nonlocal found, hung
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if int(owner.value) == pid:
            found = True
            if user32.IsHungAppWindow(hwnd):
                hung = True
                return False
        return True

    user32.EnumWindows(visit, 0)
    return hung if found else False


class ProcessSampler:
    def __init__(self, label: str) -> None:
        self.label = label
        self.pid: int | None = None
        self.probe: process_metrics.ProcessProbe | None = None

    def close(self) -> None:
        if self.probe is not None:
            self.probe.close()
        self.probe = None
        self.pid = None

    def sample(self, pid: int | None, monotonic_now: float) -> dict[str, Any] | None:
        if pid is None or pid <= 0:
            self.close()
            return None
        if pid != self.pid:
            self.close()
            self.pid = pid
            self.probe = process_metrics.ProcessProbe(process_metrics.ProcessTargetSpec(self.label, pid))
        assert self.probe is not None
        return self.probe.sample(monotonic_now)


class LogCursor:
    def __init__(self, path: Path) -> None:
        self.path = path
        try:
            self.offset = path.stat().st_size
        except OSError:
            self.offset = 0
        self.total: dict[str, int] = {key: 0 for key in LOG_MARKERS}

    def read_markers(self) -> dict[str, int]:
        counts = {key: 0 for key in LOG_MARKERS}
        try:
            size = self.path.stat().st_size
            if size < self.offset:
                self.offset = 0
            with self.path.open("rb") as stream:
                stream.seek(self.offset)
                data = stream.read()
                self.offset = stream.tell()
        except OSError:
            return counts
        text = data.decode("utf-8", errors="replace")
        for key, marker in LOG_MARKERS.items():
            counts[key] = text.count(marker)
            self.total[key] += counts[key]
        return counts


def latest_trace_row(path: Path) -> dict[str, Any] | None:
    try:
        with path.open("rb") as stream:
            stream.seek(0, os.SEEK_END)
            size = stream.tell()
            stream.seek(max(0, size - 8192))
            rows = stream.read().decode("utf-8", errors="replace").splitlines()
        for line in reversed(rows):
            try:
                value = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(value, dict):
                return value
    except OSError:
        return None
    return None


def trace_age_seconds(value: str | None, now: float) -> float | None:
    if not value:
        return None
    try:
        instant = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        if instant.tzinfo is None:
            instant = instant.replace(tzinfo=dt.timezone.utc)
        return max(0.0, now - instant.timestamp())
    except ValueError:
        return None


class OwnedIdentityTracker:
    """Retain only exact process identity needed to prove post-Stop exit.

    The product intentionally deletes adoption metadata during successful Stop.
    Retaining PID/start/path in this sampler process lets later samples verify
    that the exact former owner exited without persisting the full path.
    """

    def __init__(self, expected_game_exe: Path | None) -> None:
        self.pid: int | None = None
        self.started: str | None = None
        self.expected = expected_game_exe

    def resolve(
        self,
        adoption: dict[str, Any] | None,
    ) -> tuple[int | None, str | None, Path | None, str]:
        if adoption is not None and isinstance(adoption.get("pid"), int):
            self.pid = adoption["pid"]
            self.started = adoption.get("processCreatedAt")
            if self.expected is None and isinstance(adoption.get("gameExecutable"), str):
                self.expected = Path(adoption["gameExecutable"])
            return self.pid, self.started, self.expected, "adoption"
        if self.pid is not None:
            return self.pid, self.started, self.expected, "retained"
        return None, None, self.expected, "none"


def sample_once(
    isolated_root: Path,
    expected_game_exe: Path | None,
    host_pid: int | None,
    host_probe: ProcessSampler,
    game_probe: ProcessSampler,
    log_cursor: LogCursor,
    identity_tracker: OwnedIdentityTracker,
) -> dict[str, Any]:
    runtime = isolated_root / "overview-bridge"
    evidence = isolated_root / "overview-evidence"
    now_wall = time.time()
    now_mono = time.monotonic()

    adoption, adoption_status = safe_json(runtime / "adoption.json")
    owned_pid, owned_started, expected, identity_source = identity_tracker.resolve(adoption)
    if expected_game_exe is not None:
        expected = expected_game_exe

    heartbeat, heartbeat_status = safe_json(runtime / "heartbeat.json")
    ready, ready_status = safe_json(runtime / "ready.json")
    transport, transport_status = safe_json(runtime / "pipe-transport.json")
    adapter_text, adapter_status = safe_text(runtime / "pipe-adapter-state.txt")
    lease, lease_status = read_kv(runtime / "lease.txt")

    heartbeat_age = file_age_seconds(runtime / "heartbeat.json", now_wall)
    ready_age = file_age_seconds(runtime / "ready.json", now_wall)
    lease_age = None
    if lease is not None:
        try:
            lease_age = now_wall - int(lease.get("updatedAt", "0"))
        except ValueError:
            lease_age = None

    heartbeat_session_matches = None
    ready_session_matches = None
    lease_session_matches = None
    if adoption is not None:
        session = adoption.get("instanceId") or adoption.get("sessionId")
        heartbeat_session_matches = heartbeat is not None and heartbeat.get("sessionId") == session
        ready_session_matches = ready is not None and ready.get("sessionId") == session
        lease_session_matches = lease is not None and lease.get("sessionId") == session

    monitor = latest_trace_row(evidence / "home-connection-observations.jsonl")
    if monitor is not None:
        monitor = {key: monitor.get(key) for key in (
            "utc", "lifecyclePhase", "recoveryState", "recoveryReason", "desiredRunning",
            "autoReconnect", "ownedSnapshotPresent", "processPresent", "heartbeatFresh",
            "authenticatedRouteConnected", "authenticatedRouteObserved", "bridgeOnline",
            "gameStateObserved", "gameHealthy",
            "authenticatedRoutes", "listenerStarted", "failedRpcSessions",
        )}
        monitor_age = trace_age_seconds(monitor.get("utc"), now_wall)
        monitor["ageSeconds"] = monitor_age
        monitor["freshWithin4Seconds"] = monitor_age is not None and monitor_age <= 4.0

    return {
        "utc": utc_now(),
        "monotonicSeconds": now_mono,
        "owned": {
            "recordStatus": adoption_status,
            "identitySource": identity_source,
            "pid": owned_pid,
            "processCreatedAt": owned_started,
            **actual_process_identity(owned_pid or 0, expected, owned_started),
            "hungWindow": hung_window(owned_pid or 0),
        },
        "heartbeat": {
            "readStatus": heartbeat_status,
            "ageSeconds": heartbeat_age,
            "sessionMatchesOwned": heartbeat_session_matches,
            "ready": heartbeat.get("ready") is True if heartbeat else None,
            "gameStateObserved": heartbeat.get("gameStateObserved") is True if heartbeat else None,
            "gameReady": heartbeat.get("gameReady") is True if heartbeat else None,
            "loggedIn": heartbeat.get("loggedIn") is True if heartbeat else None,
            "connected": heartbeat.get("connected") is True if heartbeat else None,
            "connecting": heartbeat.get("connecting") is True if heartbeat else None,
            "serverValid": isinstance(heartbeat.get("serverId"), int) and heartbeat.get("serverId", 0) > 0 if heartbeat else None,
            "worldPosPositive": isinstance(heartbeat.get("worldPos"), int) and heartbeat.get("worldPos", 0) > 0 if heartbeat else None,
        },
        "ready": {
            "readStatus": ready_status,
            "ageSeconds": ready_age,
            "sessionMatchesOwned": ready_session_matches,
            "ready": ready.get("ready") is True if ready else None,
            "pidMatchesOwned": ready.get("gamePid") == owned_pid if ready and owned_pid else None,
        },
        "transport": normalize_transport(transport, transport_status),
        "adapter": {"readStatus": adapter_status, **normalize_adapter_state(adapter_text)},
        "lease": {
            "readStatus": lease_status,
            "ageSeconds": lease_age,
            "sessionMatchesOwned": lease_session_matches,
            "freshWithin5Seconds": lease_age is not None and -5 <= lease_age <= 5,
        },
        "mailbox": mailbox_summary(runtime),
        "monitor": monitor,
        "processes": {
            "host": host_probe.sample(host_pid, now_mono),
            "game": game_probe.sample(owned_pid, now_mono),
        },
        "playerLogMarkers": log_cursor.read_markers(),
    }


def first_failed_layer(sample: dict[str, Any]) -> str | None:
    layers = failed_layers(sample)
    return layers[0] if layers else None


def failed_layers(sample: dict[str, Any]) -> list[str]:
    owned = sample["owned"]
    heartbeat = sample["heartbeat"]
    transport = sample["transport"]
    adapter = sample["adapter"]
    monitor = sample.get("monitor") or {}
    failures: list[str] = []
    if owned.get("alive") is False or owned.get("imageMatchesExpected") is False or owned.get("creationMatchesAdoption") is False:
        failures.append("game_process")
    route_observed = monitor.get("authenticatedRouteObserved")
    if route_observed is None:
        route_observed = monitor.get("authenticatedRouteConnected")
    if monitor.get("freshWithin4Seconds") is True and route_observed is False:
        failures.append("authenticated_route")
    if adapter.get("state") == "error" or transport.get("state") == "error":
        failures.append("managed_adapter")
    if heartbeat.get("readStatus") != "ok" or (heartbeat.get("ageSeconds") is not None and heartbeat["ageSeconds"] > 5):
        failures.append("lua_heartbeat")
    if heartbeat.get("gameStateObserved") is False:
        failures.append("game_state_observation")
    if heartbeat.get("loggedIn") is False or heartbeat.get("connected") is False or heartbeat.get("connecting") is True:
        failures.append("game_server_login")
    return failures


def run(args: argparse.Namespace) -> dict[str, Any]:
    isolated = args.isolated_root.expanduser().resolve(strict=True)
    runtime = isolated / "overview-bridge"
    if not runtime.is_dir():
        raise RuntimeError("isolated root does not contain overview-bridge runtime")
    output = args.output.expanduser().resolve()
    if isolated == output or isolated in output.parents:
        raise RuntimeError("output must be outside the isolated runtime root")
    expected_game = args.expected_game_exe.expanduser().resolve(strict=True) if args.expected_game_exe else None
    player_log = args.player_log.expanduser().resolve() if args.player_log else (
        Path(os.environ.get("USERPROFILE", "")) / "AppData" / "LocalLow" / "FunFly" /
        "Last War-Survival Game" / "Player.log"
    )

    host_probe = ProcessSampler("host")
    game_probe = ProcessSampler("game")
    log_cursor = LogCursor(player_log)
    identity_tracker = OwnedIdentityTracker(expected_game)
    samples: list[dict[str, Any]] = []
    start = time.monotonic()
    deadline = start + args.duration
    try:
        index = 0
        while True:
            target = min(start + index * args.interval, deadline)
            delay = target - time.monotonic()
            if delay > 0:
                time.sleep(delay)
            sample = sample_once(
                isolated, expected_game, args.host_pid,
                host_probe, game_probe, log_cursor, identity_tracker,
            )
            sample["index"] = index
            sample["elapsedSeconds"] = time.monotonic() - start
            sample["firstFailedLayer"] = first_failed_layer(sample)
            sample["failedLayers"] = failed_layers(sample)
            samples.append(sample)
            if target >= deadline:
                break
            index += 1
    finally:
        host_probe.close()
        game_probe.close()

    failures = [sample for sample in samples if sample["firstFailedLayer"]]
    first_seen: dict[str, dict[str, Any]] = {}
    for sample in samples:
        for layer in sample.get("failedLayers", []):
            if layer not in first_seen:
                first_seen[layer] = {
                    "index": sample["index"],
                    "elapsedSeconds": sample["elapsedSeconds"],
                    "utc": sample["utc"],
                }
    game_pids = sorted({sample["owned"].get("pid") for sample in samples if sample["owned"].get("pid")})
    report = {
        "schemaVersion": SCHEMA_VERSION,
        "tool": "lwtool_home_stability_sampler",
        "startedUtc": samples[0]["utc"] if samples else utc_now(),
        "endedUtc": samples[-1]["utc"] if samples else utc_now(),
        "requestedDurationSeconds": args.duration,
        "intervalSeconds": args.interval,
        "hostPid": args.host_pid,
        "sampleCount": len(samples),
        "observedGamePids": game_pids,
        "naturalFailureObserved": bool(failures),
        "firstFailure": ({
            "index": failures[0]["index"],
            "elapsedSeconds": failures[0]["elapsedSeconds"],
            "layer": failures[0]["firstFailedLayer"],
            "utc": failures[0]["utc"],
        } if failures else None),
        "failureLayerFirstSeen": first_seen,
        "playerLogMarkerTotals": log_cursor.total,
        "samples": samples,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.name + f".tmp-{os.getpid()}")
    temporary.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(temporary, output)
    return report


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--isolated-root", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--host-pid", type=int)
    parser.add_argument("--expected-game-exe", type=Path)
    parser.add_argument("--player-log", type=Path)
    parser.add_argument("--duration", type=float, default=60.0)
    parser.add_argument("--interval", type=float, default=1.0)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    if sys.platform != "win32":
        parser.error("Windows only")
    if args.duration <= 0 or args.interval <= 0:
        parser.error("duration and interval must be positive")
    if args.host_pid is not None and args.host_pid <= 0:
        parser.error("host PID must be positive")
    report = run(args)
    print(json.dumps({
        "tool": report["tool"],
        "sampleCount": report["sampleCount"],
        "naturalFailureObserved": report["naturalFailureObserved"],
        "firstFailure": report["firstFailure"],
        "playerLogMarkerTotals": report["playerLogMarkerTotals"],
        "output": str(args.output),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
