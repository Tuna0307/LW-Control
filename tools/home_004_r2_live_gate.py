"""R2 isolated live recovery gate, reuses the independently checked R1
backup/registry/restoration logic but with a NEW never-reused task root.
No shell deletion, no mutation of default app state.
"""
from pathlib import Path
import home_004_r1_live_gate as gate

task = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r2-live"
gate.task = task
gate.profile_root = task / "isolated"
gate.receipt_file = task / "preflight.json"
gate.profile = "home-004-r2-live"

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit("Use prepare, enable or finish (task-only isolated).")
    {"prepare": gate.prepare, "enable": gate.enable, "finish": gate.finish}[sys.argv[1]]()
