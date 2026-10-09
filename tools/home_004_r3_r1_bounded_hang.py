"""R3-R1 task-owned 65/75-second exact-handle finite hang witness."""
from pathlib import Path
import home_004_r3_bounded_hang as hang

hang.TASK = Path(__file__).resolve().parents[1] / "artifacts" / "home-004" / "r3-r1-live"
hang.identity.TASK = hang.TASK
hang.identity.RUNTIME = hang.TASK / "isolated" / "overview-bridge"
hang.identity.PROFILE = "home-004-r3-r1-live"

if __name__ == "__main__":
    import sys
    if len(sys.argv) != 2 or sys.argv[1] not in {"65", "75"}:
        raise SystemExit("Use exactly 65 or 75 seconds after exact owner gating")
    hang.witness(int(sys.argv[1]))
