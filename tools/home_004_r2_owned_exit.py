"""R2 one-shot *exact task-owned* game exit.

Identity is verified on the very Win32 handle that is terminated: PID, full
image path, process creation FILETIME and recorded session/journal. Refuses
unowned, stale, mismatched or concurrent instances. Does not alter recovery
records, settings, scripts, backup or host; this is NOT a Home user Stop.
"""
from __future__ import annotations
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
import os
from pathlib import Path
import sys
import time
import home_launch_002_gate as gate

CORRECTED = "--corrected" in sys.argv[1:]
TASK = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / (
    "r2-live-corrected" if CORRECTED else "r2-live")
RUNTIME = TASK / "isolated" / "overview-bridge"
PROFILE = "home-004-r2-corrected" if CORRECTED else "home-004-r2-live"
QUERY_LIMITED = 0x1000
PROCESS_TERMINATE = 0x0001
SYNCHRONIZE = 0x00100000
WAIT_OBJECT_0 = 0

class FILETIME(ctypes.Structure):
    _fields_ = [("low", wintypes.DWORD), ("high", wintypes.DWORD)]
def ticks(value: FILETIME) -> int:
    return (value.high << 32) | value.low
def creation_ticks(text: str) -> int:
    if not text.endswith("Z") or "T" not in text:
        raise RuntimeError("Process creation string is not canonical UTC")
    head, dot, fraction = text[:-1].partition(".")
    if not dot or len(fraction) != 7 or not fraction.isdigit():
        raise RuntimeError("Expected exact 100ns process creation timestamp")
    utc = datetime.datetime.fromisoformat(head).replace(tzinfo=datetime.timezone.utc)
    delta = utc - datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
    return ((delta.days * 86400 + delta.seconds) * 10_000_000 +
            int(fraction))
def read_owned():
    if not (TASK / "preflight.json").exists():
        raise RuntimeError("Fresh R2 preflight receipt missing")
    adoption = json.loads((RUNTIME / "adoption.json").read_text(encoding="utf-8"))
    journal = json.loads((RUNTIME / "recovery.json").read_text(encoding="utf-8"))
    if adoption.get("profileId") != PROFILE or journal.get("profileId") != PROFILE:
        raise RuntimeError("Recorded profile is not this isolated task owner")
    apid = adoption.get("pid") or adoption.get("gamePid")
    if type(apid) is not int or apid <= 0 or journal.get("gamePid") != apid:
        raise RuntimeError("Adoption and journal game PID disagree")
    start = adoption.get("processCreatedAt")
    if not start or journal.get("gameStartedAtUtc") != start:
        raise RuntimeError("Exact session process creation identity disagrees")
    session = adoption.get("instanceId") or adoption.get("sessionId")
    if not session or journal.get("sessionId") != session:
        raise RuntimeError("Adoption/journal session mismatch")
    game_root = json.loads((TASK / "preflight.json").read_text())["gameRoot"]
    expected_path = os.path.normcase(os.path.abspath(os.path.join(game_root, "Game", "LastWar.exe")))
    apath = adoption.get("gameExecutable")
    if not apath or os.path.normcase(os.path.abspath(apath)) != expected_path or \
       os.path.normcase(os.path.abspath(journal.get("gamePath", ""))) != expected_path:
        raise RuntimeError("Recorded owned game executable path differs")
    if gate.sha(gate.TARGET) != gate.REFERENCE or not all(
            (TASK / "backup-original" / n).exists() and
            gate.sha(TASK / "backup-original" / n) == gate.EXPECTED[n]
            for n in gate.FILES):
        raise RuntimeError("Original executable identity or exact backup gate failed")
    owners = gate.owners()
    games = [p for p in owners if p["name"].lower() == "lastwar.exe"]
    if len(games) != 1 or games[0]["pid"] != apid:
        raise RuntimeError("Extra/unexpected LastWar process; preserve without action")
    if sum(p["name"].lower() == "lwbridge.desktop.exe" for p in owners) != 1:
        raise RuntimeError("Exactly one authorized task host must own the game")
    return apid, expected_path, start, session

def trigger(check_only: bool):
    if os.name != "nt":
        raise RuntimeError("This handle-bound action is Windows-only")
    pid, expected_path, start, session = read_owned()
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
    kernel.QueryFullProcessImageNameW.argtypes = (wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR,
                                                   ctypes.POINTER(wintypes.DWORD))
    kernel.GetProcessTimes.argtypes = (wintypes.HANDLE, ctypes.POINTER(FILETIME),
                                        ctypes.POINTER(FILETIME), ctypes.POINTER(FILETIME),
                                        ctypes.POINTER(FILETIME))
    kernel.TerminateProcess.argtypes = (wintypes.HANDLE, wintypes.UINT)
    kernel.WaitForSingleObject.argtypes = (wintypes.HANDLE, wintypes.DWORD)
    handle = kernel.OpenProcess(QUERY_LIMITED | PROCESS_TERMINATE | SYNCHRONIZE, False, pid)
    if not handle:
        raise RuntimeError("Exact owned PID handle could not be opened (no action)")
    try:
        image_buf = ctypes.create_unicode_buffer(32768)
        length = wintypes.DWORD(len(image_buf))
        if not kernel.QueryFullProcessImageNameW(handle, 0, image_buf, ctypes.byref(length)):
            raise RuntimeError("Handle image identity unavailable (no action)")
        if os.path.normcase(os.path.abspath(image_buf.value)) != expected_path:
            raise RuntimeError("Opened handle belongs to a different executable (no action)")
        created, exited, kernel_time, user_time = FILETIME(), FILETIME(), FILETIME(), FILETIME()
        if not kernel.GetProcessTimes(handle, ctypes.byref(created), ctypes.byref(exited),
                                      ctypes.byref(kernel_time), ctypes.byref(user_time)):
            raise RuntimeError("Handle creation identity unavailable (no action)")
        actual_ticks = ticks(created)
        expected_ticks = creation_ticks(start)
        if actual_ticks != expected_ticks:
            raise RuntimeError(f"Handle creation mismatch (no action): delta100ns={actual_ticks-expected_ticks}")
        print(json.dumps({"phase": "exact-owned-process-handle-verified", "identityValidated": True,
                          "pid": pid, "sessionDigest": hashlib.sha256(session.encode()).hexdigest()[:20],
                          "pathMatched": True, "creationMatched100ns": True,
                          "noHomeStop": True, "hostRetained": True,
                          "checkOnly": check_only}), flush=True)
        if check_only:
            return
        if not kernel.TerminateProcess(handle, 0):
            raise RuntimeError("Handle-verified terminate failed; preserve installation/ownership")
        wait_result = kernel.WaitForSingleObject(handle, 10_000)
        receipt = {"phase": "exact-owned-process-exit-confirmed",
                   "pid": pid, "identityValidated": True, "processExited": wait_result == WAIT_OBJECT_0,
                   "timeUtc": datetime.datetime.now(datetime.timezone.utc).isoformat()}
        with (TASK / "external-exit.jsonl").open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(receipt) + "\n")
        print(json.dumps(receipt), flush=True)
        if wait_result != WAIT_OBJECT_0:
            raise RuntimeError("Owned process did not exit within 10s; preserve all state")
    finally:
        kernel.CloseHandle(handle)
if __name__ == "__main__":
    action = [a for a in sys.argv[1:] if a != "--corrected"]
    if action not in (["one-shot"], ["check-only"]) or sys.argv[1:].count("--corrected") > 1:
        raise SystemExit("Use check-only / one-shot [--corrected] after exact task-owned ready session")
    trigger(check_only=action == ["check-only"])
