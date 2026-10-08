"""Replay the R1 Stop ownership cases against the archived pre-R1 revision (before) and the working tree (after).

Writes a JSON report; refuses to overwrite an existing file.  Inert: no real process, desktop or game access.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "AGENTS.md").is_file())
BEFORE_COMMIT = "cdde6736521cf741700f090c75f80982c521c4e8"


def run(tools_dir: Path, legacy: bool) -> dict:
    env = dict(os.environ, HOME009_TOOLS_DIR=str(tools_dir))
    if legacy:
        env["HOME009_LEGACY_API"] = "1"
    proc = subprocess.run([sys.executable, "-I", str(ROOT / "tools" / "test_stop_ownership_r1.py"), "--json"],
                          capture_output=True, text=True, env=env, cwd=str(ROOT))
    return json.loads(proc.stdout)


def main() -> int:
    target = Path(sys.argv[1])
    if target.exists():
        raise SystemExit(f"refusing to overwrite {target}")
    with tempfile.TemporaryDirectory(prefix="home009-before-") as td:
        old = Path(td)
        for name in ("run_overview_bridge.py", "run_live_resource_probe.py"):
            (old / name).write_bytes(subprocess.run(
                ["git", "show", f"{BEFORE_COMMIT}:tools/{name}"], capture_output=True, check=True, cwd=str(ROOT)).stdout)
        before = run(old, True)
    after = run(ROOT / "tools", False)
    rows = []
    for b, a in zip(before["results"], after["results"]):
        assert b["case"] == a["case"]
        rows.append({"case": a["case"], "finding": a.get("finding"), "beforePass": b["pass"], "afterPass": a["pass"],
                     "beforeFailure": b.get("assertion")})
    report = {"workItem": "LWB317-FUNCTION-HOME-PARITY-CLOSEOUT-009-R1", "checkpoint": "A/B",
              "before": {"commit": BEFORE_COMMIT, "note": "pre-R1 production replayed through a PID-addressed adapter over the same incarnation table"},
              "after": {"note": "working tree production"},
              "distinguishing": [r["case"] for r in rows if not r["beforePass"] and r["afterPass"]],
              "afterFailures": after["failed"], "rows": rows}
    target.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"distinguishing": report["distinguishing"], "afterFailures": report["afterFailures"]}, indent=1))
    return 1 if after["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
