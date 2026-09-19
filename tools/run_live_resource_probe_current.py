"""Current-client entry point for the live resource probe.

The historical probe implementation remains pinned. This shim installs the
fail-closed dynamic compatibility gate so compatible Lua-content updates can
advance without hard-coding each new package hash.
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


base = load(HERE / "run_live_resource_probe.py", "lwbridge_live_resource_probe_current_base")
compat = load(HERE / "current_client_compat.py", "lwbridge_live_resource_probe_compat")
compat.install_dynamic_verifier(base)


def main() -> int:
    return base.main()


if __name__ == "__main__":
    raise SystemExit(main())
