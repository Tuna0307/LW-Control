"""Bounded real Windows process/transport perturbation for ONE exact R3-owned game.

Verify PID, path, FILETIME creation, session, single host and backup with the
R2 exact-identity gate. Suspend that precise process handle and always attempt
resume in finally. Observe OS IsHungAppWindow and handle liveness only; never
write fake heartbeats, game state, bridge sessions or controller status.
"""
import ctypes
from ctypes import wintypes
import datetime
import json
from pathlib import Path
import sys
import time

import home_004_r2_owned_exit as identity

TASK = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r3-live"
identity.TASK = TASK
identity.RUNTIME = TASK / "isolated" / "overview-bridge"
identity.PROFILE = "home-004-r3-live"
PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
PROCESS_SUSPEND_RESUME = 0x0800
SYNCHRONIZE = 0x100000
WAIT_TIMEOUT = 258
WAIT_OBJECT_0 = 0
kernel = ctypes.WinDLL("kernel32", use_last_error=True)
ntdll = ctypes.WinDLL("ntdll", use_last_error=True)
user32 = ctypes.WinDLL("user32", use_last_error=True)
kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
kernel.OpenProcess.restype = wintypes.HANDLE
kernel.GetProcessTimes.argtypes = (wintypes.HANDLE, ctypes.POINTER(identity.FILETIME),
                                   ctypes.POINTER(identity.FILETIME), ctypes.POINTER(identity.FILETIME),
                                   ctypes.POINTER(identity.FILETIME))
kernel.QueryFullProcessImageNameW.argtypes = (wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR,
                                               ctypes.POINTER(wintypes.DWORD))
kernel.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
ntdll.NtSuspendProcess.argtypes = (wintypes.HANDLE,)
ntdll.NtSuspendProcess.restype = wintypes.LONG
ntdll.NtResumeProcess.argtypes = (wintypes.HANDLE,)
ntdll.NtResumeProcess.restype = wintypes.LONG

def hung_windows(pid):
    found = []
    callback = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user32.IsWindowVisible(hwnd):
            found.append(bool(user32.IsHungAppWindow(hwnd)))
        return True
    cb = callback(visit)
    user32.EnumWindows(cb, 0)
    return {"gameWindowCount": len(found), "anyWindowHung": any(found)}

def witness(seconds):
    if seconds < 1 or seconds > 90:
        raise ValueError("Choose a finite hold between 1 and 90 seconds")
    pid, expected_path, created, session = identity.read_owned()
    handle = kernel.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SUSPEND_RESUME | SYNCHRONIZE,
                                False, pid)
    if not handle:
        raise RuntimeError("Cannot open exact owned process for bounded suspend")
    receipt = TASK / "hang-boundary.jsonl"
    suspended = False
    try:
        path_buffer = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(path_buffer))
        created_actual = identity.FILETIME()
        exited = identity.FILETIME()
        kt = identity.FILETIME()
        ut = identity.FILETIME()
        if not kernel.QueryFullProcessImageNameW(handle, 0, path_buffer, ctypes.byref(length)):
            raise RuntimeError("Handle process image cannot be queried")
        if not kernel.GetProcessTimes(handle, ctypes.byref(created_actual), ctypes.byref(exited),
                                      ctypes.byref(kt), ctypes.byref(ut)):
            raise RuntimeError("Handle creation time cannot be queried")
        if (identity.os.path.normcase(identity.os.path.abspath(path_buffer.value)) != expected_path or
                identity.ticks(created_actual) != identity.creation_ticks(created) or
                kernel.WaitForSingleObject(handle, 0) != WAIT_TIMEOUT):
            raise RuntimeError("Handle path/100ns creation/liveness changed; no suspension")
        def write(stage, **extra):
            status = {"timeUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                      "phase": stage, "pid": pid, "sessionDigest": identity.hashlib.sha256(
                          session.encode()).hexdigest()[:20],
                      "handleStillAlive": kernel.WaitForSingleObject(handle, 0) == WAIT_TIMEOUT,
                      **hung_windows(pid), **extra}
            with receipt.open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(status) + "\n")
            print(json.dumps(status), flush=True)
        write("verified-before-suspend", handleExactCreation100ns=True, handlePathMatched=True)
        status = ntdll.NtSuspendProcess(handle)
        if status != 0:
            raise RuntimeError(f"NtSuspendProcess returned NTSTATUS {status:#x}")
        suspended = True
        write("suspended-actual-owned-process")
        end = time.monotonic() + seconds
        while time.monotonic() < end and kernel.WaitForSingleObject(handle, 0) == WAIT_TIMEOUT:
            time.sleep(min(2, max(0, end - time.monotonic())))
            write("held-boundary-observation")
    finally:
        try:
            if suspended:
                resumed = ntdll.NtResumeProcess(handle)
                state = {"timeUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
                         "phase": "resume-finally", "pid": pid, "resumeNtstatus": resumed,
                         "handleStillAlive": kernel.WaitForSingleObject(handle, 0) == WAIT_TIMEOUT}
                with receipt.open("a", encoding="utf-8") as stream:
                    stream.write(json.dumps(state) + "\n")
                print(json.dumps(state), flush=True)
                if resumed != 0 and state["handleStillAlive"]:
                    raise RuntimeError("FAILED TO RESUME STILL-LIVE OWNED PROCESS")
        finally:
            kernel.CloseHandle(handle)

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"65", "75"}:
        raise SystemExit("Use exactly 65 or 75 seconds; no arbitrary indefinite suspension")
    witness(int(sys.argv[1]))
