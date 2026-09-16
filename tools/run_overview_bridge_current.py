"""Current-client entry point for the Overview bridge helper.

The historical Overview lifecycle stays unchanged. This shim installs the
fail-closed dynamic compatibility gate into its shared current-client helper so
compatible Lua-content updates can advance automatically after launcher settle.
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent


def load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {path.name}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


base = load(HERE / "run_overview_bridge.py", "lwbridge_overview_current_base")
compat = load(HERE / "current_client_compat.py", "lwbridge_overview_compat")
compat.install_dynamic_verifier(base.lr)


def main() -> int:
    return base.main()


if __name__ == "__main__":
    raise SystemExit(main())
