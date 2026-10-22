"""Read-only MAP-019 admission receipt; never launches/adopts/stops a game.

The selected-installation guard is the production helper's exact guard. A
blocked receipt is a dependency, not an attempted benchmark or tool denial.
Run again with --compare <earlier-receipt> to prove selected scripts unchanged.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import sys

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / "tools"))
import run_overview_bridge_current as current


def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--output", type=Path, required=True)
    p.add_argument("--compare", type=Path)
    args = p.parse_args()
    destination = args.output.resolve()
    if not destination.is_relative_to((REPO / "artifacts").resolve()):
        p.error("receipt output must be under ignored artifacts")
    if destination.exists():
        p.error("preserve previous receipts; choose a new output filename")
    paths = current.base.overview_paths()
    installed = {key: {"path": str(paths[key]), "sha256": sha(paths[key])}
                 for key in ("data", "metadata", "version", "assembly")}
    selected = current.base.lr.selected_game_processes(paths)
    guard_error = None
    try:
        current.base.lr.require_no_selected_game_process(paths)
    except current.base.lr.LiveResourceError as error:
        guard_error = str(error)
    receipt = {
        "schemaVersion": 1,
        "capturedAtUtc": datetime.now(timezone.utc).isoformat(),
        "command": subprocess.list2cmdline([sys.executable, *sys.argv]),
        "sourceCommit": subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=REPO, text=True).strip(),
        "scope": "read-only local admission, no game RPC or benchmark",
        "selectedProcesses": selected,
        "admission": "blocked" if guard_error or selected else "available-for-owned-start",
        "guardError": guard_error,
        "installed": installed,
        "taskOwnedGameStarts": 0,
        "gameActions": 0,
        "installedWrites": 0,
    }
    if args.compare:
        before = json.loads(args.compare.read_text(encoding="utf-8"))
        receipt["comparison"] = {
            "source": str(args.compare), "sha256": sha(args.compare),
            "installedUnchanged": before["installed"] == installed,
            "sameSelectedProcesses": before["selectedProcesses"] == selected,
        }
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"receipt": str(destination), "admission": receipt["admission"],
                      "selectedProcessCount": len(selected),
                      "guardError": guard_error, "comparison": receipt.get("comparison")}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
