#!/usr/bin/env python3
"""Run exactly one admitted original-PE selective Ghidra pass, observe native child.

The project-local psutil wheel lives in ignored artifacts/rea-toolchain/monitor-py.
This is passive analysis; never launches the original application or game.
The script preserves headless project/output, does not terminate foreign processes,
and labels CPU/stack/heartbeat as insufficient to prove semantic progress.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone

REPO = Path(__file__).resolve().parents[2]
TOOLS = REPO / "artifacts/rea-toolchain"
sys.path.insert(0, str(TOOLS / "monitor-py"))
import psutil  # type: ignore[import-not-found]

ORIGINAL = Path(r"C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe")
SHA = "4e9c3113dedfd7e1a752404c6936aab304e67d7ffdb0952a5003c2ec948d6783"
GHIDRA = TOOLS / "ghidra_12.1.4_PUBLIC"
JAVA = TOOLS / "jdk-21.0.12.1+1"
ALLOWED_RANGES = {
    "140191253:140191aca",  # positive control
    "14013a8c1:14013b390",
    "140143d67:140144c41",
    "140159925:14015a27b",
    "140427120:140428a92",
}


def status_of(report: Path, headless_exit: int | None, external_expired: bool,
              resource_pressure_stopped: bool = False) -> str:
    if resource_pressure_stopped:
        return "RESOURCE_PRESSURE_STOP"
    if external_expired:
        return "OUTER_RUNNER_TIMEOUT"
    if headless_exit is None:
        return "RUNNING"
    content = report.read_text(encoding="utf-8", errors="replace") if report.exists() else ""
    if "DECOMPILE_COMPLETED=true" in content and "DECOMPILE_VALID=true" in content:
        return "COMPLETED"
    if "DECOMPILE_TIMED_OUT=true" in content:
        return "FUNCTION_TIMEOUT"
    if "DECOMPILE_CANCELLED=true" in content:
        return "FUNCTION_CANCELLED"
    if "DECOMPILE_FAILED_TO_START=true" in content:
        return "DECOMPILER_STARTUP_FAILURE"
    if headless_exit != 0:
        return "HEADLESS_FAILURE"
    return "OBSERVABILITY_INSUFFICIENT"


def sample_process(process: psutil.Process) -> dict | None:
    try:
        with process.oneshot():
            return {
                "pid": process.pid,
                "createdEpoch": process.create_time(),
                "name": process.name(),
                "cpuSeconds": sum(process.cpu_times()[:2]),
                "rssBytes": process.memory_info().rss,
                "threadCount": process.num_threads(),
                "status": process.status(),
                "command": process.cmdline()[:12],
                "threads": sorted(
                    [
                        {"tid": t.id, "cpuSeconds": round(t.user_time + t.system_time, 4)}
                        for t in process.threads()
                    ],
                    key=lambda item: item["cpuSeconds"],
                    reverse=True,
                )[:8],
            }
    except (psutil.NoSuchProcess, psutil.AccessDenied):
        return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--function", choices=sorted(ALLOWED_RANGES), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--budget-seconds", type=int, default=1800)
    parser.add_argument("--interval-seconds", type=int, default=30)
    parser.add_argument("--reuse-project", action="store_true")
    options = parser.parse_args()
    if not 10 <= options.budget_seconds <= 7200:
        parser.error("function deadline must be in [10,7200]")
    if not 2 <= options.interval_seconds <= 60:
        parser.error("monitoring sample must be in [2,60]")
    out = options.output.resolve()
    allowed = (TOOLS / "map013-monitored").resolve()
    if out != allowed and allowed not in out.parents:
        parser.error("output must be in ignored artifacts/rea-toolchain/map013-monitored")
    if hashlib.sha256(ORIGINAL.read_bytes()).hexdigest() != SHA:
        raise RuntimeError("original EXE hash mismatch")
    if not (JAVA / "bin/javac.exe").exists() or not (GHIDRA / "support/analyzeHeadless.bat").exists():
        raise RuntimeError("accepted portable toolchain absent; do not reinstall")
    out.mkdir(parents=True, exist_ok=True)
    raw = out / "raw"
    raw.mkdir(exist_ok=True)
    env = os.environ.copy()
    env["JAVA_HOME"] = str(JAVA)
    env["GHIDRA_INSTALL_DIR"] = str(GHIDRA)
    env["APPDATA"] = str(out / "process-roaming")
    env["LOCALAPPDATA"] = str(out / "process-local")
    Path(env["APPDATA"]).mkdir(exist_ok=True)
    Path(env["LOCALAPPDATA"]).mkdir(exist_ok=True)
    project_dir = TOOLS / "map013-monitored" / "saved-project"
    project_dir.mkdir(parents=True, exist_ok=True)
    project = "original-map013"
    project_file = project_dir / (project + ".gpr")
    if options.reuse_project and not project_file.exists():
        raise RuntimeError("requested project reuse but saved project absent")
    if not options.reuse_project and project_file.exists():
        raise RuntimeError("saved project exists; reuse it, do not overwrite imported state")
    args = [
        str(GHIDRA / "support/analyzeHeadless.bat"),
        str(project_dir), project,
        "-process" if options.reuse_project else "-import",
        ORIGINAL.name if options.reuse_project else str(ORIGINAL),
        "-noanalysis", "-scriptPath", str(REPO / "tools/rea-mcp"),
        "-postScript", "Map013Selective.java", str(out / "report.txt"), str(raw),
        str(options.budget_seconds), options.function,
        "-log", str(out / "headless.log"),
        "-scriptlog", str(out / "script.log"),
        "-max-cpu", "2",
    ]
    (out / "admission.json").write_text(
        json.dumps({
            "subject": str(ORIGINAL), "sha256": SHA, "function": options.function,
            "functionBudgetSeconds": options.budget_seconds, "outerAllowanceSeconds": options.budget_seconds + 360,
            "samplingSeconds": options.interval_seconds, "project": str(project_file),
            "reuseProject": options.reuse_project, "command": args, "javaHome": str(JAVA),
            "appData": env["APPDATA"], "localAppData": env["LOCALAPPDATA"],
        }, indent=2), encoding="utf-8",
    )
    start = time.monotonic()
    max_outer = options.budget_seconds + 360
    log_path = out / "samples.jsonl"
    result_path = out / "result.json"
    observation = {"nativeSeen": False, "sampleCount": 0, "firstChild": None}
    stderr_stream = (out / "headless-stdout.txt").open("wb")
    child = subprocess.Popen(
        args, cwd=str(REPO), env=env, stdout=stderr_stream, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW,
    )
    parent = psutil.Process(child.pid)
    parent_identity = parent.create_time()
    print("OWNED_GHIDRA_PID", child.pid, "START_EPOCH", parent_identity, flush=True)
    external_expired = False
    resource_stopped = False
    resource_stop_reason = None
    fast_observer_stop = threading.Event()
    native_fast_path = out / "native-child-fast-samples.jsonl"
    native_seen_lock = threading.Lock()
    def observe_native() -> None:
        # A fast process can exist for <1 second (the known-success control).
        # Independent fast sampling, rather than a 30-second monitor heartbeat,
        # captures its start identity and any actual native CPU/memory change.
        with native_fast_path.open("w", encoding="utf-8", buffering=1) as native_log:
            while not fast_observer_stop.is_set():
                try:
                    descendants_now = parent.children(recursive=True) if parent.is_running() and parent.create_time() == parent_identity else []
                    for proc in descendants_now:
                        data = sample_process(proc)
                        if data is None or data["name"].lower() != "decompile.exe":
                            continue
                        data["elapsedSeconds"] = round(time.monotonic() - start, 3)
                        data["utc"] = datetime.now(timezone.utc).isoformat()
                        native_log.write(json.dumps(data) + "\n")
                        with native_seen_lock:
                            observation["nativeSeen"] = True
                            observation["firstChild"] = observation["firstChild"] or {"pid": data["pid"], "createdEpoch": data["createdEpoch"]}
                except psutil.Error:
                    pass
                fast_observer_stop.wait(0.2)
    thread = threading.Thread(target=observe_native, name="map013-native-process-observer", daemon=True)
    thread.start()
    previous_cpu: dict[tuple[int, float], float] = {}
    last_log_bytes = 0
    with log_path.open("w", encoding="utf-8", buffering=1) as samples:
        while True:
            poll = child.poll()
            descendants = []
            try:
                if parent.create_time() == parent_identity:
                    descendants = parent.children(recursive=True)
            except psutil.Error:
                pass
            all_processes = [parent] + descendants
            snapshot = []
            for proc in all_processes:
                point = sample_process(proc)
                if point is None:
                    continue
                key = (point["pid"], point["createdEpoch"])
                point["cpuDeltaSeconds"] = max(0, point["cpuSeconds"] - previous_cpu.get(key, point["cpuSeconds"]))
                previous_cpu[key] = point["cpuSeconds"]
                snapshot.append(point)
                if point["name"].lower() == "decompile.exe":
                    with native_seen_lock:
                        observation["nativeSeen"] = True
                        observation["firstChild"] = observation["firstChild"] or {"pid": point["pid"], "createdEpoch": point["createdEpoch"]}
            ghidra_log = out / "headless.log"
            current_bytes = ghidra_log.stat().st_size if ghidra_log.exists() else 0
            recent = ""
            if current_bytes != last_log_bytes:
                with ghidra_log.open("rb") as stream:
                    stream.seek(max(0, current_bytes - 2400))
                    recent = stream.read().decode("utf-8", errors="replace")[-2200:]
            last_log_bytes = current_bytes
            record = {
                "utc": datetime.now(timezone.utc).isoformat(),
                "elapsedSeconds": round(time.monotonic() - start, 3),
                "runnerPid": child.pid,
                "runnerCreatedEpoch": parent_identity,
                "headlessExit": poll,
                "processes": snapshot,
                "headlessLogBytes": current_bytes,
                "headlessLogTailOnChange": recent,
                "systemFreePhysicalBytes": psutil.virtual_memory().available,
                "interpretation": "CPU/heartbeat/stack samples alone do not prove useful decompiler progress",
            }
            samples.write(json.dumps(record) + "\n")
            observation["sampleCount"] += 1
            print("SAMPLE", observation["sampleCount"], "t", record["elapsedSeconds"], "native", [(p["pid"], p["cpuDeltaSeconds"], p["rssBytes"]) for p in snapshot if p["name"].lower()=="decompile.exe"], "exit", poll, flush=True)
            if poll is not None:
                break
            native_children = [p for p in snapshot if p["name"].lower() == "decompile.exe"]
            if native_children and record["systemFreePhysicalBytes"] < 768 * 1024 * 1024 and \
                    max(p["rssBytes"] for p in native_children) > 5 * 1024 * 1024 * 1024:
                # Protect the owner's 16GB machine; only the exact child that
                # is still descended from the verified owned runner can stop.
                resource_stopped = True
                resource_stop_reason = "task-owned native RSS >5GiB with <768MiB free physical memory"
                pressure = {"status": "RESOURCE_PRESSURE_STOP", "timeUTC": record["utc"],
                            "reason": resource_stop_reason, "ownedNativeIdentities": native_children}
                (out / "resource-pressure-stop.json").write_text(json.dumps(pressure, indent=2), encoding="utf-8")
                for proc in descendants:
                    try:
                        if any(p["pid"] == proc.pid and p["createdEpoch"] == proc.create_time() for p in native_children):
                            proc.terminate()
                    except psutil.Error:
                        pass
                # Do not terminate the headless Java runner; allow it to
                # classify the native failure, save project and close normally.
            if time.monotonic() - start > max_outer:
                external_expired = True
                # Stop ONLY observed descendants of our own verified parent.
                for proc in reversed(descendants):
                    try:
                        if any(p["pid"] == proc.pid and p["createdEpoch"] == proc.create_time() for p in snapshot):
                            proc.terminate()
                    except psutil.Error:
                        pass
                try:
                    parent.terminate()
                except psutil.Error:
                    pass
                break
            time.sleep(options.interval_seconds)
    try:
        child.wait(timeout=10)
    except subprocess.TimeoutExpired:
        pass
    fast_observer_stop.set()
    thread.join(timeout=3)
    stderr_stream.close()
    result = {
        "status": status_of(out / "report.txt", child.poll(), external_expired,
                            resource_stopped or (out / "resource-pressure-stop.json").is_file()),
        "resourceStopReason": resource_stop_reason,
        "nativeProcessObserved": observation["nativeSeen"],
        "firstNativeChild": observation["firstChild"],
        "fastNativeSampleFile": str(native_fast_path),
        "sampleCount": observation["sampleCount"],
        "wallSeconds": round(time.monotonic() - start, 3),
        "headlessExit": child.poll(),
        "functionBudgetSeconds": options.budget_seconds,
        "limits": "native CPU and sparse logs do not establish percentage or semantic progress; OBSERVABILITY_INSUFFICIENT unless independent decompiler phase evidence",
    }
    result_path.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print("RESULT", json.dumps(result), flush=True)
    return 0 if result["status"] in ("COMPLETED", "FUNCTION_TIMEOUT", "FUNCTION_CANCELLED", "OBSERVABILITY_INSUFFICIENT") else 2


if __name__ == "__main__":
    raise SystemExit(main())
