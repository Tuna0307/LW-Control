from __future__ import annotations

import importlib.util
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE / "run_live_resource_probe.py"
COMPAT = HERE / "current_client_compat.py"


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {path.name}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> int:
    lr = load(BASE, "lwbridge_current_client_compat_base")
    compat = load(COMPAT, "lwbridge_current_client_compat")
    report = compat.inspect_current(lr, lr.paths())
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0 if report.get("ok") is True else 2


if __name__ == "__main__":
    raise SystemExit(main())
