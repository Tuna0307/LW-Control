"""A second FRESH R2 task profile after the first preserved failed witness."""
from pathlib import Path
import home_004_r1_live_gate as gate
task = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r2-live-corrected"
gate.task = task
gate.profile_root = task / "isolated"
gate.receipt_file = task / "preflight.json"
gate.profile = "home-004-r2-corrected"
if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit("Use prepare, enable, finish on the fresh corrected R2 task only")
    {"prepare": gate.prepare, "enable": gate.enable, "finish": gate.finish}[sys.argv[1]]()
