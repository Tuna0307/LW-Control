"""Recover a pending Overview candidate before official launcher settlement."""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
from pathlib import Path
import time

HERE = Path(__file__).resolve().parent
CURRENT = HERE / "run_overview_bridge_current.py"
_spec = importlib.util.spec_from_file_location("lwbridge_overview_current_recovery", CURRENT)
if _spec is None or _spec.loader is None:
    raise RuntimeError("could not load the current-client Overview helper")
current = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(current)
base = current.base
lr = base.lr


def run(game_root: str | Path | None) -> dict[str, object]:
    p = base.overview_paths(game_root)
    owner = {
        "schemaVersion": 1,
        "operation": "overview_preflight_recover",
        "helperPid": os.getpid(),
        "startedAtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    p["runtime"].mkdir(parents=True, exist_ok=True)
    with lr.OperationLease(p["runtime"], owner):
        lr.require_no_selected_game_process(p)
        recovered = lr.recover_pending(p)
        verified = lr.verify_current(p)
        return {
            "ok": True,
            "mode": "overview_preflight_recover",
            "recovered": recovered,
            "currentClient": verified,
            "installedFilesChanged": False,
        }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root")
    args = parser.parse_args()
    try:
        result = run(args.game_root)
    except Exception as exc:
        print(json.dumps({
            "ok": False,
            "error": str(exc),
            "errorType": type(exc).__name__,
        }, separators=(",", ":")))
        return 2
    print(json.dumps(result, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
