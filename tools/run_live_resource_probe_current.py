"""Current-client launcher for the hash-gated live resource probe.

The underlying recovery/candidate implementation stays in
run_live_resource_probe.py. This shim supplies only the independently
revalidated current v16 package identity before delegating to its main().
"""
from __future__ import annotations

import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE / "run_live_resource_probe.py"
_spec = importlib.util.spec_from_file_location("lwbridge_live_resource_probe_current_base", BASE)
if _spec is None or _spec.loader is None:
    raise RuntimeError("could not load the shared live-resource helper")
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)

base.EXPECTED_CONTENT_VERSION = 16
base.EXPECTED_PACKAGE_SHA256 = "943873f26af843c6cb03b9bb0a449c06fb90ae9c26ec4de23d3f6aab1375d0b4"
base.EXPECTED_PACKAGE_SIZE = 41278785
base.EXPECTED_PACKAGE_CRC32 = 3454076078


def main() -> int:
    return base.main()


if __name__ == "__main__":
    raise SystemExit(main())
