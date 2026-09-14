"""Current-client entry point for the Overview bridge helper.

The existing run_overview_bridge.py lifecycle remains unchanged. This shim
updates only its imported current-client identity gate to the independently
revalidated Lua-v16 package before delegating to the existing main().
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE / "run_overview_bridge.py"
_spec = importlib.util.spec_from_file_location("lwbridge_overview_current_base", BASE)
if _spec is None or _spec.loader is None:
    raise RuntimeError("could not load the shared Overview bridge helper")
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)

base.lr.EXPECTED_CONTENT_VERSION = 16
base.lr.EXPECTED_PACKAGE_SHA256 = "943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4"
base.lr.EXPECTED_PACKAGE_SIZE = 41278785
base.lr.EXPECTED_PACKAGE_CRC32 = 3454076078


def main() -> int:
    return base.main()


if __name__ == "__main__":
    raise SystemExit(main())
