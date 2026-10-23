#!/usr/bin/env python3
"""Passive R29 Part-C process and runtime-file sampler for Windows.

The sampler only queries process counters and observes file metadata/change
notifications. It does not send input, attach a debugger, inject code, hook a
process, or modify the monitored process/runtime root.
"""

from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
from dataclasses import dataclass
from datetime import datetime, timezone
import fnmatch
import json
import math
import os
from pathlib import Path
import struct
import sys
import threading
import time
from typing import Any, Iterable


TOOL_NAME = "r29_part_c_sampler"
SCHEMA_VERSION = 1
DEFAULT_INTERVAL_SECONDS = 0.25
DEFAULT_DURATION_SECONDS = 60.0


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def percentile(values: list[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    weight = position - lower
    return ordered[lower] * (1.0 - weight) + ordered[upper] * weight


def summarize(values: Iterable[float | int | None], *, total: bool = False) -> dict[str, Any] | None:
    usable = [float(value) for value in values if value is not None and math.isfinite(float(value))]
    if not usable:
        return None
    result: dict[str, Any] = {
        "count": len(usable),
        "min": min(usable),
        "mean": sum(usable) / len(usable),
        "max": max(usable),
        "p95": percentile(usable, 0.95),
    }
    if total:
        result["sum"] = sum(usable)
    return result


def parse_named_value(raw: str, *, kind: str) -> tuple[str, str]:
    if "=" in raw:
        label, value = raw.split("=", 1)
        label = label.strip()
        value = value.strip()
        if not label or not value:
            raise argparse.ArgumentTypeError(f"{kind} must be LABEL=VALUE or VALUE")
        return label, value
    value = raw.strip()
    if not value:
        raise argparse.ArgumentTypeError(f"{kind} cannot be empty")
    return value, value


def _inside_path(child: Path, parent: Path) -> bool:
    try:
        child_resolved = os.path.normcase(str(child.resolve()))
        parent_resolved = os.path.normcase(str(parent.resolve()))
        return os.path.commonpath([child_resolved, parent_resolved]) == parent_resolved
    except (OSError, ValueError):
        return False


if sys.platform == "win32":
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)

    PROCESS_QUERY_INFORMATION = 0x0400
    PROCESS_VM_READ = 0x0010
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    STILL_ACTIVE = 259

    TH32CS_SNAPTHREAD = 0x00000004
    INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value

    FILE_LIST_DIRECTORY = 0x0001
    FILE_SHARE_READ = 0x00000001
    FILE_SHARE_WRITE = 0x00000002
    FILE_SHARE_DELETE = 0x00000004
    OPEN_EXISTING = 3
    FILE_FLAG_BACKUP_SEMANTICS = 0x02000000
    FILE_FLAG_OVERLAPPED = 0x40000000
    FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001
    FILE_NOTIFY_CHANGE_DIR_NAME = 0x00000002
    FILE_NOTIFY_CHANGE_SIZE = 0x00000008
    FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010
    FILE_NOTIFY_CHANGE_CREATION = 0x00000040
    ERROR_IO_PENDING = 997
    ERROR_OPERATION_ABORTED = 995
    WAIT_OBJECT_0 = 0
    WAIT_TIMEOUT = 258

    ULONG_PTR = ctypes.c_size_t

    class FILETIME(ctypes.Structure):
        _fields_ = [("dwLowDateTime", wintypes.DWORD), ("dwHighDateTime", wintypes.DWORD)]

    class IO_COUNTERS(ctypes.Structure):
        _fields_ = [
            ("ReadOperationCount", ctypes.c_ulonglong),
            ("WriteOperationCount", ctypes.c_ulonglong),
            ("OtherOperationCount", ctypes.c_ulonglong),
            ("ReadTransferCount", ctypes.c_ulonglong),
            ("WriteTransferCount", ctypes.c_ulonglong),
            ("OtherTransferCount", ctypes.c_ulonglong),
        ]

    class PROCESS_MEMORY_COUNTERS_EX(ctypes.Structure):
        _fields_ = [
            ("cb", wintypes.DWORD),
            ("PageFaultCount", wintypes.DWORD),
            ("PeakWorkingSetSize", ctypes.c_size_t),
            ("WorkingSetSize", ctypes.c_size_t),
            ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
            ("QuotaPagedPoolUsage", ctypes.c_size_t),
            ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
            ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
            ("PagefileUsage", ctypes.c_size_t),
            ("PeakPagefileUsage", ctypes.c_size_t),
            ("PrivateUsage", ctypes.c_size_t),
        ]

    class THREADENTRY32(ctypes.Structure):
        _fields_ = [
            ("dwSize", wintypes.DWORD),
            ("cntUsage", wintypes.DWORD),
            ("th32ThreadID", wintypes.DWORD),
            ("th32OwnerProcessID", wintypes.DWORD),
            ("tpBasePri", wintypes.LONG),
            ("tpDeltaPri", wintypes.LONG),
            ("dwFlags", wintypes.DWORD),
        ]

    class OVERLAPPED(ctypes.Structure):
        _fields_ = [
            ("Internal", ULONG_PTR),
            ("InternalHigh", ULONG_PTR),
            ("Offset", wintypes.DWORD),
            ("OffsetHigh", wintypes.DWORD),
            ("hEvent", wintypes.HANDLE),
        ]

    kernel32.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel32.OpenProcess.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    kernel32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel32.GetExitCodeProcess.restype = wintypes.BOOL
    kernel32.GetProcessTimes.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(FILETIME),
        ctypes.POINTER(FILETIME),
        ctypes.POINTER(FILETIME),
        ctypes.POINTER(FILETIME),
    ]
    kernel32.GetProcessTimes.restype = wintypes.BOOL
    kernel32.GetProcessIoCounters.argtypes = [wintypes.HANDLE, ctypes.POINTER(IO_COUNTERS)]
    kernel32.GetProcessIoCounters.restype = wintypes.BOOL
    kernel32.GetProcessHandleCount.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel32.GetProcessHandleCount.restype = wintypes.BOOL
    psapi.GetProcessMemoryInfo.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD]
    psapi.GetProcessMemoryInfo.restype = wintypes.BOOL
    kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Thread32First.argtypes = [wintypes.HANDLE, ctypes.POINTER(THREADENTRY32)]
    kernel32.Thread32First.restype = wintypes.BOOL
    kernel32.Thread32Next.argtypes = [wintypes.HANDLE, ctypes.POINTER(THREADENTRY32)]
    kernel32.Thread32Next.restype = wintypes.BOOL

    kernel32.CreateFileW.argtypes = [
        wintypes.LPCWSTR,
        wintypes.DWORD,
        wintypes.DWORD,
        ctypes.c_void_p,
        wintypes.DWORD,
        wintypes.DWORD,
        wintypes.HANDLE,
    ]
    kernel32.CreateFileW.restype = wintypes.HANDLE
    kernel32.CreateEventW.argtypes = [ctypes.c_void_p, wintypes.BOOL, wintypes.BOOL, wintypes.LPCWSTR]
    kernel32.CreateEventW.restype = wintypes.HANDLE
    kernel32.ResetEvent.argtypes = [wintypes.HANDLE]
    kernel32.ResetEvent.restype = wintypes.BOOL
    kernel32.SetEvent.argtypes = [wintypes.HANDLE]
    kernel32.SetEvent.restype = wintypes.BOOL
    kernel32.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel32.WaitForSingleObject.restype = wintypes.DWORD
    kernel32.ReadDirectoryChangesW.argtypes = [
        wintypes.HANDLE,
        ctypes.c_void_p,
        wintypes.DWORD,
        wintypes.BOOL,
        wintypes.DWORD,
        ctypes.POINTER(wintypes.DWORD),
        ctypes.POINTER(OVERLAPPED),
        ctypes.c_void_p,
    ]
    kernel32.ReadDirectoryChangesW.restype = wintypes.BOOL
    kernel32.GetOverlappedResult.argtypes = [
        wintypes.HANDLE,
        ctypes.POINTER(OVERLAPPED),
        ctypes.POINTER(wintypes.DWORD),
        wintypes.BOOL,
    ]
    kernel32.GetOverlappedResult.restype = wintypes.BOOL
    kernel32.CancelIoEx.argtypes = [wintypes.HANDLE, ctypes.POINTER(OVERLAPPED)]
    kernel32.CancelIoEx.restype = wintypes.BOOL


def windows_error(prefix: str) -> str:
    code = ctypes.get_last_error()
    return f"{prefix}: winerror={code} {ctypes.FormatError(code).strip()}"


def filetime_seconds(value: Any) -> float:
    ticks = (int(value.dwHighDateTime) << 32) | int(value.dwLowDateTime)
    return ticks / 10_000_000.0


def process_thread_count(pid: int) -> tuple[int | None, str | None]:
    if sys.platform != "win32":
        return None, "thread count requires Windows"
    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    if not snapshot or snapshot == INVALID_HANDLE_VALUE:
        return None, windows_error("CreateToolhelp32Snapshot failed")
    try:
        entry = THREADENTRY32()
        entry.dwSize = ctypes.sizeof(entry)
        if not kernel32.Thread32First(snapshot, ctypes.byref(entry)):
            return None, windows_error("Thread32First failed")
        count = 0
        while True:
            if int(entry.th32OwnerProcessID) == pid:
                count += 1
            if not kernel32.Thread32Next(snapshot, ctypes.byref(entry)):
                break
        return count, None
    finally:
        kernel32.CloseHandle(snapshot)


@dataclass(frozen=True)
class ProcessTargetSpec:
    label: str
    pid: int


class ProcessProbe:
    def __init__(self, spec: ProcessTargetSpec) -> None:
        self.spec = spec
        self.handle: Any = None
        self.opened_successfully = False
        self.open_error: str | None = None
        self.metric_errors: dict[str, str] = {}
        self.previous_cpu_total: float | None = None
        self.previous_io: dict[str, int] | None = None
        self.previous_monotonic: float | None = None
        self.last_thread_count: int | None = None
        self.last_thread_refresh: float | None = None
        self._open()

    def _open(self) -> None:
        if sys.platform != "win32":
            self.open_error = "process sampling requires Windows"
            return
        access_modes = [
            PROCESS_QUERY_INFORMATION | PROCESS_VM_READ,
            PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ,
            PROCESS_QUERY_LIMITED_INFORMATION,
        ]
        for access in access_modes:
            ctypes.set_last_error(0)
            handle = kernel32.OpenProcess(access, False, self.spec.pid)
            if handle:
                self.handle = handle
                self.opened_successfully = True
                return
        self.open_error = windows_error(f"OpenProcess({self.spec.pid}) failed")

    def close(self) -> None:
        if self.handle:
            kernel32.CloseHandle(self.handle)
            self.handle = None

    def _record_error(self, metric: str, message: str) -> None:
        self.metric_errors.setdefault(metric, message)

    def _alive(self) -> bool | None:
        if not self.handle:
            return None
        code = wintypes.DWORD()
        if not kernel32.GetExitCodeProcess(self.handle, ctypes.byref(code)):
            self._record_error("alive", windows_error("GetExitCodeProcess failed"))
            return None
        return int(code.value) == STILL_ACTIVE

    def _cpu(self) -> tuple[float | None, float | None, float | None]:
        if not self.handle:
            return None, None, None
        creation = FILETIME()
        exit_time = FILETIME()
        kernel = FILETIME()
        user = FILETIME()
        if not kernel32.GetProcessTimes(
            self.handle,
            ctypes.byref(creation),
            ctypes.byref(exit_time),
            ctypes.byref(kernel),
            ctypes.byref(user),
        ):
            self._record_error("cpu", windows_error("GetProcessTimes failed"))
            return None, None, None
        kernel_s = filetime_seconds(kernel)
        user_s = filetime_seconds(user)
        return kernel_s, user_s, kernel_s + user_s

    def _memory(self) -> tuple[int | None, int | None]:
        if not self.handle:
            return None, None
        counters = PROCESS_MEMORY_COUNTERS_EX()
        counters.cb = ctypes.sizeof(counters)
        if not psapi.GetProcessMemoryInfo(self.handle, ctypes.byref(counters), counters.cb):
            self._record_error("memory", windows_error("GetProcessMemoryInfo failed"))
            return None, None
        return int(counters.WorkingSetSize), int(counters.PrivateUsage)

    def _io(self) -> dict[str, int] | None:
        if not self.handle:
            return None
        counters = IO_COUNTERS()
        if not kernel32.GetProcessIoCounters(self.handle, ctypes.byref(counters)):
            self._record_error("io", windows_error("GetProcessIoCounters failed"))
            return None
        return {
            "read_ops": int(counters.ReadOperationCount),
            "write_ops": int(counters.WriteOperationCount),
            "other_ops": int(counters.OtherOperationCount),
            "read_bytes": int(counters.ReadTransferCount),
            "write_bytes": int(counters.WriteTransferCount),
            "other_bytes": int(counters.OtherTransferCount),
        }

    def _handles(self) -> int | None:
        if not self.handle:
            return None
        count = wintypes.DWORD()
        if not kernel32.GetProcessHandleCount(self.handle, ctypes.byref(count)):
            self._record_error("handles", windows_error("GetProcessHandleCount failed"))
            return None
        return int(count.value)

    def sample(self, monotonic_now: float) -> dict[str, Any]:
        alive = self._alive()
        kernel_s, user_s, cpu_total_s = self._cpu()
        working_set, private_bytes = self._memory()
        io_now = self._io()
        handles = self._handles()
        threads_refreshed = self.last_thread_refresh is None or monotonic_now - self.last_thread_refresh >= 1.0
        if threads_refreshed:
            threads, thread_error = process_thread_count(self.spec.pid)
            self.last_thread_refresh = monotonic_now
            if thread_error:
                self._record_error("threads", thread_error)
            elif threads is not None:
                self.last_thread_count = threads
        threads = self.last_thread_count

        delta_wall = None
        delta_cpu = None
        cpu_percent_single = None
        cpu_percent_all = None
        if self.previous_monotonic is not None:
            delta_wall = max(monotonic_now - self.previous_monotonic, 0.0)
        if cpu_total_s is not None and self.previous_cpu_total is not None:
            delta_cpu = max(cpu_total_s - self.previous_cpu_total, 0.0)
            if delta_wall and delta_wall > 0:
                cpu_percent_single = delta_cpu / delta_wall * 100.0
                cpu_count = max(os.cpu_count() or 1, 1)
                cpu_percent_all = cpu_percent_single / cpu_count

        io_delta: dict[str, int | None] = {
            "read_ops": None,
            "write_ops": None,
            "other_ops": None,
            "read_bytes": None,
            "write_bytes": None,
            "other_bytes": None,
        }
        if io_now is not None and self.previous_io is not None:
            for key in io_delta:
                io_delta[key] = max(io_now[key] - self.previous_io[key], 0)

        self.previous_monotonic = monotonic_now
        if cpu_total_s is not None:
            self.previous_cpu_total = cpu_total_s
        if io_now is not None:
            self.previous_io = io_now

        return {
            "alive": alive,
            "cpu": {
                "kernel_seconds": kernel_s,
                "user_seconds": user_s,
                "total_seconds": cpu_total_s,
                "delta_total_seconds": delta_cpu,
                "percent_single_core": cpu_percent_single,
                "percent_all_logical_cpus": cpu_percent_all,
            },
            "memory": {
                "working_set_bytes": working_set,
                "private_bytes": private_bytes,
            },
            "io": {
                "cumulative": io_now,
                "delta": io_delta,
            },
            "handles": handles,
            "threads": threads,
            "threads_refreshed": threads_refreshed,
        }


FILE_ACTIONS = {
    1: "added",
    2: "removed",
    3: "modified",
    4: "renamed_old_name",
    5: "renamed_new_name",
}


class DirectoryChangeWatcher:
    def __init__(self, root: Path, patterns: list[str]) -> None:
        self.root = root
        self.patterns = patterns
        self.events: list[dict[str, Any]] = []
        self.error: str | None = None
        self._lock = threading.Lock()
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self._directory_handle: Any = None
        self._event_handle: Any = None
        self._overlapped: Any = None

    def _matches(self, relative: str) -> bool:
        normalized = relative.replace("\\", "/")
        basename = normalized.rsplit("/", 1)[-1]
        return any(fnmatch.fnmatch(normalized, pattern) or fnmatch.fnmatch(basename, pattern) for pattern in self.patterns)

    def start(self) -> None:
        if sys.platform != "win32":
            self.error = "ReadDirectoryChangesW requires Windows"
            return
        self._directory_handle = kernel32.CreateFileW(
            str(self.root),
            FILE_LIST_DIRECTORY,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            None,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OVERLAPPED,
            None,
        )
        if not self._directory_handle or self._directory_handle == INVALID_HANDLE_VALUE:
            self.error = windows_error(f"CreateFileW directory watcher failed for {self.root}")
            self._directory_handle = None
            return
        self._event_handle = kernel32.CreateEventW(None, True, False, None)
        if not self._event_handle:
            self.error = windows_error("CreateEventW failed")
            kernel32.CloseHandle(self._directory_handle)
            self._directory_handle = None
            return
        self._thread = threading.Thread(target=self._run, name=f"r29-file-watch-{self.root.name}", daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if sys.platform == "win32" and self._directory_handle and self._overlapped is not None:
            ctypes.set_last_error(0)
            kernel32.CancelIoEx(self._directory_handle, ctypes.byref(self._overlapped))
        if sys.platform == "win32" and self._event_handle:
            kernel32.SetEvent(self._event_handle)
        if self._thread:
            self._thread.join(timeout=2.0)
        if sys.platform == "win32" and self._event_handle:
            kernel32.CloseHandle(self._event_handle)
            self._event_handle = None
        if sys.platform == "win32" and self._directory_handle:
            kernel32.CloseHandle(self._directory_handle)
            self._directory_handle = None

    def drain(self) -> list[dict[str, Any]]:
        with self._lock:
            drained = self.events
            self.events = []
        return drained

    def _record_event(self, action: int, relative: str) -> None:
        if not self._matches(relative):
            return
        path = self.root / relative
        size = None
        mtime_ns = None
        try:
            stat = path.stat()
            if path.is_file():
                size = int(stat.st_size)
                mtime_ns = int(stat.st_mtime_ns)
        except OSError:
            pass
        event = {
            "timestamp_utc": utc_now(),
            "monotonic_seconds": time.monotonic(),
            "action": FILE_ACTIONS.get(action, f"action_{action}"),
            "relative_path": relative.replace("\\", "/"),
            "size_bytes": size,
            "mtime_ns": mtime_ns,
        }
        with self._lock:
            self.events.append(event)

    def _run(self) -> None:
        notify_filter = (
            FILE_NOTIFY_CHANGE_FILE_NAME
            | FILE_NOTIFY_CHANGE_DIR_NAME
            | FILE_NOTIFY_CHANGE_SIZE
            | FILE_NOTIFY_CHANGE_LAST_WRITE
            | FILE_NOTIFY_CHANGE_CREATION
        )
        while not self._stop.is_set():
            buffer = ctypes.create_string_buffer(64 * 1024)
            overlapped = OVERLAPPED()
            overlapped.hEvent = self._event_handle
            self._overlapped = overlapped
            kernel32.ResetEvent(self._event_handle)
            ctypes.set_last_error(0)
            ok = kernel32.ReadDirectoryChangesW(
                self._directory_handle,
                buffer,
                len(buffer),
                True,
                notify_filter,
                None,
                ctypes.byref(overlapped),
                None,
            )
            if not ok and ctypes.get_last_error() != ERROR_IO_PENDING:
                self.error = windows_error("ReadDirectoryChangesW failed")
                break

            while not self._stop.is_set():
                wait = kernel32.WaitForSingleObject(self._event_handle, 250)
                if wait == WAIT_TIMEOUT:
                    continue
                if wait != WAIT_OBJECT_0:
                    self.error = f"WaitForSingleObject failed: result={wait}"
                    self._stop.set()
                    break
                break
            if self._stop.is_set():
                break

            transferred = wintypes.DWORD()
            ctypes.set_last_error(0)
            if not kernel32.GetOverlappedResult(
                self._directory_handle,
                ctypes.byref(overlapped),
                ctypes.byref(transferred),
                False,
            ):
                error = ctypes.get_last_error()
                if error == ERROR_OPERATION_ABORTED and self._stop.is_set():
                    break
                self.error = windows_error("GetOverlappedResult failed")
                break
            if transferred.value <= 0:
                continue

            data = buffer.raw[: transferred.value]
            offset = 0
            while offset + 12 <= len(data):
                next_offset, action, name_length = struct.unpack_from("<III", data, offset)
                name_start = offset + 12
                name_end = name_start + name_length
                if name_end > len(data):
                    break
                relative = data[name_start:name_end].decode("utf-16-le", errors="replace")
                self._record_event(action, relative)
                if next_offset == 0:
                    break
                offset += next_offset


@dataclass(frozen=True)
class FileRootSpec:
    label: str
    root: Path


def file_metadata(path: Path) -> dict[str, int] | None:
    try:
        stat = path.stat()
        if not path.is_file():
            return None
        return {"size_bytes": int(stat.st_size), "mtime_ns": int(stat.st_mtime_ns)}
    except OSError:
        return None


class FileRootProbe:
    def __init__(self, spec: FileRootSpec, patterns: list[str], *, notifications: bool = True) -> None:
        self.spec = spec
        self.patterns = patterns
        self.previous: dict[str, dict[str, int]] = {}
        self.baseline: dict[str, dict[str, int]] = {}
        self.final: dict[str, dict[str, int]] = {}
        self.metadata_change_counts: dict[str, int] = {}
        self.notification_counts: dict[str, dict[str, int]] = {}
        self.all_notifications: list[dict[str, Any]] = []
        self.enumeration_errors: list[str] = []
        self.watcher = DirectoryChangeWatcher(spec.root, patterns) if notifications else None
        if self.watcher:
            self.watcher.start()

    def close(self) -> None:
        if self.watcher:
            self.watcher.stop()
            self._consume_notifications(self.watcher.drain())

    def _matches(self, relative: str) -> bool:
        normalized = relative.replace("\\", "/")
        basename = normalized.rsplit("/", 1)[-1]
        return any(fnmatch.fnmatch(normalized, pattern) or fnmatch.fnmatch(basename, pattern) for pattern in self.patterns)

    def _enumerate(self) -> dict[str, dict[str, int]]:
        current: dict[str, dict[str, int]] = {}
        try:
            for directory, _, files in os.walk(self.spec.root):
                directory_path = Path(directory)
                for filename in files:
                    path = directory_path / filename
                    try:
                        relative = path.relative_to(self.spec.root).as_posix()
                    except ValueError:
                        continue
                    if not self._matches(relative):
                        continue
                    metadata = file_metadata(path)
                    if metadata is not None:
                        current[relative] = metadata
        except OSError as error:
            self.enumeration_errors.append(str(error))
        return current

    def _consume_notifications(self, notifications: list[dict[str, Any]]) -> None:
        if not notifications:
            return
        self.all_notifications.extend(notifications)
        for event in notifications:
            relative = event["relative_path"]
            action = event["action"]
            by_action = self.notification_counts.setdefault(relative, {})
            by_action[action] = by_action.get(action, 0) + 1

    def sample(self, *, first: bool = False) -> dict[str, Any]:
        current = self._enumerate()
        if first:
            self.baseline = dict(current)
        changes: list[dict[str, Any]] = []
        if not first:
            for relative, metadata in current.items():
                prior = self.previous.get(relative)
                if prior is None:
                    action = "added"
                elif prior != metadata:
                    action = "changed"
                else:
                    continue
                self.metadata_change_counts[relative] = self.metadata_change_counts.get(relative, 0) + 1
                changes.append({"action": action, "relative_path": relative, **metadata})
            for relative, prior in self.previous.items():
                if relative not in current:
                    self.metadata_change_counts[relative] = self.metadata_change_counts.get(relative, 0) + 1
                    changes.append({"action": "removed", "relative_path": relative, **prior})

        notifications = self.watcher.drain() if self.watcher else []
        self._consume_notifications(notifications)

        self.previous = current
        self.final = dict(current)
        return {
            "tracked_file_count": len(current),
            "metadata_changes": changes,
            "notifications": notifications,
        }

    def summary(self, duration_seconds: float) -> dict[str, Any]:
        files = sorted(set(self.baseline) | set(self.final) | set(self.metadata_change_counts) | set(self.notification_counts))
        per_file: dict[str, Any] = {}
        total_notifications = 0
        modified_notifications = 0
        root_modified_times: list[float] = []
        for relative in files:
            counts = self.notification_counts.get(relative, {})
            notifications = sum(counts.values())
            modified = counts.get("modified", 0)
            total_notifications += notifications
            modified_notifications += modified
            file_events = [event for event in self.all_notifications if event["relative_path"] == relative]
            modified_times = sorted(
                float(event["monotonic_seconds"])
                for event in file_events
                if event["action"] == "modified"
            )
            root_modified_times.extend(modified_times)
            modified_intervals = [
                modified_times[index] - modified_times[index - 1]
                for index in range(1, len(modified_times))
                if modified_times[index] >= modified_times[index - 1]
            ]
            sizes: list[int] = []
            mtimes: list[int] = []
            for metadata in (self.baseline.get(relative), self.final.get(relative)):
                if metadata is not None:
                    sizes.append(metadata["size_bytes"])
                    mtimes.append(metadata["mtime_ns"])
            for event in file_events:
                if event.get("size_bytes") is not None:
                    sizes.append(int(event["size_bytes"]))
                if event.get("mtime_ns") is not None:
                    mtimes.append(int(event["mtime_ns"]))
            per_file[relative] = {
                "baseline": self.baseline.get(relative),
                "final": self.final.get(relative),
                "observed_size_bytes": summarize(sizes),
                "observed_mtime_ns_range": {
                    "min": min(mtimes),
                    "max": max(mtimes),
                } if mtimes else None,
                "observed_metadata_changes": self.metadata_change_counts.get(relative, 0),
                "notification_counts": counts,
                "observed_modified_notifications_per_second": modified / duration_seconds if duration_seconds > 0 else None,
                "modified_notification_intervals_seconds": summarize(modified_intervals),
            }
        root_modified_times.sort()
        root_modified_intervals = [
            root_modified_times[index] - root_modified_times[index - 1]
            for index in range(1, len(root_modified_times))
            if root_modified_times[index] >= root_modified_times[index - 1]
        ]
        return {
            "tracked_files": per_file,
            "observed_metadata_change_events": sum(self.metadata_change_counts.values()),
            "filesystem_notifications": total_notifications,
            "modified_notifications": modified_notifications,
            "observed_modified_notifications_per_second": modified_notifications / duration_seconds if duration_seconds > 0 else None,
            "modified_notification_intervals_seconds": summarize(root_modified_intervals),
            "watcher_available": self.watcher is not None and self.watcher.error is None,
            "watcher_error": self.watcher.error if self.watcher else "disabled",
            "enumeration_errors": self.enumeration_errors,
        }


def build_process_summary(label: str, samples: list[dict[str, Any]], probe: ProcessProbe) -> dict[str, Any]:
    process_samples = [sample["processes"].get(label) for sample in samples]
    process_samples = [sample for sample in process_samples if sample is not None]
    return {
        "sample_count": len(process_samples),
        "alive_sample_count": sum(1 for sample in process_samples if sample.get("alive") is True),
        "access": {
            "opened": probe.opened_successfully,
            "open_error": probe.open_error,
            "metric_errors": probe.metric_errors,
        },
        "cpu_delta_seconds": summarize((sample["cpu"]["delta_total_seconds"] for sample in process_samples), total=True),
        "cpu_percent_single_core": summarize((sample["cpu"]["percent_single_core"] for sample in process_samples)),
        "cpu_percent_all_logical_cpus": summarize((sample["cpu"]["percent_all_logical_cpus"] for sample in process_samples)),
        "working_set_bytes": summarize((sample["memory"]["working_set_bytes"] for sample in process_samples)),
        "private_bytes": summarize((sample["memory"]["private_bytes"] for sample in process_samples)),
        "io_read_bytes_delta": summarize((sample["io"]["delta"]["read_bytes"] for sample in process_samples), total=True),
        "io_write_bytes_delta": summarize((sample["io"]["delta"]["write_bytes"] for sample in process_samples), total=True),
        "io_read_ops_delta": summarize((sample["io"]["delta"]["read_ops"] for sample in process_samples), total=True),
        "io_write_ops_delta": summarize((sample["io"]["delta"]["write_ops"] for sample in process_samples), total=True),
        "handles": summarize((sample["handles"] for sample in process_samples)),
        "threads": summarize((sample["threads"] for sample in process_samples)),
    }


def run_sampler(
    process_specs: list[ProcessTargetSpec],
    file_specs: list[FileRootSpec],
    *,
    duration_seconds: float,
    interval_seconds: float,
    file_patterns: list[str] | None = None,
    notifications: bool = True,
) -> dict[str, Any]:
    if duration_seconds <= 0:
        raise ValueError("duration_seconds must be > 0")
    if interval_seconds <= 0:
        raise ValueError("interval_seconds must be > 0")
    if not process_specs and not file_specs:
        raise ValueError("at least one process PID or file root is required")

    patterns = file_patterns or ["*"]
    process_probes = [ProcessProbe(spec) for spec in process_specs]
    file_probes = [FileRootProbe(spec, patterns, notifications=notifications) for spec in file_specs]
    started_utc = utc_now()
    start_monotonic = time.monotonic()
    deadline = start_monotonic + duration_seconds
    sampling_ended_monotonic = start_monotonic
    samples: list[dict[str, Any]] = []

    def collect(index: int, first: bool = False) -> None:
        monotonic_now = time.monotonic()
        samples.append({
            "index": index,
            "timestamp_utc": utc_now(),
            "elapsed_seconds": monotonic_now - start_monotonic,
            "processes": {probe.spec.label: probe.sample(monotonic_now) for probe in process_probes},
            "file_roots": {probe.spec.label: probe.sample(first=first) for probe in file_probes},
        })

    try:
        collect(0, first=True)
        index = 1
        while True:
            target = min(start_monotonic + index * interval_seconds, deadline)
            delay = target - time.monotonic()
            if delay > 0:
                time.sleep(delay)
            collect(index)
            if target >= deadline:
                sampling_ended_monotonic = time.monotonic()
                break
            index += 1
    finally:
        for probe in file_probes:
            probe.close()
        for probe in process_probes:
            probe.close()

    ended_utc = utc_now()
    actual_duration = max(sampling_ended_monotonic - start_monotonic, 0.0)
    process_summary = {
        probe.spec.label: {
            "pid": probe.spec.pid,
            **build_process_summary(probe.spec.label, samples, probe),
        }
        for probe in process_probes
    }
    file_summary = {
        probe.spec.label: {
            "root": str(probe.spec.root),
            "patterns": patterns,
            **probe.summary(actual_duration),
        }
        for probe in file_probes
    }
    return {
        "schema_version": SCHEMA_VERSION,
        "tool": TOOL_NAME,
        "platform": sys.platform,
        "sampler_pid": os.getpid(),
        "logical_cpu_count": os.cpu_count(),
        "started_utc": started_utc,
        "ended_utc": ended_utc,
        "requested_duration_seconds": duration_seconds,
        "actual_duration_seconds": actual_duration,
        "interval_seconds": interval_seconds,
        "summary": {
            "processes": process_summary,
            "file_roots": file_summary,
        },
        "samples": samples,
    }


def write_report(report: dict[str, Any], output: Path) -> None:
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(output.name + f".tmp-{os.getpid()}")
    try:
        with temporary.open("w", encoding="utf-8", newline="\n") as handle:
            json.dump(report, handle, indent=2, sort_keys=True)
            handle.write("\n")
        os.replace(temporary, output)
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Passively sample Windows process CPU/memory/I/O/handle/thread counters and "
            "mailbox/heartbeat file activity under isolated runtime roots."
        )
    )
    parser.add_argument(
        "--pid",
        action="append",
        default=[],
        metavar="[LABEL=]PID",
        help="Process PID to sample; repeat for app/game (example: --pid app=1234 --pid game=5678).",
    )
    parser.add_argument(
        "--root",
        action="append",
        default=[],
        metavar="[LABEL=]PATH",
        help="Isolated proof/runtime root to observe recursively; repeatable.",
    )
    parser.add_argument(
        "--file-glob",
        action="append",
        default=[],
        metavar="GLOB",
        help="File glob matched against relative path or basename. Repeatable; default is all files.",
    )
    parser.add_argument("--duration", type=float, default=DEFAULT_DURATION_SECONDS, help="Requested sample duration in seconds.")
    parser.add_argument("--interval", type=float, default=DEFAULT_INTERVAL_SECONDS, help="Process/file metadata sample interval in seconds.")
    parser.add_argument("--no-file-notify", action="store_true", help="Disable ReadDirectoryChangesW notifications and use interval metadata only.")
    parser.add_argument("--output", type=Path, help="Write JSON report here; omitted prints JSON to stdout.")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)
    if sys.platform != "win32":
        parser.error("this sampler currently requires Windows")
    if args.duration <= 0:
        parser.error("--duration must be > 0")
    if args.interval <= 0:
        parser.error("--interval must be > 0")

    process_specs: list[ProcessTargetSpec] = []
    labels: set[str] = set()
    for index, raw in enumerate(args.pid, start=1):
        label, pid_text = parse_named_value(raw, kind="--pid")
        if "=" not in raw:
            label = f"pid-{pid_text}"
        try:
            pid = int(pid_text, 10)
        except ValueError:
            parser.error(f"invalid PID: {pid_text!r}")
        if pid <= 0:
            parser.error("PID must be positive")
        if label in labels:
            parser.error(f"duplicate target label: {label}")
        labels.add(label)
        process_specs.append(ProcessTargetSpec(label, pid))

    file_specs: list[FileRootSpec] = []
    root_labels: set[str] = set()
    for index, raw in enumerate(args.root, start=1):
        label, path_text = parse_named_value(raw, kind="--root")
        root = Path(path_text).expanduser().resolve()
        if "=" not in raw:
            label = root.name or f"root-{index}"
        if label in root_labels:
            parser.error(f"duplicate root label: {label}")
        if not root.is_dir():
            parser.error(f"runtime root does not exist or is not a directory: {root}")
        root_labels.add(label)
        file_specs.append(FileRootSpec(label, root))

    if not process_specs and not file_specs:
        parser.error("provide at least one --pid or --root")
    if args.output:
        output = args.output.expanduser().resolve()
        for spec in file_specs:
            if _inside_path(output, spec.root):
                parser.error(f"--output must be outside monitored root {spec.root} so the sampler does not contaminate measurements")

    report = run_sampler(
        process_specs,
        file_specs,
        duration_seconds=args.duration,
        interval_seconds=args.interval,
        file_patterns=args.file_glob or ["*"],
        notifications=not args.no_file_notify,
    )
    if args.output:
        write_report(report, args.output)
        print(f"{TOOL_NAME}: wrote {args.output}")
    else:
        json.dump(report, sys.stdout, indent=2, sort_keys=True)
        sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
