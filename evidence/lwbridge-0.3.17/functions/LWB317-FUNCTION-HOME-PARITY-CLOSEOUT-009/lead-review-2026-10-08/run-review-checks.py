"""Run current inert production checks; save new lead evidence, never historical files."""
import json
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p / "AGENTS.md").is_file())
DLL = ROOT / "tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll"
commands = [
    ["dotnet", str(DLL), "--home009-recovery-trace", str(HERE / "scenarios.json"), str(HERE / "production-trace.json")],
    [sys.executable, "tools/lwbridge317/home009_recovery_compare.py", str(HERE / "oracle.json"), str(HERE / "production-trace.json"), "--status", "--summary", str(HERE / "recovery-comparison.json")],
]
for flag in ["--overview-process-ownership-check", "--overview-close-timing-check", "--overview-reconnect-policy-check", "--home-campaign-lifecycle-check", "--profile-runtime-owner-check", "--map-campaign-canonical-check", "--map317-native-boundary-check"]:
    commands.append(["dotnet", str(DLL), flag])
commands.extend([
    [sys.executable, "tools/test_overview_bridge_lifecycle.py"],
    [sys.executable, "tools/lwbridge317/home009_contract.py"],
    [sys.executable, "tools/lwbridge317/home009_recovery_contract_check.py"],
    ["npm.cmd", "--prefix", "src/LWBridge.UI-0.3.17", "run", "check"],
    ["npm.cmd", "--prefix", "src/LWBridge.UI-0.3.17", "run", "check:production-build"],
])
results = []
for index, command in enumerate(commands):
    started = time.monotonic()
    try:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=150)
        output = result.stdout + result.stderr
        code = result.returncode
    except subprocess.TimeoutExpired as exc:
        code = 124
        output = f"Timed out: {exc}"
    log = HERE / f"check-{index:02d}.log"
    log.write_text(output, encoding="utf-8")
    record = {"command": command, "exitCode": code, "seconds": round(time.monotonic() - started, 2), "log": log.name}
    results.append(record)
    (HERE / "executed-checks.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    print(f"{index:02d} exit={code} {command[2:4]}", flush=True)
print(f"finished: {sum(r['exitCode'] == 0 for r in results)}/{len(results)} checks pass", flush=True)
