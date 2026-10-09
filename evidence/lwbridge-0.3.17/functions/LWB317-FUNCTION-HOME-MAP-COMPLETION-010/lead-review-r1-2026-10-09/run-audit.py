"""Independent stopped-checkpoint audit; static/headless/inert commands only."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / "tools/lwbridge317"))
import home009_r2_verify as sweep

OUT = Path(__file__).resolve().parent / "replay"
OUT.mkdir(parents=True, exist_ok=True)
scratch = Path(tempfile.mkdtemp(prefix="lwb317-lead010r1-audit-"))
results = []
try:
    commands = sweep.commands(OUT, scratch)
    commands += [(name, ["dotnet", str(sweep.DLL), flag], 300) for name, flag in [
        ("r1-status-owner", "--completion010-status-guard-check"),
        ("r1-pilot-proof-gates", "--completion010-pilot-inverses"),
        ("r1-ordered-profiles", "--ordered-profile-reconcile-check"),
        ("configured-active-root", "--game-root-select-check"),
        ("adoption-owner", "--overview-adoption-check"),
    ]]
    commands += [("map-module", ["dotnet", "run", "--project",
        "tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj", "-c", "Release"], 400)]
    for name, command, timeout in commands:
        if any("live-pilot" in arg or "background-witness" in arg for arg in command):
            raise RuntimeError("Live command refused by audit")
        started = time.monotonic()
        try:
            p = subprocess.run(command, cwd=ROOT, capture_output=True, text=True,
                encoding="utf-8", errors="replace", timeout=timeout)
            code, body = p.returncode, p.stdout + p.stderr
        except subprocess.TimeoutExpired as error:
            code, body = 124, str(error)
        log = "log-" + name.replace("/", "_") + ".txt"
        (OUT / log).write_text(body, encoding="utf-8")
        results.append(dict(name=name, command=command, exitCode=code,
            seconds=round(time.monotonic()-started, 2), log=log))
        (OUT / "results.json").write_text(json.dumps(results, separators=(",", ":")) + "\n", encoding="utf-8")
        print(f"AUDIT {len(results)}/{len(commands)} {name}: exit={code}", flush=True)
finally:
    shutil.rmtree(scratch)
failed = [r["name"] for r in results if r["exitCode"]]
print(json.dumps(dict(total=len(results), failed=failed)), flush=True)
sys.exit(1 if failed else 0)
