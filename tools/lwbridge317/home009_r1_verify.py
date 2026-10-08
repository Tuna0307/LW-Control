"""HOME 009-R1 integrated verification runner (static/headless/inert; never launches the game or touches the desktop).

Runs the applicable native/profile/Home/Map/recovery/ownership checks, the Python contract and comparator checks, the
canonical frontend/package checks, the mounted App suite and a scratch Release publish, and records every exit code.
Lead immutable inputs are only read; outputs go to the R1 evidence directory.  Usage:
    python tools/lwbridge317/home009_r1_verify.py <output-dir> [--only NAME ...]
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "AGENTS.md").is_file())
E009 = ROOT / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009"
LEAD = E009 / "lead-review-2026-10-08"
E007 = ROOT / "evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-A-TO-A-CAMPAIGN-007"
DLL = ROOT / "tests/LWBridge.Desktop.Checks/bin/Release/net10.0-windows10.0.17763.0/LWBridge.Desktop.Checks.dll"
PY = sys.executable


def commands(out: Path, scratch: Path) -> list[tuple[str, list[str], int]]:
    native = lambda flag: ["dotnet", str(DLL), flag]
    c: list[tuple[str, list[str], int]] = [
        ("build-release-checks", ["dotnet", "build", "tests/LWBridge.Desktop.Checks/LWBridge.Desktop.Checks.csproj", "-c", "Release", "--nologo", "-v", "q"], 900),
        ("recovery-trace-actual", ["dotnet", str(DLL), "--home009-recovery-trace", str(LEAD / "scenarios.json"), str(out / "production-trace.json")], 600),
        ("recovery-trace-compare-normalized", [PY, "tools/lwbridge317/home009_recovery_compare.py", str(LEAD / "oracle.json"), str(out / "production-trace.json"), "--status", "--summary", str(out / "recovery-comparison.json")], 300),
    ]
    for flag in ("--overview-process-ownership-check", "--overview-close-timing-check", "--overview-reconnect-policy-check",
                 "--home-campaign-lifecycle-check", "--profile-runtime-owner-check", "--map-campaign-canonical-check",
                 "--map317-native-boundary-check", "--overview-launch-spam-check", "--overview-official-settle-check",
                 "--unmanaged-close-check", "--owned-process-handle-binding-check", "--recovery-async-ownership-check",
                 "--recovery-boundary-check"):
        c.append(("native" + flag, native(flag), 400))
    c += [
        ("py-stop-ownership-r1", [PY, "tools/test_stop_ownership_r1.py"], 300),
        ("py-overview-lifecycle", [PY, "tools/test_overview_bridge_lifecycle.py"], 300),
        ("py-close-comparator-21", [PY, "tools/lwbridge317/home009_close_compare.py", "--out", str(out / "close-compare.json")], 300),
        ("py-original-byte-contract", [PY, "tools/lwbridge317/home009_contract.py"], 300),
        ("py-recovery-contract", [PY, "tools/lwbridge317/home009_recovery_contract_check.py"], 300),
        ("py-launch-contract", [PY, "tools/lwbridge317/home009_launch_contract.py"], 300),
        ("py-profile-contract", [PY, "tools/lwbridge317/home009_profile_contract.py"], 300),
        ("frontend-check", ["npm.cmd", "--prefix", "src/LWBridge.UI-0.3.17", "run", "check"], 900),
        ("frontend-production-build-check", ["npm.cmd", "--prefix", "src/LWBridge.UI-0.3.17", "run", "check:production-build"], 900),
        ("mounted-app-profiles-aba-deferred", ["node", "src/LWBridge.UI-0.3.17/scripts/check-campaign007-r1-mounted-app-profiles.mjs",
                                               str(E007 / "command-actual-resource-proof.payload.json"), str(out / "mounted-app-profiles.json")], 900),
        ("current-client-contract", [PY, "tools/check_current_client_runtime_contract.py"], 300),
        ("release-publish-scratch", ["dotnet", "publish", "src/LWBridge.Desktop/LWBridge.Desktop.csproj", "-c", "Release", "--nologo", "-v", "q",
                                     "-o", str(scratch / "publish")], 1200),
    ]
    return c


def main() -> int:
    out = Path(sys.argv[1]).resolve()
    only = set(sys.argv[sys.argv.index("--only") + 1:]) if "--only" in sys.argv else None
    out.mkdir(parents=True, exist_ok=True)
    scratch = Path(tempfile.mkdtemp(prefix="home009-r1-verify-"))
    results = []
    try:
        for name, cmd, timeout in commands(out, scratch):
            if only and name not in only:
                continue
            started = time.monotonic()
            try:
                proc = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
                code, output = proc.returncode, proc.stdout + proc.stderr
            except subprocess.TimeoutExpired as exc:
                code, output = 124, f"timeout after {timeout}s: {exc}"
            log = out / f"log-{name.replace('/', '_')}.txt"
            log.write_text(output, encoding="utf-8")
            results.append({"name": name, "command": cmd, "exitCode": code, "seconds": round(time.monotonic() - started, 1), "log": log.name})
            (out / "final-checks.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
            print(f"{name}: exit={code}", flush=True)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
    failed = [r["name"] for r in results if r["exitCode"] != 0]
    print(json.dumps({"total": len(results), "failed": failed}))
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
