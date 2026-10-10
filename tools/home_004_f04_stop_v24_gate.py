"""Bounded F-04 live Stop-during-disconnect run; prepare | enable | finish.

The exact-v24 preflight, game, backup, and cleanup safeguards are implemented
by home_004_f04_v24_gate. This run has an independent isolated profile and
evidence root; its fault affects only its authenticated task-owned pipe.
"""
from __future__ import annotations

import sys
import home_004_f04_v24_gate as gate

gate.ROOT = gate.ROOT.parent / "f04-v24-stop-during-recovery"
gate.ISOLATED = gate.ROOT / "isolated"
gate.PREFLIGHT = gate.ROOT / "preflight.json"
gate.PROFILE = "home-004-f04-stop-v24"
gate.TASK_PROOF = "HOME004_F04_V24_STOP_DURING_RECOVERY"

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in {"prepare", "enable", "finish"}:
        raise SystemExit(__doc__)
    {"prepare": gate.prepare, "enable": gate.enable, "finish": gate.finish}[sys.argv[1]]()
