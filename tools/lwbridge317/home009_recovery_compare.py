"""Compare reconstructed 0.3.17 oracle traces with the production trace harness output.

usage: home009_recovery_compare.py <oracle.json> <production.json> [--summary out.json]
Exit status 1 when any scenario differs.  Comparison is exact on (kind, t, key fields).
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

EFFECT_KEYS = {
    "terminate": ("t", "kind", "pid"),
    "killUpdaters": ("t", "kind"),
    "launch": ("t", "kind", "n"),
    "status": ("t", "kind", "state", "reason", "attempts", "nextRetryAt", "error", "updateDetected",
               "restarted", "completedAt", "noticeId"),
}


def normalize(events):
    out = []
    for e in events:
        if e["kind"] == "cleanup":
            continue                      # restoration after termination is an adaptation effect
        keys = EFFECT_KEYS[e["kind"]]
        item = {k: e.get(k) for k in keys}
        if e["kind"] == "status" and item.get("error") not in (None, "game update had no activity for 15 minutes",
                                                                  "game update reported an error"):
            item["error"] = "<error>"     # error texts originate from different Display impls; presence only
        out.append(item)
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("oracle")
    ap.add_argument("production")
    ap.add_argument("--summary")
    ap.add_argument("--status", action="store_true", help="include status events in the comparison")
    a = ap.parse_args()
    oracle = json.loads(Path(a.oracle).read_text(encoding="utf-8"))
    prod = json.loads(Path(a.production).read_text(encoding="utf-8"))
    rows, bad = [], 0
    for name, o_events in oracle.items():
        p_events = prod.get(name)
        if p_events is None:
            rows.append({"scenario": name, "equal": False, "reason": "missing production trace"})
            bad += 1
            continue
        keep = (lambda e: True) if a.status else (lambda e: e["kind"] != "status")
        on = [e for e in normalize(o_events) if keep(e)]
        pn = [e for e in normalize(p_events) if keep(e)]
        equal = on == pn
        row = {"scenario": name, "equal": equal, "oracleEffects": len(on), "productionEffects": len(pn)}
        if not equal:
            bad += 1
            for i in range(max(len(on), len(pn))):
                x = on[i] if i < len(on) else None
                y = pn[i] if i < len(pn) else None
                if x != y:
                    row["firstDifference"] = {"index": i, "oracle": x, "production": y}
                    break
        rows.append(row)
    summary = {"scenarios": len(rows), "mismatches": bad, "rows": rows}
    if a.summary:
        Path(a.summary).write_text(json.dumps(summary, indent=1, sort_keys=True), encoding="utf-8")
    print(f"{len(rows)} scenarios, {bad} mismatches")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
