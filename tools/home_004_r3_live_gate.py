"""Fresh, separate R3 task owner. See home_004_r1_live_gate for hard gates."""
from pathlib import Path
import home_004_r1_live_gate as gate

gate.task = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r3-live"
gate.profile_root = gate.task / "isolated"
gate.receipt_file = gate.task / "preflight.json"
gate.profile = "home-004-r3-live"

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit("Use prepare, enable or finish for only the fresh R3 isolated owner")
    {"prepare": gate.prepare, "enable": gate.enable, "finish": gate.finish}[sys.argv[1]]()
