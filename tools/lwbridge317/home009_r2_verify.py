"""HOME 009-R2 integrated verification: the R1 command set plus the R2 additions (static/headless/inert).

Usage: python tools/lwbridge317/home009_r2_verify.py <output-dir> [--only NAME ...]
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import home009_r1_verify as r1  # noqa: E402

ROOT, DLL, PY = r1.ROOT, r1.DLL, r1.PY


def commands(out: Path, scratch: Path):
    base = r1.commands(out, scratch)
    publish = [c for c in base if c[0] == "release-publish-scratch"]
    rest = [c for c in base if c[0] != "release-publish-scratch"]
    extra = [(f"native{flag}", ["dotnet", str(DLL), flag], 400) for flag in (
        "--reconcile-admission-check", "--bridge-ready-window-check", "--overview-bridge-lifecycle-launch-binding-check",
        "--overview-bridge-normal-composition-check", "--production-root-isolation-check", "--map-auto-scan-campaign-check")]
    extra += [
        ("py-readiness-window-r2", [PY, "tools/test_readiness_window_r2.py"], 300),
        ("py-start-failure-matrix", [PY, "tools/test_overview_start_failure_matrix.py"], 600),
        ("py-launch-contract-r2", [PY, "tools/lwbridge317/home009_launch_contract_r2.py"], 300),
        ("native-default-flow", ["dotnet", str(DLL)], 1800),
    ]
    return rest + extra + publish


def main() -> int:
    out = Path(sys.argv[1]).resolve()
    only = set(sys.argv[sys.argv.index("--only") + 1:]) if "--only" in sys.argv else None
    out.mkdir(parents=True, exist_ok=True)
    scratch = Path(tempfile.mkdtemp(prefix="home009-r2-verify-"))
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
