"""Fresh R1 Release / native / original-byte / App integration sweep.

Offline/inert only. Replays the 010 reference command inventory WITHOUT writing
to the immutable 010/sweep-final directory. Adds R1 held/inverse checks.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

root = Path(__file__).resolve().parents[2]
base = root / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-COMPLETION-010"
out = base / "R1" / "final-sweep"
out.mkdir(parents=True, exist_ok=True)
original = json.loads((base / "sweep-final/final-checks.json").read_text(encoding="utf-8"))
live_names = ("--completion010-live-pilot", "--background-witness-002")
bin_dll = str(root / "tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll")
checks = []
scratch = Path(tempfile.mkdtemp(prefix="LWB317-R1-RELEASE-"))
try:
    for entry in original:
        name = entry["name"]
        args = [str(x).replace(
            str(base / "sweep-final"), str(out)
        ).replace(
            str(base / "sweep-final").replace("/", "\\"), str(out).replace("/", "\\")
        ) for x in entry["command"]]
        if name == "release-publish-scratch":
            args[-1] = str(scratch / "publish")
        if any(flag in args for flag in live_names):
            raise SystemExit("Unexpected live command in inert sweep: " + name)
        checks.append((name, args))
    for name, flag in [
        ("native-r1-status-guard", "--completion010-status-guard-check"),
        ("native-r1-pilot-inverses", "--completion010-pilot-inverses"),
        ("native-r1-ordered-profile", "--ordered-profile-reconcile-check"),
    ]:
        checks.append((name, ["dotnet", bin_dll, flag]))
    checks.append(("map-module-all", ["dotnet", "run", "--project",
        "tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj",
        "-c", "Release", "--no-build"]))

    results = []
    for index, (name, cmd) in enumerate(checks, 1):
        start = time.perf_counter()
        print(f"R1_SWEEP [{index}/{len(checks)}] {name}", flush=True)
        try:
            completed = subprocess.run(cmd, cwd=root, capture_output=True, text=True,
                errors="replace", timeout=200, shell=False)
            status = completed.returncode
            body = (completed.stdout or "") + ("\n--- STDERR ---\n" + completed.stderr
                if completed.stderr else "")
        except Exception as e:
            status, body = -1, repr(e)
        duration = round(time.perf_counter() - start, 2)
        logfile = f"log-{index:02d}-{name}.txt"
        (out / logfile).write_text(body, encoding="utf-8")
        results.append({"name": name, "exitCode": status, "seconds": duration,
                        "log": logfile, "command": cmd})
        print(f"R1_SWEEP_RESULT {name} exit={status} seconds={duration}", flush=True)
        (out / "final-checks-r1.json").write_text(
            json.dumps(results, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    fails = [x for x in results if x["exitCode"] != 0]
    print(f"R1_SWEEP_TOTAL {len(results)-len(fails)}/{len(results)} passed", flush=True)
    if fails: print("R1_SWEEP_FAILURES " + ",".join(x["name"] for x in fails), flush=True)
    sys.exit(1 if fails else 0)
finally:
    # Scratch publish artifacts are deliberately not attached to the repo or a game root.
    shutil.rmtree(scratch, ignore_errors=True)
