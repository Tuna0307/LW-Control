"""Fresh R3-R1 native Home owner; refuse reused or active task identity."""
from pathlib import Path
import home_004_r1_live_gate as gate

gate.task = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r3-r1-live"
gate.profile_root = gate.task / "isolated"
gate.receipt_file = gate.task / "preflight.json"
gate.profile = "home-004-r3-r1-live"

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit("Use prepare, enable or finish with an owned fresh R3-R1 task")
    {"prepare": gate.prepare, "enable": gate.enable, "finish": gate.finish}[sys.argv[1]]()
