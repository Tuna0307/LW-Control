"""Bounded, exact-handle R3 still-running fault. Usage: check|pause|resume.

Pauses only the task-owned, path/FILETIME/session-verified LastWar process.
`pause` restores the same process in a finally block after at most 46 seconds;
`resume` is the manual safety backstop. No heartbeat or ready-file writes.
"""
import ctypes
from ctypes import wintypes
import datetime
import json
from pathlib import Path
import sys
import time
import home_004_r2_owned_exit as exact

TASK = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r3-live"
exact.TASK = TASK
exact.RUNTIME = TASK / "isolated" / "overview-bridge"
exact.PROFILE = "home-004-r3-live"
PROCESS_SUSPEND_RESUME = 0x0800
WAIT_OBJECT_0 = 0
MAX_SECONDS = 46


def emit(phase, pid, **data):
    receipt = {"timeUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
               "phase": phase, "pid": pid, **data}
    with (TASK / "still-running-fault.jsonl").open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(receipt, sort_keys=True) + "\n")
    print(json.dumps(receipt, sort_keys=True), flush=True)


def run(mode):
    if sys.platform != "win32" or mode not in ("check", "pause", "resume"):
        raise RuntimeError("Windows only; use check, pause, or resume")
    pid, expected_path, start, _ = exact.read_owned()
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    ntdll = ctypes.WinDLL("ntdll")
    kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
    kernel.QueryFullProcessImageNameW.argtypes = (
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD))
    kernel.GetProcessTimes.argtypes = (
        wintypes.HANDLE, ctypes.POINTER(exact.FILETIME), ctypes.POINTER(exact.FILETIME),
        ctypes.POINTER(exact.FILETIME), ctypes.POINTER(exact.FILETIME))
    kernel.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
    ntdll.NtSuspendProcess.argtypes = (wintypes.HANDLE,)
    ntdll.NtSuspendProcess.restype = wintypes.LONG
    ntdll.NtResumeProcess.argtypes = (wintypes.HANDLE,)
    ntdll.NtResumeProcess.restype = wintypes.LONG
    handle = kernel.OpenProcess(exact.QUERY_LIMITED | PROCESS_SUSPEND_RESUME | exact.SYNCHRONIZE, False, pid)
    if not handle:
        raise RuntimeError("Cannot open verified process with bounded suspend rights; no action")
    try:
        image_buf = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(image_buf))
        if not kernel.QueryFullProcessImageNameW(handle, 0, image_buf, ctypes.byref(length)):
            raise RuntimeError("Cannot verify opened handle image; no action")
        if exact.os.path.normcase(exact.os.path.abspath(image_buf.value)) != expected_path:
            raise RuntimeError("Opened process image changed; no action")
        created, exited, kt, ut = (exact.FILETIME() for _ in range(4))
        if not kernel.GetProcessTimes(handle, ctypes.byref(created), ctypes.byref(exited),
                                       ctypes.byref(kt), ctypes.byref(ut)):
            raise RuntimeError("Cannot verify opened handle FILETIME; no action")
        if exact.ticks(created) != exact.creation_ticks(start):
            raise RuntimeError("Opened process creation changed; no action")
        emit("exact-handle-verified", pid, mode=mode, pathMatched=True, creationMatched100ns=True)
        if mode == "check":
            return
        if kernel.WaitForSingleObject(handle, 0) == WAIT_OBJECT_0:
            raise RuntimeError("Game exited before fault operation; no action")
        if mode == "resume":
            status = ntdll.NtResumeProcess(handle)
            emit("safety-resume", pid, ntstatus=int(status))
            if status != 0:
                raise RuntimeError("Verified owned process resume failed")
            return
        status = ntdll.NtSuspendProcess(handle)
        if status != 0:
            raise RuntimeError(f"Verified owned process suspend failed: NTSTATUS {status}")
        emit("exact-owned-process-suspended", pid, maxSeconds=MAX_SECONDS)
        try:
            deadline = time.monotonic() + MAX_SECONDS
            while time.monotonic() < deadline:
                if kernel.WaitForSingleObject(handle, 0) == WAIT_OBJECT_0:
                    emit("process-exited-while-paused", pid)
                    return
                time.sleep(0.25)
        finally:
            if kernel.WaitForSingleObject(handle, 0) != WAIT_OBJECT_0:
                resume_status = ntdll.NtResumeProcess(handle)
                emit("automatic-safety-resume", pid, ntstatus=int(resume_status))
                if resume_status != 0:
                    raise RuntimeError("Verified owned process could not resume")
    finally:
        kernel.CloseHandle(handle)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    run(sys.argv[1])
