#!/usr/bin/env python3
"""Discover exact 0.3.17 scheduled Map plunder worker markers/xrefs."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from inspect_map_surface import native_surface


MARKERS = [
    "read armable dispatch plunder",
    "read armable truck plunder",
    "mark dispatch plunder waiting connection",
    "mark truck plunder waiting connection",
    "recover dispatch plunder jobs",
    "recover truck plunder jobs",
    "stop dispatch plunder jobs at daily limit",
    "record dispatch plunder result",
    "record truck plunder result",
    "map.dispatch-plunder-result",
    "map.truck-plunder-result",
    "DISPATCH_PLUNDER_TASK_EXPIRED",
    "DISPATCH_PLUNDER_GAME_DISCONNECTED",
    "DISPATCH_PLUNDER_CLIENT_RESTARTED",
    "DISPATCH_PLUNDER_DAILY_LIMIT_REACHED",
    "TRUCK_PLUNDER_TASK_EXPIRED",
    "TRUCK_PLUNDER_GAME_DISCONNECTED",
    "TRUCK_PLUNDER_CLIENT_RESTARTED",
    "armMapPlunder",
    "clearMapPlunderPending",
]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = {
        "schema": 1,
        "findingId": "LWB317-RE-MAP-005-PLUNDER-WORKER-SURFACE",
        "evidenceState": "EXACT_BYTES_DISCOVERY",
        "native": native_surface(args.binary, MARKERS),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
