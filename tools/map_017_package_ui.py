"""MAP017 isolated packaged UI observations via the exact-owned native runner.

Set LWBRIDGE_MAP014_UI_EXE to an extracted Windows package executable.
Each invocation starts a fresh isolated profile and owned Chinese game session,
copies only an audited exact-original settlement marker (no updater), and audits
same-session Stop, script restoration and process exit before returning.
Zero boss rows remain a negative observation in the JSON; they are not acceptance.
"""
from pathlib import Path
import os
import runpy
import sys

MODES = ("boss-first", "boss-after-resource", "picker-empty", "once-two-server")

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in MODES:
        raise SystemExit("usage: python tools/map_017_package_ui.py " + "|".join(MODES))
    executable = os.environ.get("LWBRIDGE_MAP014_UI_EXE")
    if not executable or not Path(executable).resolve().is_file():
        raise SystemExit("Set LWBRIDGE_MAP014_UI_EXE to the extracted packaged LWBridge.Desktop.exe first")
    runpy.run_path(str(Path(__file__).resolve().parent / "map_014_lead_native/run_ui.py"), run_name="__main__")
